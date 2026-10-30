namespace ElectricGliderMk2;

// Velocities use the game's 60 Hz motion units; settings below use blocks/second.
public static class FlightModel
{
    public const double MotionTicksPerSecond = 60;
    public const double MaxGlideBlocksPerSecond = 35;
    public const double MaxBoostBlocksPerSecond = 45;
    public const double CoastBlocksPerSecond = 12;
    public const double BoostAcceleration = 9;
    public const double ReverseThrustAcceleration = 6;
    public const double CoastDeceleration = 2.5;
    public const double LaunchSafetySeconds = 2;
    public const double LiftBlocksPerSecond = 4;
    public const double PositioningBlocksPerSecond = 4;
    public const double MaximumClimbRadians = Math.PI / 3;
    public static double SignedPitch(double pitch) => Math.Atan2(Math.Sin(pitch), Math.Cos(pitch));
    public static double DrainPerSecond(bool lift, double pitch, double baseDrain)
    {
        if (lift) return baseDrain * 3;
        double demand = Math.Clamp(Math.Sin(Math.Clamp(SignedPitch(pitch), 0, MaximumClimbRadians)) / Math.Sin(MaximumClimbRadians), 0, 1);
        return baseDrain * (1.5 + 0.5 * demand);
    }
    public static double Approach(double current, double target, double response, double dt)
        => target + (current - target) * Math.Exp(-response * Math.Max(0, dt));

    // This is a changing speed ceiling around native flight, not a replacement
    // movement controller. Preserve heading, launch, gravity and collision handling.
    public static double StepSpeedLimit(double previous, double pitch, bool boosting, double configuredCap, double dt,
        bool braking = false)
    {
        double hardCap = Math.Min(configuredCap * MotionTicksPerSecond,
            boosting ? MaxBoostBlocksPerSecond : MaxGlideBlocksPerSecond);
        double slope = Math.Sin(SignedPitch(pitch));
        double target = boosting ? MaxBoostBlocksPerSecond
            : CoastBlocksPerSecond + (slope < 0 ? -slope * (MaxGlideBlocksPerSecond - CoastBlocksPerSecond) : -slope * 6);
        // Otherwise native cruise thrust would immediately refill speed lost
        // to the weak opposing impulse. S lets the glider slow below cruise.
        if (braking && !boosting) target = 0.005 * MotionTicksPerSecond;
        target = Math.Min(target, hardCap);
        double current = previous * MotionTicksPerSecond;
        double rate = target > current ? (boosting ? BoostAcceleration : 6) : CoastDeceleration;
        if (braking) rate = boosting ? BoostAcceleration - ReverseThrustAcceleration : ReverseThrustAcceleration;
        double next = current + Math.Clamp(target - current, -rate * Math.Max(0, dt), rate * Math.Max(0, dt));
        return Math.Clamp(next, 0.005 * MotionTicksPerSecond, hardCap) / MotionTicksPerSecond;
    }

    public static (double X, double Y, double Z) LimitFlightStep(double x, double y, double z,
        double previousY, double pitch, double yaw, bool boosting, double cap, double elapsed, double dt,
        bool braking = false)
    {
        dt = Math.Max(0, dt);
        cap = Math.Clamp(cap, 0.005, (boosting ? MaxBoostBlocksPerSecond : MaxGlideBlocksPerSecond) / MotionTicksPerSecond);
        if ((boosting || braking) && dt > 0)
        {
            // Add thrust along the same look vector used by native gliding.
            // Preserve momentum; do not target a vertical speed or rotate it.
            double impulse = ((boosting ? BoostAcceleration : 0) - (braking ? ReverseThrustAcceleration : 0))
                / MotionTicksPerSecond * dt;
            double horizontal = Math.Cos(pitch) * impulse;
            x -= Math.Sin(yaw) * horizontal;
            y += Math.Sin(pitch) * impulse;
            z -= Math.Cos(yaw) * horizontal;
        }
        if (elapsed < LaunchSafetySeconds && dt > 0 && !boosting)
        {
            // Prevent a free upward launch, without limiting any descent or
            // downward acceleration. Existing jumps can decelerate normally.
            y = Math.Min(y, Math.Max(0, previousY));
        }
        double magnitude = Math.Sqrt(x * x + y * y + z * z);
        if (magnitude > cap) { x *= cap / magnitude; y *= cap / magnitude; z *= cap / magnitude; }
        return (x, y, z);
    }
    public static (double X, double Y, double Z) StepLift(double x, double y, double z, double yaw, double strafe, double forward, double cap, double dt)
    {
        cap = Math.Clamp(cap, 0.005, 1);
        double length = Math.Max(1, Math.Sqrt(strafe*strafe+forward*forward));
        double positioning = Math.Min(PositioningBlocksPerSecond / 60, cap) / length;
        // Upright lift uses normal walking directions, not flight-vector forward.
        double tx = (Math.Sin(yaw)*forward-Math.Cos(yaw)*strafe)*positioning;
        double tz = (Math.Cos(yaw)*forward+Math.Sin(yaw)*strafe)*positioning;
        double ty = Math.Min(LiftBlocksPerSecond / 60, cap);
        x=Approach(x,tx,6,dt); y=Approach(y,ty,6,dt); z=Approach(z,tz,6,dt);
        double magnitude=Math.Sqrt(x*x+y*y+z*z);
        if(magnitude>cap){x*=cap/magnitude;y*=cap/magnitude;z*=cap/magnitude;}
        return (x,y,z);
    }
    public static (int Charge,double Fraction) Debit(int charge,double fraction,double rate,double dt)
    {
        double use=Math.Max(0,fraction)+Math.Max(0,rate)*Math.Max(0,dt);
        int whole=(int)Math.Min(int.MaxValue,Math.Floor(use+1e-9));
        int remaining=Math.Max(0,charge-whole);
        return (remaining,remaining==0?0:Math.Max(0,use-whole));
    }
}
