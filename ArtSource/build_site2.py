# Second batch of site models for the map layout: Power Grid (3 tiers), car park, staff housing, perimeter fence, gate, generic plot.
#   Headless:  /Applications/Blender.app/Contents/MacOS/Blender -b --python ArtSource/build_site2.py
# Metres, origin at the centre of the base, +Z up, front (entrance) faces -Y. Reuses the builder + helpers from build_site.py.
import bpy, bmesh, math, os, json
from mathutils import Matrix, Vector

try:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    ROOT = "/Users/tasrifibnmizan/Documents/GitHub/space-agency"
_src = open(os.path.join(ROOT, "ArtSource", "build_site.py"), encoding="utf-8").read()
exec(_src.split("# ================================================================ BUILD + EXPORT")[0].split("# ================================================================ ROADS")[0])
OUT_S = os.path.join(ROOT, "Assets", "Assets", "Models", "Site")


def export(ob, name):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_S, name + ".fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)


BODY0 = 16                              # palette cells 16..23 are the eight car body colours


# ---------------------------------------------------------------- small parts
def transformer(b, x, y, s=1.0):
    b.box((x, y, 0.3), (6 * s, 4 * s, 0.6), CONC)                                  # plinth
    b.box((x, y, 2.6 * s), (5 * s, 3 * s, 4 * s), GRAY)                            # tank
    for i in range(6):
        b.box((x - 2.1 * s + i * 0.84 * s, y + 1.9 * s, 2.4 * s), (0.18 * s, 0.7 * s, 3.2 * s), DARK)   # cooling fins
    for dx in (-1.5, 0, 1.5):
        b.cyl((x + dx * s, y - 0.4 * s, 5.4 * s), 0.28 * s, 2.2 * s, WHITE, seg=8)     # bushings
        b.cyl((x + dx * s, y - 0.4 * s, 6.6 * s), 0.38 * s, 0.18 * s, DARK, seg=8)
    b.cyl((x, y - 1.2 * s, 4.9 * s), 0.55 * s, 4.2 * s, STEEL, seg=10, rot=RY(90))      # oil conservator


def portal(b, x, y, w=14.0, h=9.0):
    for sx in (-1, 1):
        b.box((x + sx * w / 2, y, h / 2), (0.5, 0.5, h), STEEL)
    b.box((x, y, h), (w + 0.8, 0.5, 0.5), STEEL)
    for k in range(4):
        b.cyl((x - w / 2 + 1.2 + k * (w - 2.4) / 3, y, h - 0.9), 0.14, 1.6, WHITE, seg=6)     # insulator strings


def container(b, x, y, rot_z=0.0, w=12.0):
    R = Matrix.Rotation(math.radians(rot_z), 3, "Z")
    b.box((x, y, 1.45), (w, 2.5, 2.6), WHITE, rot=R)
    for i in range(5):
        off = R @ Vector((-w / 2 + 1.4 + i * (w - 2.8) / 4, -1.28, 1.5))
        b.box((x + off.x, y + off.y, off.z), (1.6, 0.06, 1.6), DARK, rot=R)              # vents
    b.box((x, y, 2.85), (w + 0.1, 2.6, 0.12), STEEL, rot=R)


def cabin(b, x, y, w, d, h, windows=3):
    b.box((x, y, 0.2), (w + 1.0, d + 1.0, 0.4), LCONC)
    b.box((x, y, h / 2 + 0.4), (w, d, h), WALL)
    b.box((x, y, h + 0.55), (w + 0.6, d + 0.6, 0.3), SLATE)
    for i in range(windows):
        px = x - w / 2 + (i + 0.5) * w / windows
        b.win((px - x, y - d / 2 - 0.0, h * 0.62 + 0.4), min(1.8, w / windows - 0.8), 1.1, "y") if False else None
        b.box((px, y - d / 2 - 0.04, h * 0.62 + 0.4), (min(1.8, w / windows - 0.8) + 0.2, 0.08, 1.3), DARK)
        b.box((px, y - d / 2 - 0.07, h * 0.62 + 0.4), (min(1.8, w / windows - 0.8), 0.08, 1.1), WIN)
    b.box((x + w / 2 - 1.3, y - d / 2 - 0.05, 1.5), (1.2, 0.1, 2.2), DOOR)


def solar_field(b, x, y, rows, cols, dx=6.4, dy=5.2, tilt=28):
    rot = RX(-tilt)
    for r in range(rows):
        for c in range(cols):
            px, py = x + c * dx, y + r * dy
            b.box((px, py, 1.5), (6.0, 3.6, 0.1), BLUEC, rot=rot)
            b.box((px, py + 0.9, 0.7), (0.2, 0.2, 1.4), STEEL)


def pylon(b, x, y, h=32.0, cell=STEEL):
    segs = [(7.0, 7.0, 0.34), (5.0, 5.0, 0.3), (3.0, 3.0, 0.26)]
    z = 0.0
    sh = h / 3.0
    for i, (w, d, t) in enumerate(segs):
        lattice(b, x, y, z, w, d, sh, 3, leg=t * 2.2, cell=cell, brace=t)
        z += sh
    for k, (zz, ln) in enumerate(((h - 3.0, 9.0), (h - 7.5, 12.0), (h - 12.0, 9.0))):
        b.box((x, y, zz), (ln * 2, 0.5, 0.5), STEEL)
        for sx in (-1, 1):
            b.cyl((x + sx * (ln - 0.5), y, zz - 1.2), 0.18, 2.2, WHITE, seg=6)
    b.box((x, y, h + 1.5), (0.4, 0.4, 3.0), STEEL)


def wind_turbine(b, x, y, h=48.0):
    b.cyl((x, y, h / 2), 1.5, h, WHITE, seg=14, r2=0.95)
    b.box((x, y - 1.6, h + 1.0), (3.0, 6.4, 3.0), WHITE)                              # nacelle
    sphere(b, (x, y - 4.6, h + 1.0), 1.7, WHITE, seg=10)                              # hub
    for k in range(3):
        a = math.radians(90 + k * 120)
        tip = Vector((x + math.cos(a) * 24, y - 4.8, h + 1.0 + math.sin(a) * 24))
        beam(b, (x + math.cos(a) * 1.2, y - 4.8, h + 1.0 + math.sin(a) * 1.2), tip, 1.1, WHITE)


# ---------------------------------------------------------------- Power station: fenced compound, halls, stacks, switchyard, pylons with wires
def wire(b, p0, p1, sag=2.4, n=8, t=0.14, cell=DARK):
    """A hanging cable (catenary-ish) between two points, made of short beams."""
    p0, p1 = Vector(p0), Vector(p1)
    pts = []
    for i in range(n + 1):
        s = i / n
        p = p0 * (1 - s) + p1 * s
        p.z -= sag * 4 * s * (1 - s)
        pts.append(p)
    for a, c in zip(pts, pts[1:]):
        beam(b, a, c, t, cell)


def power_line(b, x, ys, h=32.0):
    """A row of lattice pylons along Y at x = x, with six conductors and a top earth wire strung between them."""
    for y in ys:
        pylon(b, x, y, h)
    tips = [(h - 3.0, 9.0), (h - 7.5, 12.0), (h - 12.0, 9.0)]
    for y0, y1 in zip(ys, ys[1:]):
        for zz, ln in tips:
            for sx in (-1, 1):
                wire(b, (x + sx * (ln - 0.5), y0, zz - 2.4), (x + sx * (ln - 0.5), y1, zz - 2.4), sag=2.6 + (y1 - y0) * 0.012)
        wire(b, (x, y0, h + 2.9), (x, y1, h + 2.9), sag=2.0 + (y1 - y0) * 0.01, t=0.1, cell=STEEL)


def border(b, w, d, gate=12.0, post=4.0):
    """Perimeter fence with a gate gap in the front (-Y) side, gate pillars and a guard booth."""
    segs = [(-w / 2, -d / 2, -gate / 2, -d / 2), (gate / 2, -d / 2, w / 2, -d / 2), (w / 2, -d / 2, w / 2, d / 2),
            (w / 2, d / 2, -w / 2, d / 2), (-w / 2, d / 2, -w / 2, -d / 2)]
    for x0, y0, x1, y1 in segs:
        L = math.hypot(x1 - x0, y1 - y0)
        n = max(1, int(round(L / post)))
        for i in range(n + 1):
            s = i / n
            b.box((x0 + (x1 - x0) * s, y0 + (y1 - y0) * s, 1.4), (0.2, 0.2, 2.8), DARK)
        for z in (0.4, 1.1, 1.8, 2.5):
            beam(b, (x0, y0, z), (x1, y1, z), 0.07, STEEL)
    for s in (-1, 1):
        b.box((s * gate / 2, -d / 2, 1.7), (0.7, 0.7, 3.4), CONC)
    b.box((gate / 2 + 4.5, -d / 2 + 3.0, 1.6), (4.0, 3.0, 3.0), WALL)                  # guard booth
    b.box((gate / 2 + 4.5, -d / 2 + 3.0, 3.25), (4.6, 3.6, 0.3), SLATE)
    b.box((gate / 2 + 4.5, -d / 2 + 1.45, 1.9), (2.6, 0.08, 1.1), WIN)
    b.box((-gate / 2 - 3.2, -d / 2 + 0.7, 1.3), (6.0, 0.15, 2.4), STEEL)                # sliding gate leaf (open)


def hall(b, x, y, w, d, h, windows=6):
    """Turbine hall: long shed with window band, roof ventilators and a roller door."""
    b.box((x, y, 0.3), (w + 1.0, d + 1.0, 0.6), LCONC)
    b.box((x, y, h / 2 + 0.6), (w, d, h), GRAY)
    b.box((x, y, h + 0.9), (w + 0.8, d + 0.8, 0.5), SLATE)
    for i in range(windows):
        px = x - w / 2 + (i + 0.5) * w / windows
        b.box((px, y - d / 2 - 0.05, h * 0.72 + 0.6), (w / windows * 0.6, 0.1, 1.5), WIN)
    for i in range(int(w / 9)):
        b.box((x - w / 2 + 5 + i * 9.0, y, h + 1.6), (3.2, 2.2, 1.1), STEEL)                 # roof ventilators
    b.box((x + w / 2 - 6, y - d / 2 - 0.08, 3.0), (6.0, 0.15, 5.4), DARK)                    # roller door
    b.box((x + w / 2 - 6, y - d / 2 - 0.15, 3.0), (5.6, 0.1, 5.0), STEEL)
    b.cyl((x - w / 2 + 2.0, y + d / 2 + 1.4, 3.0), 0.7, 5.4, STEEL, seg=8)                  # feed pipe


def stack(b, x, y, h=46.0, r=2.6):
    """Chimney with red and white bands."""
    b.cyl((x, y, 0.8), r * 1.9, 1.6, CONC, seg=14)
    segs = 9
    for i in range(segs):
        rr1 = r - (r - r * 0.62) * i / segs
        rr2 = r - (r - r * 0.62) * (i + 1) / segs
        b.cyl((x, y, 1.6 + (i + 0.5) * (h - 1.6) / segs), rr1, (h - 1.6) / segs, RED if i % 2 == 0 else WHITE, seg=14, r2=rr2)
    b.cyl((x, y, h + 0.15), r * 0.7, 0.3, DARK, seg=14)


def cooling_tower(b, x, y, r=11.0, h=34.0):
    """Hyperbolic cooling tower built from three cones."""
    b.cyl((x, y, 1.0), r * 1.05, 2.0, CONC, seg=22)
    b.cyl((x, y, 2.0 + h * 0.2), r, h * 0.4, LCONC, seg=22, r2=r * 0.66)
    b.cyl((x, y, 2.0 + h * 0.6), r * 0.66, h * 0.4, LCONC, seg=22, r2=r * 0.74)
    b.cyl((x, y, 2.0 + h * 0.9), r * 0.74, h * 0.2, LCONC, seg=22, r2=r * 0.8)
    b.cyl((x, y, 2.0 + h + 0.2), r * 0.82, 0.5, DARK, seg=22)


def switchyard(b, x, y, n_tr, n_gantry):
    for i in range(n_tr):
        transformer(b, x + (i - (n_tr - 1) / 2) * 12.0, y + 6.0, 1.15)
    for i in range(n_gantry):
        portal(b, x + (i - (n_gantry - 1) / 2) * 14.0, y - 3.0, 12.0, 10.5)
    b.box((x, y + 1.0, 0.7), (max(1.0, n_tr) * 12.0 + 6.0, 0.6, 0.6), STEEL)                    # busbar


def power_grid(level):
    b = B()
    if level == 1:                                    # substation (about 76 x 70 m with its pylons)
        W, D = 70.0, 40.0
        b.box((0, 0, 0.15), (W, D, 0.3), LCONC)
        border(b, W, D)
        switchyard(b, 4, -6, 2, 2)
        cabin(b, -22, 10, 15, 8, 4.4, 4)
        container(b, 24, 12, 0, 11.0)
        solar_field(b, -30, -12, 1, 8, 5.4, 4.6)
        power_line(b, W / 2 - 8, [-D / 2 - 14, 4, D / 2 + 14], 30.0)
    elif level == 2:                                  # power plant
        W, D = 100.0, 50.0
        b.box((0, 0, 0.15), (W, D, 0.3), LCONC)
        border(b, W, D, gate=14.0)
        hall(b, -8, 10, 44, 15, 15, 7)
        stack(b, -34, 22, 44, 2.6)
        cabin(b, -36, -10, 18, 9, 5.2, 5)
        switchyard(b, 28, -8, 3, 2)
        for i in range(3):
            container(b, 8 + i * 12.6, -16, 0, 11.4)
        solar_field(b, -44, -20, 2, 7, 6.0, 5.0)
        power_line(b, W / 2 - 10, [-D / 2 - 14, -6, 24, D / 2 + 14], 34.0)
    else:                                             # full power station
        W, D = 140.0, 62.0
        b.box((0, 0, 0.15), (W, D, 0.3), LCONC)
        border(b, W, D, gate=16.0)
        hall(b, -26, 8, 50, 17, 17, 8)
        hall(b, 30, 8, 44, 17, 17, 7)
        stack(b, -50, 22, 52, 2.8)
        stack(b, -42, 24, 52, 2.8)
        cooling_tower(b, 56, 21, 10.0, 32.0)
        cooling_tower(b, 38, 25, 10.0, 32.0)
        cabin(b, -48, -12, 24, 11, 5.6, 6)
        b.box((-20, -14, 6.0), (12.0, 10.0, 11.0), WALL)                                   # control tower
        b.box((-20, -14, 11.7), (12.8, 10.8, 0.5), SLATE)
        b.box((-20, -19.05, 8.5), (9.0, 0.1, 3.2), GLASS)
        switchyard(b, 28, -12, 6, 3)
        for r in range(2):
            for i in range(4):
                container(b, -6 + i * 12.8, -22 + r * 4.5, 0, 11.6)
        solar_field(b, -66, -26, 3, 8, 6.4, 5.2)
        power_line(b, W / 2 - 12, [-D / 2 - 18, -14, 12, 38, D / 2 + 18], 38.0)
    return b.finish("PowerGrid_L%d" % level)


# ---------------------------------------------------------------- Parking lot with parked cars
def car(b, x, y, rot_z, body):
    R = Matrix.Rotation(math.radians(rot_z), 3, "Z")
    o = Vector((x, y, 0))
    def at(v):
        w = R @ Vector(v)
        return (o.x + w.x, o.y + w.y, w.z)
    b.box(at((0, 0, 0.62)), (1.8, 4.3, 0.62), BODY0 + body, rot=R)
    b.box(at((0, -0.1, 1.2)), (1.6, 2.3, 0.62), BODY0 + body, rot=R)
    b.box(at((0, -0.1, 1.22)), (1.64, 2.0, 0.46), WIN, rot=R)
    b.box(at((0, 0, 0.28)), (1.84, 4.2, 0.22), DARK, rot=R)                          # bumpers / tyres line


PARK_LAMPS = (-27.0, -9.0, 9.0, 27.0)


def car_park():
    b = B()
    W, D = 62.0, 42.0
    b.box((0, 0, 0.13), (W, D, 0.26), ASPH)
    for s in (-1, 1):
        b.box((0, s * (D / 2 + 0.3), 0.17), (W + 1.2, 0.6, 0.34), LCONC)                 # kerbs
    for s in (-1, 1):
        b.box((s * (W / 2 + 0.3), 0, 0.17), (0.6, D + 1.2, 0.34), LCONC)
    # two double rows of bays with thin white lines
    import random
    rng = random.Random(5)
    for row_y in (-11.0, 11.0):
        for i in range(13):
            x = -W / 2 + 4.0 + i * 4.5
            b.box((x - 2.25, row_y, 0.27), (0.12, 5.2, 0.02), WHITE)
        b.box((0, row_y, 0.27), (W - 6.0, 0.12, 0.02), WHITE)
    for row_y, rot in ((-11.0, 0.0), (11.0, 180.0)):
        for i in range(12):
            if rng.random() < 0.66:
                car(b, -W / 2 + 6.25 + i * 4.5, row_y + rng.uniform(-0.3, 0.3), rot + rng.uniform(-2, 2), rng.randrange(8))
    for x in (-24.0, 0.0, 24.0):                                                       # tree planters in the middle
        b.box((x, 0, 0.36), (4.0, 3.0, 0.5), GREEN)
    for sx in (-1, 1):                                                                 # shelters
        b.box((sx * 24, -D / 2 + 0.2, 3.0), (10, 0.15, 0.15), STEEL)
    for x in PARK_LAMPS:                                                                # light poles down the middle aisle (lamp glass = slot 1)
        b.cyl((x, 0, 0.35), 0.45, 0.7, LCONC, seg=8)
        b.cyl((x, 0, 4.6), 0.13, 8.2, STEEL, seg=8, r2=0.1)
        for s in (-1, 1):
            b.box((x, s * 1.1, 8.6), (0.12, 2.2, 0.12), STEEL)                          # arm
            b.box((x, s * 2.2, 8.5), (1.0, 0.8, 0.2), DARK)                              # lamp housing
            b.box((x, s * 2.2, 8.36), (0.85, 0.65, 0.05), LCONC, mat=1)                  # lamp glass
    return b.finish("CarPark")


# ---------------------------------------------------------------- Staff housing: two rows of dormitory blocks + hall + paths
def dorm(b, x, y):
    b.box((x, y, 0.2), (18.0, 11.0, 0.4), LCONC)
    b.box((x, y, 3.9), (16.0, 9.0, 7.0), WALL)
    b.box((x, y, 7.55), (16.8, 9.8, 0.4), SLATE)
    for z in (2.9, 6.1):
        for i in range(4):
            px = x - 6.0 + i * 4.0
            b.box((px, y - 4.55, z), (2.2, 0.08, 1.6), DARK)
            b.box((px, y - 4.58, z), (1.9, 0.08, 1.3), WIN)
            b.box((px, y + 4.55, z), (2.2, 0.08, 1.6), DARK)
            b.box((px, y + 4.58, z), (1.9, 0.08, 1.3), WIN)
    b.box((x, y - 5.05, 4.4), (16.4, 0.9, 0.18), BLUEC)                               # balcony band
    b.box((x - 6.6, y - 4.6, 1.2), (1.4, 0.1, 2.2), DOOR)
    for i in range(3):
        b.box((x - 4 + i * 4.2, y + 0.5, 7.9), (3.2, 2.0, 0.12), BLUEC, rot=RX(-14))  # roof solar


def staff_housing():
    b = B()
    W, D = 190.0, 112.0
    b.box((0, 0, 0.1), (W, D, 0.2), GREEN)                                           # lawn
    for y in (-18.0, 24.0):                                                           # footpaths
        b.box((0, y, 0.24), (W - 12, 3.2, 0.1), SAND)
    for x in (-72.0, 0.0, 72.0):
        b.box((x, 3.0, 0.24), (3.2, 46.0, 0.1), SAND)
    for x in (-72.0, -36.0, 0.0, 36.0, 72.0):
        dorm(b, x, 34.0)
    for x in (-72.0, -36.0, 0.0, 36.0, 72.0):
        dorm(b, x, -30.0)
    # community hall with a canopy in the middle
    b.box((0, 3.0, 0.2), (30.0, 14.0, 0.4), LCONC)
    b.box((0, 3.0, 2.8), (26.0, 10.0, 5.0), GRAY)
    b.box((0, 3.0, 5.4), (27.0, 11.0, 0.4), SLATE)
    for i in range(5):
        b.box((-10 + i * 5.0, -2.1, 2.8), (3.0, 0.08, 2.4), WIN)
    b.box((0, -4.8, 3.9), (14.0, 4.0, 0.2), BLUEC)
    for sx in (-1, 1):
        b.cyl((sx * 6.5, -6.4, 1.9), 0.12, 3.8, STEEL, seg=6)
    # bike / car shelters
    for x in (-84.0, 84.0):
        b.box((x, 3.0, 2.2), (6.0, 14.0, 0.15), BLUEC)
        for sy in (-6, 6):
            b.cyl((x - 2.5, 3.0 + sy, 1.1), 0.08, 2.2, STEEL, seg=6)
            b.cyl((x + 2.5, 3.0 + sy, 1.1), 0.08, 2.2, STEEL, seg=6)
    return b.finish("StaffHousing")


# ---------------------------------------------------------------- Fence + gate
def fence(L=20.0):
    b = B()
    n = int(L / 4)
    for i in range(n + 1):
        b.box((-L / 2 + i * 4.0, 0, 1.4), (0.18, 0.18, 2.8), DARK)
    for z in (0.5, 1.0, 1.5, 2.0, 2.5):
        b.box((0, 0, z), (L, 0.05, 0.05), STEEL)
    for i in range(int(L / 0.7)):
        b.box((-L / 2 + 0.35 + i * 0.7, 0, 1.4), (0.03, 0.03, 2.2), STEEL)
    b.box((0, 0, 2.85), (L, 0.12, 0.1), DARK)
    return b.finish("FenceSegment")


def gate():
    """Two pillars with a sign beam, a guard booth and a red/white barrier arm across a 12 m road."""
    b = B()
    for s in (-1, 1):
        b.box((s * 8.0, 0, 2.6), (1.6, 1.6, 5.2), CONC)
        b.box((s * 8.0, 0, 5.3), (2.0, 2.0, 0.3), LCONC)
    b.box((0, 0, 5.6), (18.0, 1.0, 1.3), BLUEC)                                      # sign beam
    b.box((0, -0.6, 5.6), (10.0, 0.1, 0.4), WHITE)
    b.box((11.5, 0, 1.6), (4.5, 3.2, 3.2), WALL)                                     # guard booth
    b.box((11.5, 0, 3.4), (5.2, 3.9, 0.3), SLATE)
    b.box((11.5, -1.62, 1.9), (3.0, 0.08, 1.2), WIN)
    b.cyl((-5.0, 1.5, 0.6), 0.25, 1.2, DARK, seg=8)                                   # barrier post
    for i in range(6):
        b.box((-2.0 + i * 1.6 - 0.8, 1.5, 1.15), (1.6, 0.16, 0.2), RED if i % 2 == 0 else WHITE)
    return b.finish("Gate")


def plot_generic(w=80.0, d=68.0):
    b = B()
    b.box((0, 0, 0.12), (w, d, 0.24), LCONC)
    b.box((0, 0, 0.27), (w - 6.0, d - 6.0, 0.06), CONC)
    t = 0.7
    L = 11.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * (w / 2 - L / 2), sy * (d / 2 - t / 2), 0.3), (L, t, 0.08), HAZ)
            b.box((sx * (w / 2 - t / 2), sy * (d / 2 - L / 2), 0.3), (t, L, 0.08), HAZ)
            b.box((sx * (w / 2 - 1.2), sy * (d / 2 - 1.2), 1.1), (0.4, 0.4, 2.0), HAZ)
    b.box((0, -d / 2 + 2.5, 1.9), (0.25, 0.25, 3.4), STEEL)
    b.box((0, -d / 2 + 2.5, 3.7), (7.0, 0.2, 2.6), BLUEC)
    b.box((0, -d / 2 + 2.35, 3.9), (4.8, 0.1, 0.45), WHITE)
    b.box((0, -d / 2 + 2.35, 3.2), (3.2, 0.1, 0.3), WHITE)
    return b.finish("Plot_Generic")


# ---------------------------------------------------------------- build + export
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)

makers = [("PowerGrid_L1", lambda: power_grid(1)), ("PowerGrid_L2", lambda: power_grid(2)), ("PowerGrid_L3", lambda: power_grid(3)),
          ("CarPark", car_park), ("StaffHousing", staff_housing), ("FenceSegment", fence), ("Gate", gate), ("Plot_Generic", plot_generic)]
manifest = {}
objs = []
for name, fn in makers:
    ob = fn()
    objs.append(ob)
    d = ob.dimensions
    manifest[name] = {"w": round(d.x, 2), "d": round(d.y, 2), "h": round(d.z, 2), "tris": sum(len(p.vertices) - 2 for p in ob.data.polygons)}
    export(ob, name)
path = os.path.join(OUT_S, "site-manifest.json")
old = json.load(open(path)) if os.path.exists(path) else {}
old.update(manifest)
json.dump(old, open(path, "w"), indent=1)
x = 0
for ob in objs:
    ob.location.x = x
    x += 140
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "space-site2.blend"))
print("DONE", manifest)
