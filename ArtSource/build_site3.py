# Road-end models for the long highway: a rock-cut tunnel portal (the road ends in a cave), a guardrail section and a
# chevron sign for the mountain bend.   Headless:  /Applications/Blender.app/Contents/MacOS/Blender -b --python ArtSource/build_site3.py
# Metres, +Z up, front faces -Y (Unity +Z at yaw 0). The portal's origin is the centre of its front face at road level;
# the tunnel runs into +Y (into the mountain).
import bpy, bmesh, math, os, json
from mathutils import Matrix, Vector

try:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    ROOT = "/Users/tasrifibnmizan/Documents/GitHub/space-agency"
_src = open(os.path.join(ROOT, "ArtSource", "build_site.py"), encoding="utf-8").read()
exec(_src.split("# ================================================================ BUILD + EXPORT")[0].split("# ================================================================ ROADS")[0])
OUT_S = os.path.join(ROOT, "Assets", "Assets", "Models", "Site")
BLACK = 32                                           # tyre cell (near black): the unlit inside of the tunnel


def export(ob, name):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_S, name + ".fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)


def tunnel_portal():
    b = B()
    OW, OH = 27.0, 11.5                              # opening: two 11.4 m lanes + margins, 11.5 m clearance
    T = 5.0                                          # head wall thickness
    HW, HH = 52.0, 24.0                              # head wall width and height
    pier = (HW - OW) / 2
    for sx in (-1, 1):
        b.box((sx * (OW / 2 + pier / 2), T / 2, HH / 2), (pier, T, HH), CONC)             # piers
        b.box((sx * (OW / 2 + 0.6), T / 2 + 0.3, OH / 2), (1.2, T + 0.6, OH), LCONC)      # reveal
        for k in range(5):                                                                 # pilaster ribs
            b.box((sx * (OW / 2 + 1.6 + k * 4.4), -0.25, HH / 2), (0.7, 0.5, HH - 1.5), LCONC)
        # sloped wing walls stepping back into the hillside
        for i in range(4):
            wx = sx * (HW / 2 + 3.0 + i * 5.5)
            wh = HH - 4.5 - i * 5.0
            b.box((wx, T / 2 + i * 1.5, wh / 2), (5.6, T, max(wh, 2.0)), CONC)
    b.box((0, T / 2, OH + (HH - OH) / 2), (OW + 2.4, T, HH - OH), CONC)                    # lintel
    b.box((0, -0.3, OH + 0.3), (OW + 3.0, 0.6, 0.9), LCONC)                                # arch beam
    b.box((0, -0.35, HH - 0.5), (HW + 1.6, 0.8, 1.0), LCONC)                               # cornice
    b.box((0, -0.55, HH - 3.6), (14.0, 0.2, 3.2), BLUEC)                                   # tunnel name plate
    b.box((0, -0.7, HH - 3.6), (11.0, 0.12, 0.6), WHITE)
    # hazard chevrons on the portal edges
    for sx in (-1, 1):
        for k in range(8):
            b.box((sx * (OW / 2 + 0.5), -0.45, 0.8 + k * 1.3), (0.3, 0.12, 0.7), HAZ if k % 2 else DARK)
    # the tunnel: concrete shell with a dark interior that ends in a black wall
    L = 40.0
    b.box((0, T + L / 2, -0.5), (OW + 2.4, L, 1.0), ASPH)                                  # floor
    b.box((0, T + L / 2, OH + 0.9), (OW + 3.6, L, 1.8), CONC)                              # roof slab
    for sx in (-1, 1):
        b.box((sx * (OW / 2 + 0.7), T + L / 2, OH / 2), (1.4, L, OH + 1.0), CONC)          # side walls
        b.box((sx * (OW / 2 - 0.12), T + L / 2, OH / 2), (0.1, L, OH), DARK)               # inner lining
    b.box((0, T + L - 0.2, OH / 2), (OW, 0.4, OH), BLACK)                                  # black end
    b.box((0, T + L / 2, OH - 0.05), (OW, L, 0.1), DARK)                                   # ceiling lining
    for k in range(6):                                                                     # tunnel lamps (glass slot, lit at night)
        y = T + 4.0 + k * 6.0
        for sx in (-1, 1):
            b.box((sx * (OW / 2 - 1.2), y, OH - 0.7), (0.5, 1.6, 0.25), GLASS, mat=1)
    # road edge posts and the dark verge in front
    for sx in (-1, 1):
        for k in range(4):
            b.box((sx * (OW / 2 + 2.0), -3.0 - k * 6.0, 0.55), (0.18, 0.18, 1.1), WHITE)
    return b.finish("TunnelPortal")


def guardrail():
    """Twelve metres of W-beam guardrail along +X."""
    b = B()
    for i in range(7):
        b.box((-6.0 + i * 2.0, 0, 0.45), (0.14, 0.14, 0.9), STEEL)
    for z in (0.62, 0.78):
        b.box((0, 0.08, z), (12.0, 0.06, 0.26 if z < 0.7 else 0.12), STEEL)
    b.box((0, 0.1, 0.62), (12.0, 0.1, 0.1), GRAY)
    for i in range(6):
        b.box((-5.0 + i * 2.0, 0.1, 0.7), (0.3, 0.14, 0.2), HAZ)                           # reflectors
    return b.finish("Guardrail")


def road_lamp():
    """Street lamp for the site roads: pole with one arm reaching over the road (towards -Y = Unity +Z). Lamp glass is slot 1."""
    b = B()
    b.box((0, 0, 0.25), (0.7, 0.7, 0.5), CONC)                                     # base
    b.cyl((0, 0, 4.2), 0.13, 7.6, STEEL, seg=8, r2=0.1)                            # pole
    b.box((0, -1.2, 7.85), (0.12, 2.5, 0.12), STEEL)                               # arm
    b.box((0, -2.3, 7.7), (0.56, 1.1, 0.24), DARK)                                 # lamp housing
    b.box((0, -2.3, 7.56), (0.46, 0.92, 0.05), LCONC, mat=1)                       # lamp glass
    b.box((0, 0, 7.5), (0.22, 0.22, 0.5), STEEL)                                   # arm bracket
    return b.finish("RoadLamp")


def chevron_sign():
    """Large black-and-yellow chevron board on a post for the bend."""
    b = B()
    b.box((0, 0, 1.4), (0.14, 0.14, 2.8), STEEL)
    b.box((0, -0.12, 3.0), (2.2, 0.1, 1.6), HAZ)
    for dx in (-0.45, 0.45):
        for k in range(3):
            y = 3.0 + (k - 1) * 0.42
            b.box((dx, -0.2, y + 0.12), (0.9, 0.05, 0.14), DARK, rot=Matrix.Rotation(math.radians(-35 if dx < 0 else 35), 3, "Y"))
            b.box((dx, -0.2, y - 0.12), (0.9, 0.05, 0.14), DARK, rot=Matrix.Rotation(math.radians(35 if dx < 0 else -35), 3, "Y"))
    return b.finish("ChevronSign")


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)

manifest = {}
x = 0
for name, fn in (("TunnelPortal", tunnel_portal), ("Guardrail", guardrail), ("ChevronSign", chevron_sign), ("RoadLamp", road_lamp)):
    ob = fn()
    d = ob.dimensions
    manifest[name] = {"w": round(d.x, 2), "d": round(d.y, 2), "h": round(d.z, 2), "tris": sum(len(p.vertices) - 2 for p in ob.data.polygons)}
    export(ob, name)
    ob.location.x = x
    x += 80
path = os.path.join(OUT_S, "site-manifest.json")
old = json.load(open(path)) if os.path.exists(path) else {}
old.update(manifest)
json.dump(old, open(path, "w"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "space-site3.blend"))
print("DONE", manifest)
