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

def service(path, W=512, H=1024, width_m=11.4, tile_m=32.0):
    """Plain asphalt. No kerb or edge strips, so roads that cross or join blend together with no visible seams."""
    asphalt = srgb((52, 54, 59))
    img = np.zeros((H, W, 3), np.float32) + asphalt
    img += (noise(H, W, 2, 0.05) + noise(H, W, 9, 0.04) + noise(H, W, 40, 0.03))[..., None]
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(path)

highway(os.path.join(OUT, "road-highway.png"))
service(os.path.join(OUT, "road-service.png"))
print("textures written to", OUT)
