"""Paints the road textures used by the procedural road meshes (Assets/Assets/Graphics/Site).
Highway: asphalt with darker wheel tracks, gravel shoulders, a grass median and only a few thin white lines.
Service road: plain asphalt with lighter kerb edges. u = along the road (tile length below), v = across the road."""
import numpy as np
from PIL import Image
import os
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "Assets", "Graphics", "Site")
rng = np.random.default_rng(11)

def srgb(c): return np.array(c, dtype=np.float32) / 255.0

def noise(h, w, scale, amp):
    n = rng.random((h // scale + 2, w // scale + 2)).astype(np.float32)
    im = Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return (np.asarray(im, dtype=np.float32) / 255.0 - 0.5) * amp

def blend(a, b, t):
    t = np.clip(t, 0, 1)[..., None]
    return a * (1 - t) + b * t

def smooth(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0, 1)
    return t * t * (3 - 2 * t)

def line(cx, width, soft, X):
    return 1.0 - smooth(width / 2 - soft, width / 2 + soft, np.abs(X - cx))

def highway(path, W=1024, H=1024, width_m=22.8, tile_m=32.0):
    # x across (metres from the left edge), y along
    xs = (np.arange(W) + 0.5) / W * width_m - width_m / 2        # -11.4 .. 11.4
    ys = (np.arange(H) + 0.5) / H * tile_m
    X, Y = np.meshgrid(xs, ys)
    asphalt = srgb((46, 48, 53)); gravel = srgb((128, 118, 100)); grass = srgb((88, 128, 70)); curb = srgb((178, 176, 168)); white = srgb((236, 236, 230))
    img = np.zeros((H, W, 3), np.float32) + asphalt
    img += (noise(H, W, 2, 0.05) + noise(H, W, 9, 0.04))[..., None]
    ax = np.abs(X)
    # wheel tracks in each lane (centres 3.5 and 7.3 m from the middle): slightly darker, smoother
    for c in (3.5, 7.3):
        img *= (1.0 - 0.09 * line(c - 0.9, 0.9, 0.4, ax) - 0.09 * line(c + 0.9, 0.9, 0.4, ax))[..., None]
    # median (grass strip between two kerbs) and shoulders (gravel)
    med = 1.0 - smooth(1.45, 1.6, ax)
    kerb = (1.0 - smooth(1.6, 1.75, ax)) * smooth(1.3, 1.45, ax)
    shoulder = smooth(9.2, 9.6, ax)
    grass_img = grass + (noise(H, W, 3, 0.1) + noise(H, W, 14, 0.08))[..., None]
    gravel_img = gravel + (noise(H, W, 2, 0.12) + noise(H, W, 7, 0.06))[..., None]
    img = blend(img, gravel_img, shoulder)
    img = blend(img, grass_img, med)
    img = blend(img, curb, kerb)
    # outer verge fades into the ground
    img = blend(img, gravel_img * 0.92, smooth(10.9, 11.3, ax))
    # markings: thin continuous edge lines and one dashed lane divider
    edge = line(9.0, 0.16, 0.05, ax)
    inner = line(1.95, 0.12, 0.04, ax)
    dash = (np.mod(Y, tile_m / 2) < 3.0).astype(np.float32)
    dash = np.clip(dash + 0.0, 0, 1)
    lane = line(5.4, 0.13, 0.05, ax) * dash
    m = np.clip(edge * 0.9 + inner * 0.75 + lane * 0.9, 0, 1)
    img = blend(img, white, m)
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(path)

def service(path, W=512, H=1024, width_m=11.4, tile_m=32.0, markings=True):
    """Concrete road of the facility site: light grey slabs with thin joints every 8 m, white edge lines and a dashed centre line.
    Lighter and flatter than the dark highway asphalt. u = across (metres from the left edge), v = along."""
    xs = (np.arange(W) + 0.5) / W * width_m - width_m / 2
    ys = (np.arange(H) + 0.5) / H * tile_m
    X, Y = np.meshgrid(xs, ys)
    concrete = srgb((168, 168, 163)); joint = srgb((112, 112, 110)); white = srgb((240, 240, 235)); stain = srgb((120, 118, 112))
    img = np.zeros((H, W, 3), np.float32) + concrete
    img += (noise(H, W, 3, 0.025) + noise(H, W, 17, 0.035) + noise(H, W, 64, 0.03))[..., None]
    # every slab is a very slightly different shade
    slab = np.floor(Y / 8.0)
    tint = (np.sin(slab * 12.9898 + 4.1) * 43758.5453) % 1.0
    img += ((tint - 0.5) * 0.045)[..., None]
    # slightly darker tyre paths where cars drive
    for c in (-2.6, 2.6):
        img = blend(img, stain, 0.16 * line(c, 1.5, 0.7, X))
    # joints across the road every 8 m, and one along the middle
    cross = line(0.0, 0.07, 0.03, np.mod(Y + 4.0, 8.0) - 4.0)
    img = blend(img, joint, cross * 0.7)
    img = blend(img, joint, line(0.0, 0.06, 0.03, X) * 0.35)
    # markings: white edge lines and a dashed centre line (3.2 m dash, 4.8 m gap)
    edge = line(-5.05, 0.16, 0.05, X) + line(5.05, 0.16, 0.05, X)
    dash = (np.mod(Y, 8.0) < 3.2).astype(np.float32)
    centre = line(0.0, 0.15, 0.05, X) * dash
    if markings: img = blend(img, white, np.clip(edge * 0.9 + centre * 0.95, 0, 1))
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(path)

def zebra(path, W=512, H=128, width_m=11.4, len_m=3.2):
    """Pedestrian crossing: white bars running along the road, 0.5 m wide with 0.5 m gaps, on the same concrete. u = across."""
    xs = (np.arange(W) + 0.5) / W * width_m
    ys = (np.arange(H) + 0.5) / H * len_m
    X, Y = np.meshgrid(xs, ys)
    img = np.zeros((H, W, 3), np.float32) + srgb((168, 168, 163))
    img += (noise(H, W, 3, 0.025) + noise(H, W, 17, 0.035))[..., None]
    n = int(width_m / 1.0)
    off = (width_m - (2 * n - 1) * 0.5) / 2 if False else (width_m - (n * 1.0 - 0.5)) / 2
    bar = np.zeros((H, W), np.float32)
    for k in range(n):
        x0 = off + k * 1.0
        bar = np.maximum(bar, smooth(x0 - 0.03, x0 + 0.03, X) * (1 - smooth(x0 + 0.47, x0 + 0.53, X)))
    img = blend(img, srgb((242, 242, 238)), bar * 0.95)
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(path)

def gravel(path, W=512, H=1024, width_m=9.0, tile_m=32.0):
    """Natural gravel track: beige stones, two lightly compacted wheel ruts, ragged edges that fade into the ground. No markings."""
    xs = (np.arange(W) + 0.5) / W * width_m - width_m / 2
    ys = (np.arange(H) + 0.5) / H * tile_m
    X, Y = np.meshgrid(xs, ys)
    base = srgb((158, 138, 108)); dark = srgb((112, 96, 74)); light = srgb((196, 178, 148)); edge_col = srgb((104, 106, 76))
    img = np.zeros((H, W, 3), np.float32) + base
    img += (noise(H, W, 2, 0.10) + noise(H, W, 6, 0.07) + noise(H, W, 30, 0.05))[..., None]
    for c in (-1.5, 1.5):                                        # compacted ruts
        img = blend(img, dark, 0.28 * line(c, 1.0, 0.5, X))
    # scattered pebbles
    peb = (rng.random((H, W)) > 0.985).astype(np.float32)
    peb = np.asarray(Image.fromarray((peb * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR), dtype=np.float32) / 255.0
    img = blend(img, light, np.clip(peb * 1.2, 0, 1) * 0.8)
    # ragged edges
    jag = noise(H, W, 4, 0.9) + noise(H, W, 20, 0.7)
    ax = np.abs(X) + jag * 0.5
    img = blend(img, edge_col, smooth(3.7, 4.2, ax))
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(path)

def paving(path, W=256, H=512, width_m=2.2, tile_m=4.0):
    """Footpath: light concrete pavers (two per width, 1 m long) with thin joints."""
    xs = (np.arange(W) + 0.5) / W * width_m
    ys = (np.arange(H) + 0.5) / H * tile_m
    X, Y = np.meshgrid(xs, ys)
    base = srgb((206, 202, 192)); joint = srgb((140, 138, 130)); lip = srgb((170, 168, 160))
    img = np.zeros((H, W, 3), np.float32) + base
    img += (noise(H, W, 2, 0.03) + noise(H, W, 10, 0.03))[..., None]
    pid = np.floor(X / (width_m / 2)) * 7 + np.floor(Y / 1.0)
    tint = (np.sin(pid * 12.9898 + 1.7) * 43758.5453) % 1.0
    img += ((tint - 0.5) * 0.06)[..., None]
    jx = line(width_m / 2, 0.06, 0.02, X)
    jy = line(0.0, 0.06, 0.02, np.mod(Y + 0.5, 1.0) - 0.5)
    img = blend(img, joint, np.clip(jx + jy, 0, 1) * 0.8)
    img = blend(img, lip, smooth(width_m / 2 - 0.12, width_m / 2, np.abs(X - width_m / 2)))
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(path)

highway(os.path.join(OUT, "road-highway.png"))
service(os.path.join(OUT, "road-service.png"))
service(os.path.join(OUT, "road-plain.png"), markings=False)
zebra(os.path.join(OUT, "road-zebra.png"))
gravel(os.path.join(OUT, "road-gravel.png"))
paving(os.path.join(OUT, "path-pave.png"))
print("textures written to", OUT)
