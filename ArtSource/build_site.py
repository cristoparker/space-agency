# Builds the realistic-scale space centre: highway, access roads, the launch complex, seven facilities with three
# upgrade tiers each (L1 starter, L2 expanded, L3 full site), plot markers and highway lamps. Exports FBX to Assets/Assets/Models/Site.
#   Headless:  /Applications/Blender.app/Contents/MacOS/Blender -b --python ArtSource/build_site.py
# Metres. Origin at the centre of each model's base, +Z up (Y up in Unity), front (entrance) faces -Y.
# Reuses the low-poly builder (class B, palette) from build_base.py.
import bpy, bmesh, math, os, json
from mathutils import Matrix, Vector

try:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    ROOT = "/Users/tasrifibnmizan/Documents/GitHub/space-agency"
_src = open(os.path.join(ROOT, "ArtSource", "build_base.py"), encoding="utf-8").read()
exec(_src.split("# ============================== BUILDINGS")[0])          # B, palette cells, RX / RY, helpers
OUT_S = os.path.join(ROOT, "Assets", "Assets", "Models", "Site")
os.makedirs(OUT_S, exist_ok=True)
ASPH, SAND, BLUEC = 22, 20, 17            # extra palette cells (dark asphalt, sand, corporate blue)
STATS = {}


# ---------------------------------------------------------------- builder extensions
def fast_cleanup(self):
    """Only drops the unseen bottom faces (the full buried-face test is too slow for models this size)."""
    bm = self.bm
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    kill = [f for f in bm.faces if f.normal.z < -0.9 and max(v.co.z for v in f.verts) < 0.02]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    return len(bm.faces), len(kill)


B.cleanup = fast_cleanup


def sphere(b, c, r, cell, seg=14):
    try:
        v = bmesh.ops.create_uvsphere(b.bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)["verts"]
    except TypeError:
        v = bmesh.ops.create_uvsphere(b.bm, u_segments=seg, v_segments=max(6, seg // 2), diameter=r)["verts"]
    bmesh.ops.translate(b.bm, vec=c, verts=v)
    b._paint(v, cell)


def beam(b, p0, p1, t, cell):
    """Thin square beam between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    L = d.length
    if L < 1e-4:
        return
    rot = d.to_track_quat("Z", "Y").to_matrix()
    b.box((p0 + p1) / 2, (t, t, L), cell, rot=rot)


def lattice(b, cx, cy, z0, w, d, h, bays, leg=0.7, cell=STEEL, brace=0.3):
    """Square lattice tower (legs, rings and X bracing)."""
    xs, ys = (-w / 2, w / 2), (-d / 2, d / 2)
    for x in xs:
        for y in ys:
            b.box((cx + x, cy + y, z0 + h / 2), (leg, leg, h), cell)
    bh = h / bays
    for i in range(bays + 1):
        z = z0 + i * bh
        b.box((cx, cy - d / 2, z), (w, brace, brace), cell)
        b.box((cx, cy + d / 2, z), (w, brace, brace), cell)
        b.box((cx - w / 2, cy, z), (brace, d, brace), cell)
        b.box((cx + w / 2, cy, z), (brace, d, brace), cell)
    for i in range(bays):
        za, zb = z0 + i * bh, z0 + (i + 1) * bh
        flip = i % 2 == 0
        for y in ys:
            beam(b, (cx - w / 2 * (1 if flip else -1), cy + y, za), (cx + w / 2 * (1 if flip else -1), cy + y, zb), brace, cell)
        for x in xs:
            beam(b, (cx + x, cy - d / 2 * (1 if flip else -1), za), (cx + x, cy + d / 2 * (1 if flip else -1), zb), brace, cell)


def hband(b, x0, x1, y, z, w, h, n, axis="y"):
    """n windows spread along a wall."""
    for i in range(n):
        t = x0 + (x1 - x0) * (i + 0.5) / n
        c = (t, y, z) if axis == "y" else (y, t, z)
        b.win(c, w, h, axis)


def curtain(b, cx, cy, z0, w, d, h, floors, cell_glass=GLASS):
    """Glass curtain wall block: glass skin, floor slabs and vertical fins."""
    b.box((cx, cy, z0 + h / 2), (w, d, h), cell_glass)
    fh = h / floors
    for i in range(floors + 1):
        b.box((cx, cy, z0 + i * fh), (w + 0.5, d + 0.5, 0.35), WHITE)
    for k in range(5):
        x = cx - w / 2 + w * k / 4
        b.box((x, cy - d / 2 - 0.15, z0 + h / 2), (0.35, 0.3, h), DARK)
        b.box((x, cy + d / 2 + 0.15, z0 + h / 2), (0.35, 0.3, h), DARK)


def ac_units(b, cx, cy, z, n, spread=2.4):
    for i in range(n):
        x = cx + (i - (n - 1) / 2) * spread
        b.box((x, cy, z + 0.4), (1.8, 1.2, 0.8), STEEL)
        b.cyl((x, cy, z + 0.82), 0.45, 0.06, DARK, seg=8)


def flag(b, x, y, z0, h):
    b.cyl((x, y, z0 + h / 2), 0.09, h, STEEL, seg=6)
    b.box((x + 0.9, y, z0 + h - 0.6), (1.7, 0.04, 1.0), RED)


def plinth(b, w, d, t=0.4, cell=LCONC):
    b.box((0, 0, t / 2), (w, d, t), cell)


def new(name):
    return B(), name


def done(b, name):
    ob = b.finish(name)
    return ob


# ================================================================ ROADS
def highway(L=175.0):
    """Six lane highway along X: two carriageways, a concrete median, shoulders, lane markings, guard rails."""
    b = B()
    H = 0.3
    for s in (-1, 1):
        b.box((0, s * 7.6, H / 2), (L, 11.2, H), ASPH)                    # carriageway
        b.box((0, s * 14.3, H / 2 - 0.02), (L, 2.2, H - 0.04), SAND)       # verge gravel
        for k in (-1, 1):                                                  # solid edge lines
            pass
        b.box((0, s * 12.8, H + 0.01), (L, 0.22, 0.02), WHITE)
        b.box((0, s * 2.7, H + 0.01), (L, 0.22, 0.02), WHITE)
        n = int(L / 9)
        for lane in (s * 5.2, s * 8.2):                                     # dashed lane lines
            for i in range(n):
                b.box((-L / 2 + 4.5 + i * 9.0, lane, H + 0.01), (4.0, 0.18, 0.02), WHITE)
        n4 = int(L / 4)
        for i in range(n4 + 1):                                            # guard rail posts + rail
            x = -L / 2 + i * 4.0
            b.box((x, s * 13.7, 0.5), (0.14, 0.14, 0.7), STEEL)
        b.box((0, s * 13.7, 0.85), (L, 0.1, 0.32), STEEL)
    b.box((0, 0, 0.55), (L, 1.5, 1.1), LCONC)                                # median barrier
    b.box((0, 0, 1.14), (L, 0.6, 0.12), HAZ)
    for i in range(int(L / 25)):
        b.box((-L / 2 + 12 + i * 25.0, 0, 1.2), (0.3, 1.3, 0.06), RED)         # reflectors
    return b.finish("Highway")


def road(L=50.0, W=12.0):
    """Two lane service / access road along X."""
    b = B()
    H = 0.28
    b.box((0, 0, H / 2), (L, W, H), ASPH)
    for s in (-1, 1):
        b.box((0, s * (W / 2 - 0.9), H + 0.01), (L, 0.2, 0.02), WHITE)
        b.box((0, s * (W / 2 + 0.3), H / 2 - 0.02), (L, 0.6, H - 0.04), LCONC)    # kerb
    for i in range(int(L / 6)):
        b.box((-L / 2 + 3 + i * 6.0, 0, H + 0.01), (3.0, 0.2, 0.02), HAZ)
    return b.finish("Road")


def highway_lamp():
    """Tall double-arm lamp for the median (lamp glass = slot 1)."""
    b = B()
    b.cyl((0, 0, 0.5), 0.55, 1.0, LCONC, seg=8)
    b.cyl((0, 0, 7.0), 0.17, 13.0, STEEL, seg=8, r2=0.12)
    for s in (-1, 1):
        b.box((0, s * 2.2, 13.2), (0.16, 4.4, 0.16), STEEL)
        b.box((0, s * 4.3, 13.0), (1.1, 1.0, 0.24), DARK)
        b.box((0, s * 4.3, 12.85), (0.9, 0.8, 0.06), LCONC, mat=1)
    return b.finish("HighwayLamp")


def tree(seed):
    """Low poly conifer / round tree for the verge and hills."""
    b = B()
    b.cyl((0, 0, 1.2), 0.3, 2.4, DOOR, seg=6)
    for i, (r, z) in enumerate([(2.6, 3.0), (2.1, 5.0), (1.5, 6.8)]):
        b.cyl((0, 0, z), r, 2.6, GREEN, seg=8, r2=0.1)
    return b.finish("Tree")


# ================================================================ LAUNCH COMPLEX
def launch_complex():
    """Launch pad with mount + flame trench and deflector, deluge water system, 100 m service tower with swing arms and lift,
    crawlerway, lightning masts, floodlight towers, propellant farm, control bunker, flare stack and a fenced perimeter with a gate.
    Rocket stands on the mount: top of the mount is z = 12.0 at the origin (do not move: the game places the rocket there)."""
    b = B()
    b.box((0, 0, 0.3), (130, 130, 0.6), LCONC)                                 # concrete apron
    for i in range(-3, 4):
        b.box((i * 18.0, 0, 0.62), (0.3, 130, 0.04), CONC)                     # expansion joints
        b.box((0, i * 18.0, 0.62), (130, 0.3, 0.04), CONC)
    b.cyl((0, 0, 0.66), 26.0, 0.1, CONC, seg=36)                               # heat-resistant pad around the mount
    b.cyl((0, 0, 0.72), 19.0, 0.06, DARK, seg=36)
    b.box((0, -40, 0.7), (16, 52, 0.08), SAND)                                 # crawlerway (gravel) from the gate to the mount
    for s in (-1, 1):
        b.box((s * 6.0, -40, 0.76), (0.5, 52, 0.06), CONC)                      # crawler tracks
        b.box((s * 8.4, -40, 0.74), (0.3, 52, 0.06), HAZ)                       # edge lines
    # ---- flame trench with sloped deflector and deluge pipes
    b.box((0, -34, 0.64), (10, 52, 0.06), DARK)
    b.box((0, -34, 0.9), (13, 52, 0.5), DARK)
    b.box((-6.8, -34, 1.3), (0.8, 52, 1.6), LCONC)
    b.box((6.8, -34, 1.3), (0.8, 52, 1.6), LCONC)
    b.box((0, -16, 2.0), (9.0, 16.0, 0.9), STEEL, rot=RX(14))                  # flame deflector ramp
    for s in (-1, 1):
        b.cyl((s * 7.6, -34, 2.35), 0.35, 52.0, STEEL, seg=8, rot=RX(90))        # deluge water pipes along the trench rim
        for yy in range(-58, -8, 8):
            b.cyl((s * 7.6, yy, 2.0), 0.2, 1.2, STEEL, seg=6)                    # risers
    # ---- launch mount: four legs, cross braces, deck, hold-down ring and clamps
    for s in (-1, 1):
        for t_ in (-1, 1):
            b.box((s * 8.5, t_ * 8.5, 5.5), (5.0, 5.0, 10.0), CONC)
    for s in (-1, 1):
        beam(b, (s * 8.5, -8.5, 1.5), (s * 8.5, 8.5, 9.0), 0.5, STEEL)
        beam(b, (-8.5, s * 8.5, 1.5), (8.5, s * 8.5, 9.0), 0.5, STEEL)
    b.box((0, 0, 11.0), (26, 26, 2.0), LCONC)
    b.cyl((0, 0, 12.1), 6.2, 0.3, DARK, seg=24)
    for k in range(8):
        a_ = k * math.pi / 4
        b.box((math.cos(a_) * 5.4, math.sin(a_) * 5.4, 12.4), (1.4, 0.8, 0.8), HAZ, rot=Matrix.Rotation(a_, 3, "Z"))
    b.box((0, 0, 9.6), (11, 11, 0.4), DARK)
    b.cyl((0, 0, 8.4), 3.5, 2.4, STEEL, seg=12, r2=7.5)
    # ---- service tower: lattice, lift, swing arms, platforms with rails, striped top
    tx = 24.0
    lattice(b, tx, 0, 0.6, 9.0, 9.0, 100.0, 12, leg=0.9, cell=RED, brace=0.35)
    b.box((tx + 6.0, 0, 50), (3.0, 3.0, 100), GRAY)
    for z in (22.0, 64.0):
        b.box((tx + 6.0, -1.8, z), (2.4, 0.6, 3.2), WHITE)                       # lift cars
    b.box((tx, 0, 101.0), (11, 11, 1.0), DARK)
    b.box((tx, 0, 105.0), (6, 6, 7.0), WHITE)
    b.box((tx - 12, 0, 107.5), (26, 1.8, 1.8), RED)
    for i in range(4):                                                             # red / white obstruction bands on the crane house
        b.box((tx, 0, 102.5 + i * 1.8), (6.1, 6.1, 0.8), RED if i % 2 == 0 else WHITE)
    b.cyl((tx, 0, 118.0), 0.2, 24.0, STEEL, seg=6)
    for z, ln in ((24, 20.0), (42, 22.0), (60, 22.0), (78, 20.0), (92, 18.0)):
        l2 = tx - 4.5 - 3.0
        b.box((tx - 4.5 - l2 / 2, 0, z), (l2, 2.6, 1.6), STEEL)
        b.box((tx - 4.5 - l2, 0, z), (1.0, 3.6, 2.2), HAZ)
        b.box((tx - 4.5 - l2 / 2, 1.5, z + 1.0), (l2, 0.08, 0.9), DARK)           # rail
    for z in range(10, 100, 20):
        b.box((tx, -5.2, z), (9.5, 1.0, 0.25), HAZ)
        b.box((tx, -5.6, z + 0.6), (9.5, 0.08, 1.0), DARK)
    b.box((14.0, 0, 0.9), (22.0, 2.0, 0.9), STEEL)                                 # cable run from the tower base to the mount
    b.box((tx - 4.0, 0, 11.4), (10.0, 2.4, 1.0), STEEL)                            # umbilical bridge
    # ---- lightning masts and floodlight towers (lamp glass = slot 1)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * 56, sy * 56
            b.cyl((x, y, 0.8), 1.2, 1.6, CONC, seg=8)
            b.cyl((x, y, 55.0), 0.35, 110.0, STEEL, seg=8, r2=0.15)
            sphere(b, (x, y, 110.5), 0.9, RED, seg=8)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * 60.0, sy * 30.0
            b.cyl((x, y, 0.5), 1.0, 1.0, CONC, seg=8)
            b.cyl((x, y, 9.5), 0.3, 18.0, STEEL, seg=8, r2=0.2)
            b.box((x, y, 18.2), (3.6, 0.5, 0.4), DARK)
            for k in (-1.2, 0, 1.2):
                b.box((x - sx * 0.45, y + k * 0.0 + 0.0, 17.2), (0.2, 0.2, 0.2), DARK) if False else None
            for k in (-1.4, 0, 1.4):
                b.box((x - sx * 0.6, y, 17.7 + 0.0), (0.0001, 0.0001, 0.0001), DARK) if False else None
            for k in (-1.2, 0.0, 1.2):
                b.box((x + k, y - sy * 0.6, 17.6), (0.9, 0.35, 0.9), DARK)
                b.box((x + k, y - sy * 0.82, 17.6), (0.75, 0.06, 0.75), LCONC, mat=1)
    # ---- sound-suppression water tower + deluge tanks
    wx, wy = -46.0, 22.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((wx + sx * 4.5, wy + sy * 4.5, 14), (1.0, 1.0, 28), STEEL)
    b.cyl((wx, wy, 31.0), 8.0, 7.0, WHITE, seg=16)
    b.cyl((wx, wy, 35.6), 8.0, 2.2, WHITE, seg=16, r2=2.0)
    b.box((wx + 12, wy - 9, 5), (24, 1.2, 1.2), STEEL)
    for y in (-14.0, -22.0):
        b.cyl((-46, y, 2.6), 2.4, 16.0, WHITE, seg=14, rot=RY(90))                  # horizontal water tanks
        for x in (-52, -40):
            b.box((x, y, 1.1), (1.2, 4.6, 1.0), STEEL)
    # ---- propellant farm
    for i, (sx, sy, r) in enumerate([(-44, -44, 9.5), (-24, -44, 9.5), (-44, -64 + 6, 7.0)]):
        b.box((sx, sy, 0.8), (r * 2.4, r * 2.4, 1.6), CONC)
        sphere(b, (sx, sy, r + 1.6), r, WHITE, seg=16)
        for k in range(4):
            a_ = k * math.pi / 2 + math.pi / 4
            b.cyl((sx + math.cos(a_) * r * 0.75, sy + math.sin(a_) * r * 0.75, 1.0 + r * 0.45), 0.4, r * 0.9, STEEL, seg=6)
    for x in (24, 36):
        b.cyl((x, -52, 2.8), 2.2, 16.0, WHITE, seg=14, rot=RX(90))                   # bullet tanks
        for yy in (-57.5, -46.5):
            b.box((x, yy, 1.1), (4.6, 1.2, 1.0), STEEL)
    b.box((12, -16, 0.9), (40, 1.0, 1.0), STEEL)
    b.box((-34, -44, 3.0), (20, 1.0, 1.0), STEEL)
    b.box((12, -34, 1.2), (1.0, 40, 1.0), STEEL)
    # ---- ground support building, control bunker with a berm, flare stack
    b.box((40, -30, 4.0), (24, 14, 7.4), WALL)
    b.box((40, -30, 7.9), (25, 15, 0.5), SLATE)
    for i in range(5):
        b.box((40 - 9 + i * 4.5, -37.2, 4.2), (2.4, 0.1, 2.4), WIN)
    b.box((-18, -50, 2.2), (14, 9, 4.0), LCONC)
    b.box((-18, -50, 4.4), (15, 10, 0.5), SLATE)
    b.box((-18, -57.5, 1.6), (22, 6, 3.0), GREEN)                                  # earth berm in front of the bunker
    b.cyl((-12, -48, 9.0), 0.12, 10.0, STEEL, seg=6)
    b.cyl((56, -10, 15.0), 0.5, 30.0, STEEL, seg=8)
    b.box((56, -10, 30.4), (1.2, 1.2, 1.0), ORANGE)
    # ---- perimeter fence with a gate in the front side
    gate = 18.0
    for (x0, y0, x1, y1) in [(-62, -62, -gate / 2, -62), (gate / 2, -62, 62, -62), (62, -62, 62, 62), (62, 62, -62, 62), (-62, 62, -62, -62)]:
        L = math.hypot(x1 - x0, y1 - y0)
        n = max(1, int(round(L / 4.0)))
        for i in range(n + 1):
            s = i / n
            b.box((x0 + (x1 - x0) * s, y0 + (y1 - y0) * s, 1.5), (0.22, 0.22, 3.0), DARK)
        for z in (0.5, 1.2, 1.9, 2.6):
            beam(b, (x0, y0, z), (x1, y1, z), 0.08, STEEL)
    for s in (-1, 1):
        b.box((s * gate / 2, -62, 2.2), (1.2, 1.2, 4.4), CONC)
        b.box((s * gate / 2, -62, 4.6), (1.6, 1.6, 0.4), LCONC)
    b.box((0, -62, 4.6), (gate + 1.6, 0.8, 1.0), BLUEC)
    b.box((gate / 2 + 5.0, -58, 1.6), (4.5, 3.4, 3.2), WALL)
    b.box((gate / 2 + 5.0, -58, 3.4), (5.1, 4.0, 0.3), SLATE)
    return b.finish("LaunchComplex")


# ================================================================ FACILITIES (3 tiers each)
def admin(level):
    b = B()
    if level == 1:                                                              # modular starter office
        plinth(b, 20, 12)
        b.box((0, 0, 2.5), (16, 9, 4.2), WALL)
        b.box((0, 0, 4.75), (16.6, 9.6, 0.4), SLATE)
        b.box((-3.2, -4.55, 1.2), (4, 0.1, 0.25), BLUEC)
        hband(b, -7, 1.5, -4.5, 2.8, 1.5, 1.2, 4)
        b.box((5.2, -4.58, 1.3), (1.6, 0.1, 2.2), DOOR)
        b.box((5.2, -5.5, 2.7), (3.0, 2.2, 0.15), SLATE)
        for x in (3.8, 6.6):
            b.cyl((x, -6.3, 1.3), 0.07, 2.6, STEEL, seg=6)
        hband(b, -7, 7, 4.5, 2.8, 1.4, 1.1, 5)
        ac_units(b, -3, 0, 4.9, 2)
        flag(b, -10.5, -5.5, 0.4, 9.0)
    elif level == 2:                                                            # three-storey HQ
        plinth(b, 44, 24)
        b.box((0, 0, 6.4), (36, 16, 12.0), WALL)
        b.box((0, 0, 12.6), (36.8, 16.8, 0.5), SLATE)
        for z in (3.2, 7.0, 10.6):
            hband(b, -16, 16, -8.0, z, 2.0, 1.6, 8)
            hband(b, -16, 16, 8.0, z, 2.0, 1.6, 8)
        b.box((-12, -8.4, 6.4), (0.8, 0.6, 12.0), BLUEC)
        b.box((12, -8.4, 6.4), (0.8, 0.6, 12.0), BLUEC)
        b.box((0, -10.5, 5.0), (10, 5, 9.4), GLASS)                              # entrance atrium
        b.box((0, -10.5, 10.1), (11, 6, 0.5), WHITE)
        b.box((0, -13.0, 2.6), (7, 0.2, 2.8), DARK)
        for x in (-10, -4, 4, 10):
            hband(b, -1, 1, x, 6.4, 1.6, 12.0, 1, "x") if False else None
        ac_units(b, -10, 2, 12.9, 3, 4.0)
        b.cyl((12, 3, 14.5), 0.2, 4.0, STEEL, seg=6)
        flag(b, -18, -12, 0.4, 14.0)
        flag(b, -21, -12, 0.4, 14.0)
    else:                                                                       # glass tower campus
        plinth(b, 62, 36)
        b.box((0, 0, 4.4), (56, 30, 8.0), WALL)
        b.box((0, 0, 8.6), (57, 31, 0.5), SLATE)
        for z in (2.6, 6.2):
            hband(b, -26, 26, -15.2, z, 2.4, 1.8, 12)
        curtain(b, -8, 2, 8.8, 22, 22, 44, 11)
        b.box((-8, 2, 53.4), (23, 23, 0.8), SLATE)
        b.cyl((-8, 2, 56.0), 0.2, 5.5, STEEL, seg=6)
        b.box((-8, 2, 54.4), (6, 6, 1.4), STEEL)
        b.cyl((20, -3, 9.1), 8.0, 0.2, DARK, seg=24)                             # helipad on the podium
        b.box((20, -3, 9.25), (1.2, 8.0, 0.05), WHITE)
        b.box((20, -3, 9.25), (6.0, 1.2, 0.05), WHITE)
        b.box((24, -10, 5.0), (14, 8, 9.6), GLASS)                                # entrance gallery
        b.box((24, -10, 10.0), (15, 9, 0.5), WHITE)
        for x in (-24, 4):
            flag(b, x, -17, 0.4, 16.0)
        b.box((-28, 14, 0.6), (4, 4, 1.2), HAZ)
    return b.finish("AdminOffice_L%d" % level)


def mission_control(level):
    b = B()
    if level == 1:
        plinth(b, 22, 15)
        b.box((0, 0, 2.7), (18, 11, 4.6), WALL)
        b.box((0, 0, 5.2), (18.6, 11.6, 0.5), SLATE)
        b.box((-3, -5.55, 2.9), (8, 0.12, 2.0), DARK)
        b.box((-3, -5.6, 2.9), (7.4, 0.1, 1.6), GLASS)
        b.box((5.5, -5.58, 1.4), (1.6, 0.1, 2.2), DOOR)
        b.cyl((5, 2, 6.0), 0.3, 1.5, STEEL, seg=6)
        b.dish((5, 2, 7.2), 1.8, 55)
        b.cyl((-6, 3, 9.0), 0.08, 7.0, STEEL, seg=6)
        ac_units(b, -5, 2.5, 5.45, 2)
    elif level == 2:
        plinth(b, 44, 28)
        b.box((0, 0, 4.5), (36, 22, 8.0), WALL)
        b.box((-6, 0, 9.5), (22, 18, 3.0), GRAY)                                  # raised control room
        b.box((0, 0, 8.7), (37, 23, 0.5), SLATE)
        b.box((-6, 0, 11.2), (23, 19, 0.4), SLATE)
        for z in (3.2, 6.2):
            hband(b, -15, 15, -11.1, z, 2.4, 1.6, 7)
            hband(b, -15, 15, 11.1, z, 2.4, 1.6, 7)
        b.box((-6, -9.05, 9.5), (18, 0.12, 1.8), GLASS)
        b.box((14, -11.12, 1.5), (2.4, 0.12, 2.6), DOOR)
        b.box((14, -13, 3.2), (5, 3, 0.2), SLATE)
        for x in (12, 16):
            b.cyl((x, -14.2, 1.6), 0.09, 3.2, STEEL, seg=6)
        b.cyl((12, 6, 9.0), 0.4, 3.0, STEEL, seg=6)
        b.dish((12, 6, 11.0), 3.2, 50)
        b.cyl((-14, 6, 12.0), 0.4, 3.0, STEEL, seg=6)
        b.dish((-14, 6, 14.0), 2.4, 60)
        sphere(b, (3, 5, 11.8), 3.0, WHITE, seg=14)                               # radome
        flag(b, -19, -13, 0.4, 12.0)
    else:
        plinth(b, 68, 44)
        b.box((0, 0, 6.5), (60, 36, 12.0), WALL)
        b.box((-8, 0, 15.0), (36, 28, 5.0), GRAY)
        b.box((0, 0, 12.6), (61, 37, 0.6), SLATE)
        b.box((-8, 0, 17.8), (37, 29, 0.6), SLATE)
        for z in (3.6, 8.6):
            hband(b, -26, 26, -18.1, z, 2.8, 2.2, 10)
            hband(b, -26, 26, 18.1, z, 2.8, 2.2, 10)
        b.box((-8, -14.1, 15.0), (30, 0.14, 2.8), GLASS)                           # viewing gallery
        b.box((22, -18.2, 1.8), (3.0, 0.12, 3.2), DOOR)
        b.box((22, -21, 4.0), (8, 5, 0.3), SLATE)
        for x in (18.5, 25.5):
            b.cyl((x, -22.6, 2.0), 0.12, 4.0, STEEL, seg=6)
        b.cyl((20, 8, 12.0), 1.1, 12.0, STEEL, seg=8)
        b.dish((20, 8, 20.0), 7.5, 50)
        b.cyl((-24, 8, 18.0), 0.6, 6.0, STEEL, seg=8)
        b.dish((-24, 8, 22.0), 4.0, 55)
        sphere(b, (4, 8, 21.5), 4.8, WHITE, seg=16)
        b.cyl((-30, -8, 24.0), 0.12, 14.0, STEEL, seg=6)
        for z in (20, 24, 28):
            b.box((-30, -8, z), (4.0, 0.08, 0.08), STEEL)
        for x in (-30, 0):
            flag(b, x, -22, 0.4, 18.0)
        ac_units(b, 12, -8, 18.1, 4, 4.5)
    return b.finish("MissionControl_L%d" % level)


def hangar(level):
    b = B()
    if level == 1:
        plinth(b, 30, 22)
        b.box((0, 0, 4.6), (26, 18, 8.4), GRAY)
        b.prism((0, 0, 8.8), 27, 19, 3.2, RUST)
        b.box((0, -9.1, 3.8), (14, 0.2, 7.2), DARK)
        for x in (-3.6, 3.6):
            b.box((x, -9.25, 3.7), (6.8, 0.14, 6.8), STEEL)
        b.box((0, -9.3, 7.5), (14.4, 0.2, 0.5), HAZ)
        hband(b, -11, 11, -9.1, 6.0, 1.2, 0.9, 1) if False else None
        for i in range(4):
            b.win((-13.1, -6 + i * 4.0, 6.2), 1.4, 0.9, "x")
            b.win((13.1, -6 + i * 4.0, 6.2), 1.4, 0.9, "x")
        b.box((16, 2, 2.2), (6, 8, 3.8), CONC)
        b.box((16, 2, 4.2), (6.4, 8.4, 0.3), SLATE)
    elif level == 2:
        plinth(b, 64, 50)
        b.box((0, 0, 11.5), (56, 40, 22.0), GRAY)
        b.prism((0, 0, 22.5), 58, 42, 6.0, RUST)
        b.box((0, -20.1, 9.0), (30, 0.3, 18.0), DARK)
        for x in (-7.5, 7.5):
            b.box((x, -20.4, 8.8), (14.4, 0.3, 17.4), STEEL)
        b.box((0, -20.5, 18.4), (31, 0.4, 0.9), HAZ)
        b.box((0, -20.7, 19.6), (32, 0.4, 0.5), DARK)
        for i in range(5):
            b.win((-28.1, -14 + i * 7.0, 15.0), 2.4, 1.4, "x")
            b.win((28.1, -14 + i * 7.0, 15.0), 2.4, 1.4, "x")
        b.box((34, -6, 4.5), (14, 22, 8.0), WALL)                                 # office / workshop wing
        b.box((34, -6, 8.6), (14.8, 22.8, 0.5), SLATE)
        hband(b, -15, 3, -17.05, 5.0, 2.0, 1.4, 5) if False else None
        for i in range(4):
            b.win((34 - 3.2 + i * 2.2 - 3.3, -17.1, 5.0), 1.5, 1.4, "y")
        b.cyl((-14, 22, 3.0), 3.4, 6.0, WHITE, seg=14)                            # service tank
    else:                                                                       # assembly building with the high bay
        plinth(b, 90, 80)
        b.box((-16, 0, 17.0), (60, 56, 34.0), WALL)                               # low bay
        b.box((-16, 0, 34.5), (61, 57, 0.8), SLATE)
        b.box((16, 6, 36.0), (46, 46, 72.0), WALL)                                # high bay
        b.box((16, 6, 72.4), (47, 47, 0.8), SLATE)
        for z in range(10, 70, 10):
            b.box((16, 6, z), (46.4, 46.4, 0.5), LCONC)                           # panel seams
        b.box((16, -17.2, 28), (22, 0.5, 56), DARK)                               # high bay door (28 x 56 scale)
        for k in range(7):
            b.box((16, -17.5, 4 + k * 8.2), (21.4, 0.3, 7.6), STEEL)
        b.box((16, -17.3, 61), (22, 0.6, 3.6), HAZ)
        b.box((-16, -28.2, 11), (30, 0.5, 20), DARK)                              # low bay door
        for k in (-1, 1):
            b.box((-16 + k * 7.4, -28.5, 10.8), (14.6, 0.3, 19.0), STEEL)
        for i in range(5):                                                         # painted national marking blocks
            b.box((40.0, -17.3, 58 - i * 0.0), (0.1, 0.1, 0.1), WHITE) if False else None
        b.box((16, -17.6, 46), (14, 0.3, 8), BLUEC)                               # logo panel on the high bay
        b.box((16, -17.9, 46), (11, 0.3, 2.0), WHITE)
        b.box((16, -17.9, 43), (11, 0.3, 1.0), RED)
        b.box((-34, 0, 7), (18, 40, 14), CONC)                                     # utility wing
        b.box((-34, 0, 14.4), (19, 41, 0.8), SLATE)
        b.cyl((44, -20, 9.0), 6.0, 18.0, WHITE, seg=16)                           # storage tanks
        b.cyl((44, -8, 9.0), 6.0, 18.0, WHITE, seg=16)
        b.cyl((16, 6, 78.0), 0.3, 12.0, STEEL, seg=6)
        sphere(b, (16, 6, 84.5), 0.9, RED, seg=8)
    return b.finish("Hangar_L%d" % level)


def fuel_depot(level):
    b = B()
    bund = (30, 20) if level == 1 else (50, 34) if level == 2 else (70, 50)
    b.box((0, 0, 0.3), (bund[0], bund[1], 0.6), CONC)
    for sx in (-1, 1):
        b.box((sx * bund[0] / 2, 0, 0.9), (0.8, bund[1], 1.2), LCONC)
    for sy in (-1, 1):
        b.box((0, sy * bund[1] / 2, 0.9), (bund[0], 0.8, 1.2), LCONC)
    if level == 1:                                                               # two horizontal tanks
        for y in (-4.0, 4.0):
            b.cyl((0, y, 3.0), 2.3, 15.0, WHITE, seg=14, rot=RY(90))
            for x in (-4.5, 4.5):
                b.box((x, y, 1.1), (1.2, 4.4, 1.0), STEEL)
        b.box((-10, 0, 2.2), (5, 4, 3.4), WALL)
        b.box((-10, 0, 4.0), (5.4, 4.4, 0.3), SLATE)
        b.box((0, 0, 1.0), (16, 0.4, 0.4), STEEL)
    elif level == 2:                                                             # vertical tanks + pipe rack
        for x in (-12, -2, 8):
            for y in (-7, 7):
                b.cyl((x, y, 8.0), 4.4, 14.0, WHITE, seg=16)
                b.cyl((x, y, 15.4), 4.5, 0.8, STEEL, seg=16, r2=3.6)
        b.box((0, 0, 2.2), (28, 1.0, 1.0), STEEL)
        b.box((-20, 0, 2.4), (7, 6, 3.8), WALL)
        b.box((-20, 0, 4.4), (7.4, 6.4, 0.3), SLATE)
        b.box((20, 0, 2.8), (6, 12, 5.0), STEEL)                                 # tanker loading bay
        b.box((20, 0, 5.6), (7, 13, 0.3), SLATE)
    else:                                                                        # cryogenic spheres + tanks + flare
        for x, y in ((-18, -9), (6, -9)):
            b.box((x, y, 1.0), (24, 24, 1.6), CONC)
            sphere(b, (x, y, 12.8), 10.5, WHITE, seg=18)
            for k in range(4):
                a = k * math.pi / 2 + math.pi / 4
                b.cyl((x + math.cos(a) * 8, y + math.sin(a) * 8, 6.0), 0.5, 10.0, STEEL, seg=6)
        for x in (-26, -14, -2, 10):
            b.cyl((x, 16, 11.0), 4.4, 20.0, WHITE, seg=16)
        b.box((-6, 4, 3.0), (44, 1.2, 1.2), STEEL)
        b.box((-6, 12, 3.4), (44, 1.2, 1.2), STEEL)
        b.box((28, -4, 3.6), (8, 14, 6.4), WALL)
        b.box((28, -4, 7.0), (8.4, 14.4, 0.4), SLATE)
        b.cyl((30, 14, 17.0), 0.6, 34.0, STEEL, seg=8)
        b.box((30, 14, 34.6), (1.4, 1.4, 1.2), ORANGE)
    return b.finish("FuelDepot_L%d" % level)


def tracking(level):
    b = B()
    if level == 1:
        plinth(b, 24, 20)
        b.box((-5, 0, 2.0), (9, 7, 3.4), WALL)
        b.box((-5, 0, 3.85), (9.6, 7.6, 0.3), SLATE)
        b.box((-5, -3.55, 1.2), (1.4, 0.1, 2.2), DOOR)
        b.win((-8, -3.55, 2.2), 1.4, 1.0, "y")
        b.cyl((5, 0, 2.0), 0.7, 4.0, STEEL, seg=8)
        b.box((5, 0, 4.2), (2.4, 2.4, 0.8), DARK)
        b.dish((5, 0, 6.4), 3.2, 65)
        b.cyl((-8, 4, 8.0), 0.1, 8.0, STEEL, seg=6)
    elif level == 2:
        plinth(b, 44, 34)
        b.box((-10, -3, 3.0), (18, 12, 5.4), WALL)
        b.box((-10, -3, 5.9), (18.6, 12.6, 0.5), SLATE)
        hband(b, -16, -4, -9.05, 3.2, 1.8, 1.4, 4)
        b.box((-4, -9.05, 1.4), (1.6, 0.1, 2.6), DOOR)
        lattice(b, 8, 4, 0.4, 6.0, 6.0, 16.0, 4, leg=0.5, cell=RED, brace=0.2)
        b.box((8, 4, 16.8), (7, 7, 1.0), DARK)
        b.dish((8, 4, 22.0), 6.5, 60)
        b.cyl((16, -8, 2.0), 0.6, 4.0, STEEL, seg=8)
        b.dish((16, -8, 6.0), 2.6, 60)
        b.cyl((-18, 10, 10.0), 0.12, 20.0, STEEL, seg=6)
    else:                                                                        # deep-space tracking array
        plinth(b, 74, 60)
        b.box((-20, -14, 4.0), (26, 14, 7.4), WALL)
        b.box((-20, -14, 8.0), (26.6, 14.6, 0.5), SLATE)
        hband(b, -31, -9, -21.05, 4.0, 2.2, 1.6, 6)
        b.box((-8, -21.05, 1.8), (2.0, 0.1, 3.4), DOOR)
        for x, y, r in ((12, 8, 12.0), (-12, 14, 9.0), (26, -12, 9.0)):
            b.cyl((x, y, 8.0), r * 0.12, 16.0, STEEL, seg=8)
            b.box((x, y, 16.4), (r * 0.5, r * 0.5, 1.2), DARK)
            b.dish((x, y, 16.4 + r * 0.7), r, 62)
        lattice(b, -26, 14, 0.4, 5.0, 5.0, 36.0, 8, leg=0.5, cell=RED, brace=0.2)
        sphere(b, (-26, 14, 39.5), 3.4, WHITE, seg=14)
        b.cyl((-30, 24, 12.0), 0.15, 24.0, STEEL, seg=6)
        ac_units(b, -20, -14, 8.4, 3, 3.4)
    return b.finish("RadarTower_L%d" % level)


def solar_row(b, x, y, n, spacing=4.2, tilt=30, cell=BLUEC):
    rot = RX(-tilt)
    for i in range(n):
        b.box((x + i * spacing, y, 1.6), (3.6, 2.6, 0.12), cell, rot=rot)
        b.box((x + i * spacing, y + 0.5, 0.8), (0.18, 0.18, 1.5), STEEL)


def research_lab(level):
    b = B()
    if level == 1:
        plinth(b, 26, 16)
        b.box((-3, 0, 2.4), (18, 10, 4.2), WALL)
        b.box((-3, 0, 4.7), (18.6, 10.6, 0.4), SLATE)
        hband(b, -10, 4, -5.05, 2.6, 1.7, 1.3, 5)
        b.box((5, -5.05, 1.3), (1.5, 0.1, 2.2), DOOR)
        b.box((8, 0, 3.0), (6, 6, 5.0), GRAY)                                      # dome base
        sphere(b, (8, 0, 5.7), 2.9, WHITE, seg=14)
        b.box((8, 0, 6.5), (0.6, 3.2, 1.8), DARK)                                  # slit
        solar_row(b, -9, 3.6, 3, 5.0)
    elif level == 2:
        plinth(b, 52, 34)
        b.box((-10, -4, 4.4), (28, 14, 7.6), WALL)
        b.box((-10, -4, 8.4), (28.8, 14.8, 0.5), SLATE)
        for z in (2.8, 6.0):
            hband(b, -22, 2, -11.05, z, 2.2, 1.6, 6)
        b.box((8, 4, 3.0), (8, 20, 4.6), GLASS)                                    # link corridor
        b.box((8, 4, 5.4), (8.6, 20.6, 0.4), WHITE)
        b.box((-4, 12, 3.8), (24, 10, 6.4), GRAY)
        b.box((-4, 12, 7.2), (24.6, 10.6, 0.4), SLATE)
        b.box((18, 8, 4.0), (10, 10, 6.8), GRAY)
        sphere(b, (18, 8, 8.8), 5.0, WHITE, seg=16)
        b.box((18, 8, 11.0), (0.8, 5.5, 2.6), DARK)
        b.box((-8, -10.5, 1.2), (3, 0.1, 2.4), DOOR)
        for i in range(2):
            solar_row(b, -22, 20 - i * 4.0, 4, 5.0)
    else:                                                                        # science campus
        plinth(b, 74, 58)
        b.box((-18, -10, 5.0), (34, 18, 9.0), WALL)                                # wing A
        b.box((-18, -10, 9.7), (34.8, 18.8, 0.5), SLATE)
        for z in (3.0, 7.0):
            hband(b, -33, -3, -19.05, z, 2.4, 1.8, 7)
        b.box((-6, 10, 4.5), (40, 14, 8.0), GRAY)                                  # wing B
        b.box((-6, 10, 8.7), (40.8, 14.8, 0.5), SLATE)
        hband(b, -24, 12, 3.0, 4.4, 2.6, 1.8, 7)
        b.box((14, -6, 4.0), (22, 12, 7.0), WALL)                                  # wing C
        b.box((14, -6, 7.6), (22.8, 12.8, 0.5), SLATE)
        b.box((0, -2, 3.0), (12, 8, 5.4), GLASS)                                   # atrium link
        b.box((26, 12, 6.0), (14, 14, 10.0), GRAY)                                 # dome tower
        sphere(b, (26, 12, 14.5), 8.0, WHITE, seg=18)
        b.box((26, 12, 17.0), (1.2, 9.0, 3.6), DARK)
        b.cyl((-16, 24, 4.6), 4.0, 38.0, STEEL, seg=14, rot=RY(90))                 # wind tunnel tube
        b.cyl((-16, 24, 4.6), 4.6, 5.0, WHITE, seg=14, rot=RY(90))
        b.cyl((-36, 24, 4.6), 2.0, 6.0, WHITE, seg=12, r2=4.6, rot=RY(90))
        for i in range(4):
            solar_row(b, -4, 26 - i * 3.6, 6, 5.0)
        b.box((-8, -20.2, 1.4), (3.0, 0.1, 2.6), DOOR)
        for x in (-30, 0):
            flag(b, x, -26, 0.4, 14.0)
    return b.finish("ResearchLab_L%d" % level)


def test_stand(level):
    b = B()
    s = (20, 16) if level == 1 else (34, 26) if level == 2 else (50, 38)
    b.box((0, 0, 0.4), (s[0], s[1], 0.8), LCONC)
    h = (24.0, 40.0, 64.0)[level - 1]
    w = (8.0, 12.0, 16.0)[level - 1]
    lattice(b, 0, 0, 0.8, w, w, h, (4, 6, 9)[level - 1], leg=0.8, cell=STEEL, brace=0.35)
    b.box((0, 0, h + 1.2), (w + 1.5, w + 1.5, 1.0), DARK)                         # thrust structure platform
    b.cyl((0, 0, h - 2.0), w * 0.18, 4.0, WHITE, seg=10)                           # engine under test
    b.box((0, 0, 0.8 + 1.0), (w * 0.9, w * 0.9, 2.0), CONC)                        # flame deflector block
    b.box((0, -w * 0.9, 2.0), (w * 0.6, w * 1.0, 0.9), DARK, rot=RX(18))
    b.box((0, w / 2 + 3.5, 4.0), (s[0] * 0.9, 1.6, 8.0), LCONC)                     # blast wall
    b.box((-s[0] / 2 + 5, -s[1] / 2 + 5, 2.4), (9, 6, 3.6), CONC)                  # control bunker
    b.box((-s[0] / 2 + 5, -s[1] / 2 + 5, 4.4), (9.6, 6.6, 0.4), SLATE)
    b.box((-s[0] / 2 + 5, -s[1] / 2 + 1.6, 2.2), (2.0, 0.1, 2.0), DOOR)
    if level >= 2:
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.box((s[0] / 2 - 4 + sx * 2.2, s[1] / 2 - 5 + sy * 2.2, 11), (0.6, 0.6, 22), STEEL)
        b.cyl((s[0] / 2 - 4, s[1] / 2 - 5, 24.0), 4.4, 4.0, WHITE, seg=14)
    if level >= 3:
        for x, y in ((-18, 10), (-8, 14)):
            sphere(b, (x, y, 9.0), 6.5, WHITE, seg=14)
            b.cyl((x, y, 1.6), 0.9, 3.0, STEEL, seg=8)
        b.cyl((18, -12, 20.0), 0.5, 40.0, STEEL, seg=8)
        b.box((18, -12, 40.6), (1.4, 1.4, 1.2), ORANGE)
    return b.finish("TestStand_L%d" % level)


def plot_marker(name, w, d):
    """Empty lot: yellow corner brackets, a thin slab and a sign."""
    b = B()
    b.box((0, 0, 0.1), (w, d, 0.2), CONC)
    t = 0.7
    L = min(w, d) * 0.22
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * (w / 2 - L / 2), sy * (d / 2 - t / 2), 0.24), (L, t, 0.08), HAZ)
            b.box((sx * (w / 2 - t / 2), sy * (d / 2 - L / 2), 0.24), (t, L, 0.08), HAZ)
            b.box((sx * (w / 2 - 1.0), sy * (d / 2 - 1.0), 1.1), (0.4, 0.4, 2.0), HAZ)
    b.box((0, -d / 2 + 1.5, 1.8), (0.25, 0.25, 3.2), STEEL)
    b.box((0, -d / 2 + 1.5, 3.5), (6.0, 0.2, 2.4), BLUEC)
    b.box((0, -d / 2 + 1.35, 3.8), (4.4, 0.1, 0.4), WHITE)
    b.box((0, -d / 2 + 1.35, 3.1), (3.0, 0.1, 0.3), WHITE)
    return b.finish(name)


# ================================================================ BUILD + EXPORT
def export(ob, name):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_S, name + ".fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)

makers = [("Highway", highway), ("Road", road), ("HighwayLamp", highway_lamp), ("Tree", lambda: tree(0)), ("LaunchComplex", launch_complex)]
for lv in (1, 2, 3):
    makers += [("AdminOffice_L%d" % lv, lambda lv=lv: admin(lv)), ("MissionControl_L%d" % lv, lambda lv=lv: mission_control(lv)),
               ("Hangar_L%d" % lv, lambda lv=lv: hangar(lv)), ("FuelDepot_L%d" % lv, lambda lv=lv: fuel_depot(lv)),
               ("RadarTower_L%d" % lv, lambda lv=lv: tracking(lv)), ("ResearchLab_L%d" % lv, lambda lv=lv: research_lab(lv)),
               ("TestStand_L%d" % lv, lambda lv=lv: test_stand(lv))]
PLOTS = {"AdminOffice": (62, 36), "MissionControl": (68, 44), "Hangar": (90, 80), "FuelDepot": (70, 50),
         "RadarTower": (74, 60), "ResearchLab": (74, 58), "TestStand": (50, 38)}
for k, (w, d) in PLOTS.items():
    makers.append(("Plot_" + k, lambda k=k, w=w, d=d: plot_marker("Plot_" + k, w, d)))

manifest = {}
objs = []
for name, fn in makers:
    ob = fn()
    objs.append(ob)
    dims = ob.dimensions
    manifest[name] = {"w": round(dims.x, 2), "d": round(dims.y, 2), "h": round(dims.z, 2),
                      "tris": sum(len(p.vertices) - 2 for p in ob.data.polygons)}
    export(ob, name)
json.dump(manifest, open(os.path.join(OUT_S, "site-manifest.json"), "w"), indent=1)
x = 0
for ob in objs:
    ob.location.x = x
    x += 30
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "space-site.blend"))
result = {"exported": len(objs)}
print("DONE", result)
