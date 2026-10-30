"""Generate the Mk II's original cuboid model; no third-party shapes are copied.

Run with Python 3 from any directory. Textures stay references to installed
game/EP assets. Geometry uses the native UpperTorso attachment coordinate frame.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets/electricglidermk2/shapes/entity/mk2"
TEXTURES = {
    "mk2steel": "game:block/metal/plate/steel",
    "mk2edge": "game:block/metal/plate/zinc",
    "mk2copper": "game:block/metal/plate/copper",
    "mk2panel": "electricalprogressiveequipment:armor/cheap",
    "mk2vent": "game:block/coal/coke",
}


def box(name, lo, hi, material, glow=0):
    faces = {}
    size = [hi[i] - lo[i] for i in range(3)]
    for face, axes in {"north": (0, 1), "east": (2, 1), "south": (0, 1),
                       "west": (2, 1), "up": (0, 2), "down": (0, 2)}.items():
        faces[face] = {"texture": "#mk2" + material,
                       "uv": [0, 0, min(16, size[axes[0]] * 2), min(16, size[axes[1]] * 2)]}
        if material in ("steel", "edge", "copper"):
            faces[face]["reflectiveMode"] = "Weak"
        if glow:
            faces[face]["glow"] = glow
    return {"name": name, "from": lo, "to": hi, "faces": faces}


def joint(name, origin, children, angle=0):
    return {"name": name, "from": origin, "to": origin, "rotationOrigin": origin,
            "rotationX": angle, "faces": {}, "children": children}


def wing(side, angles):
    tag = "L" if side == 1 else "R"
    # Each span segment has its own hinge; the pack accordion-folds to a short
    # stack instead of storing a full-length wing through the player's legs.
    def panel(index):
        span = [10, 11, 12][index]
        chord = [12, 10, 7][index]
        z0, z1 = sorted((0, side * span))
        result = [
            box(f"{tag}Panel{index}", [-0.22, 0, z0], [0.02, chord, z1], "panel"),
            box(f"{tag}LeadingEdge{index}", [-0.40, chord, z0], [0.28, chord + 0.65, z1], "edge"),
            box(f"{tag}TrailingEdge{index}", [-0.34, -0.45, z0], [0.18, 0, z1], "steel"),
            box(f"{tag}Rib{index}", [0.03, 0, min(side * (span - 0.55), side * span)],
                [0.38, chord, max(side * (span - 0.55), side * span)], "steel"),
            box(f"{tag}PowerBus{index}", [0.04, chord - 1.2, z0], [0.18, chord - 0.8, z1], "copper"),
        ]
        if index < 2:
            hinge = joint(f"{tag}Hinge{index + 1}", [0.45, 3, side * span], panel(index + 1))
            hinge["rotationY"] = side * angles[index + 1]
            hinge["rotationX"] = -side * [9, 8, 10][index + 1] * (1 - angles[0] / 87)
            result.append(hinge)
        else:
            # Raised paired bars are the Mk II identity mark; no lettering atlas.
            for n in range(2):
                a, b = sorted((side * (span - 3 - n * 1.2), side * (span - 2.45 - n * 1.2)))
                result.append(box(f"{tag}MarkII{n}", [0.03, 1.2, a], [0.26, 5.4, b], "edge"))
        return result
    hinge = joint(f"{tag}WingRoot", [-0.15, -3.5, 0.75 + side * 4], panel(0))
    hinge["rotationY"] = side * angles[0]
    hinge["rotationX"] = -side * 9 * (1 - angles[0] / 87)
    return hinge


def pack():
    parts = [
        box("Mk2Spine", [-0.9, -4.6, -2.9], [1.1, 8.2, 4.4], "steel"),
        box("AccumulatorCasing", [1.1, -3.8, -1.7], [3.1, 6.9, 3.2], "panel"),
        box("BatteryCrown", [1.05, 6.9, -1.85], [3.25, 7.7, 3.35], "edge"),
        box("BatteryFoot", [1.05, -4.4, -1.85], [3.25, -3.8, 3.35], "edge"),
        box("BackplateRailL", [3.10, -3.8, -1.7], [3.40, 6.9, -1.15], "steel"),
        box("BackplateRailR", [3.10, -3.8, 2.65], [3.40, 6.9, 3.2], "steel"),
    ]
    for z in [-0.25, 1.25]:
        parts.append(box("SpineMarkII" + str(z), [3.12, 0.5, z], [3.48, 4.8, z + 0.45], "edge"))
    for side in [-1, 1]:
        z = 0.75 + side * 4.3
        tag = "L" if side == 1 else "R"
        parts += [
            box(tag + "ThrusterHousing", [0.6, -4.2, z - 1.45], [3.8, 5.4, z + 1.45], "steel"),
            box(tag + "ThrusterArmour", [3.81, -2.2, z - 1.1], [4.15, 4.1, z + 1.1], "panel"),
            box(tag + "IntakeRim", [0.35, 5.4, z - 1.65], [4.05, 6.15, z + 1.65], "edge"),
            box(tag + "Intake", [0.65, 6.16, z - 1.15], [3.75, 6.32, z + 1.15], "vent"),
            box(tag + "NozzleCollar", [0.25, -4.9, z - 1.65], [4.15, -4.2, z + 1.65], "copper"),
            box(tag + "Nozzle", [0.75, -6.0, z - 1.1], [3.65, -4.9, z + 1.1], "vent"),
            # A modest warm vent glow makes the nozzle readable; actual cast
            # light is conditional in ThrustLight, never in a static texture.
            box(tag + "Emitter", [1.0, -6.08, z - 0.85], [3.4, -6.0, z + 0.85], "copper", 32),
        ]
        for y in [-1.8, -0.5, 0.8, 2.1, 3.4]:
            parts.append(box(tag + "CoolingFin" + str(y), [4.16, y, z - 1.18], [4.45, y + 0.3, z + 1.18], "edge"))
        # Exposed copper conduit runs from the accumulator to each pod.
        za, zb = sorted((0.75 + side * 1.4, z))
        parts.append(box(tag + "PowerFeed", [2.0, 6.4, za], [2.5, 6.9, zb], "copper"))
    return parts


def make_shape(angles):
    origin = joint("Mk2Origin", [6, -3, 2.5], pack() + [wing(1, angles), wing(-1, angles)])
    origin["stepParentName"] = "UpperTorso"
    return {"editor": {"backDropShape": "game:entity/humanoid/seraph", "allAngles": True, "entityTextureMode": True},
            "textureWidth": 16, "textureHeight": 16,
            "textures": TEXTURES, "elements": [origin]}


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for stage, angles in {"backpack": [87, -178, 178], "transition1": [58, -118, 118],
                          "transition2": [27, -56, 56], "unfolded": [0, 0, 0]}.items():
        (OUT / f"glider-{stage}.json").write_text(json.dumps(make_shape(angles), indent=2) + "\n", encoding="utf-8")
    item_path = ROOT / "assets/electricglidermk2/itemtypes/glider-mk2.json"
    item = json.loads(item_path.read_text(encoding="utf-8-sig"))
    for key, stage in {"attachShape": "backpack", "unfoldShapeStep1": "transition1",
                       "unfoldShapeStep2": "transition2", "unfoldedShape": "unfolded"}.items():
        item["attributes"][key]["base"] = f"electricglidermk2:entity/mk2/glider-{stage}"
    item["shape"]["base"] = "electricglidermk2:entity/mk2/glider-backpack"
    item_path.write_text(json.dumps(item, indent=2) + "\n", encoding="utf-8")
    print("Generated four original Mk II deployment shapes and updated only Mk II shape references.")
