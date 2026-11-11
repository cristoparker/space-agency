# Props for the mission-control office of the flight scene (3. Flight): consoles with three screens, an office chair, a plant, a ceiling light panel,
# a filing cabinet, a window wall with blinds (a lit night skyline behind the glass), a whiteboard and a coffee station.
# Headless:  /Applications/Blender.app/Contents/MacOS/Blender -b --python ArtSource/build_office.py
# Metres, +Z up, front faces -Y (Unity +Z at yaw 0). Screens of the console face +Y (Unity -Z: towards the operators); the chair's back is at +Y.
import bpy, bmesh, math, os, json
from mathutils import Matrix, Vector

try:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    ROOT = "/Users/tasrifibnmizan/Documents/GitHub/space-agency"
_src = open(os.path.join(ROOT, "ArtSource", "build_site.py"), encoding="utf-8").read()
exec(_src.split("# ================================================================ BUILD + EXPORT")[0].split("# ================================================================ ROADS")[0])
OUT_O = os.path.join(ROOT, "Assets", "Resources", "Office")
os.makedirs(OUT_O, exist_ok=True)
BLACK = 32


def export(ob, name):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_O, name + ".fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)


def console():
    """A two-and-a-half metre control desk with three screens and keyboards. Screens (slot 1, lit) face +Y."""
    b = B()
    b.box((0, 0, 0.72), (2.7, 0.95, 0.06), WHITE)                                   # desk top
    b.box((0, 0, 0.685), (2.62, 0.87, 0.02), GRAY)                                  # under-edge
    for sx in (-1, 1):
        b.box((sx * 1.28, 0, 0.35), (0.06, 0.82, 0.7), STEEL)                       # side panels
    b.box((0, -0.38, 0.36), (2.5, 0.03, 0.6), GRAY)                                 # modesty panel
    for x in (-0.9, 0.0, 0.9):
        b.box((x, -0.12, 0.745), (0.34, 0.22, 0.02), DARK)                          # stand base
        b.box((x, -0.12, 0.93), (0.06, 0.05, 0.36), DARK)                           # stand
        b.box((x, -0.12, 1.22), (0.78, 0.05, 0.46), DARK)                           # screen housing
        b.box((x, -0.092, 1.22), (0.72, 0.012, 0.4), WIN, mat=1)                    # lit screen
        b.box((x, 0.2, 0.745), (0.44, 0.14, 0.02), DARK)                            # keyboard
        b.box((x + 0.34, 0.2, 0.745), (0.07, 0.1, 0.02), DARK)                      # mouse
    b.box((1.05, 0.3, 0.76), (0.12, 0.12, 0.04), BLUEC)                             # headset base
    return b.finish("OfficeConsole")


def chair():
    b = B()
    b.cyl((0, 0, 0.28), 0.035, 0.4, STEEL, seg=8)                                   # gas lift
    for k in range(5):
        a = k * 72
        rot = Matrix.Rotation(math.radians(a), 3, "Z")
        b.box((math.cos(math.radians(a)) * 0.15, math.sin(math.radians(a)) * 0.15, 0.1), (0.34, 0.05, 0.04), DARK, rot=rot)   # base arm
        b.cyl((math.cos(math.radians(a)) * 0.3, math.sin(math.radians(a)) * 0.3, 0.04), 0.035, 0.07, BLACK, seg=6)              # wheel
    b.box((0, 0, 0.5), (0.52, 0.5, 0.09), SLATE)                                    # seat
    b.box((0, 0.24, 0.82), (0.5, 0.08, 0.56), SLATE)                                # back
    b.box((0, 0.2, 0.55), (0.1, 0.06, 0.12), DARK)                                  # back post
    for sx in (-1, 1):
        b.box((sx * 0.29, 0.02, 0.7), (0.05, 0.36, 0.04), DARK)                     # arm rests
        b.box((sx * 0.29, -0.12, 0.6), (0.04, 0.04, 0.2), DARK)
    return b.finish("OfficeChair")


def plant():
    b = B()
    b.cyl((0, 0, 0.2), 0.24, 0.4, RUST, seg=10, r2=0.18)                            # pot
    b.cyl((0, 0, 0.4), 0.21, 0.04, DOOR, seg=10)                                    # soil
    import random
    rnd = random.Random(4)
    for k in range(11):
        a = rnd.uniform(0, 360); tilt = rnd.uniform(15, 50); h = rnd.uniform(0.5, 1.0)
        rot = Matrix.Rotation(math.radians(a), 3, "Z") @ Matrix.Rotation(math.radians(tilt), 3, "X")
        b.box((math.sin(math.radians(a)) * 0.12, -math.cos(math.radians(a)) * 0.12, 0.7), (0.08, 0.02, h), GREEN, rot=rot)
        b.box((math.sin(math.radians(a)) * 0.3, -math.cos(math.radians(a)) * 0.3, 0.8 + h * 0.2), (0.24, 0.05, 0.16), GREEN, rot=rot)
    return b.finish("OfficePlant")


def light_panel():
    """A 1.2 x 0.6 m ceiling light. Origin at the ceiling; the lit glass (slot 1) faces down."""
    b = B()
    b.box((0, 0, 0.02), (1.28, 0.68, 0.05), WHITE)
    b.box((0, 0, -0.01), (1.14, 0.54, 0.02), LCONC, mat=1)
    return b.finish("OfficeLight")


def cabinet():
    b = B()
    b.box((0, 0, 0.9), (0.9, 0.46, 1.8), GRAY)
    for i in range(4):
        z = 0.25 + i * 0.42
        b.box((0, -0.235, z), (0.82, 0.02, 0.38), CONC)                            # drawer front
        b.box((0, -0.255, z + 0.1), (0.2, 0.02, 0.03), STEEL)                      # handle
    b.box((0, 0, 1.82), (0.92, 0.48, 0.03), DARK)
    return b.finish("OfficeCabinet")


def window_wall():
    """Four metres of wall with two tall windows, blinds half down, and a night skyline of lit windows behind the glass. The room is on -Y."""
    b = B()
    W, H = 4.0, 4.2
    b.box((0, 0, 0.1), (W, 0.2, 0.2), DARK)                                         # skirting
    b.box((-1.9, 0, H / 2), (0.2, 0.2, H), WALL); b.box((1.9, 0, H / 2), (0.2, 0.2, H), WALL)
    b.box((0, 0, H / 2), (0.2, 0.2, H), WALL)                                       # pier between the windows
    b.box((0, 0, 3.8), (W, 0.2, 0.8), WALL); b.box((0, 0, 0.6), (W, 0.2, 0.8), WALL)
    for cx in (-0.95, 0.95):
        b.box((cx, 0.06, 2.2), (1.7, 0.03, 2.5), BLACK)                              # dark glass
        for k in range(9):                                                           # skyline: lit windows of far buildings
            x = cx - 0.7 + (k % 5) * 0.32; z = 1.4 + ((k * 7) % 5) * 0.42
            b.box((x, 0.1, z), (0.1, 0.01, 0.1), ORANGE, mat=1)
        for k in range(10):                                                          # blinds
            b.box((cx, -0.06, 3.35 - k * 0.07), (1.66, 0.025, 0.045), LCONC)
        b.box((cx, -0.1, 3.52), (1.74, 0.07, 0.07), STEEL)                           # blind box
    return b.finish("OfficeWindowWall")


def whiteboard():
    b = B()
    b.box((0, 0, 1.5), (2.5, 0.05, 1.3), STEEL)
    b.box((0, -0.03, 1.5), (2.4, 0.02, 1.2), WHITE)
    b.box((0, -0.06, 0.85), (1.6, 0.07, 0.05), STEEL)
    for i, c in enumerate((RED, BLUEC, GREEN)):
        b.box((-0.4 + i * 0.3, -0.07, 0.89), (0.12, 0.025, 0.025), c)
    b.box((-0.6, -0.04, 1.7), (0.6, 0.01, 0.01), BLUEC); b.box((-0.5, -0.04, 1.55), (0.8, 0.01, 0.01), BLUEC); b.box((-0.4, -0.04, 1.4), (0.5, 0.01, 0.01), RED)
    return b.finish("OfficeWhiteboard")


def coffee():
    b = B()
    b.box((0, 0, 0.45), (1.8, 0.6, 0.9), GRAY)                                      # counter
    b.box((0, 0, 0.92), (1.84, 0.64, 0.04), WHITE)
    b.box((-0.45, 0.1, 1.15), (0.4, 0.38, 0.45), DARK)                              # machine
    b.box((-0.45, -0.1, 1.28), (0.1, 0.02, 0.06), RED, mat=1)                       # light
    b.box((0.35, 0.1, 1.02), (0.3, 0.3, 0.16), STEEL)                               # kettle
    for i in range(4):
        b.cyl((0.0 + i * 0.12, -0.12, 0.99), 0.04, 0.1, WHITE, seg=8)               # cups
    b.box((0, -0.31, 0.45), (1.7, 0.02, 0.7), DARK)                                 # cupboard doors
    return b.finish("OfficeCoffee")


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)

manifest = {}
x = 0
for name, fn in (("OfficeConsole", console), ("OfficeChair", chair), ("OfficePlant", plant), ("OfficeLight", light_panel), ("OfficeCabinet", cabinet),
                 ("OfficeWindowWall", window_wall), ("OfficeWhiteboard", whiteboard), ("OfficeCoffee", coffee)):
    ob = fn()
    d = ob.dimensions
    manifest[name] = {"w": round(d.x, 2), "d": round(d.y, 2), "h": round(d.z, 2), "tris": sum(len(p.vertices) - 2 for p in ob.data.polygons)}
    export(ob, name)
    ob.location.x = x
    x += 6
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "space-office.blend"))
print("DONE", manifest)
