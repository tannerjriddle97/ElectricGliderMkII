using ElectricGliderMk2;
try
{
 int checks=0;
 void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 double Length((double X,double Y,double Z) m)=>Math.Sqrt(m.X*m.X+m.Y*m.Y+m.Z*m.Z);
 foreach(double dt in new[]{1.0/20,1.0/60,1.0/144})
 {
  var motion=(X:0.0,Y:0.0,Z:0.0);
  for(int i=0;i<(int)Math.Round(6/dt);i++)
  {
   double before=motion.Y;
   motion=FlightModel.LimitFlightStep(0,motion.Y-1.2*dt,0,before,-Math.PI/2,0,true,1,i*dt,dt);
   Check(Length(motion)*60<=45+1e-8,"boost speed cap, including dives");
   if((i+1)*dt<=2+1e-8)
   {
    Check(motion.Y==FlightModel.LimitFlightStep(0,before-1.2*dt,0,before,-Math.PI/2,0,true,1,3,dt).Y,"startup applies no extra downward damping");
   }
   if(i==0)Check(motion.Y<0,"deployment allows immediate descent");
  }
  Check(motion.Y*60 < -8.1,"native dive retains downward motion at speed cap");
  foreach(double pitch in new[]{-Math.PI/2,-Math.PI/3,-0.1,0,0.1,Math.PI/3,Math.PI/2})
  foreach(double yaw in new[]{0.0,0.8,Math.PI,4.9})
  {
   var thrust=FlightModel.LimitFlightStep(0,0,0,0,pitch,yaw,true,1,3,dt);
   double dx=-Math.Cos(pitch)*Math.Sin(yaw),dy=Math.Sin(pitch),dz=-Math.Cos(pitch)*Math.Cos(yaw);
   double along=thrust.X*dx+thrust.Y*dy+thrust.Z*dz;
   Check(Math.Abs(along*60-9*dt)<1e-8,"boost impulse has equal strength along every look direction");
   var reverse=FlightModel.LimitFlightStep(0,0,0,0,pitch,yaw,false,1,3,dt,true);
   Check(Math.Abs(reverse.X+thrust.X*2/3)+Math.Abs(reverse.Y+thrust.Y*2/3)+Math.Abs(reverse.Z+thrust.Z*2/3)<1e-9,
    "S thrust opposes the look vector at two-thirds boost strength");
   var both=FlightModel.LimitFlightStep(0,0,0,0,pitch,yaw,true,1,3,dt,true);
   Check(Math.Abs(both.X-thrust.X/3)+Math.Abs(both.Y-thrust.Y/3)+Math.Abs(both.Z-thrust.Z/3)<1e-9,
    "S and Shift apply opposing rather than additive forward thrust");
   Check(Math.Abs(thrust.X*dy-thrust.Y*dx)+Math.Abs(thrust.Y*dz-thrust.Z*dy)+Math.Abs(thrust.Z*dx-thrust.X*dz)<1e-9,
    "boost impulse is parallel to look direction, including straight up/down");
   var momentum=FlightModel.LimitFlightStep(0.04,0.03,-0.02,0.03,pitch,yaw,true,1,3,dt);
   Check(Math.Abs(momentum.X-0.04-thrust.X)+Math.Abs(momentum.Y-0.03-thrust.Y)+Math.Abs(momentum.Z+0.02-thrust.Z)<1e-9,
    "directional thrust adds to existing momentum without targeting climb speed");
  }
  var native=FlightModel.LimitFlightStep(0.05,-0.02,0.08,0,0,0,false,1,3,dt);
  Check(native==(0.05,-0.02,0.08),"native motion survives unchanged below the cap after protection");
  var launched=FlightModel.LimitFlightStep(0.05,-0.02,0.08,0,0,0,false,1,0,dt);
  Check(launched.X==0.05&&launched.Z==0.08&&launched.Y<0,"deployment preserves native horizontal acceleration and allows descent");
  var falling=FlightModel.LimitFlightStep(0,-0.3,0,-0.2,-Math.PI/2,0,false,1,0,dt);
  Check(falling.Y==-0.3,"startup leaves native downward acceleration unchanged below the total cap");
  Check(Length(FlightModel.LimitFlightStep(1,1,1,1,0,0,false,0.1,3,dt))<=0.1+1e-8,"lower EP speed cap");
 }
 foreach(double dt in new[]{1.0/20,1.0/60,1.0/144})
 {
  double limit=12.0/60;
  for(int i=0;i<Math.Round(4/dt);i++)
  {
   double before=limit;
   limit=FlightModel.StepSpeedLimit(limit,0,true,1,dt);
   Check(limit>=before && (limit-before)*60<=9*dt+1e-8,"continuous boost builds speed gradually");
  }
  Check(Math.Abs(limit*60-45)<1e-8,"four seconds of boost opens forty-five from twelve");
  limit=FlightModel.StepSpeedLimit(limit,0,false,1,dt);
  Check(limit*60<=35+1e-8,"boost release restores normal hard cap");
  for(int i=0;i<Math.Round(10/dt);i++)limit=FlightModel.StepSpeedLimit(limit,0,false,1,dt);
  Check(Math.Abs(limit*60-12)<1e-8,"level coasting settles to twelve");
  for(int i=0;i<Math.Round(4/dt);i++)limit=FlightModel.StepSpeedLimit(limit,-Math.PI/2,false,1,dt);
  Check(Math.Abs(limit*60-35)<1e-8,"passive dive opens thirty-five");
  limit=FlightModel.StepSpeedLimit(limit,0,false,1,dt);
  Check(limit*60>34,"dive momentum carries into level coasting");
  for(int i=0;i<Math.Round(12/dt);i++)limit=FlightModel.StepSpeedLimit(limit,Math.PI/3,false,1,dt);
  Check(limit*60<8,"unpowered pull-up trades speed for height");
  Check(FlightModel.StepSpeedLimit(0.5,0,true,0.1,dt)<=0.1,"lower EP setting overrides boost ceiling");
  double brakeLimit=12.0/60;
  for(int i=0;i<Math.Round(1/dt);i++)brakeLimit=FlightModel.StepSpeedLimit(brakeLimit,0,false,1,dt,true);
  Check(Math.Abs(brakeLimit*60-6)<1e-8,"S allows gradual braking below normal cruise speed");
  Check(Length(FlightModel.LimitFlightStep(1,1,1,0,0,0,false,1,3,dt))*60<=35+1e-8,"normal total cap is thirty-five");
 }
 var crossing=FlightModel.LimitFlightStep(0,-0.2,0,0,-Math.PI/2,0,false,1,1.99,0.02);
 Check(crossing.Y == -0.2,"no downward damping around the former two-second boundary");
 Check(FlightModel.LimitFlightStep(0,-0.2,0,0,-Math.PI/2,0,false,1,2,0.02).Y==-0.2,
  "downward motion remains unmodified after two seconds");
 var launch=FlightModel.LimitFlightStep(0.05,0.3,0.08,0,Math.PI/3,0,false,1,0,0.02);
 Check(launch.Y==0,"passive deployment cannot create an upward launch");
 var jump=FlightModel.LimitFlightStep(0.05,0.045,0.08,0.05,0,0,false,1,0,0.02);
 Check(Math.Abs(jump.Y-0.045)<1e-10,"deployment protection lets an existing jump decelerate normally");
 foreach(double dt in new[]{1.0/20,1.0/60,1.0/144})
 {
  double x=0,y=0,z=0,height=0;
  for(int i=0;i<Math.Round(5/dt);i++){var m=FlightModel.StepLift(x,y,z,0,0,0,1,dt);(x,y,z)=m;height+=y*dt*60;}
  Check(Math.Abs(y*60-4)<1e-6,"vertical speed");
  Check(height>19 && height<20.1,"vertical launch distance");
  Check(x==0 && z==0,"no automatic forward motion");
  int charge=4000;double fraction=0;
  for(int i=0;i<Math.Round(60/dt);i++)(charge,fraction)=FlightModel.Debit(charge,fraction,22.5,dt);
  Check(Math.Abs(charge-fraction-2650)<1e-6,"fractional billing independent of tick rate");
 }
 foreach(double yaw in new[]{0.0,0.8,1.7,3.14,4.9})
 {
  var f=FlightModel.StepLift(0,0,0,yaw,0,1,1,0.1);
  var b=FlightModel.StepLift(0,0,0,yaw,0,-1,1,0.1);
  var r=FlightModel.StepLift(0,0,0,yaw,1,0,1,0.1);
  Check(f.X*Math.Sin(yaw)+f.Z*Math.Cos(yaw)>0,"upright forward");
  Check(b.X*Math.Sin(yaw)+b.Z*Math.Cos(yaw)<0,"upright backward");
  Check(r.X*(-Math.Cos(yaw))+r.Z*Math.Sin(yaw)>0,"upright right");
  foreach(var input in new[]{(Strafe:1.0,Forward:0.0),(Strafe:0.0,Forward:1.0),(Strafe:1.0,Forward:1.0)})
  {
   var hover=FlightModel.StepLift(0,0,0,yaw,input.Strafe,input.Forward,1,5);
   Check(Math.Abs(Math.Sqrt(hover.X*hover.X+hover.Z*hover.Z)*60-4)<1e-8,
    "hover horizontal speed is four, including normalized diagonal movement");
   Check(Math.Abs(hover.Y*60-4)<1e-8,"hover positioning preserves four-block ascent");
  }
 }
 Check(FlightModel.DrainPerSecond(true,0,15)==45,"lift cost");
 Check(FlightModel.DrainPerSecond(false,0,15)==22.5,"boost cost");
 Check(FlightModel.DrainPerSecond(false,Math.PI/3,15)==30,"climb cost");
 var empty=FlightModel.Debit(1,0,45,1);
 Check(empty.Charge==0 && empty.Fraction==0,"depletion clamps to zero");
 Console.WriteLine($"PASS: {checks} glide, climb, descent, lift and energy checks.");
}
catch(Exception error){Console.Error.WriteLine(error);Environment.ExitCode=1;}
