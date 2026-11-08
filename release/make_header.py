# Builds the 1300x372 Nexus header for Scavenger from game art (trash texture, item icons, map trash sign).
import os, math
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance

HERE = os.path.dirname(os.path.abspath(__file__)); ART = os.path.join(HERE, "art")
W, H = 1300, 372
F = lambda name, size: ImageFont.truetype(os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", name), size)
A = lambda n: Image.open(os.path.join(ART, n)).convert("RGBA")

# --- background: tiled trash pile, darkened, with a dark fade on the left
tile = A("trash_pile_001.png").resize((420, 420), Image.LANCZOS)
bg = Image.new("RGBA", (W, H))
for y in range(-60, H, 420):
    for x in range(0, W, 420):
        bg.alpha_composite(tile, (x, y))
bg = bg.filter(ImageFilter.GaussianBlur(1.2))
bg = ImageEnhance.Brightness(bg).enhance(0.55)
bg = ImageEnhance.Color(bg).enhance(0.8)
fade = Image.new("L", (W, H)); fd = ImageDraw.Draw(fade)
for x in range(W):
    fd.line([(x, 0), (x, H)], fill=max(0, min(245, int(245 - max(0, x - 380) * 0.55))))
img = Image.composite(Image.new("RGBA", (W, H), (18, 16, 14, 255)), bg, fade)
vig = Image.new("L", (W, H), 0); vd = ImageDraw.Draw(vig)
for i in range(60):
    vd.rectangle((i, i, W - 1 - i, H - 1 - i), outline=int(150 * (1 - i / 60)))
img = Image.composite(Image.new("RGBA", (W, H), (0, 0, 0, 255)), img, vig)
d = ImageDraw.Draw(img, "RGBA")

# --- title block
OCHRE, CREAM = (201, 154, 59), (240, 228, 205)
d.text((44, 50), "O B E N S E U E R", font=F("courbd.ttf", 22), fill=OCHRE)
d.text((38, 80), "SCAVENGER", font=F("courbd.ttf", 82), fill=CREAM)
d.line((46, 178, 452, 178), fill=OCHRE, width=3)
d.text((46, 192), "A Scavenging skill: dig through the trash,", font=F("segoeuib.ttf", 21), fill=(236, 228, 212))
d.text((46, 220), "level up, and find more and better loot.", font=F("segoeuib.ttf", 21), fill=(236, 228, 212))
x, y, cf = 46, 274, F("segoeuib.ttf", 14)
for c in ["Character tab skill", "Levels 1-10", "Scavenger instinct"]:
    w = d.textlength(c, font=cf) + 22
    d.rounded_rectangle((x, y, x + w, y + 30), radius=4, fill=(154, 106, 28), outline=OCHRE)
    d.text((x + 11, y + 6), c, font=cf, fill=(255, 246, 225)); x += w + 8
d.text((46, 322), "BepInEx 6 mod  \u00b7  formerly Scavenge Timer", font=F("segoeui.ttf", 14), fill=(163, 152, 131))

# --- the game's skill card (Character tab look)
cx, cy, cw, ch = 560, 58, 380, 96
d.rectangle((cx + 5, cy + 6, cx + cw + 5, cy + ch + 6), fill=(0, 0, 0, 140))
d.rectangle((cx, cy, cx + cw, cy + ch), fill=(28, 27, 25, 250), outline=(48, 46, 42), width=2)
t = F("courbd.ttf", 19)
d.text((cx + 16, cy + 12), "S C A V E N G I N G", font=t, fill=(236, 231, 222))
d.text((cx + cw - 16 - d.textlength("LVL 4", font=t), cy + 12), "LVL 4", font=t, fill=(236, 231, 222))
xp = "4380 XP"; xf = F("courbd.ttf", 12)
d.text((cx + cw - 16 - d.textlength(xp, font=xf), cy + 36), xp, font=xf, fill=(200, 194, 184))
seg_w, gap, sx, sy = 28, 7, cx + 16, cy + 58
ORANGE = (214, 140, 40)
for i in range(10):
    x0 = sx + i * (seg_w + gap)
    d.rectangle((x0, sy, x0 + seg_w, sy + 22), fill=(40, 39, 36), outline=(22, 21, 19))
    fill = 1.0 if i < 4 else (0.38 if i == 4 else 0)
    if fill > 0:
        d.rectangle((x0 + 2, sy + 2, x0 + 2 + (seg_w - 4) * fill, sy + 20), fill=ORANGE)
        d.line((x0 + 2, sy + 11, x0 + 2 + (seg_w - 4) * fill, sy + 11), fill=(160, 98, 22), width=2)

# --- "Scavenger instinct!" notification
nx, ny, nw, nh = 560, 196, 380, 98
d.rectangle((nx + 5, ny + 6, nx + nw + 5, ny + nh + 6), fill=(0, 0, 0, 140))
d.rectangle((nx, ny, nx + nw, ny + nh), fill=(20, 19, 17, 245), outline=OCHRE, width=2)
d.text((nx + 16, ny + 12), "Scavenger instinct!", font=F("courbd.ttf", 20), fill=(242, 190, 90))
d.text((nx + 16, ny + 44), "You dig deeper and find:", font=F("segoeui.ttf", 15), fill=(226, 220, 208))
d.text((nx + 16, ny + 66), "Copper Wire x2, Lock Pick, Fancy Paper", font=F("segoeuib.ttf", 15), fill=(120, 214, 120))

# --- dumpster with finds popping out, and the map's TRASH sign
dump = A("Green_Dumpster.png").resize((190, 190), Image.LANCZOS)
img.alpha_composite(dump, (1060, 160))
for name, (px, py), size, rot in [("Paper_Trash_spr.png", (1000, 92), 86, -18), ("Plastic_trash.png", (1112, 52), 92, 10),
                                  ("Trash_Bale_spr.png", (1190, 118), 80, 24), ("Junk_Wood_spr.png", (985, 196), 84, -8)]:
    it = A(name).resize((size, size), Image.LANCZOS).rotate(rot, expand=True, resample=Image.BICUBIC)
    glow = Image.new("RGBA", it.size, (0, 0, 0, 0)); gl = ImageDraw.Draw(glow)
    gl.ellipse((6, 6, it.size[0] - 6, it.size[1] - 6), fill=(242, 190, 90, 32))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(10)), (px, py))
    img.alpha_composite(it, (px, py))
sign = A("landmark_trash.png").resize((150, 150), Image.LANCZOS).rotate(-8, expand=True, resample=Image.BICUBIC)
img.alpha_composite(sign, (1135, -14))

# frame like the game's windows
d = ImageDraw.Draw(img, "RGBA")
d.rectangle((0, 0, W - 1, H - 1), outline=(12, 11, 10), width=6)
d.rectangle((6, 6, W - 7, H - 7), outline=(61, 56, 47), width=1)

rgb = img.convert("RGB")
rgb.save(os.path.join(HERE, "header.jpg"), quality=92, optimize=True)
rgb.save(os.path.join(HERE, "header.png"), optimize=True)
print("header", rgb.size, os.path.getsize(os.path.join(HERE, "header.jpg")) // 1024, "KB")
