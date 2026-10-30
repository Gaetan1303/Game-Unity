#!/usr/bin/env python3
"""
Met les sprites aux NORMES du projet (voir standard.json et SPRITE_STANDARD.md) :
  * même taille de pixel partout (un seul PPU),
  * canevas standards (32x32, 48x48, 64x64), largeur paire, sprite posé en bas-centre,
  * agrandissement "pixel-art" (Scale2x / Scale3x) : on AJOUTE de vrais pixels au lieu d'étirer ceux qui existent,
  * effets pixelisés (alpha à 4 niveaux).

  python tools/standardize.py                  # source_original/ -> source/ (+ installe dans Unity)
  python tools/standardize.py --no-install     # ne touche pas à Assets/
  python tools/standardize.py --check          # vérifie que tout source/ respecte la norme (code retour 1 sinon)
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from common import PACK, UNITY_SPRITES, SPRITE_PACK_CFG, load_json, save_json


# ------------------------------------------------------------------ algorithmes de mise à l'échelle pixel-art
def _neighbors(a):
    """a: (h,w,4) -> dict des voisins (bord = transparent)."""
    p = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode="constant")
    h, w = a.shape[:2]
    g = lambda dy, dx: p[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
    return {"A": g(-1, -1), "B": g(-1, 0), "C": g(-1, 1), "D": g(0, -1), "E": a, "F": g(0, 1), "G": g(1, -1), "H": g(1, 0), "I": g(1, 1)}


def eq(x, y):
    # deux pixels sont "égaux" si même couleur, ou tous deux transparents
    same = (x == y).all(axis=2)
    both_clear = (x[..., 3] == 0) & (y[..., 3] == 0)
    return same | both_clear


def scale2x(a):
    n = _neighbors(a)
    B, D, E, F, H = n["B"], n["D"], n["E"], n["F"], n["H"]
    h, w = a.shape[:2]
    out = np.zeros((h * 2, w * 2, 4), dtype=a.dtype)
    cond = ~eq(B, H) & ~eq(D, F)
    e0 = np.where((eq(D, B) & cond)[..., None], D, E)
    e1 = np.where((eq(B, F) & cond)[..., None], F, E)
    e2 = np.where((eq(D, H) & cond)[..., None], D, E)
    e3 = np.where((eq(H, F) & cond)[..., None], F, E)
    out[0::2, 0::2], out[0::2, 1::2], out[1::2, 0::2], out[1::2, 1::2] = e0, e1, e2, e3
    return out


def scale3x(a):
    n = _neighbors(a)
    A, B, C, D, E, F, G, H, I = (n[k] for k in "ABCDEFGHI")
    h, w = a.shape[:2]
    out = np.zeros((h * 3, w * 3, 4), dtype=a.dtype)
    cond = (~eq(B, H) & ~eq(D, F))[..., None]
    ne = lambda x, y: ~eq(x, y)
    sel = lambda c, v: np.where((c)[..., None] & cond, v, E)
    e0 = sel(eq(D, B), D)
    e1 = sel((eq(D, B) & ne(E, C)) | (eq(B, F) & ne(E, A)), B)
    e2 = sel(eq(B, F), F)
    e3 = sel((eq(D, B) & ne(E, G)) | (eq(D, H) & ne(E, A)), D)
    e4 = E
    e5 = sel((eq(B, F) & ne(E, I)) | (eq(H, F) & ne(E, C)), F)
    e6 = sel(eq(D, H), D)
    e7 = sel((eq(D, H) & ne(E, I)) | (eq(H, F) & ne(E, G)), H)
    e8 = sel(eq(H, F), F)
    for i, v in enumerate([e0, e1, e2, e3, e4, e5, e6, e7, e8]):
        out[i // 3::3, i % 3::3] = v
    return out


def pixelate(img, size, levels):
    """Effets : réduit sans lissage excessif et force un alpha à N niveaux (look pixel-art)."""
    small = img.resize(size, Image.BOX)
    a = np.array(small).astype(np.float32)
    alpha = a[..., 3] / 255.0
    q = np.round(alpha * (levels - 1)) / (levels - 1)
    q[alpha < 0.08] = 0
    lum = np.clip(a[..., :3].max(axis=2), 200, 255)          # effets blancs : on garde une teinte claire, Unity les colore
    out = np.dstack([np.repeat(lum[..., None], 3, axis=2), (q * 255)[..., None]]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


# ------------------------------------------------------------------ mise en canevas
def place(img, canvas, align):
    cw, ch = canvas
    out = Image.new("RGBA", canvas, (0, 0, 0, 0))
    if align == "fill":
        if img.size != canvas:
            raise ValueError(f"taille {img.size} != canevas {canvas} (align=fill)")
        return img
    if align == "bottom_keep":                               # on garde la position relative (animation : ne pas faire bouger les frames)
        if img.width > cw or img.height > ch:
            raise ValueError(f"{img.size} dépasse le canevas {canvas}")
        out.paste(img, ((cw - img.width) // 2, ch - img.height))
        return out
    bb = img.getbbox()                                        # "bottom" : on rogne puis on pose en bas-centre
    if bb is None:
        return out
    c = img.crop(bb)
    if c.width > cw or c.height > ch:
        raise ValueError(f"contenu {c.size} dépasse le canevas {canvas}")
    out.paste(c, ((cw - c.width) // 2, ch - c.height))
    return out


def convert(src_path, spec):
    im = Image.open(src_path).convert("RGBA")
    m = spec["method"]
    if m == "scale2x":
        im = Image.fromarray(scale2x(np.array(im)), "RGBA")
    elif m == "scale3x":
        im = Image.fromarray(scale3x(np.array(im)), "RGBA")
    elif m == "pixelate":
        im = pixelate(im, tuple(spec["canvas"]), spec.get("alpha_levels", 4))
    elif m != "keep":
        raise ValueError(f"méthode inconnue {m}")
    return place(im, tuple(spec["canvas"]), spec["align"])


def check_image(path, canvas, pixel_alpha):
    problems = []
    im = Image.open(path).convert("RGBA")
    if im.size != tuple(canvas):
        problems.append(f"taille {im.size} au lieu de {tuple(canvas)}")
    if im.width % 2:
        problems.append("largeur impaire (pivot sur un demi-pixel)")
    if pixel_alpha:
        a = np.array(im)[..., 3]
        if ((a > 0) & (a < 255)).any():
            problems.append("alpha semi-transparent (pixel-art : alpha 0 ou 255)")
    return problems


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-install", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    std = load_json(PACK / "standard.json")
    bad = 0

    if args.check:
        for cat, spec in std["categories"].items():
            for p in sorted((PACK / "source" / cat).glob("*.png")):
                v = spec["canvas"]
                for k, var in std.get("variants", {}).items():
                    if k == f"{cat}/{p.stem}":
                        v = var["canvas"]
                for msg in check_image(p, v, cat != "fx"):
                    bad += 1
                    print(f"  ✗ {cat}/{p.name}: {msg}")
        print("Norme respectée." if not bad else f"{bad} problème(s).")
        raise SystemExit(1 if bad else 0)

    done = 0
    for cat, spec in std["categories"].items():
        (PACK / "source" / cat).mkdir(parents=True, exist_ok=True)
        for p in sorted((PACK / "source_original" / cat).glob("*.png")):
            out = convert(p, spec)
            out.save(PACK / "source" / cat / p.name)
            done += 1
    for key, var in std.get("variants", {}).items():
        cat, name = key.split("/")
        src = PACK / "source_original" / var["from"].split("/")[0] / (var["from"].split("/")[1] + ".png")
        spec = dict(std["categories"][cat]); spec.update(var)
        convert(src, spec).save(PACK / "source" / cat / f"{name}.png")
        done += 1
    print(f"{done} sprites mis aux normes dans source/")

    if not args.no_install:
        for cat, spec in std["categories"].items():
            dst = UNITY_SPRITES / spec["folder"]
            dst.mkdir(parents=True, exist_ok=True)
            for p in (PACK / "source" / cat).glob("*.png"):
                (dst / p.name).write_bytes(p.read_bytes())
        cfg = load_json(SPRITE_PACK_CFG)
        for f in cfg["folders"]:
            spec = next((s for s in std["categories"].values() if s["folder"] == f["folder"]), None)
            if spec:
                ppu = spec.get("ppu", std["ppu"])
                f["base_ppu"] = ppu; f["ppu"] = ppu; f["filter"] = "Point"
        save_json(SPRITE_PACK_CFG, cfg)
        print("Installé dans Assets/_Project/Resources/Sprites + SpritePack.json (PPU unifié). Dans Unity : Jeu > 0 - Importer les sprites")


if __name__ == "__main__":
    main()
