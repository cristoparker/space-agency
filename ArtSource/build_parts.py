# Rocket-builder part models (tanks, engines, fairings, boosters, comms, power, recovery, structure).
#   Live MCP:  python ArtSource/blender_exec.py ArtSource/build_parts.py
# Reuses the low-poly builder (class B, cleanup, dish...) from build_base.py.
# Units are metres. Every part: origin at the centre of its BOTTOM, +Z up (becomes +Y in Unity),
# stack parts by their top/bottom nodes. Front (outward-facing side of comms/panels) is -Y.
# Writes FBX files + parts-palette.png to Assets/Assets/Models/Parts and the stats catalog to Assets/Data/Parts.
import bpy, bmesh, math, os, json, struct, zlib
from mathutils import Matrix, Vector

try:
    ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
except NameError:
    ROOT = r"C:\Users\devsuki\space-agency"
_src = open(os.path.join(ROOT, "ArtSource", "build_base.py"), encoding="utf-8").read()
exec(_src.split("# ============================== BUILDINGS")[0])          # B class, helpers, palette fns

OUT_P = os.path.join(ROOT, "Assets", "Assets", "Models", "Parts")
OUT_D = os.path.join(ROOT, "Assets", "Data", "Parts")
os.makedirs(OUT_P, exist_ok=True)
os.makedirs(OUT_D, exist_ok=True)

# ---------------- palette: 8 columns x 16 rows. Row 0/1 = livery (Unity shifts UV by k/8 to recolour) ----------------
GW, GH = 8, 16
LIVERY, STRIPE = 0, 8
K_WHITE, K_LGRAY, K_MGRAY, K_DGRAY, K_BLACK, K_ORANGE, K_RED, K_STEEL, K_GOLD, K_SOLAR, K_SOLARL, K_GLASS, K_HAZ, K_COPPER = \
    [r * 8 for r in range(2, 16)]
LIVERIES = [(232, 232, 228), (44, 46, 52), (205, 135, 80), (86, 112, 150), (170, 82, 72), (130, 134, 140), (96, 128, 100), (196, 168, 88)]
STRIPES = [(44, 46, 52), (232, 232, 228), (232, 232, 228), (232, 232, 228), (232, 232, 228), (40, 40, 44), (232, 232, 228), (44, 46, 52)]
CONST = [(232, 232, 228), (170, 172, 176), (120, 124, 130), (62, 64, 70), (34, 34, 38), (205, 135, 80), (170, 82, 72),
         (112, 124, 138), (196, 160, 84), (52, 76, 112), (92, 118, 158), (110, 146, 168), (204, 178, 92), (170, 108, 72)]


def cell_uv(i):
    return ((i % GW + 0.5) / GW, (i // GW + 0.5) / GH)


def write_parts_palette(path):
    rows = []
    for py in range(GH * CELL):
        r = GH - 1 - py // CELL
        row = bytearray([0])
        for px in range(GW * CELL):
            c = px // CELL
            if r == 0: col = LIVERIES[c]
            elif r == 1: col = STRIPES[c]
            else: col = CONST[r - 2]
            row += bytes((col[0], col[1], col[2], 255))
        rows.append(bytes(row))
    raw = b"".join(rows)
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", GW * CELL, GH * CELL, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


REG = {}
RS, RM, RL = 0.6, 1.0, 1.5          # radius of small / medium / large stack diameter classes


class P(B):
    def lathe(self, prof, cell, seg=14, off=(0, 0, 0), bottom_cell=None, top_cell=None):
        bm = self.bm
        rings = []
        for r, z in prof:
            if r < 1e-6:
                rings.append([bm.verts.new((off[0], off[1], z + off[2]))])
            else:
                rings.append([bm.verts.new((off[0] + r * math.cos(2 * math.pi * i / seg), off[1] + r * math.sin(2 * math.pi * i / seg), z + off[2]))
                              for i in range(seg)])
        for a, b2 in zip(rings[:-1], rings[1:]):
            if len(a) == 1 and len(b2) == 1:
                continue
            for i in range(seg):
                j = (i + 1) % seg
                if len(a) == 1: bm.faces.new((a[0], b2[j], b2[i]))
                elif len(b2) == 1: bm.faces.new((a[i], a[j], b2[0]))
                else: bm.faces.new((a[i], a[j], b2[j], b2[i]))
        caps = []
        if len(rings[0]) > 1: caps.append((bottom_cell, bm.faces.new(rings[0][::-1])))
        if len(rings[-1]) > 1: caps.append((top_cell, bm.faces.new(rings[-1])))
        self._paint([v for r in rings for v in r], cell)
        for c, f in caps:
            if c is not None:
                for l in f.loops:
                    l[self.uv].uv = cell_uv(c)

    def slab(self, pts, t, cell, off=(0, 0, 0)):
        """Convex polygon in the XZ plane extruded t along Y."""
        bm = self.bm
        fr = [bm.verts.new((x + off[0], -t / 2 + off[1], z + off[2])) for x, z in pts]
        bk = [bm.verts.new((x + off[0], t / 2 + off[1], z + off[2])) for x, z in pts]
        bm.faces.new(fr)
        bm.faces.new(bk[::-1])
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((fr[i], bk[i], bk[j], fr[j]))
        self._paint(fr + bk, cell)

    def finish(self, name, meta):
        self.cleanup()
        zs = [v.co.z for v in self.bm.verts]
        xs = [v.co.x for v in self.bm.verts]
        ys = [v.co.y for v in self.bm.verts]
        me = bpy.data.meshes.new(name)
        tris = sum(len(f.verts) - 2 for f in self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        me.materials.append(bpy.data.materials.get("Base") or bpy.data.materials.new("Base"))
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        meta = dict(meta)
        meta.update(id=name, model=name, bottomZ=round(min(zs), 3), heightM=round(max(zs) - min(zs), 3),
                    widthM=round(max(xs) - min(xs), 3), depthM=round(max(ys) - min(ys), 3), tris=tris)
        REG[name] = meta
        return ob


def nose(r, z0, h, n=6):
    return [(r * max(0.0, 1 - (i / n) ** 1.8) ** 0.6, z0 + h * i / n) for i in range(n + 1)]


def ring(b, r, z0, z1, cell, seg=14):
    b.lathe([(r, z0), (r, z1)], cell, seg=seg)


def spec(**kw):
    return kw


# =============================== FUEL TANKS ===============================
def make_tank(name, size, r, h):
    b = P()
    b.lathe([(r * 0.96, 0), (r, 0.12), (r, h - 0.12), (r * 0.96, h)], LIVERY)
    ring(b, r * 1.02, 0, 0.22, K_DGRAY)
    ring(b, r * 1.02, h - 0.22, h, K_DGRAY)
    ring(b, r * 1.012, h * 0.42, h * 0.42 + 0.3 * r + 0.1, STRIPE)
    b.box((r + 0.04, 0, h * 0.5), (0.08, 0.16, h * 0.72), K_MGRAY)             # feed line
    b.box((r + 0.07, 0, h * 0.15), (0.16, 0.22, 0.3), K_STEEL)
    vol = math.pi * (0.96 * r) ** 2 * (h - 0.3)
    fuel = round(vol * 900, -1)
    return b.finish(name, spec(category="Tank", size=size, diameterClass=size, fuelKg=fuel, dryMassKg=round(fuel * 0.06 + 30),
                               costM=round(0.3 + fuel / 1000 * 0.35, 2)))


# =============================== ENGINES ===============================
def bell_profile(R, h):
    return [(R, 0), (R * .9, .2 * h), (R * .7, .45 * h), (R * .5, .65 * h), (R * .36, .8 * h), (R * .45, .85 * h), (R * .45, h)]


def add_bell(b, R, h, off=(0, 0, 0)):
    b.lathe(bell_profile(R, h), K_MGRAY, seg=12, off=off, bottom_cell=K_BLACK)
    for a in (0, 120, 240):                                                   # copper feed pipes
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        b.cyl((off[0] + c * R * 0.55, off[1] + s * R * 0.55, off[2] + h * 0.62), max(0.03, R * 0.06), h * 0.4, K_COPPER, seg=6)


def make_engine(name, size, mount_r, R, h, **stats):
    b = P()
    add_bell(b, R, h)
    b.cyl((0, 0, h - 0.07), mount_r, 0.14, K_DGRAY, seg=14)
    b.cyl((0, 0, h * 0.86), R * 0.5, 0.1, K_DGRAY, seg=12)                    # gimbal ring
    b.box((R * 0.5, 0, h * 0.8), (R * 0.4, R * 0.3, R * 0.35), K_COPPER)      # turbopump
    return b.finish(name, spec(category="Engine", size=size, diameterClass=size, **stats))


def make_cluster(name, size, mount_r, R, h, n, **stats):
    b = P()
    pos = [(0, 0)] if n == 1 else [(math.cos(2 * math.pi * i / n) * mount_r * 0.55, math.sin(2 * math.pi * i / n) * mount_r * 0.55) for i in range(n)]
    for x, y in pos:
        add_bell(b, R, h * 0.9, (x, y, 0))
    b.cyl((0, 0, h - 0.1), mount_r, 0.2, K_DGRAY, seg=16)
    b.cyl((0, 0, h * 0.8), mount_r * 0.4, h * 0.25, K_COPPER, seg=8)
    return b.finish(name, spec(category="Engine", size=size, diameterClass=size, bells=n, **stats))


def make_rcs():
    b = P()
    b.box((0, 0, 0.15), (0.36, 0.36, 0.3), K_DGRAY)
    for rot, c in ((RY(90), (0.22, 0, 0.15)), (RY(-90), (-0.22, 0, 0.15)), (RX(90), (0, -0.22, 0.15)), (RX(-90), (0, 0.22, 0.15))):
        b.cyl(c, 0.03, 0.18, K_MGRAY, seg=8, r2=0.075, rot=rot)
    b.box((0, 0, 0.32), (0.2, 0.2, 0.05), K_COPPER)
    return b.finish("Thr_RCS_Block", spec(category="Thruster", size="S", diameterClass="S", thrustKn=4 * 0.4, ispVac=240, dryMassKg=22, costM=0.4,
                                           note="4 nozzles, attitude control, needs monoprop (tiny fuel use)"))


def make_ion():
    b = P()
    b.lathe([(0.3, 0), (0.3, 0.45), (0.22, 0.6)], K_LGRAY, seg=14, bottom_cell=K_GLASS)
    ring(b, 0.31, 0.0, 0.07, K_DGRAY)
    b.box((0.34, 0, 0.3), (0.12, 0.2, 0.3), K_COPPER)
    b.box((-0.34, 0, 0.3), (0.12, 0.2, 0.3), K_COPPER)
    b.cyl((0, 0, 0.7), 0.12, 0.2, K_DGRAY, seg=8)
    return b.finish("Thr_Ion", spec(category="Thruster", size="S", diameterClass="S", thrustKn=0.0005, ispVac=3000, dryMassKg=35, costM=4.2, powerW=1200,
                                    note="tiny thrust, huge efficiency: deep-space / station keeping; needs 1.2 kW"))


def make_solid_kick():
    b = P()
    b.lathe([(0.45, 0), (0.5, 0.15), (0.5, 1.2), (0.3, 1.6), (0.0, 1.7)], K_LGRAY, seg=14, bottom_cell=K_DGRAY)
    add_bell(b, 0.3, 0.7, (0, 0, -0.55))
    ring(b, 0.52, 1.0, 1.1, K_HAZ)
    return b.finish("Eng_S_SolidKick", spec(category="Engine", size="S", diameterClass="S", thrustKn=40, thrustVacKn=44, ispSl=240, ispVac=268, dryMassKg=140,
                                           fuelKg=420, costM=2.6, note="fixed solid fuel, cheap one-shot kick stage"))


# =============================== HEADS (fairings, nose cones, capsules) ===============================
def make_fairing(name, size, r, cyl_h, nose_h, **stats):
    b = P()
    prof = [(r * 0.97, 0), (r, 0.1), (r, cyl_h)] + nose(r, cyl_h, nose_h)[1:]
    b.lathe(prof, LIVERY, seg=16)
    ring(b, r * 1.02, 0, 0.25, K_DGRAY, 16)
    ring(b, r * 1.012, 0.55, 0.55 + 0.2 + 0.1 * r, STRIPE, 16)
    for sx in (-1, 1):
        b.box((sx * (r + 0.004), 0, 0.25 + cyl_h * 0.5), (0.02, 0.07, cyl_h * 0.85), K_DGRAY)       # separation seams
    b.cyl((0, 0, cyl_h + nose_h - 0.08), r * 0.1, 0.16, K_BLACK, seg=8)
    return b.finish(name, spec(category="Head", size=size, diameterClass=size, **stats))


def make_nosecone(name, size, r, h, cost):
    b = P()
    b.lathe([(r * 0.97, 0), (r, 0.1), (r, 0.35)] + nose(r, 0.35, h - 0.35)[1:], LIVERY, seg=16)
    ring(b, r * 1.02, 0, 0.2, K_DGRAY, 16)
    b.cyl((0, 0, h - 0.1), r * 0.12, 0.2, K_BLACK, seg=8)
    return b.finish(name, spec(category="Head", size=size, diameterClass=size, payloadSlots=0, payloadMaxKg=0, dryMassKg=round(60 * r ** 2 + 20),
                               costM=cost, note="solid aerodynamic cap, carries no payload"))


def make_capsule(name, size, r, crew):
    b = P()
    k = r
    b.lathe([(r * 0.97, 0), (r, 0.1), (r, 1.3 * k)], LIVERY, seg=16)                       # service module
    ring(b, r * 1.02, 0, 0.2 * k, K_DGRAY, 16)
    ring(b, r * 1.012, 0.5 * k, 0.5 * k + 0.25, STRIPE, 16)
    for a in (45, 135, 225, 315):                                                           # RCS quads on the service module
        x, y = math.cos(math.radians(a)) * r * 1.02, math.sin(math.radians(a)) * r * 1.02
        b.box((x, y, 1.0 * k), (0.18 * k, 0.18 * k, 0.2 * k), K_DGRAY)
    for sx in (-1, 1):                                                                      # radiator panels
        b.box((sx * (r + 0.03), 0, 0.7 * k), (0.05, 0.9 * k, 0.9 * k), K_LGRAY)
    b.lathe([(r, 1.3 * k), (r * 0.96, 1.4 * k), (r * 0.5, 2.8 * k), (r * 0.46, 2.85 * k)], K_WHITE, seg=16, top_cell=K_LGRAY)   # capsule
    for sx in (-1, 1):
        b.box((sx * r * 0.7, 0, 2.05 * k), (0.02, 0.35 * k, 0.28 * k), K_GLASS, rot=RY(sx * 22))   # windows
    b.cyl((0, 0, 2.95 * k), r * 0.4, 0.2 * k, K_DGRAY, seg=12)                              # docking port
    for a in (0, 90, 180, 270):                                                             # launch-escape tower
        x, y = math.cos(math.radians(a)) * r * 0.25, math.sin(math.radians(a)) * r * 0.25
        b.cyl((x, y, 3.65 * k), 0.025 * k + 0.01, 1.3 * k, K_DGRAY, seg=4)
    b.cyl((0, 0, 4.45 * k), r * 0.22, 0.7 * k, K_WHITE, seg=10)
    b.lathe([(r * 0.22, 4.8 * k), (r * 0.1, 5.1 * k), (0, 5.3 * k)], K_RED, seg=10)
    return b.finish(name, spec(category="Head", size=size, diameterClass=size, crew=crew, payloadSlots=0, payloadMaxKg=0,
                               dryMassKg=round(2400 * k ** 2), costM=round(22 * k ** 2, 1), powerW=0, heatShield=True,
                               note="human payload, includes escape tower and parachute-ready capsule"))


# =============================== PAYLOADS ===============================
def panel(b, c, w, h, cell=K_SOLAR, hinge=None):
    b.box(c, (w, 0.03, h), cell)
    for i in range(1, 4):
        b.box((c[0] - w / 2 + w * i / 4, c[1] - 0.018, c[2]), (0.02, 0.01, h), K_SOLARL)
    b.box((c[0], c[1] - 0.018, c[2]), (w, 0.01, 0.02), K_SOLARL)


def make_sat(name, size, bw, bh, wing, mass, power, mbps, cost):
    b = P()
    b.box((0, 0, bh / 2), (bw, bw, bh), K_GOLD)
    b.box((0, 0, bh + 0.03), (bw * 0.9, bw * 0.9, 0.06), K_DGRAY)
    for sx in (-1, 1):
        b.box((sx * (bw / 2 + 0.12), 0, bh / 2), (0.24, 0.06, 0.06), K_STEEL)
        for i in range(2):
            panel(b, (sx * (bw / 2 + 0.25 + wing * (0.25 + i * 0.5)), 0, bh / 2), wing * 0.5, bw * 0.9)
    b.dish((0, -bw * 0.1, bh + 0.35), bw * 0.32, 55, cell=K_LGRAY, rim=K_WHITE)
    b.cyl((bw * 0.3, bw * 0.3, bh + 0.3), 0.02, 0.5, K_STEEL, seg=6)
    return b.finish(name, spec(category="Payload", size=size, payloadType="Satellite", massKg=mass, powerW=power, commMbps=mbps, costM=cost,
                               note="deployable satellite"))


def make_rover():
    b = P()
    b.box((0, 0, 0.55), (1.0, 1.5, 0.35), K_LGRAY)
    b.box((0, 0, 0.77), (0.9, 1.3, 0.1), K_DGRAY)
    panel(b, (0, 0.0, 0.84), 0.9, 1.1)
    for sx in (-1, 1):
        for y in (-0.6, 0, 0.6):
            b.cyl((sx * 0.62, y, 0.26), 0.26, 0.2, K_BLACK, seg=10, rot=RY(90))
            b.cyl((sx * 0.62 + sx * 0.11, y, 0.26), 0.13, 0.04, K_STEEL, seg=8, rot=RY(90))
        b.box((sx * 0.5, 0, 0.45), (0.06, 1.3, 0.06), K_STEEL)                         # rocker bars
    b.cyl((0.3, -0.5, 1.1), 0.03, 0.55, K_STEEL, seg=6)
    b.box((0.3, -0.52, 1.42), (0.22, 0.12, 0.12), K_DGRAY)
    b.dish((-0.3, 0.45, 1.0), 0.18, 50, cell=K_LGRAY)
    return b.finish("Pay_Rover", spec(category="Payload", size="M", payloadType="Rover", massKg=900, powerW=-500, commMbps=0.1, costM=60,
                                      note="surface rover; needs 500 W of power from the carrier"))


def make_lander():
    b = P()
    b.lathe([(0.7, 0.55), (0.85, 0.7), (0.85, 1.15), (0.6, 1.3)], K_GOLD, seg=8, bottom_cell=K_DGRAY, top_cell=K_DGRAY)
    add_bell(b, 0.2, 0.45, (0, 0, 0.12))
    for a in (45, 135, 225, 315):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        b.cyl((c * 1.05, s * 1.05, 0.65), 0.04, 1.1, K_STEEL, seg=6, rot=Matrix.Rotation(math.radians(-24), 3, Vector((-s, c, 0))))
        b.cyl((c * 1.32, s * 1.32, 0.05), 0.2, 0.1, K_DGRAY, seg=8)
    for sx in (-1, 1):
        b.cyl((sx * 0.55, 0, 1.45), 0.2, 0.4, K_WHITE, seg=8)
    b.dish((0, 0.2, 1.5), 0.3, 60, cell=K_LGRAY)
    panel(b, (0.0, 0.0, 1.38), 0.7, 0.7)
    return b.finish("Pay_Lander", spec(category="Payload", size="L", payloadType="Lander", massKg=1400, powerW=-300, commMbps=2, costM=85,
                                       note="soft-landing lander with legs and descent engine"))


# =============================== BOOSTERS & RECOVERY ===============================
def make_booster(name, size, r, H, bells, thrust, cost):
    b = P()
    base = 0.9 * r + 0.3
    nose_h = r * 2.2
    b.lathe([(r * 0.88, base), (r, base + 0.15), (r, H - nose_h)] + nose(r, H - nose_h, nose_h)[1:], LIVERY, seg=16)
    ring(b, r * 0.96, base - 0.05, base + 0.55 * r + 0.3, K_DGRAY, 16)                      # engine skirt
    ring(b, r * 1.012, H - nose_h - 1.0 * r - 0.4, H - nose_h - 1.0 * r - 0.1, STRIPE, 16)
    ring(b, r * 1.015, (H - nose_h) * 0.45, (H - nose_h) * 0.45 + 0.28, K_DGRAY, 16)       # interstage
    pos = [(0, 0)] if bells == 1 else [(math.cos(2 * math.pi * i / (bells - 1 if bells > 4 else bells)) * r * 0.5,
                                         math.sin(2 * math.pi * i / (bells - 1 if bells > 4 else bells)) * r * 0.5) for i in range(bells - 1 if bells > 4 else bells)]
    if bells > 4:
        pos.append((0, 0))
    for x, y in pos:
        add_bell(b, r * (0.42 if bells == 1 else 0.28), base + 0.15, (x, y, 0))
    for a in (0, 90, 180, 270):                                                              # grid-fin sockets + leg hard points
        x, y = math.cos(math.radians(a)) * r, math.sin(math.radians(a))
        b.box((x * 1.0, y * r, H - nose_h - 0.5 * r - 0.3), (0.12, 0.12, 0.2), K_DGRAY)
        b.box((x, y * r, base + 0.45), (0.14, 0.14, 0.3), K_DGRAY)
    for z in (H * 0.3, H * 0.7):                                                             # radial attach points
        b.box((r * 1.0, 0, z), (0.2, 0.3, 0.3), K_STEEL)
    vol = math.pi * (0.95 * r) ** 2 * (H - nose_h - base - 0.4)
    fuel = round(vol * 900, -1)
    return b.finish(name, spec(category="Booster", size=size, diameterClass=size, bells=bells, fuelKg=fuel, thrustKn=thrust, thrustVacKn=round(thrust * 1.1),
                               ispSl=282, ispVac=311, dryMassKg=round(fuel * 0.07 + 120 * bells), costM=cost, reusable=True,
                               note="side booster; grid fins + landing legs are separate parts (landing comes later)"))


def make_leg():
    b = P()
    b.box((0, 0, 0), (0.2, 0.2, 0.2), K_DGRAY)                                               # hinge at the origin
    b.cyl((0, -0.6, -1.0), 0.07, 2.2, K_STEEL, seg=6, rot=RX(-30))
    b.cyl((0, -0.25, -0.55), 0.04, 1.2, K_STEEL, seg=6, rot=RX(-8))
    b.cyl((0, -1.2, -1.95), 0.3, 0.1, K_DGRAY, seg=8)
    return b.finish("Rec_LandingLeg", spec(category="Recovery", size="S", payloadSlots=0, dryMassKg=90, costM=0.9, note="hinge at origin, deploys down/outward (-Y)"))


def make_gridfin():
    b = P()
    b.box((0, 0, 0), (0.14, 0.14, 0.14), K_DGRAY)                                              # hinge at the origin
    w, h = 0.9, 0.75
    for y in (-w / 2, w / 2 - 0.07):
        pass
    b.box((0, -0.2, -h / 2), (0.1, 0.05, h), K_DGRAY)                                          # arm
    for off in (-w / 2, w / 2):
        b.box((off, -0.5, -h / 2), (0.05, 0.05, h), K_STEEL)
    for off in (0, h):
        b.box((0, -0.5, -off), (w, 0.05, 0.05), K_STEEL)
    for i in range(1, 4):
        b.box((-w / 2 + w * i / 4, -0.5, -h / 2), (0.025, 0.05, h), K_MGRAY)
    for i in range(1, 3):
        b.box((0, -0.5, -h * i / 3), (w, 0.05, 0.025), K_MGRAY)
    return b.finish("Rec_GridFin", spec(category="Recovery", size="S", dryMassKg=45, costM=0.5, note="hinge at origin, flat lattice fin steers the descent"))


def make_chute(name, r, mass, cost):
    b = P()
    b.lathe([(r, 0), (r, 0.5), (r * 0.7, 0.7)], K_ORANGE, seg=12, top_cell=K_DGRAY)
    ring(b, r * 1.03, 0.2, 0.28, K_BLACK, 12)
    return b.finish(name, spec(category="Recovery", size="S" if r < 0.4 else "M", dryMassKg=mass, costM=cost, chuteDragArea=round(r * 120, 1),
                               note="parachute canister, helps soft landing"))


# =============================== COMMUNICATION ===============================
def make_whip():
    b = P()
    b.cyl((0, 0, 0.05), 0.08, 0.1, K_DGRAY, seg=8)
    b.cyl((0, 0, 0.8), 0.015, 1.5, K_STEEL, seg=6)
    b.cyl((0, 0, 1.58), 0.04, 0.06, K_RED, seg=6)
    return b.finish("Com_Whip", spec(category="Comms", size="S", dataMbps=0.01, powerW=2, dryMassKg=2, costM=0.1, note="10 kbps, any craft, cheap"))


def make_patch():
    b = P()
    b.box((0, 0, 0.05), (0.5, 0.5, 0.1), K_DGRAY)
    b.cyl((0, -0.1, 0.2), 0.25, 0.3, K_LGRAY, seg=4, r2=0.1, rot=RX(90) @ RY(45))
    b.box((0, -0.27, 0.2), (0.3, 0.02, 0.3), K_GLASS)
    return b.finish("Com_Patch", spec(category="Comms", size="S", dataMbps=1, powerW=8, dryMassKg=6, costM=0.6, note="S-band patch, 1 Mbps"))


def make_dish(name, r, elev, dm, power, mass, cost, size, note):
    b = P()
    b.cyl((0, 0, 0.2), 0.12 + 0.05 * r, 0.4, K_DGRAY, seg=8)
    b.box((0, 0, 0.45), (0.3 + 0.1 * r, 0.3, 0.1), K_STEEL)
    b.dish((0, 0, 0.45 + r * 0.6), r, elev, cell=K_LGRAY, rim=K_WHITE)
    return b.finish(name, spec(category="Comms", size=size, dataMbps=dm, powerW=power, dryMassKg=mass, costM=cost, note=note))


def make_array():
    b = P()
    b.cyl((0, 0, 0.3), 0.08, 0.6, K_DGRAY, seg=6)
    b.box((0, 0, 0.9), (1.5, 0.12, 1.1), K_DGRAY)
    for i in range(6):
        for j in range(4):
            b.box((-0.6 + i * 0.24, -0.07, 0.5 + j * 0.27), (0.18, 0.03, 0.2), K_GLASS)
    b.box((0, 0.1, 0.9), (1.2, 0.08, 0.8), K_MGRAY)
    return b.finish("Com_PhasedArray", spec(category="Comms", size="L", dataMbps=300, powerW=180, dryMassKg=60, costM=12, note="steerable relay array, 300 Mbps"))


# =============================== POWER ===============================
def make_solar(name, panels, w, h, power, mass, cost, size):
    b = P()
    b.box((0, 0, 0.15), (0.3, 0.3, 0.3), K_DGRAY)
    for sx in (-1, 1):
        b.box((sx * 0.3, 0, 0.15), (0.3, 0.06, 0.06), K_STEEL)
        for i in range(panels):
            panel(b, (sx * (0.45 + w * (i + 0.5) + i * 0.04), 0, 0.15), w, h)
    return b.finish(name, spec(category="Power", size=size, powerW=power, dryMassKg=mass, costM=cost, note="solar wings, need sunlight"))


def make_battery():
    b = P()
    b.box((0, 0, 0.3), (0.6, 0.5, 0.6), K_STEEL)
    for sx in (-0.15, 0.15):
        b.cyl((sx, 0, 0.64), 0.06, 0.08, K_COPPER, seg=6)
    b.box((0, -0.26, 0.3), (0.4, 0.02, 0.2), K_HAZ)
    return b.finish("Pow_Battery", spec(category="Power", size="S", storageWh=2000, dryMassKg=24, costM=0.8, note="stores energy for eclipses and night"))


def make_rtg():
    b = P()
    b.lathe([(0.22, 0), (0.25, 0.1), (0.25, 0.9), (0.2, 1.0)], K_DGRAY, seg=10, bottom_cell=K_BLACK)
    for i in range(8):
        a = math.radians(i * 45)
        b.box((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.5), (0.12, 0.03, 0.7), K_LGRAY, rot=Matrix.Rotation(a, 3, "Z"))
    b.box((0, 0, 1.05), (0.2, 0.2, 0.1), K_HAZ)
    return b.finish("Pow_RTG", spec(category="Power", size="S", powerW=250, dryMassKg=45, costM=9, note="nuclear battery, works far from the Sun"))


# =============================== STRUCTURE ===============================
def make_decoupler(name, size, r):
    b = P()
    b.lathe([(r * 0.99, 0), (r * 1.03, 0.04), (r * 1.03, 0.26), (r * 0.99, 0.3)], K_DGRAY, seg=16)
    ring(b, r * 1.04, 0.1, 0.2, K_HAZ, 16)
    for i in range(8):
        a = math.radians(i * 45)
        b.box((math.cos(a) * r * 1.04, math.sin(a) * r * 1.04, 0.15), (0.1, 0.1, 0.12), K_STEEL)
    return b.finish(name, spec(category="Structure", size=size, diameterClass=size, dryMassKg=round(40 * r ** 2 * 3), costM=round(0.25 * r + 0.2, 2),
                               note="separates a stage with explosive bolts"))


def make_adapter(name, r1, r2, h, s1, s2):
    b = P()
    b.lathe([(r1 * 0.97, 0), (r1, 0.1), (r1, 0.25), (r2, h - 0.25), (r2, h - 0.1), (r2 * 0.97, h)], LIVERY, seg=16)
    ring(b, r1 * 1.02, 0, 0.18, K_DGRAY, 16)
    ring(b, r2 * 1.02, h - 0.18, h, K_DGRAY, 16)
    return b.finish(name, spec(category="Structure", size=s1 + s2, diameterClass=s1 + "-" + s2, dryMassKg=round(120 * r2), costM=round(0.4 * r2, 2),
                               note="joins stack sizes %s -> %s (bottom -> top)" % (s1, s2)))


def make_avionics(name, size, r):
    b = P()
    b.lathe([(r * 0.97, 0), (r, 0.08), (r, 0.5), (r * 0.97, 0.58)], K_LGRAY, seg=16)
    ring(b, r * 1.01, 0.22, 0.34, STRIPE, 16)
    for a in (30, 150, 270):
        x, y = math.cos(math.radians(a)) * r * 1.0, math.sin(math.radians(a)) * r * 1.0
        b.box((x, y, 0.3), (0.12, 0.12, 0.18), K_GLASS)
    b.cyl((0, 0, 0.7), 0.03, 0.25, K_STEEL, seg=6)
    return b.finish(name, spec(category="Structure", size=size, diameterClass=size, controlLevel={"S": 1, "M": 2, "L": 3}[size], reactionWheelNm={"S": 5, "M": 25, "L": 120}[size],
                               powerW={"S": 20, "M": 45, "L": 90}[size], dryMassKg={"S": 35, "M": 90, "L": 220}[size], costM={"S": 0.8, "M": 2.2, "L": 5.5}[size],
                               note="guidance computer + reaction wheels, required to steer"))


def make_fin(name, size, s):
    b = P()
    b.slab([(0, 0), (1.0 * s, 0.4 * s), (1.0 * s, 1.6 * s), (0, 2.6 * s)], 0.08, K_DGRAY)
    b.slab([(0.02, 0.05), (0.9 * s, 0.42 * s), (0.9 * s, 1.5 * s), (0.02, 2.4 * s)], 0.1, STRIPE)
    return b.finish(name, spec(category="Structure", size=size, dryMassKg=int(30 * s), costM=round(0.15 * s, 2), note="atmospheric stability fin, mount radially at the tail"))


# =============================== BUILD ===============================
def reg_all():
    # tanks: 6 sizes
    for n, sz, r, h in [("Tank_S_Short", "S", RS, 2), ("Tank_S_Long", "S", RS, 4), ("Tank_M_Short", "M", RM, 3), ("Tank_M_Long", "M", RM, 6),
                        ("Tank_L_Short", "L", RL, 4), ("Tank_L_Long", "L", RL, 8)]:
        make_tank(n, sz, r, h)
    # engines
    make_engine("Eng_S_Spark", "S", 0.55, 0.45, 1.3, thrustKn=90, thrustVacKn=100, ispSl=270, ispVac=298, dryMassKg=180, costM=3.0, gimbalDeg=5,
                note="small sea-level lifter")
    make_engine("Eng_S_Vac", "S", 0.5, 0.42, 1.9, thrustKn=0, thrustVacKn=62, ispSl=0, ispVac=342, dryMassKg=150, costM=3.6, gimbalDeg=4,
                note="small vacuum upper-stage engine (long bell)")
    make_engine("Eng_M_Lifter", "M", 0.95, 0.8, 1.9, thrustKn=450, thrustVacKn=500, ispSl=285, ispVac=312, dryMassKg=620, costM=12.0, gimbalDeg=6,
                note="medium sea-level first-stage engine")
    make_engine("Eng_M_Vac", "M", 0.9, 0.78, 2.7, thrustKn=0, thrustVacKn=300, ispSl=0, ispVac=346, dryMassKg=540, costM=14.0, gimbalDeg=5,
                note="medium vacuum engine for upper stages")
    make_cluster("Eng_M_Cluster3", "M", 1.0, 0.45, 1.6, 3, thrustKn=720, thrustVacKn=800, ispSl=276, ispVac=306, dryMassKg=1250, costM=20.0, gimbalDeg=4,
                 note="3 bells: more thrust, engine-out safe")
    make_cluster("Eng_L_Cluster4", "L", 1.5, 0.62, 2.1, 4, thrustKn=1900, thrustVacKn=2100, ispSl=281, ispVac=311, dryMassKg=2700, costM=38.0, gimbalDeg=4,
                 note="4 big bells for heavy lift")
    make_engine("Eng_L_Vac", "L", 1.4, 1.25, 3.3, thrustKn=0, thrustVacKn=950, ispSl=0, ispVac=352, dryMassKg=1450, costM=30.0, gimbalDeg=3,
                note="large vacuum engine, best efficiency")
    make_solid_kick()
    make_rcs()
    make_ion()
    # heads
    make_fairing("Head_Fairing_S", "S", RS, 1.0, 1.7, payloadSlots=1, payloadMaxKg=600, dryMassKg=90, costM=0.9, note="small payload bay: small satellites")
    make_fairing("Head_Fairing_M", "M", RM, 1.6, 2.4, payloadSlots=1, payloadMaxKg=1800, dryMassKg=260, costM=2.4, note="medium bay: satellites and rovers")
    make_fairing("Head_Fairing_L", "L", RL, 2.4, 3.4, payloadSlots=1, payloadMaxKg=4000, dryMassKg=620, costM=5.2, note="large bay: landers and heavy payloads")
    make_nosecone("Head_Nose_S", "S", RS, 1.8, 0.2)
    make_nosecone("Head_Nose_M", "M", RM, 2.8, 0.5)
    make_nosecone("Head_Nose_L", "L", RL, 4.0, 1.1)
    make_capsule("Head_Capsule_M", "M", RM, 2)
    make_capsule("Head_Capsule_L", "L", RL, 4)
    # payloads
    make_sat("Pay_Satellite_S", "S", 0.6, 0.8, 1.4, 180, 120, 1, 14)
    make_sat("Pay_Satellite_M", "M", 1.0, 1.4, 2.2, 650, 900, 10, 40)
    make_rover()
    make_lander()
    # boosters + recovery
    make_booster("Booster_S", "S", 0.45, 9, 1, 160, 5.5)
    make_booster("Booster_M", "M", 0.7, 13, 3, 650, 16)
    make_booster("Booster_L", "L", 0.95, 18, 5, 1500, 34)
    make_leg()
    make_gridfin()
    make_chute("Rec_Parachute_S", 0.3, 18, 0.5)
    make_chute("Rec_Parachute_M", 0.45, 45, 1.2)
    # comms
    make_whip()
    make_patch()
    make_dish("Com_Dish_S", 0.4, 40, 10, 25, 14, 2.0, "S", "X-band dish, 10 Mbps")
    make_dish("Com_Dish_L", 0.9, 45, 100, 80, 38, 6.5, "M", "Ka-band dish, 100 Mbps")
    make_array()
    # power
    make_solar("Pow_Solar_S", 2, 0.9, 0.7, 300, 12, 1.2, "S")
    make_solar("Pow_Solar_L", 3, 1.3, 0.9, 1200, 42, 4.5, "L")
    make_battery()
    make_rtg()
    # structure
    for sz, r in (("S", RS), ("M", RM), ("L", RL)):
        make_decoupler("Dec_" + sz, sz, r)
        make_avionics("Avi_" + sz, sz, r)
    make_adapter("Adp_S_M", RS, RM, 1.0, "S", "M")
    make_adapter("Adp_M_L", RM, RL, 1.2, "M", "L")
    make_adapter("Adp_M_S", RM, RS, 1.0, "M", "S")      # tapering adapters: big stage below, smaller stage above
    make_adapter("Adp_L_M", RL, RM, 1.2, "L", "M")
    make_fin("Fin_S", "S", 0.6)
    make_fin("Fin_L", "L", 1.1)


def export(ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_P, ob.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False)


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)
write_parts_palette(os.path.join(OUT_P, "parts-palette.png"))
reg_all()
objs = [bpy.data.objects[n] for n in REG]
for ob in objs:
    export(ob)
# lay out by category for the preview
rows = {}
for ob in objs:
    cat = REG[ob.name]["category"]
    rows.setdefault(cat, []).append(ob)
y = 0
for cat, lst in rows.items():
    x = 0
    for ob in lst:
        d = REG[ob.name]
        ob.location = (x + d["widthM"] / 2, y, 0)
        x += d["widthM"] + 1.5
    y += max(REG[o.name]["heightM"] for o in lst) + 3
catalog = sorted(REG.values(), key=lambda m: (m["category"], m["id"]))
json.dump({"scale": "metres, kg, kN, M$ (millions)", "parts": catalog}, open(os.path.join(OUT_D, "parts-catalog.json"), "w", encoding="utf-8"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "rocket-parts.blend"))
result = {"parts": len(REG), "categories": {c: len(v) for c, v in rows.items()},
          "tallest": max(((m["heightM"], m["id"]) for m in REG.values())), "tris_total": sum(m["tris"] for m in REG.values())}
