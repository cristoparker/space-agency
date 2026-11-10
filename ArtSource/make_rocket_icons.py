# Icons for the simplified rocket designer: the five part categories and the stat cells (white glyph on transparent, 128 px).
#   python3 ArtSource/make_rocket_icons.py   ->  Assets/Resources/UIIcons/
import math, os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "UIIcons")
S = 512


def canvas():
    im = Image.new("L", (S, S), 0)
    return im, ImageDraw.Draw(im)


def save(im, name):
    im = im.resize((128, 128), Image.LANCZOS)
    rgba = Image.new("RGBA", (128, 128), (255, 255, 255, 0))
    rgba.putalpha(im)
    rgba.save(os.path.join(OUT, name + ".png"))


def line(d, pts, w):
    d.line(pts, fill=255, width=w, joint="curve")
    for p in (pts[0], pts[-1]):
        d.ellipse((p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2), fill=255)


# ---------- stats ----------
im, d = canvas()                                            # mass: a weight
d.polygon([(110, 440), (160, 190), (352, 190), (402, 440)], fill=255)
d.arc((176, 60, 336, 260), 180, 360, fill=255, width=36)
d.ellipse((226, 270, 286, 330), fill=0)
save(im, "stat_mass")

im, d = canvas()                                            # lift: arrow up over a base
d.polygon([(256, 40), (420, 230), (322, 230), (322, 380), (190, 380), (190, 230), (92, 230)], fill=255)
d.rounded_rectangle((100, 420, 412, 470), radius=20, fill=255)
save(im, "stat_lift")

im, d = canvas()                                            # fuel: a drop
pts = []
for t in range(0, 181):
    a = math.radians(t * 2)
    pts.append((256 + 150 * math.sin(a) * (0.35 + 0.65 * (1 - math.cos(a)) / 2), 300 - 170 * math.cos(a) * (1 if math.cos(a) > 0 else 0.62)))
d.polygon([(256, 40), (390, 290)] + [(256 + 140 * math.cos(math.radians(a)), 330 + 140 * math.sin(math.radians(a))) for a in range(20, 161)] + [(122, 290)], fill=255)
d.ellipse((170, 280, 230, 340), fill=0)
save(im, "stat_fuel")

im, d = canvas()                                            # payload: a crate
d.polygon([(256, 50), (450, 150), (450, 360), (256, 462), (62, 360), (62, 150)], fill=255)
line(Image.new("L", (1, 1)) and d, [(62, 150), (256, 250), (450, 150)], 0)
d2 = ImageDraw.Draw(im)
d2.line([(62, 150), (256, 250), (450, 150)], fill=0, width=22)
d2.line([(256, 250), (256, 462)], fill=0, width=22)
save(im, "stat_payload")

im, d = canvas()                                            # stability: a target
for r, w in ((210, 36), (130, 34)):
    d.ellipse((256 - r, 256 - r, 256 + r, 256 + r), outline=255, width=w)
d.ellipse((216, 216, 296, 296), fill=255)
for a in (0, 90, 180, 270):
    v = (math.cos(math.radians(a)), math.sin(math.radians(a)))
    line(d, [(256 + v[0] * 215, 256 + v[1] * 215), (256 + v[0] * 275, 256 + v[1] * 275)], 28)
save(im, "stat_stability")

im, d = canvas()                                            # cost: a coin with a $
d.ellipse((40, 40, 472, 472), fill=255)
d.ellipse((90, 90, 422, 422), outline=0, width=20)
try:
    f = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 250, index=1)
    d.text((256, 262), "$", fill=0, font=f, anchor="mm")
except Exception:
    d.rectangle((236, 150, 276, 362), fill=0)
save(im, "stat_cost")

im, d = canvas()                                            # rating: a shield with a check
d.polygon([(256, 30), (450, 100), (450, 260), (256, 480), (62, 260), (62, 100)], fill=255)
line(ImageDraw.Draw(im), [(160, 250), (236, 330), (362, 180)], 0)
d.line([(160, 250), (236, 330), (362, 180)], fill=0, width=40, joint="curve")
save(im, "stat_rating")

im, d = canvas()                                            # orbit: planet with a ring and a satellite
d.ellipse((150, 150, 362, 362), fill=255)
ring = Image.new("L", (S, S), 0); rd = ImageDraw.Draw(ring)
rd.ellipse((30, 190, 482, 330), outline=255, width=26)
ring = ring.rotate(-25, resample=Image.BICUBIC)
cut = Image.new("L", (S, S), 0)
im.paste(0, (0, 0), ring.resize((S, S)).point(lambda v: 255 if v > 0 else 0).filter(Image.Filter.MaxFilter(1)) if False else ring)
im.paste(255, (0, 0), ring)
d.ellipse((410, 90, 470, 150), fill=255)
save(im, "stat_orbit")

# ---------- categories ----------
im, d = canvas()                                            # payload / head: a nose cone
d.polygon([(256, 30), (330, 130), (370, 260), (370, 400), (142, 400), (142, 260), (182, 130)], fill=255)
d.ellipse((216, 200, 296, 280), fill=0)
d.rectangle((142, 420, 370, 470), fill=255)
save(im, "cat_payload")

im, d = canvas()                                            # fuel tank: a cylinder
d.rounded_rectangle((120, 60, 392, 452), radius=60, fill=255)
for y in (170, 256, 342):
    d.rectangle((120, y - 9, 392, y + 9), fill=0)
save(im, "cat_tank")

im, d = canvas()                                            # engine: a bell nozzle
d.rectangle((190, 40, 322, 150), fill=255)
d.polygon([(170, 160), (342, 160), (450, 470), (62, 470)], fill=255)
d.polygon([(215, 190), (297, 190), (350, 430), (162, 430)], fill=0)
save(im, "cat_engine")

im, d = canvas()                                            # outer layer: a rocket with two side boosters
d.polygon([(256, 40), (310, 130), (310, 400), (202, 400), (202, 130)], fill=255)
for sx in (-1, 1):
    x = 256 + sx * 150
    d.polygon([(x, 150), (x + 38, 220), (x + 38, 400), (x - 38, 400), (x - 38, 220)], fill=255)
    d.polygon([(256 + sx * 54, 330), (256 + sx * 110, 330), (256 + sx * 54, 450)], fill=255)
d.rectangle((226, 410, 286, 470), fill=255)
save(im, "cat_outer")

im, d = canvas()                                            # extras: a parachute
d.pieslice((50, 50, 462, 400), 180, 360, fill=255)
for x in (80, 160, 256, 352, 432):
    pass
for x2 in (70, 150, 256, 362, 442):
    d.line([(x2, 225), (256, 410)], fill=255, width=14)
d.rounded_rectangle((216, 400, 296, 470), radius=14, fill=255)
for x in (153, 256, 359):
    d.ellipse((x - 14, 210, x + 14, 238), fill=0)
save(im, "cat_extras")
print("rocket icons written")

# ---------- sub-tab icons (one distinct glyph per tab) ----------
im, d = canvas()                                            # head: an ogive nose cone
d.polygon([(256, 40), (330, 150), (372, 300), (372, 440), (140, 440), (140, 300), (182, 150)], fill=255)
d.arc((140, 200, 372, 330), 200, 340, fill=0, width=22)
save(im, "sub_head")

im, d = canvas()                                            # cargo: a crate with a lid and a strap
d.rounded_rectangle((60, 190, 452, 450), radius=26, fill=255)
d.rounded_rectangle((40, 100, 472, 200), radius=26, fill=255)
d.rectangle((226, 100, 286, 450), fill=0)
d.rectangle((246, 100, 266, 450), fill=255)
save(im, "sub_cargo")

im, d = canvas()                                            # main stage: a stage with its flame below
d.rounded_rectangle((170, 40, 342, 330), radius=40, fill=255)
d.polygon([(190, 340), (322, 340), (256, 480)], fill=255)
d.rectangle((170, 120, 342, 140), fill=0); d.rectangle((170, 210, 342, 230), fill=0)
save(im, "sub_main")

im, d = canvas()                                            # upper stage: a small stage above, arrow up
d.rounded_rectangle((190, 190, 322, 400), radius=34, fill=255)
d.polygon([(256, 30), (340, 130), (172, 130)], fill=255)
d.polygon([(220, 410), (292, 410), (310, 470), (202, 470)], fill=255)
save(im, "sub_upper")

im, d = canvas()                                            # boosters: two tall boosters
for x in (150, 362):
    d.polygon([(x, 40), (x + 62, 140), (x + 62, 450), (x - 62, 450), (x - 62, 140)], fill=255)
    d.rectangle((x - 62, 300, x + 62, 322), fill=0)
save(im, "sub_boosters")

im, d = canvas()                                            # fins: a body segment with a swept fin
d.rounded_rectangle((300, 60, 420, 450), radius=36, fill=255)
d.polygon([(300, 140), (60, 440), (60, 470), (300, 450)], fill=255)
save(im, "sub_fins")

im, d = canvas()                                            # combos: a booster with a fin
d.polygon([(330, 40), (386, 130), (386, 440), (274, 440), (274, 130)], fill=255)
d.polygon([(274, 250), (110, 440), (110, 470), (274, 440)], fill=255)
d.polygon([(386, 250), (450, 330), (386, 330)], fill=255)
save(im, "sub_combo")

im, d = canvas()                                            # recovery: a landing leg
line(d, [(256, 60), (256, 240)], 56)
line(d, [(256, 240), (110, 420)], 44)
line(d, [(256, 240), (402, 420)], 44)
d.rounded_rectangle((60, 420, 180, 470), radius=14, fill=255); d.rounded_rectangle((332, 420, 452, 470), radius=14, fill=255)
save(im, "sub_recovery")

im, d = canvas()                                            # power: a lightning bolt
d.polygon([(300, 30), (110, 290), (240, 290), (190, 482), (402, 210), (270, 210)], fill=255)
save(im, "sub_power")

im, d = canvas()                                            # comms: a dish
d.pieslice((60, 120, 400, 460), 160, 340, fill=255)
d.polygon([(230, 290), (250, 310), (410, 130), (390, 110)], fill=255)
d.ellipse((380, 80, 450, 150), fill=255)
d.rectangle((226, 300, 262, 470), fill=255)
save(im, "sub_comms")

im, d = canvas()                                            # thrusters: an RCS nozzle with puffs
d.polygon([(80, 190), (220, 190), (290, 130), (290, 380), (220, 320), (80, 320)], fill=255)
for k, y in enumerate((170, 256, 342)):
    line(d, [(330, y), (450 - k * 14, y - 20 + k * 20)], 30)
save(im, "sub_thrusters")
print("sub-tab icons written")
