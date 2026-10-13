"""Generate original Osaka night street materials and storefront graphics.

Only Pillow and NumPy are required. All surface textures wrap at the edges.
The graphics are created from shapes and installed Windows typefaces rather
than sampled from the stage reference. Run from the repository root.
"""
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Project/Art/Environment/OsakaNight/Textures"
ART = ROOT / "Artifacts/OsakaNight"
FONT = Path("C:/Windows/Fonts")
RNG = np.random.default_rng(1994)
OUT.mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)


def font(size, face="impact.ttf"):
    return ImageFont.truetype(str(FONT / face), size)


def textfit(draw, label, box, fill, face="impact.ttf", outline=None, line=0,
            maxsize=300, align="center"):
    x0, y0, x1, y1 = box
    size = maxsize
    while size > 8:
        f = font(size, face)
        bounds = draw.textbbox((0, 0), label, font=f, stroke_width=line)
        if bounds[2]-bounds[0] <= x1-x0 and bounds[3]-bounds[1] <= y1-y0:
            break
        size -= 1
    tw, th = bounds[2]-bounds[0], bounds[3]-bounds[1]
    x = x0 if align == "left" else (x0+x1-tw)/2
    y = (y0+y1-th)/2-bounds[1]
    draw.text((x, y), label, font=f, fill=fill, stroke_width=line,
              stroke_fill=outline)


def noise(size, period, seed=0):
    """Periodic bilinear value noise whose outermost pixel is not a duplicate."""
    rng = np.random.default_rng(seed)
    tile = rng.uniform(-1, 1, (period, period))
    coordinates = np.arange(size) * period / size
    lo = np.floor(coordinates).astype(int)
    t = coordinates-lo
    t = t*t*(3-2*t)
    result = np.zeros((size, size), dtype=np.float32)
    for dy in range(2):
        wy = (1-t) if dy == 0 else t
        for dx in range(2):
            wx = (1-t) if dx == 0 else t
            result += tile[(lo[:, None]+dy) % period,
                           (lo[None, :]+dx) % period]*wy[:, None]*wx[None, :]
    return result


def normal(height, strength=3.0):
    dx = (np.roll(height, -1, axis=1)-np.roll(height, 1, axis=1))*strength
    dy = (np.roll(height, -1, axis=0)-np.roll(height, 1, axis=0))*strength
    n = np.stack((-dx, dy, np.ones_like(dx)), axis=-1)
    n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-4)
    return Image.fromarray(np.uint8(np.clip(n*0.5+0.5, 0, 1)*255))


def save(name, img):
    img.convert("RGB").save(OUT / (name+".png"), optimize=True)


def materials():
    s = 1024
    # Fine aggregate, modest pooled-water tint and subtle patched variation.
    broad = noise(s, 4, 22)*0.5 + noise(s, 9, 26)*0.3
    mid = noise(s, 35, 31)
    fine = noise(s, 256, 33)*0.36 + noise(s, 512, 18)*0.26
    grain = RNG.normal(0, 1, (s, s))*0.35
    h = (mid*0.02 + fine*0.08 + grain*0.06)
    pores = RNG.random((s, s)) > 0.983
    h[pores] -= 0.13
    tone = broad*8 + mid*2.5 + fine*17 + grain*3
    stone = np.stack((34+tone, 47+tone*1.08, 48+tone*1.08), -1)
    flecks = RNG.random((s, s)) > 0.996
    stone[flecks] += [20, 21, 19]
    save("Asphalt", Image.fromarray(np.uint8(np.clip(stone, 0, 255))))
    save("AsphaltNormal", normal(h, 5.2))

    # Small rectangular granite blocks: eight rows with a running bond.
    yy, xx = np.mgrid[0:s, 0:s]
    row_h, brick_w = 128, 256
    row = yy//row_h
    local_x = (xx+(row%2)*128)%brick_w
    local_y = yy%row_h
    col = ((xx+(row%2)*128)//brick_w)%4
    rng = np.random.default_rng(41)
    variations = rng.uniform(-1, 1, (8, 4))
    value = variations[row, col]
    edge = np.minimum.reduce((local_x, brick_w-1-local_x,
                              local_y, row_h-1-local_y)).astype(float)
    mortar = edge < 4
    bevel = np.clip((edge-3)/8, 0, 1)
    grit = noise(s, 350, 36)*0.65 + RNG.normal(0, 0.18, (s, s))
    stain = noise(s, 7, 51)*3+noise(s, 21, 55)*2
    base = 90+value*10+grit*8+stain
    pavers = np.stack((base*0.88, base, base*1.025), -1)
    pavers *= (0.58+0.42*bevel[:, :, None])
    pavers[mortar] = [33, 41, 40]
    # A little lichen accumulates in the recessed joints.
    moss = mortar & (noise(s, 40, 78) > 0.25)
    pavers[moss] = [39, 49, 37]
    height = bevel*0.42 + grit*0.017
    save("Pavers", Image.fromarray(np.uint8(np.clip(pavers, 0, 255))))
    save("PaversNormal", normal(height, 2.8))

    plaster = (noise(s, 6, 62)*12+noise(s, 20, 67)*5+
               noise(s, 120, 68)*4+RNG.normal(0, 1.2, (s, s)))
    plaster = np.stack((105+plaster, 113+plaster, 106+plaster*0.9), -1)
    speck = RNG.random((s, s)) > 0.991
    plaster[speck] -= 14
    save("Concrete", Image.fromarray(np.uint8(np.clip(plaster, 0, 255))))


def board(size, base, rim, inset=18):
    im = Image.new("RGB", size, base)
    d = ImageDraw.Draw(im)
    w, h = size
    d.rectangle((3, 3, w-4, h-4), outline=(15, 25, 28), width=8)
    d.rectangle((inset, inset, w-inset-1, h-inset-1), outline=rim, width=5)
    return im, d


def finish(im, seed=14, strength=1.6):
    """Very light analog print texture, kept legible at gameplay distance."""
    arr = np.array(im, dtype=float)
    rng = np.random.default_rng(seed)
    grain = rng.normal(0, strength, arr.shape[:2])[..., None]
    arr += grain
    return Image.fromarray(np.uint8(np.clip(arr, 0, 255)))


def star(d, cx, cy, radius, fill):
    pts = []
    for k in range(10):
        r = radius if k%2 == 0 else radius*0.38
        a = k*math.pi/5-math.pi/2
        pts.append((cx+math.cos(a)*r, cy+math.sin(a)*r))
    d.polygon(pts, fill=fill)


def signs():
    cream, gold, red, cyan = (247, 233, 184), (255, 208, 54), (211, 36, 34), (82, 221, 226)
    im, d = board((2048, 512), (86, 18, 24), gold, 15)
    # Fine gold pinstripes and marquee corners reinforce the 1990s arcade feel.
    d.rectangle((29, 29, 2018, 483), outline=(191, 110, 36), width=2)
    d.line((80, 95, 1968, 95), fill=(136, 59, 29), width=3)
    d.line((80, 392, 1968, 392), fill=(136, 59, 29), width=3)
    for x in (72, 1976):
        for y in range(70, 463, 55):
            d.ellipse((x-6, y-6, x+6, y+6), fill=gold)
    textfit(d, "NEO GEO LAND", (130, 83, 1918, 335), gold,
            outline=(224, 46, 26), line=9, maxsize=255)
    textfit(d, "AMUSEMENT  •  OSAKA  •  1994", (285, 381, 1763, 443), cyan,
            face="arialbd.ttf", maxsize=55)
    textfit(d, "PLAY / FUN / FUTURE", (760, 48, 1288, 81), (237, 176, 103),
            face="arialbd.ttf", maxsize=26)
    star(d, 196, 417, 24, cyan)
    star(d, 1852, 417, 24, cyan)
    save("ArcadeHero", finish(im, 12, 1))

    im, d = board((512, 1024), (18, 39, 92), (137, 211, 247), 16)
    d.rectangle((35, 38, 476, 986), outline=(63, 106, 160), width=3)
    d.rectangle((54, 59, 458, 137), fill=(31, 72, 141))
    textfit(d, "SING ALL NIGHT", (63, 75, 449, 116), cream,
            face="arialbd.ttf", maxsize=31)
    for i, char in enumerate("カラオケ"):
        textfit(d, char, (97, 176+i*160, 415, 326+i*160), (247, 249, 244),
                face="YuGothB.ttc", maxsize=152)
    d.line((80, 842, 432, 842), fill=(111, 203, 240), width=5)
    textfit(d, "KARAOKE", (60, 867, 452, 934), (131, 222, 255), maxsize=73)
    textfit(d, "B1  /  OPEN 24H", (70, 951, 442, 981), cream,
            face="arialbd.ttf", maxsize=29)
    save("Karaoke", finish(im, 23))

    im, d = board((1024, 512), (240, 218, 163), (162, 41, 30), 18)
    d.rectangle((33, 33, 990, 478), outline=(150, 89, 44), width=2)
    # A stamp, broad Japanese brushlike face, and restrained shop framing.
    d.rectangle((63, 70, 175, 386), fill=(167, 39, 29))
    for i, char in enumerate("大阪"):
        textfit(d, char, (78, 105+i*118, 160, 205+i*118), cream,
                face="YuGothB.ttc", maxsize=92)
    textfit(d, "らーめん", (205, 64, 938, 324), (159, 34, 23),
            face="meiryob.ttc", maxsize=188)
    d.line((231, 345, 918, 345), fill=(177, 72, 40), width=4)
    textfit(d, "RAMEN  /  HOT NOODLES", (216, 364, 932, 424), (98, 46, 27),
            face="arialbd.ttf", maxsize=51)
    textfit(d, "OPEN LATE", (360, 447, 790, 472), (133, 79, 42),
            face="arialbd.ttf", maxsize=22)
    save("Ramen", finish(im, 32, 2.2))

    im, d = board((1024, 512), (12, 59, 55), (70, 182, 151), 17)
    d.rectangle((36, 36, 987, 475), outline=(38, 112, 97), width=2)
    textfit(d, "商店街", (177, 67, 847, 304), (153, 237, 193),
            face="meiryob.ttc", maxsize=214)
    for x in (100, 924):
        d.line((x, 100, x, 286), fill=(114, 194, 142), width=4)
        d.polygon([(x, 320), (x-24, 300), (x+24, 300)], fill=(114, 194, 142))
    d.line((190, 323, 834, 323), fill=(54, 145, 123), width=3)
    textfit(d, "NIGHT WALK", (165, 345, 859, 423), (222, 234, 204),
            face="arialbd.ttf", maxsize=83)
    textfit(d, "OSAKA  /  SHOPPING & DINING", (190, 444, 834, 469),
            (111, 184, 163), face="arialbd.ttf", maxsize=23)
    save("Market", finish(im, 53, 1.2))

    im, d = board((1024, 512), (62, 35, 26), (198, 155, 103), 18)
    d.rectangle((34, 34, 989, 477), outline=(119, 76, 51), width=2)
    # A simple engraved coffee emblem.
    d.ellipse((60, 122, 261, 323), outline=(201, 165, 115), width=5)
    d.rounded_rectangle((102, 174, 205, 255), radius=18, fill=(199, 163, 108))
    d.arc((178, 180, 236, 242), 270, 90, fill=(199, 163, 108), width=8)
    d.line((100, 266, 211, 266), fill=(199, 163, 108), width=7)
    d.arc((131, 123, 156, 184), 270, 90, fill=(199, 163, 108), width=4)
    d.arc((165, 116, 190, 177), 270, 90, fill=(199, 163, 108), width=4)
    textfit(d, "喫茶", (285, 57, 930, 310), (238, 205, 147),
            face="meiryob.ttc", maxsize=218)
    textfit(d, "MIDNIGHT COFFEE", (85, 347, 939, 416), (217, 179, 118),
            face="arialbd.ttf", maxsize=63)
    textfit(d, "COFFEE  •  JAZZ  •  1968", (200, 441, 824, 471),
            (167, 126, 86), face="arial.ttf", maxsize=25)
    save("Coffee", finish(im, 72, 1.8))

    im, d = board((512, 1024), (159, 24, 28), (255, 199, 32), 17)
    d.rectangle((35, 35, 476, 988), outline=(224, 98, 37), width=3)
    textfit(d, "ゲーム", (53, 63, 459, 152), (255, 208, 52),
            face="YuGothB.ttc", maxsize=96)
    for i, char in enumerate("GAME"):
        textfit(d, char, (81, 199+i*174, 431, 355+i*174), (255, 214, 44),
                outline=(91, 18, 26), line=5, maxsize=175)
    textfit(d, "2F / 3F", (82, 934, 430, 980), (255, 233, 147),
            face="arialbd.ttf", maxsize=43)
    save("VerticalArcade", finish(im, 83, 1.6))

    vending()


def vending():
    im, d = board((512, 1024), (231, 234, 223), (170, 34, 28), 8)
    d.rectangle((25, 21, 487, 157), fill=(186, 30, 29))
    textfit(d, "OSAKA DRINKS", (38, 43, 474, 117), (249, 238, 199),
            face="arialbd.ttf", maxsize=51)
    textfit(d, "COLD  /  REFRESH", (84, 126, 428, 148), (237, 184, 154),
            face="arialbd.ttf", maxsize=20)
    d.rectangle((28, 173, 381, 751), fill=(23, 37, 41), outline=(96, 102, 94), width=6)
    colors = [(196, 44, 40), (215, 166, 33), (42, 123, 179),
              (87, 136, 69), (234, 209, 151), (97, 61, 48)]
    labels = ["COLA", "LEMON", "SODA", "TEA", "MILK", "CAFE"]
    for row in range(3):
        y = 202+row*172
        for col in range(4):
            x = 49+col*80
            idx = (row*2+col)%6
            c = colors[idx]
            d.rounded_rectangle((x, y, x+52, y+101), radius=7, fill=c)
            d.ellipse((x, y-4, x+52, y+10), fill=(198, 208, 204))
            d.line((x+8, y+11, x+8, y+87), fill=tuple(min(255, k+35) for k in c), width=4)
            d.rectangle((x+4, y+42, x+48, y+62), fill=(236, 228, 190))
            textfit(d, labels[idx], (x+6, y+43, x+46, y+59), (40, 51, 47),
                    face="arialbd.ttf", maxsize=11)
            d.ellipse((x+5, y+113, x+47, y+129), fill=(190, 230, 209))
            textfit(d, "¥120", (x+5, y+135, x+47, y+151), (226, 227, 193),
                    face="arialbd.ttf", maxsize=12)
        d.line((37, y+160, 372, y+160), fill=(83, 107, 98), width=4)
    d.rounded_rectangle((400, 224, 471, 294), radius=4, fill=(28, 50, 43))
    textfit(d, "120", (406, 239, 465, 276), (163, 234, 98),
            face="arialbd.ttf", maxsize=33)
    d.rounded_rectangle((411, 332, 461, 378), radius=5, fill=(97, 107, 103))
    d.ellipse((426, 340, 447, 361), fill=(39, 43, 43))
    d.rectangle((414, 403, 463, 419), fill=(59, 65, 61))
    d.ellipse((417, 459, 457, 499), fill=(148, 152, 137), outline=(73, 86, 80), width=4)
    d.rectangle((395, 525, 478, 616), fill=(171, 37, 30))
    textfit(d, "つめたい", (399, 540, 473, 568), (249, 228, 173),
            face="meiryob.ttc", maxsize=18)
    textfit(d, "COLD", (402, 578, 471, 605), (249, 228, 173),
            face="arialbd.ttf", maxsize=23)
    d.rounded_rectangle((55, 813, 355, 902), radius=13, fill=(55, 65, 61),
                        outline=(148, 153, 139), width=7)
    d.rectangle((81, 843, 329, 875), fill=(27, 37, 34))
    d.line((34, 941, 478, 941), fill=(190, 32, 28), width=11)
    textfit(d, "THANK YOU  /  ありがとう", (75, 965, 437, 994), (103, 112, 100),
            face="meiryob.ttc", maxsize=19)
    save("Vending", finish(im, 91, 1.8))


def contact_sheet():
    names = ["ArcadeHero", "Ramen", "Market", "Coffee", "Karaoke",
             "VerticalArcade", "Vending", "Asphalt", "AsphaltNormal",
             "Pavers", "PaversNormal", "Concrete"]
    sheet = Image.new("RGB", (1600, 1220), (19, 24, 28))
    d = ImageDraw.Draw(sheet)
    d.text((30, 18), "OSAKA NIGHT / ORIGINAL STOREFRONTS + SURFACE MATERIALS",
           font=font(27, "arialbd.ttf"), fill=(219, 224, 213))
    cells = [(20, 70, 1578, 268), (20, 320, 508, 560), (548, 320, 1036, 560),
             (1076, 320, 1564, 560), (20, 605, 210, 985), (250, 605, 440, 985),
             (480, 605, 670, 985), (715, 605, 981, 871),
             (1015, 605, 1281, 871), (715, 929, 981, 1195),
             (1015, 929, 1281, 1195), (1315, 605, 1581, 871)]
    for name, cell in zip(names, cells):
        x0, y0, x1, y1 = cell
        img = Image.open(OUT / (name+".png"))
        img.thumbnail((x1-x0, y1-y0), Image.Resampling.LANCZOS)
        sheet.paste(img, (x0+(x1-x0-img.width)//2, y0))
        d.text((x0, y0-26), name, font=font(18, "arial.ttf"), fill=(172, 187, 184))
    sheet.save(ART / "textures-contact.png")


if __name__ == "__main__":
    materials()
    signs()
    contact_sheet()
    print(f"Generated {len(list(OUT.glob('*.png')))} textures in {OUT}")
    print(ART / "textures-contact.png")
