"""Private, standalone preview of the actual JSON geometry and installed textures.

Usage: python scripts/Build-VisualPreview.py GAME_FOLDER EP_EQUIPMENT_ZIP
The generated artifact embeds installed textures; do not redistribute it.
This is a model inspection viewer, not a game or VSPT render.
"""
import base64
import json
import math
import sys
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
game, ep = Path(sys.argv[1]), zipfile.ZipFile(sys.argv[2])
faces = {"north": [0, 2, 6, 4], "south": [5, 7, 3, 1], "east": [4, 6, 7, 5],
         "west": [1, 3, 2, 0], "up": [2, 3, 7, 6], "down": [1, 0, 4, 5]}


def flatten(shape):
    quads = []
    def walk(e, parent):
        origin = e.get("rotationOrigin", [0, 0, 0])
        def transform(p):
            x, y, z = [p[i] - origin[i] for i in range(3)]
            for axis in "XYZ":
                a = math.radians(e.get("rotation" + axis, 0)); c, s = math.cos(a), math.sin(a)
                if axis == "X": y, z = c*y-s*z, s*y+c*z
                if axis == "Y": x, z = c*x+s*z, -s*x+c*z
                if axis == "Z": x, y = c*x-s*y, s*x+c*y
            return parent([x + origin[0], y + origin[1], z + origin[2]])
        lo, hi = e["from"], e["to"]
        corners = [[(hi if i & 4 else lo)[0], (hi if i & 2 else lo)[1], (hi if i & 1 else lo)[2]] for i in range(8)]
        for face, spec in e.get("faces", {}).items():
            quads.append({"p": [transform(corners[i]) for i in faces[face]], "m": spec["texture"][1:], "uv": spec["uv"], "glow": spec.get("glow", 0)})
        for child in e.get("children", []):
            walk(child, lambda p: transform([p[i] + lo[i] for i in range(3)]))
    for e in shape["elements"]: walk(e, lambda p: p)
    return quads


shapes, textures = {}, {}
for file in (root / "assets/electricglidermk2/shapes/entity/mk2").glob("*.json"):
    shape = json.loads(file.read_text()); shapes[file.stem.replace("glider-", "")] = flatten(shape)
    for name, location in shape["textures"].items():
        if name in textures: continue
        domain, path = location.split(":")
        if domain == "game":
            data = next(p.read_bytes() for d in (game / "assets").iterdir() if (p := d / "textures" / (path + ".png")).is_file())
        else:
            data = ep.read(f"assets/{domain}/textures/{path}.png")
        textures[name] = "data:image/png;base64," + base64.b64encode(data).decode()

template = r'''<!doctype html><html><head><meta charset="utf-8"><title>Mk II — model review</title>
<style>*{box-sizing:border-box}body{margin:0;background:#10171c;color:#dbe4e8;font:15px system-ui}header{padding:28px 36px 16px;display:flex;justify-content:space-between;gap:30px}h1{font-size:28px;letter-spacing:-.8px;margin:4px 0 8px}small{color:#9caeb8}header p{max-width:580px;color:#a9b8c0;line-height:1.5;margin:0}.badge{color:#e6a26b;font-size:11px;letter-spacing:2px}nav{padding:12px 36px;display:flex;gap:8px;align-items:center}button{padding:9px 16px;border:1px solid #3e4a53;background:#19242c;color:#d4dfe5;border-radius:6px;cursor:pointer}button.active{border-color:#e4a46d;background:#49372b}canvas{display:block;width:100%;height:65vh;min-height:400px;cursor:grab;background:radial-gradient(ellipse at center,#283844,#131c23 73%)}footer{padding:18px 36px;display:flex;gap:30px;flex-wrap:wrap;color:#a9b8c0}.swatch{display:inline-block;width:17px;height:17px;vertical-align:-3px;margin-right:7px;border:1px solid #66717a}.note{margin:0 36px 24px;color:#879da9;font-size:12px;line-height:1.5}input{accent-color:#df9f6b}label{margin-left:auto;color:#a9b8c0}</style></head>
<body><header><div><div class="badge">ELECTRICAL PROGRESSIVE · MK II</div><h1>Armored flight assembly</h1><p>Steel chassis. Blue armor panels. Copper power feeds. Twin vented thrusters and reinforced folding wings.</p></div><small>MODEL REVIEW<br>Drag to orbit · Scroll to zoom</small></header>
<nav><button data-stage="backpack">Stowed</button><button data-stage="transition1">Deploy 1</button><button data-stage="transition2">Deploy 2</button><button class="active" data-stage="unfolded">Flight</button><button id="reset">Reset view</button><label><input id="wire" type="checkbox"> Edges</label></nav><canvas id="view"></canvas>
<footer><span><i class="swatch" data-m="mk2panel"></i>Armor blue</span><span><i class="swatch" data-m="mk2steel"></i>Steel chassis</span><span><i class="swatch" data-m="mk2edge"></i>Zinc edging</span><span><i class="swatch" data-m="mk2copper"></i>Copper feeds</span><span><i class="swatch" data-m="mk2vent"></i>Recessed vents</span></footer>
<p class="note">Actual packaged model geometry and installed textures, viewed with simple preview lighting. This is not an in-game or VSPT screenshot. Runtime thrust adds orange entity light only while charged boost or lift is active. This private preview embeds dependency textures.</p>
<script>const models=__MODELS__,sources=__TEXTURES__;const images={};const canvas=document.querySelector('#view'),ctx=canvas.getContext('2d');let stage='unfolded',yaw=.38,pitch=.24,zoom=1,drag=null;
for(const [key,url]of Object.entries(sources)){images[key]=new Image();images[key].src=url;images[key].onload=draw}document.querySelectorAll('[data-m]').forEach(e=>e.style.backgroundImage=`url(${sources[e.dataset.m]})`);
function orient(p){let[x,y,z]=p;let d=x*Math.cos(yaw)+z*Math.sin(yaw),h=z*Math.cos(yaw)-x*Math.sin(yaw);return [h,y*Math.cos(pitch)-d*Math.sin(pitch),y*Math.sin(pitch)+d*Math.cos(pitch)]}
function draw(){let w=canvas.clientWidth,h=canvas.clientHeight;canvas.width=w*devicePixelRatio;canvas.height=h*devicePixelRatio;ctx.setTransform(devicePixelRatio,0,0,devicePixelRatio,0,0);ctx.clearRect(0,0,w,h);ctx.imageSmoothingEnabled=false;
let mesh=models[stage].map(q=>({...q,p:q.p.map(orient)}));let pts=mesh.flatMap(q=>q.p),min=[0,1].map(i=>Math.min(...pts.map(p=>p[i]))),max=[0,1].map(i=>Math.max(...pts.map(p=>p[i])));let scale=Math.min((w-130)/(max[0]-min[0]),(h-70)/(max[1]-min[1]))*zoom,cx=(min[0]+max[0])/2,cy=(min[1]+max[1])/2;
mesh.sort((a,b)=>a.p.reduce((s,p)=>s+p[2],0)-b.p.reduce((s,p)=>s+p[2],0));for(let q of mesh){let a=q.p[0],b=q.p[1],c=q.p[2],u=b.map((v,i)=>v-a[i]),v=c.map((x,i)=>x-a[i]),n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]],len=Math.hypot(...n);if(n[2]<=.00001)continue;let p=q.p.map(v=>[(v[0]-cx)*scale+w/2,-(v[1]-cy)*scale+h/2]);ctx.save();ctx.beginPath();p.forEach((v,i)=>i?ctx.lineTo(...v):ctx.moveTo(...v));ctx.closePath();ctx.clip();let im=images[q.m];if(im?.complete&&im.naturalWidth){let [u0,v0,u1,v1]=q.uv,sw=(u1-u0)*im.width/16,sh=(v1-v0)*im.height/16;ctx.save();ctx.transform((p[3][0]-p[0][0])/sw,(p[3][1]-p[0][1])/sw,(p[1][0]-p[0][0])/sh,(p[1][1]-p[0][1])/sh,p[0][0],p[0][1]);ctx.drawImage(im,u0*im.width/16,v0*im.height/16,sw,sh,0,0,sw,sh);ctx.restore()}else{ctx.fillStyle='#66717a';ctx.fill()}
let light=Math.max(.32,Math.min(1,.66+(n[1]*.35+n[2]*.25-n[0]*.2)/len+q.glow/400));ctx.fillStyle=`rgba(0,0,0,${1-light})`;ctx.fill();ctx.restore();if(document.querySelector('#wire').checked){ctx.beginPath();p.forEach((v,i)=>i?ctx.lineTo(...v):ctx.moveTo(...v));ctx.closePath();ctx.strokeStyle='#c0d3dd66';ctx.lineWidth=.5;ctx.stroke()}}
ctx.fillStyle='#8095a2';ctx.font='12px system-ui';ctx.fillText(stage==='backpack'?'STOWED · COMPACT FOLD':'DEPLOYMENT · '+stage.toUpperCase(),28,h-20)}
document.querySelectorAll('[data-stage]').forEach(b=>b.onclick=()=>{stage=b.dataset.stage;zoom=1;document.querySelectorAll('[data-stage]').forEach(n=>n.classList.toggle('active',n===b));draw()});document.querySelector('#reset').onclick=()=>{yaw=.38;pitch=.24;zoom=1;draw()};document.querySelector('#wire').onchange=draw;canvas.onpointerdown=e=>{drag=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId)};canvas.onpointerup=()=>drag=null;canvas.onpointermove=e=>{if(!drag)return;yaw+=(e.clientX-drag[0])*.008;pitch=Math.max(-1.4,Math.min(1.4,pitch+(e.clientY-drag[1])*.008));drag=[e.clientX,e.clientY];draw()};canvas.onwheel=e=>{e.preventDefault();zoom=Math.max(.4,Math.min(3,zoom*Math.exp(-e.deltaY*.001)));draw()};window.onresize=draw;draw();</script></body></html>'''
out = root / "artifacts/mk2-visual-preview.html"
out.parent.mkdir(exist_ok=True)
out.write_text(template.replace("__MODELS__", json.dumps(shapes)).replace("__TEXTURES__", json.dumps(textures)), encoding="utf-8")
print(out)
