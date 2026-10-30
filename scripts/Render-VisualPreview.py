"""Render model-inspection stills directly from cuboids, without a browser/game.
Accepts the same arguments as Build-VisualPreview.py; requires Pillow/numpy.
Output contains installed dependency textures and is private review material.
"""
import base64
import io
import math
import runpy
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageEnhance

data = runpy.run_path(str(Path(__file__).with_name("Build-VisualPreview.py")))
models = data["shapes"]
textures = {k: Image.open(io.BytesIO(base64.b64decode(v.split(",", 1)[1]))).convert("RGB") for k, v in data["textures"].items()}
out = Image.new("RGB", (1600, 1050), "#111a21")
draw = ImageDraw.Draw(out)
font_root = Path("C:/Windows/Fonts")
font = lambda size: ImageFont.truetype(str(font_root / "segoeui.ttf"), size)


def render(stage, rect, yaw=.32, pitch=.25):
    left, top, width, height = rect
    view = Image.new("RGB", (width, height), "#202e38")
    def orient(points):
        p = np.array(points); x,y,z = p.T
        d = x*math.cos(yaw)+z*math.sin(yaw); h = z*math.cos(yaw)-x*math.sin(yaw)
        return np.stack([h, y*math.cos(pitch)-d*math.sin(pitch), y*math.sin(pitch)+d*math.cos(pitch)], axis=1)
    quads = [(q, orient(q["p"])) for q in models[stage]]
    allp = np.concatenate([p for q,p in quads]); lo=allp.min(axis=0); hi=allp.max(axis=0)
    scale = min((width-55)/(hi[0]-lo[0]), (height-55)/(hi[1]-lo[1]))
    for q,p in sorted(quads, key=lambda qp: qp[1][:,2].mean()):
        n=np.cross(p[1]-p[0],p[2]-p[0]); n=n/np.linalg.norm(n)
        if n[2] <= .00001: continue
        dest = np.stack([(p[:,0]-(lo[0]+hi[0])/2)*scale+width/2, -(p[:,1]-(lo[1]+hi[1])/2)*scale+height/2],axis=1)
        texture = textures[q["m"]]
        u0,v0,u1,v1=q["uv"]
        crop=texture.crop((u0/16*texture.width, v0/16*texture.height,
                           max(u0/16*texture.width+1,u1/16*texture.width), max(v0/16*texture.height+1,v1/16*texture.height)))
        # Affine projection of each cuboid face; no perspective warping needed.
        basis = np.column_stack(((dest[3]-dest[0])/crop.width, (dest[1]-dest[0])/crop.height))
        if abs(np.linalg.det(basis)) < 1e-8: continue
        inverse=np.linalg.inv(basis); offset=-inverse@dest[0]
        coeff=(inverse[0,0],inverse[0,1],offset[0],inverse[1,0],inverse[1,1],offset[1])
        plane=crop.transform((width,height),Image.Transform.AFFINE,coeff,resample=Image.Resampling.NEAREST)
        light=max(.32,min(1,.66+n[1]*.35+n[2]*.25-n[0]*.2+q["glow"]/400))
        plane=ImageEnhance.Brightness(plane).enhance(light)
        mask=Image.new("L",(width,height));ImageDraw.Draw(mask).polygon([tuple(x) for x in dest],fill=255)
        view.paste(plane,(0,0),mask)
    out.paste(view,(left,top))
    return lo,hi


draw.text((55,32),"ELECTRIC GLIDER  /  MK II",font=font(17),fill="#e4a477")
draw.text((55,60),"Armored flight assembly",font=font(38),fill="#e8eff3")
draw.text((55,117),"Steel and blue armor panels · Copper power feeds · Twin vented thrusters",font=font(19),fill="#adc0ca")
render("unfolded",(45,165,1040,520))
render("backpack",(1100,165,455,520),.5,.2)
draw.text((65,635),"DEPLOYED",font=font(16),fill="#becdd5")
draw.text((1120,635),"STOWED",font=font(16),fill="#becdd5")
render("transition1",(45,710,510,235),.32,.1)
render("transition2",(575,710,510,235),.32,.1)
draw.text((1120,730),"Materials",font=font(22),fill="#e3eaf0")
for i,(key,label) in enumerate([("mk2panel","Armor blue"),("mk2steel","Steel chassis"),("mk2edge","Zinc edging"),("mk2copper","Copper feeds"),("mk2vent","Recessed vents")]):
    out.paste(textures[key].resize((30,25),Image.Resampling.NEAREST),(1120,772+i*32))
    draw.text((1165,772+i*32-2),label,font=font(17),fill="#b4c6d0")
draw.text((55,978),"Model inspection render from the packaged geometry and installed textures. Not an in-game or VSPT screenshot.",font=font(17),fill="#98aebd")
path=data["root"] / "artifacts/mk2-visual-review.png"
out.save(path)
print(path)
