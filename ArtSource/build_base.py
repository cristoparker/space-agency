# Builds the low-poly space base (6 buildings/pad + 5 vehicles) and exports each as FBX for Unity.
#   Headless:  blender -b --python ArtSource/build_base.py
#   Live MCP:  python ArtSource/blender_exec.py ArtSource/build_base.py
# Metres, origin at the centre of each model's base, front (door / bonnet) faces -Y in Blender.
# Hidden geometry (faces buried inside other parts, bottom faces) is removed by B.cleanup().
import bpy, bmesh, math, os, struct, zlib
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

try:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    ROOT = r"C:\Users\devsuki\space-agency"
OUT = os.path.join(ROOT, "Assets", "Assets", "Models", "Base")
os.makedirs(OUT, exist_ok=True)

# ---- palette: 8x8 cells. Index i -> column i%8, row i//8 (row 0 = bottom of the image) ----
PAL = [None] * 64
PAL[0:16] = [
    (206, 203, 196), (150, 155, 160), (82, 90, 102), (150, 98, 82), (110, 146, 168), (148, 148, 146),
    (205, 135, 80), (58, 60, 66), (92, 74, 60), (204, 178, 92), (232, 232, 228), (112, 124, 138),
    (170, 82, 72), (96, 128, 100), (178, 178, 172), (130, 170, 185)]
BODY_COLORS = [(170, 82, 72), (86, 112, 150), (222, 222, 218), (96, 128, 100),
               (196, 168, 88), (130, 134, 140), (54, 56, 62), (205, 135, 80)]
for k in range(8):                      # car rows: Unity shifts the UV by k/8 to recolour only the body
    PAL[16 + k] = BODY_COLORS[k]
    PAL[24 + k] = (110, 146, 168)       # glass
    PAL[32 + k] = (38, 38, 42)          # tyres / bumpers
    PAL[40 + k] = (236, 230, 200)       # head lights
    PAL[48 + k] = (176, 60, 56)         # tail lights
    PAL[56 + k] = (112, 124, 138)       # steel / hubs
(WALL, GRAY, SLATE, RUST, WIN, CONC, ORANGE, DARK, DOOR, HAZ, WHITE, STEEL, RED, GREEN, LCONC, GLASS) = range(16)
BODY, CWIN, TIRE, HEAD, TAIL, CSTEEL = 16, 24, 32, 40, 48, 56
CELL = 4


def write_palette(path):
    rows = []
    for py in range(8 * CELL):
        rb = 7 - py // CELL
        row = bytearray([0])
        for px in range(8 * CELL):
            r, g, b = PAL[rb * 8 + px // CELL]
            row += bytes((r, g, b, 255))
        rows.append(bytes(row))
    raw = b"".join(rows)
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 8 * CELL, 8 * CELL, 8, 6, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))
    open(path, "wb").write(png)


def cell_uv(i):
    return ((i % 8 + 0.5) / 8.0, (i // 8 + 0.5) / 8.0)


RX = lambda a: Matrix.Rotation(math.radians(a), 3, "X")
RY = lambda a: Matrix.Rotation(math.radians(a), 3, "Y")
D_RAY = Vector((0.3715, 0.5234, 0.7671)).normalized()


class B:
    """Tiny bmesh builder. Every part is a closed shell painted with one palette cell."""
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.parts = []

    def _paint(self, verts, cell, mat=0):
        u = cell_uv(cell)
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = mat          # slot 1 = lamp glass (Unity switches it on at night)
            for l in f.loops:
                l[self.uv].uv = u
        self.parts.append(faces)

    def box(self, c, s, cell, rot=None, mat=0):
        v = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        bmesh.ops.scale(self.bm, vec=s, verts=v)
        if rot is not None:
            bmesh.ops.rotate(self.bm, cent=(0, 0, 0), matrix=rot, verts=v)
        bmesh.ops.translate(self.bm, vec=c, verts=v)
        self._paint(v, cell, mat)

    def cyl(self, c, r, h, cell, seg=10, r2=None, rot=None, mat=0):
        v = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg,
                                  radius1=r, radius2=r if r2 is None else r2, depth=h)["verts"]
        if rot is not None:
            bmesh.ops.rotate(self.bm, cent=(0, 0, 0), matrix=rot, verts=v)
        bmesh.ops.translate(self.bm, vec=c, verts=v)
        self._paint(v, cell, mat)

    def prism(self, c, w, d, h, cell):
        """Gabled roof, ridge along Y. c = centre of the base."""
        hw, hd = w / 2, d / 2
        pts = [(-hw, -hd, 0), (hw, -hd, 0), (0, -hd, h), (-hw, hd, 0), (hw, hd, 0), (0, hd, h)]
        vs = [self.bm.verts.new((p[0] + c[0], p[1] + c[1], p[2] + c[2])) for p in pts]
        for idx in [(0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)]:
            self.bm.faces.new([vs[i] for i in idx])
        self._paint(vs, cell)

    def dish(self, c, r, elev, cell=STEEL, rim=WHITE):
        """Parabolic-ish dish whose open side faces -Y and tilts up by elev degrees."""
        th = -(90 + elev)
        rot = RX(th)
        f = Vector((0, -math.cos(math.radians(elev)), math.sin(math.radians(elev))))
        self.cyl(c, r, 0.5, cell, seg=14, r2=0.22, rot=rot)
        self.cyl(Vector(c) + f * 0.26, r + 0.06, 0.1, rim, seg=14, rot=rot)
        self.cyl(Vector(c) + f * 0.75, 0.05, 1.0, DARK, seg=6, rot=rot)       # feed arm
        self.cyl(Vector(c) + f * 1.25, 0.13, 0.2, RED, seg=6, rot=rot)        # feed horn

    def win(self, c, w, h, axis="y"):
        """Window with a dark frame, sitting on a wall whose normal is along `axis`."""
        if axis == "y":
            self.box(c, (w + 0.16, 0.06, h + 0.16), DARK)
            self.box((c[0], c[1] + (-0.02 if c[1] < 0 else 0.02), c[2]), (w, 0.1, h), WIN)
        else:
            self.box(c, (0.06, w + 0.16, h + 0.16), DARK)
            self.box((c[0] + (-0.02 if c[0] < 0 else 0.02), c[1], c[2]), (0.1, w, h), WIN)

    def cleanup(self):
        """Delete faces buried inside other parts, and the bottom faces nobody sees."""
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bvhs = []
        for faces in self.parts:
            idx, cos, polys = {}, [], []
            for f in faces:
                p = []
                for v in f.verts:
                    if v not in idx:
                        idx[v] = len(cos)
                        cos.append(v.co.copy())
                    p.append(idx[v])
                polys.append(p)
            bvhs.append(BVHTree.FromPolygons(cos, polys))

        def inside(p, skip):
            for j, t in enumerate(bvhs):
                if j == skip:
                    continue
                o, n = p, 0
                for _ in range(32):
                    h = t.ray_cast(o, D_RAY)
                    if h[0] is None:
                        break
                    n += 1
                    o = h[0] + D_RAY * 1e-4
                if n % 2 == 1:
                    return True
            return False

        kill = set()
        for i, faces in enumerate(self.parts):
            for f in faces:
                n = f.normal
                pts = [f.calc_center_median()] + [v.co.copy() for v in f.verts]
                if n.z < -0.9 and max(p.z for p in pts) < 0.02:
                    kill.add(f)
                elif all(inside(p + n * 0.01, i) for p in pts):
                    kill.add(f)
        total = len(bm.faces)
        bmesh.ops.delete(bm, geom=list(kill), context="FACES")
        return total, len(kill)

    def finish(self, name):
        stats = self.cleanup()
        used = max([f.material_index for f in self.bm.faces] + [0]) + 1
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        for mname in ("Base", "Lamp")[:used]:
            me.materials.append(bpy.data.materials.get(mname) or bpy.data.materials.new(mname))
        self.bm.free()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        STATS[name] = stats
        return ob


STATS = {}


# ============================== BUILDINGS ==============================
def mission_control():
    b = B()
    b.box((0, 0, 0.15), (6.8, 5.2, 0.3), CONC)                    # plinth
    b.box((0, 0, 1.9), (6.4, 4.8, 3.2), WALL)                     # body
    b.box((0, 0, 3.65), (6.9, 5.3, 0.3), SLATE)                   # roof slab
    for y in (-2.6, 2.6):                                          # parapet
        b.box((0, y, 3.95), (6.9, 0.12, 0.3), LCONC)
    for x in (-3.4, 3.4):
        b.box((x, 0, 3.95), (0.12, 5.3, 0.3), LCONC)
    b.box((-0.9, -2.41, 0.95), (4.4, 0.05, 0.3), ORANGE)           # stripe
    for x in (-2.2, -0.6):
        b.win((x, -2.4, 2.3), 1.1, 1.0, "y")
    b.box((0.9, -2.41, 2.3), (1.3, 0.06, 1.1), DARK)               # wide display window
    b.box((0.9, -2.44, 2.3), (1.15, 0.06, 0.95), GLASS)
    b.box((2.5, -2.43, 1.2), (1.1, 0.1, 2.1), DARK)                # door
    b.box((2.5, -2.48, 1.15), (0.95, 0.06, 1.95), DOOR)
    b.box((2.5, -2.9, 2.45), (1.7, 0.9, 0.1), SLATE)               # canopy
    for x in (1.75, 3.25):
        b.cyl((x, -3.25, 1.2), 0.05, 2.4, STEEL, seg=6)
    b.box((2.5, -2.95, 0.12), (1.6, 0.9, 0.24), CONC)              # steps
    for x in (-3.2, 3.2):
        for y in (-1.0, 1.0):
            b.win((x, y, 2.2), 1.0, 1.0, "x")
    b.box((-1.8, 0.8, 4.35), (1.4, 1.0, 0.5), STEEL)               # AC units
    b.cyl((-1.8, 0.8, 4.64), 0.33, 0.06, DARK, seg=8)
    b.box((-0.2, 1.3, 4.3), (0.9, 0.7, 0.4), STEEL)
    b.cyl((1.7, 0.7, 4.6), 0.15, 1.0, STEEL, seg=6)                # dish pedestal
    b.dish((1.7, 0.7, 5.5), 1.0, 55)
    b.cyl((-2.7, 1.6, 5.2), 0.05, 3.0, STEEL, seg=6)               # mast + arms
    b.box((-2.7, 1.6, 6.2), (1.1, 0.05, 0.05), STEEL)
    b.box((-2.7, 1.6, 5.6), (0.7, 0.05, 0.05), STEEL)
    b.box((3.5, 2.0, 0.3), (0.3, 0.3, 0.6), HAZ)                   # bollards
    return b.finish("MissionControl")


def hangar():
    b = B()
    b.box((0, 0, 0.15), (9.6, 7.6, 0.3), CONC)
    b.box((0, 0, 2.65), (9.0, 7.0, 4.7), GRAY)                     # body z .3..5
    b.prism((0, 0, 5.0), 9.6, 7.6, 2.0, RUST)
    b.box((0, -3.56, 2.5), (5.8, 0.14, 4.4), DARK)                 # door frame
    for x in (-1.45, 1.45):
        b.box((x, -3.64, 2.4), (2.8, 0.12, 4.1), STEEL)            # door leaves
    b.box((0, -3.7, 4.65), (5.8, 0.14, 0.4), HAZ)                  # hazard band
    b.box((0, -3.7, 4.95), (6.2, 0.22, 0.22), DARK)                # rail
    b.win((0, -3.85, 6.0), 0.9, 0.7, "y")                          # gable window
    for x in (-3.4, 3.4):
        b.box((x, -3.62, 1.2), (0.6, 0.1, 1.8), DOOR)              # side doors
    for i in range(4):
        b.win((-4.55, -2.4 + i * 1.6, 3.7), 0.9, 0.6, "x")
        b.win((4.55, -2.4 + i * 1.6, 3.7), 0.9, 0.6, "x")
    b.box((0, 0, 7.1), (0.9, 5.2, 0.35), STEEL)                    # ridge vent
    b.box((5.6, 0, 1.4), (2.6, 4.0, 2.2), CONC)                    # lean-to
    b.box((5.6, 0, 2.6), (2.9, 4.4, 0.2), SLATE)
    b.box((5.6, -2.03, 1.0), (1.0, 0.1, 1.8), DOOR)
    b.win((5.6, 2.03, 1.6), 1.0, 0.8, "y")
    for x in (-4.4, 4.4):                                          # flood lights
        b.cyl((x, -4.3, 2.6), 0.07, 5.0, DARK, seg=6)
        b.box((x, -4.45, 5.15), (0.5, 0.35, 0.2), HAZ)
    b.cyl((-4.7, 3.7, 0.9), 0.5, 1.2, STEEL, seg=8)                # small tank
    return b.finish("Hangar")


def fuel_depot():
    b = B()
    b.box((0, 0, 0.15), (10.0, 6.4, 0.3), CONC)
    for y in (-3.0, 3.0):                                          # bund walls
        b.box((0, y, 0.5), (10.0, 0.18, 0.4), LCONC)
    for x in (-4.9, 4.9):
        b.box((x, 0, 0.5), (0.18, 6.4, 0.4), LCONC)
    for x in (-3.2, 0, 3.2):
        b.cyl((x, 0.4, 2.6), 1.35, 4.6, WHITE, seg=12)             # tank z .3..4.9
        b.cyl((x, 0.4, 5.1), 1.0, 0.5, WHITE, seg=12, r2=0.35)     # dome
        b.cyl((x, 0.4, 1.3), 1.4, 0.3, ORANGE, seg=12)             # bands
        b.cyl((x, 0.4, 3.9), 1.4, 0.3, ORANGE, seg=12)
        b.cyl((x, 0.4, 5.4), 0.1, 0.4, STEEL, seg=6)               # vent
    for x in (-1.6, 1.6):                                          # catwalk between tanks
        b.box((x, 0.4, 4.95), (1.9, 0.8, 0.08), DARK)
    for dx in (-0.2, 0.2):                                         # ladder on the left tank
        b.box((-3.2 + dx, -0.98, 2.6), (0.05, 0.05, 4.6), STEEL)
    for i in range(9):
        b.box((-3.2, -0.98, 0.7 + i * 0.5), (0.45, 0.05, 0.05), STEEL)
    b.cyl((0, -2.3, 0.8), 0.17, 9.4, STEEL, seg=6, rot=RY(90))      # long pipe
    for x in (-3.2, 0, 3.2):
        b.cyl((x, -1.3, 0.8), 0.11, 2.0, STEEL, seg=6, rot=RX(90))  # branch to tank
    for x in (-4.4, -1.6, 1.6, 4.4):
        b.box((x, -2.3, 0.45), (0.25, 0.25, 0.9), DARK)            # pipe supports
    b.box((4.0, 2.1, 0.95), (1.6, 1.4, 1.3), RED)                  # pump house
    b.box((4.0, 2.1, 1.65), (1.8, 1.6, 0.15), SLATE)
    b.box((4.0, 1.38, 0.8), (0.6, 0.06, 1.1), DOOR)
    b.cyl((4.0, 2.1, 1.95), 0.13, 0.5, STEEL, seg=6)               # exhaust
    return b.finish("FuelDepot")


def admin_office():
    b = B()
    b.box((0, 0, 0.15), (5.8, 4.8, 0.3), CONC)
    b.box((0, 0, 2.9), (5.4, 4.4, 5.2), WALL)                      # z .3..5.5
    b.box((0, 0, 3.1), (5.6, 4.6, 0.18), LCONC)                    # floor band
    b.prism((0, 0, 5.5), 6.0, 4.9, 1.4, SLATE)
    for z in (1.8, 4.2):
        for x in (-1.7, 0, 1.7):
            if z == 1.8 and x == 0:
                continue
            b.win((x, -2.2, z), 0.95, 1.1, "y")
        for x in (-1.7, 0, 1.7):
            b.win((x, 2.2, z), 0.95, 1.1, "y")
        for y in (-1.0, 1.0):
            b.win((-2.7, y, z), 0.9, 1.1, "x")
            b.win((2.7, y, z), 0.9, 1.1, "x")
    b.box((0, -2.21, 1.1), (1.2, 0.1, 2.0), DARK)                  # door
    b.box((0, -2.26, 1.05), (1.05, 0.06, 1.85), DOOR)
    b.box((0, -2.85, 2.35), (2.0, 1.0, 0.1), RUST)                 # canopy
    for x in (-0.9, 0.9):
        b.cyl((x, -3.3, 1.2), 0.05, 2.4, STEEL, seg=6)
    b.box((0, -3.0, 0.12), (1.8, 1.0, 0.24), CONC)                 # steps
    b.box((1.7, 1.2, 6.9), (0.5, 0.5, 1.0), RED)                   # chimney
    b.cyl((-2.9, -2.4, 3.6), 0.04, 7.0, STEEL, seg=6)              # flag pole
    b.box((-2.5, -2.4, 6.6), (0.8, 0.04, 0.5), RED)
    b.box((2.9, 0.4, 0.5), (0.4, 1.4, 1.0), STEEL)                 # AC condenser on ground
    return b.finish("AdminOffice")


def radar_tower():
    b = B()
    b.box((0, 0, 0.1), (3.6, 3.6, 0.2), CONC)
    b.box((0, 0, 0.8), (3.2, 3.2, 1.2), GRAY)                      # hut
    b.box((0, 0, 1.5), (3.5, 3.5, 0.18), SLATE)
    b.box((0, -1.62, 0.7), (0.9, 0.08, 1.2), DOOR)
    b.win((-1.1, -1.62, 0.95), 0.7, 0.5, "y")
    b.box((1.2, 1.0, 1.75), (0.7, 0.7, 0.35), STEEL)               # roof vent
    for sx in (-0.9, 0.9):                                         # lattice legs
        for sy in (-0.9, 0.9):
            b.box((sx, sy, 6.2), (0.18, 0.18, 9.2), STEEL)
    for z in (2.0, 3.8, 5.6, 7.4, 9.0):                            # ring braces
        for s in (-0.9, 0.9):
            b.box((0, s, z), (1.8, 0.1, 0.1), STEEL)
            b.box((s, 0, z), (0.1, 1.8, 0.1), STEEL)
    b.box((0, 0, 10.9), (2.6, 2.6, 0.25), DARK)                    # platform
    for s in (-1.2, 1.2):
        b.box((0, s, 11.5), (2.6, 0.06, 0.06), STEEL)
        b.box((s, 0, 11.5), (0.06, 2.6, 0.06), STEEL)
        b.box((s, s, 11.4), (0.06, 0.06, 1.0), STEEL)
    b.cyl((0, 0, 11.4), 0.25, 1.0, STEEL, seg=8)                   # pedestal
    b.dish((0, 0, 12.6), 1.9, 35)
    b.cyl((0, 0.4, 13.7), 0.04, 1.2, STEEL, seg=6)                 # beacon mast
    b.cyl((0, 0.4, 14.4), 0.12, 0.2, RED, seg=6)
    return b.finish("RadarTower")


def launch_pad():
    b = B()
    b.cyl((0, 0, 0.2), 7.6, 0.4, CONC, seg=20)                     # pad slab
    b.cyl((0, 0, 0.42), 5.2, 0.06, LCONC, seg=20)
    b.cyl((0, 0, 0.45), 3.0, 0.06, DARK, seg=20)                   # deflector
    for a in range(4):                                             # hold-downs
        x, y = math.cos(math.radians(45 + a * 90)) * 2.2, math.sin(math.radians(45 + a * 90)) * 2.2
        b.box((x, y, 0.7), (0.55, 0.55, 0.5), HAZ)
    tx = 6.3                                                       # lattice tower (+X)
    for sx in (-0.9, 0.9):
        for sy in (-0.9, 0.9):
            b.box((tx + sx, sy, 10.2), (0.26, 0.26, 20.0), STEEL)
    for z in range(1, 21, 2):
        for s in (-0.9, 0.9):
            b.box((tx, s, z), (1.8, 0.12, 0.12), STEEL)
            b.box((tx + s, 0, z), (0.12, 1.8, 0.12), STEEL)
    b.box((tx, 0, 20.3), (2.2, 2.2, 0.3), DARK)
    b.box((tx - 2.9, 0, 14.0), (5.8, 0.4, 0.4), RED)               # umbilical arm
    b.box((tx - 5.6, 0, 14.0), (0.6, 0.6, 0.6), HAZ)
    b.box((tx + 1.05, 0, 8.0), (0.7, 1.2, 1.6), WHITE)             # lift car
    b.cyl((tx, 0, 21.8), 0.05, 2.6, STEEL, seg=6)                  # lightning mast
    b.box((tx, 0, 0.45), (3.0, 3.0, 0.3), CONC)                    # tower base
    b.box((-6.2, -6.0, 0.9), (1.8, 1.8, 1.4), GRAY)                # equipment shed
    b.box((-6.2, -6.0, 1.7), (2.0, 2.0, 0.18), SLATE)
    b.box((-6.2, -6.92, 0.8), (0.75, 0.08, 1.3), DOOR)
    b.cyl((tx - 3.2, -1.2, 0.55), 0.18, 7.0, STEEL, seg=6, rot=RY(90))   # ground pipes
    b.cyl((tx - 3.2, -1.6, 0.55), 0.18, 7.0, ORANGE, seg=6, rot=RY(90))
    return b.finish("LaunchPad")


# ============================== VEHICLES ==============================
def wheels(b, xs, ys, r=0.34, w=0.26):
    for x in xs:
        for y in ys:
            sx = 1 if x > 0 else -1
            b.cyl((x, y, r), r, w, TIRE, seg=10, rot=RY(90))
            b.cyl((x + sx * (w / 2 + 0.01), y, r), r * 0.5, 0.05, CSTEEL, seg=8, rot=RY(90))


def lights(b, front_y, back_y, w, z=0.72):
    for sx in (-1, 1):
        b.box((sx * (w / 2 - 0.3), front_y, z), (0.34, 0.06, 0.16), HEAD)
        b.box((sx * (w / 2 - 0.3), back_y, z), (0.34, 0.06, 0.16), TAIL)
    b.box((0, front_y, z), (0.5, 0.05, 0.14), TIRE)                # grille
    b.box((0, front_y - 0.03, 0.42), (w, 0.1, 0.2), TIRE)          # bumpers
    b.box((0, back_y + 0.03, 0.42), (w, 0.1, 0.2), TIRE)


def sedan():
    b = B()
    b.box((0, 0, 0.66), (1.7, 3.9, 0.55), BODY)
    b.box((0, 0.3, 1.18), (1.5, 2.0, 0.5), BODY)
    for sx in (-1, 1):
        b.box((sx * 0.76, 0.3, 1.2), (0.04, 1.7, 0.34), CWIN)
    b.box((0, -0.72, 1.2), (1.3, 0.04, 0.34), CWIN)
    b.box((0, 1.31, 1.2), (1.3, 0.04, 0.32), CWIN)
    b.box((0, 0.3, 1.45), (1.52, 2.02, 0.06), BODY)
    lights(b, -1.96, 1.96, 1.7)
    wheels(b, (-0.78, 0.78), (-1.25, 1.2))
    return b.finish("CarSedan")


def hatchback():
    b = B()
    b.box((0, 0, 0.7), (1.75, 3.6, 0.6), BODY)
    b.box((0, 0.45, 1.3), (1.62, 2.5, 0.62), BODY)
    for sx in (-1, 1):
        b.box((sx * 0.82, 0.45, 1.32), (0.04, 2.1, 0.42), CWIN)
    b.box((0, -0.82, 1.32), (1.4, 0.04, 0.42), CWIN)
    b.box((0, 1.71, 1.32), (1.4, 0.04, 0.38), CWIN)
    for sx in (-0.7, 0.7):                                         # roof rails
        b.box((sx, 0.45, 1.66), (0.06, 2.3, 0.06), TIRE)
    lights(b, -1.81, 1.81, 1.75, 0.78)
    wheels(b, (-0.8, 0.8), (-1.1, 1.15), r=0.36)
    return b.finish("CarHatchback")


def pickup():
    b = B()
    b.box((0, -0.2, 0.7), (1.8, 4.2, 0.55), BODY)                  # lower body
    b.box((0, -0.55, 1.28), (1.6, 1.5, 0.6), BODY)                 # cab
    for sx in (-1, 1):
        b.box((sx * 0.81, -0.55, 1.3), (0.04, 1.2, 0.38), CWIN)
    b.box((0, -1.3, 1.3), (1.4, 0.04, 0.38), CWIN)
    b.box((0, 0.2, 1.3), (1.4, 0.04, 0.36), CWIN)
    for sx in (-1, 1):                                             # bed walls
        b.box((sx * 0.86, 1.15, 1.08), (0.1, 1.7, 0.4), BODY)
    b.box((0, 2.0, 1.08), (1.8, 0.1, 0.4), BODY)
    b.box((0, 1.15, 0.97), (1.6, 1.7, 0.05), TIRE)                 # bed floor
    lights(b, -2.3, 2.3, 1.8)
    wheels(b, (-0.8, 0.8), (-1.4, 1.35), r=0.38, w=0.28)
    return b.finish("CarPickup")


def service_van():
    b = B()
    b.box((0, 0, 1.15), (1.85, 4.3, 1.7), BODY)
    b.box((0, -2.1, 1.0), (1.85, 0.12, 0.9), BODY)
    b.box((0, -1.62, 1.55), (1.6, 0.04, 0.6), CWIN)                # windscreen
    for sx in (-1, 1):
        b.box((sx * 0.93, -1.2, 1.6), (0.04, 0.9, 0.5), CWIN)
        b.box((sx * 0.93, 0.7, 1.1), (0.04, 0.05, 1.3), TIRE)      # sliding-door seam
        b.box((sx * 0.93, 0.2, 1.95), (0.04, 3.0, 0.14), CSTEEL)   # roof stripe on the side
    b.box((0, 0.3, 2.04), (0.9, 1.0, 0.2), CSTEEL)                 # roof vent
    b.box((0, 2.16, 1.2), (1.4, 0.05, 1.0), TIRE)                  # rear doors
    lights(b, -2.17, 2.17, 1.85, 0.62)
    wheels(b, (-0.82, 0.82), (-1.4, 1.3), r=0.36)
    return b.finish("CarVan")


def fuel_truck():
    b = B()
    b.box((0, 0, 0.7), (1.9, 6.4, 0.3), TIRE)                      # chassis
    b.box((0, -2.2, 1.35), (1.9, 1.5, 1.4), BODY)                  # cab
    b.box((0, -2.96, 1.55), (1.7, 0.04, 0.6), CWIN)
    for sx in (-1, 1):
        b.box((sx * 0.96, -2.4, 1.6), (0.04, 0.9, 0.5), CWIN)
    b.cyl((0, 0.7, 1.55), 0.95, 4.2, CSTEEL, seg=12, rot=RX(90))   # tank (axis along Y)
    b.cyl((0, 0.7, 1.55), 0.99, 0.35, BODY, seg=12, rot=RX(90))
    b.cyl((0, 2.0, 1.55), 0.99, 0.35, BODY, seg=12, rot=RX(90))
    b.box((0, 0.7, 2.6), (0.5, 0.5, 0.2), TIRE)                    # hatch
    b.box((0, 3.2, 0.6), (1.7, 0.1, 0.25), TIRE)
    lights(b, -3.0, 3.22, 1.9, 0.62)
    wheels(b, (-0.88, 0.88), (-2.3, 1.0, 2.2), r=0.42, w=0.3)
    return b.finish("CarFuelTruck")


# ============================== LAMPS (slot 1 = lamp glass) ==============================
def street_lamp():
    b = B()
    b.cyl((0, 0, 0.15), 0.3, 0.3, CONC, seg=8)
    b.cyl((0, 0, 3.2), 0.07, 6.0, DARK, seg=8)
    b.box((0, -0.65, 6.05), (0.12, 1.3, 0.12), DARK)               # arm
    b.box((0, -1.3, 6.02), (0.55, 0.95, 0.14), DARK)               # housing
    b.box((0, -1.3, 5.93), (0.44, 0.84, 0.04), LCONC, mat=1)       # lamp glass
    return b.finish("LampStreet")


def floodlight_tower():
    b = B()
    b.cyl((0, 0, 0.2), 0.55, 0.4, CONC, seg=8)
    b.cyl((0, 0, 4.7), 0.13, 9.0, DARK, seg=8)
    b.box((0, 0, 9.0), (0.9, 0.9, 0.12), DARK)                      # platform
    tilt = RX(22)
    head = Vector((0, -0.45, 9.55))
    b.box(head, (1.7, 0.2, 1.1), DARK, rot=tilt)                    # lamp bank frame
    for sx in (-0.4, 0.4):
        for sz in (-0.25, 0.25):
            b.box(head + tilt @ Vector((sx, -0.12, sz)), (0.62, 0.05, 0.4), LCONC, rot=tilt, mat=1)
    b.box((0, -0.1, 9.25), (0.12, 0.12, 0.5), DARK)                 # yoke
    return b.finish("LampFloodTower")


def bollard_light():
    b = B()
    b.cyl((0, 0, 0.05), 0.17, 0.1, CONC, seg=8)
    b.box((0, 0, 0.4), (0.15, 0.15, 0.7), DARK)
    b.cyl((0, 0, 0.88), 0.13, 0.22, LCONC, seg=8, mat=1)           # glowing head
    b.cyl((0, 0, 1.02), 0.17, 0.05, DARK, seg=8)
    return b.finish("LampBollard")


def uplight():
    b = B()
    b.box((0, 0, 0.1), (0.7, 0.55, 0.2), DARK)
    tilt = RX(-55)
    c = Vector((0, -0.05, 0.38))
    b.box(c, (0.55, 0.14, 0.55), DARK, rot=tilt)
    b.box(c + tilt @ Vector((0, -0.09, 0)), (0.42, 0.04, 0.42), LCONC, rot=tilt, mat=1)
    return b.finish("LampUplight")


# ============================== BUILD + EXPORT ==============================
def export(ob, name):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False)


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)
write_palette(os.path.join(OUT, "base-palette.png"))
makers = [mission_control, hangar, fuel_depot, admin_office, radar_tower, launch_pad,
          sedan, hatchback, pickup, service_van, fuel_truck,
          street_lamp, floodlight_tower, bollard_light, uplight]
objs = [(fn.__name__, fn()) for fn in makers]
for _, ob in objs:
    export(ob, ob.name)
x = 0
for _, ob in objs:
    ob.location.x = x
    x += 20
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "space-base.blend"))
result = {"exported": [ob.name for _, ob in objs], "faces_total_removed": STATS}
print("DONE", result)
