# Draws the extra UI icons (white glyph on transparent, 128 px, same style as Assets/Assets/Graphics/modern-icons):
# the Layer button, the items of the bottom-bar pop-up menus and the layer switches.   python3 ArtSource/make_ui_icons.py
import math, os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "UIIcons")
os.makedirs(OUT, exist_ok=True)
S = 512


def canvas():
    im = Image.new("L", (S, S), 0)
    return im, ImageDraw.Draw(im)


def save(im, name):
    im = im.resize((128, 128), Image.LANCZOS)
    rgba = Image.new("RGBA", (128, 128), (255, 255, 255, 0))
    rgba.putalpha(im)
    rgba.save(os.path.join(OUT, name + ".png"))


def rot(points, deg, c=(256, 256)):
    a = math.radians(deg); ca, sa = math.cos(a), math.sin(a)
    return [(c[0] + (x - c[0]) * ca - (y - c[1]) * sa, c[1] + (x - c[0]) * sa + (y - c[1]) * ca) for x, y in points]


def thick_line(d, pts, w):
    d.line(pts, fill=255, width=w, joint="curve")
    for p in (pts[0], pts[-1]):
        d.ellipse((p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2), fill=255)


# layers: a diamond on two chevrons
im, d = canvas()
d.polygon([(256, 40), (480, 160), (256, 280), (32, 160)], fill=255)
thick_line(d, [(50, 262), (256, 372), (462, 262)], 44)
thick_line(d, [(50, 352), (256, 462), (462, 352)], 44)
save(im, "nav_layers")

# atom (scientist training)
im, d = canvas()
for a in (0, 60, 120):
    ring = Image.new("L", (S, S), 0); rd = ImageDraw.Draw(ring)
    rd.ellipse((36, 150, 476, 362), outline=255, width=26)
    im.paste(255, (0, 0), ring.rotate(a, resample=Image.BICUBIC))
d.ellipse((206, 206, 306, 306), fill=255)
save(im, "icon_scientist")

# aeroplane (pilot training)
im, d = canvas()
d.polygon(rot([(256, 30), (292, 70), (292, 196), (486, 318), (486, 372), (292, 312), (292, 408), (352, 452), (352, 488), (256, 462), (160, 488), (160, 452), (220, 408), (220, 312), (26, 372), (26, 318), (220, 196), (220, 70)], 0), fill=255)
save(im, "icon_pilot")

# factory (facility)
im, d = canvas()
d.polygon([(40, 460), (40, 200), (170, 290), (170, 200), (300, 290), (300, 120), (360, 120), (360, 290), (472, 290), (472, 460)], fill=255)
d.rectangle((300, 50, 360, 130), fill=255)
for x in (96, 196, 296, 396):
    d.rectangle((x, 350, x + 52, 410), fill=0)
save(im, "icon_facility")

# satellite (payload)
im, d = canvas()
body = rot([(196, 196), (316, 196), (316, 316), (196, 316)], 45)
d.polygon(body, fill=255)
for sgn in (-1, 1):
    pan = [(256 + sgn * 98, 150), (256 + sgn * 248, 150), (256 + sgn * 248, 220), (256 + sgn * 98, 220)]
    panel = rot([(x, y + 70) for x, y in pan], 45)
    d.polygon(panel, fill=255)
d.ellipse((226, 226, 286, 286), fill=0)
thick_line(d, [(256, 256), (404, 108)], 18)
d.ellipse((384, 88, 424, 128), fill=255)
save(im, "icon_payload")

# flame (engine test)
im, d = canvas()
pts = []
for t in range(0, 101):
    u = t / 100
    # teardrop: x = r(u) * sin, y from tip (top) to round bottom
    y = 40 + 430 * u
    w = 190 * math.sin(math.pi * min(1, u * 1.15)) ** 0.9 if u < 0.87 else 190 * math.cos((u - 0.87) / 0.13 * math.pi / 2) ** 0.6
    pts.append((256 + w * (0.25 + 0.75 * u), y))
left = [(512 - x, y) for x, y in reversed(pts)]
d.polygon(pts + left, fill=255)
d.polygon([(256, 270), (330, 380), (330, 410), (256, 466), (182, 410), (182, 380)], fill=0)
d.polygon([(256, 330), (296, 392), (256, 436), (216, 392)], fill=255)
save(im, "icon_engine")

# truss (structure test)
im, d = canvas()
d.rectangle((40, 120, 472, 160), fill=255)
d.rectangle((40, 352, 472, 392), fill=255)
for x0, x1 in ((40, 148), (148, 256), (256, 364), (364, 472)):
    thick_line(d, [(x0 + 10, 380), (x1 - 10, 140)] if (x0 // 108) % 2 == 0 else [(x0 + 10, 140), (x1 - 10, 380)], 30)
d.rectangle((40, 120, 66, 392), fill=255); d.rectangle((446, 120, 472, 392), fill=255)
save(im, "icon_structure")

# antenna waves (comms test)
im, d = canvas()
c = (256, 400)
for r, w in ((90, 36), (170, 36), (250, 36)):
    d.arc((c[0] - r, c[1] - r, c[0] + r, c[1] + r), 228, 312, fill=255, width=w)
d.ellipse((c[0] - 36, c[1] - 36, c[0] + 36, c[1] + 36), fill=255)
save(im, "icon_comms")

# tag (labels)
im, d = canvas()
d.polygon([(60, 60), (300, 60), (470, 230), (470, 262), (300, 432), (268, 432), (60, 224)], fill=255)
d.ellipse((120, 120, 200, 200), fill=0)
save(im, "icon_labels")

# car (vehicles)
im, d = canvas()
d.rounded_rectangle((34, 250, 478, 360), radius=40, fill=255)
d.polygon([(110, 250), (170, 140), (350, 140), (420, 250)], fill=255)
d.polygon([(184, 232), (206, 166), (250, 166), (250, 232)], fill=0)
d.polygon([(272, 232), (272, 166), (338, 166), (380, 232)], fill=0)
for x in (140, 372):
    d.ellipse((x - 62, 296, x + 62, 420), fill=0)
    d.ellipse((x - 46, 312, x + 46, 404), fill=255)
save(im, "icon_vehicles")

# tree (greenery)
im, d = canvas()
d.polygon([(256, 40), (380, 190), (322, 190), (430, 320), (330, 320), (460, 440), (52, 440), (182, 320), (82, 320), (190, 190), (132, 190)], fill=255)
d.rectangle((226, 430, 286, 480), fill=255)
save(im, "icon_greenery")

# cloud (haze)
im, d = canvas()
d.ellipse((60, 230, 220, 390), fill=255)
d.ellipse((130, 130, 330, 330), fill=255)
d.ellipse((250, 170, 450, 370), fill=255)
d.rounded_rectangle((100, 280, 440, 400), radius=60, fill=255)
save(im, "icon_haze")
# star (what a building unlocks)
im, d = canvas()
pts = []
for i in range(10):
    ang = math.radians(-90 + i * 36)
    r = 230 if i % 2 == 0 else 95
    pts.append((256 + r * math.cos(ang), 270 + r * math.sin(ang)))
d.polygon(pts, fill=255)
save(im, "icon_star")

# lightning bolt (power)
im, d = canvas()
d.polygon([(300, 30), (110, 290), (240, 290), (200, 482), (402, 210), (270, 210)], fill=255)
save(im, "icon_power")
# flag (objectives)
im, d = canvas()
d.rectangle((86, 50, 126, 470), fill=255)
d.polygon([(126, 70), (440, 70), (380, 160), (440, 250), (126, 250)], fill=255)
save(im, "icon_flag")

# check mark
im, d = canvas()
thick_line(d, [(90, 270), (210, 390), (430, 140)], 70)
save(im, "icon_check")

# rocket (launch)
im, d = canvas()
d.polygon([(256, 20), (330, 120), (344, 300), (168, 300), (182, 120)], fill=255)
d.ellipse((218, 130, 294, 206), fill=0)
d.polygon([(168, 250), (90, 360), (90, 410), (178, 350)], fill=255)
d.polygon([(344, 250), (422, 360), (422, 410), (334, 350)], fill=255)
d.polygon([(206, 316), (306, 316), (256, 470)], fill=255)
save(im, "icon_launch")
# interface / screen (UI layer)
im, d = canvas()
d.rounded_rectangle((40, 70, 472, 360), radius=48, fill=255)
d.rounded_rectangle((80, 110, 432, 320), radius=24, fill=0)
d.rectangle((70, 140, 442, 160), fill=255)
d.rounded_rectangle((200, 380, 312, 410), radius=14, fill=255)
d.rounded_rectangle((150, 420, 362, 452), radius=16, fill=255)
save(im, "icon_ui")
# upgrade arrow
im, d = canvas()
d.polygon([(256, 40), (440, 230), (330, 230), (330, 340), (182, 340), (182, 230), (72, 230)], fill=255)
d.rounded_rectangle((120, 380, 392, 440), radius=24, fill=255)
save(im, "icon_upgrade")
# cinematic camera: a movie camera
im, d = canvas()
d.rounded_rectangle((30, 150, 350, 390), radius=48, fill=255)
d.polygon([(360, 230), (480, 160), (480, 380), (360, 310)], fill=255)
d.ellipse((90, 40, 210, 160), fill=255)
d.ellipse((220, 60, 330, 170), fill=255)
d.ellipse((130, 200, 250, 320), fill=0)
d.ellipse((165, 235, 215, 285), fill=255)
save(im, "icon_camera")
# one day per second: a circle, sun on the left half, moon on the right half
im, d = canvas()
d.pieslice((70, 70, 442, 442), 90, 270, fill=255)                     # left half: day
d.arc((70, 70, 442, 442), 270, 90, fill=255, width=34)                # right half: ring
d.ellipse((290, 150, 400, 260), fill=255)                             # moon disc ...
d.ellipse((330, 130, 430, 230), fill=0)                               # ... bitten into a crescent
d.ellipse((330, 300, 360, 330), fill=255); d.ellipse((380, 340, 400, 360), fill=255)   # stars
save(im, "icon_daynight")
# settings: a gear
im, d = canvas()
d.ellipse((120, 120, 392, 392), fill=255)
for k in range(8):
    a = math.radians(k * 45)
    cx, cy = 256 + math.cos(a) * 190, 256 + math.sin(a) * 190
    pts = rot([(cx - 42, cy - 60), (cx + 42, cy - 60), (cx + 42, cy + 60), (cx - 42, cy + 60)], k * 45 + 90, (cx, cy))
    d.polygon(pts, fill=255)
d.ellipse((196, 196, 316, 316), fill=0)
save(im, "icon_settings")
# ---- designer toolbar: rotate, move, reset view, undo, redo, delete, save
im, d = canvas()                                                   # rotate: a circular arrow
d.arc((70, 70, 442, 442), 30, 330, fill=255, width=46)
ang = math.radians(330); px, py = 256 + 186 * math.cos(ang), 256 + 186 * math.sin(ang)
tx, ty = -math.sin(ang), math.cos(ang); nx, ny = ty, -tx
d.polygon([(px + tx * 70, py + ty * 70), (px + nx * 62, py + ny * 62), (px - nx * 62, py - ny * 62)], fill=255)
save(im, "icon_rotate")

im, d = canvas()                                                   # move: a four-way arrow
thick_line(d, [(256, 90), (256, 422)], 44); thick_line(d, [(90, 256), (422, 256)], 44)
for (dx, dy) in ((0, -1), (0, 1), (-1, 0), (1, 0)):
    cx, cy = 256 + dx * 190, 256 + dy * 190
    pts = [(cx + dx * 60, cy + dy * 60), (cx - dx * 30 + dy * 80, cy - dy * 30 + dx * 80), (cx - dx * 30 - dy * 80, cy - dy * 30 - dx * 80)]
    d.polygon(pts, fill=255)
save(im, "icon_move")

im, d = canvas()                                                   # reset view: corner brackets round a dot
for sx in (-1, 1):
    for sy in (-1, 1):
        cx, cy = 256 + sx * 170, 256 + sy * 170
        thick_line(d, [(cx, cy - sy * 110), (cx, cy), (cx - sx * 110, cy)], 40)
d.ellipse((206, 206, 306, 306), fill=255)
save(im, "icon_reset")

im, d = canvas()                                                   # undo: an arrow that turns back
thick_line(d, [(190, 190), (300, 190)], 44)
d.arc((190, 190, 410, 410), -90, 90, fill=255, width=44)
thick_line(d, [(300, 410), (170, 410)], 44)
d.polygon([(50, 190), (200, 80), (200, 300)], fill=255)
save(im, "icon_undo")
from PIL import ImageOps
save(ImageOps.mirror(im), "icon_redo")

im, d = canvas()                                                   # delete: a bin
d.rounded_rectangle((90, 110, 422, 160), radius=18, fill=255)
d.rounded_rectangle((190, 60, 322, 130), radius=18, fill=255)
d.polygon([(120, 190), (392, 190), (366, 450), (146, 450)], fill=255)
for x in (200, 256, 312):
    d.rounded_rectangle((x - 14, 225, x + 14, 415), radius=12, fill=0)
save(im, "icon_trash")

im, d = canvas()                                                   # save: a floppy disk
d.rounded_rectangle((60, 60, 452, 452), radius=44, fill=255)
d.rectangle((140, 60, 372, 180), fill=0); d.rectangle((300, 80, 346, 160), fill=255)
d.rounded_rectangle((120, 250, 392, 420), radius=18, fill=0)
d.rectangle((150, 290, 362, 305), fill=255); d.rectangle((150, 330, 362, 345), fill=255); d.rectangle((150, 370, 300, 385), fill=255)
save(im, "icon_save")
print("icons written to", OUT)
