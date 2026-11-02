#!/usr/bin/env python3
"""
Transforme les sorties de TON workflow ComfyUI (personnage sur fond vert uni, 384 ou 768 px) en sprites aux NORMES du jeu :
  fond détouré -> réduit à la grille du jeu (cadre 64x64 px, moyenne par blocs) -> palette limitée -> alpha 0/255 -> posé en bas du cadre.

Géométrie (commune à make_pose_guides.py) : le canvas entier de ComfyUI correspond à 64x64 px de sprite ;
le personnage debout (75 % du canvas) fait donc 48 px de haut = 2 unités à PPU 24.

  python tools/ingest.py --names idle_0 walk_0 --input C:/ComfyUI/output/pixelart/characters      # cherche <nom>.png (ou <nom>_*.png)
  python tools/ingest.py --file mon_image.png --name walk_1 --install                               # une image précise
  python tools/ingest.py --names walk_0 --colors 24 --install                                       # lit AssetPack/comfy_output/hero_raw/
  python tools/ingest.py --category enemies --fit --names slime bat --install                       # monstres 32x32 (lit comfy_output/enemies_raw/)
  python tools/ingest.py --category enemies --fit --frame 48 --names brute_boss --install            # boss 48x48
Le résultat va dans AssetPack/unity_ready/hero/<nom>.png ; avec --install il est copié dans Assets/_Project/Resources/Sprites/Hero/.
Les clips (hero.json) utilisent automatiquement les nouveaux noms (attack1_0, run_2, hurt...) dès qu'ils existent.
"""
import argparse
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image

from common import PACK, UNITY_SPRITES, load_json


def detect_bg(arr):
    """Couleur de fond = médiane des 4 coins."""
    h, w = arr.shape[:2]
    corners = np.array([arr[2, 2], arr[2, w - 3], arr[h - 3, 2], arr[h - 3, w - 3]])
    return np.median(corners, axis=0)


def key_out(rgb, bg, tol, despill):
    a = rgb.astype(np.int16)
    dist = np.abs(a - bg.astype(np.int16)).max(axis=2)
    alpha = (dist > tol).astype(np.float32)
    if despill:                                              # reste de vert sur les bords
        r, g, b = a[..., 0], a[..., 1], a[..., 2]
        alpha[(g > r + 50) & (g > b + 50)] = 0.0
    return alpha


def to_grid(rgb, alpha, size):
    """Réduit à size=(w,h) en moyennant par blocs ; les couleurs sont pondérées par l'alpha (pas de halo)."""
    if isinstance(size, int):
        size = (size, size)
    pm_rgb = (rgb * alpha[..., None]).astype(np.uint8)
    pm_a = (alpha * 255).astype(np.uint8)
    small_rgb = np.array(Image.fromarray(pm_rgb).resize(size, Image.BOX)).astype(np.float32)
    small_a = np.array(Image.fromarray(pm_a).resize(size, Image.BOX)).astype(np.float32) / 255.0
    col = np.where(small_a[..., None] > 0.01, small_rgb / np.maximum(small_a[..., None], 0.01), 0)
    return np.clip(col, 0, 255).astype(np.uint8), small_a


def quantize(rgb, opaque, colors):
    pix = rgb[opaque]
    if pix.shape[0] < 2 or colors <= 0:
        return rgb
    pal = Image.fromarray(pix.reshape(1, -1, 3), "RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    out = Image.fromarray(rgb, "RGB").quantize(palette=pal, dither=Image.Dither.NONE).convert("RGB")
    return np.array(out)


def process(path, args, geo):
    im = Image.open(path).convert("RGB")
    rgb = np.array(im)
    bg = detect_bg(rgb) if args.bg == "auto" else np.array([int(v) for v in args.bg.split(",")])
    alpha = key_out(rgb, bg, args.tolerance, not args.no_despill)
    col, a = to_grid(rgb.astype(np.float32), alpha, args.frame)
    opaque = a >= 0.5
    col = quantize(col, opaque, args.colors)
    out = np.zeros((args.frame, args.frame, 4), dtype=np.uint8)
    out[..., :3] = np.where(opaque[..., None], col, 0)
    out[..., 3] = np.where(opaque, 255, 0)

    ys = np.where(opaque.any(axis=1))[0]
    if ys.size == 0:
        raise ValueError("image vide après détourage (fond mal détecté ? essaie --bg 0,255,0 ou --tolerance 40)")
    if args.align == "baseline":                              # même décalage pour toutes les frames (pieds du cadre = sol)
        shift = args.frame - int(round(geo["baseline"] * args.frame))
    else:                                                     # "bottom" : le point le plus bas touche le bas du cadre
        shift = args.frame - 1 - ys.max()
    if shift:
        shifted = np.zeros_like(out)
        if shift > 0:
            shifted[shift:] = out[:-shift]
        else:
            shifted[:shift] = out[-shift:]
        out = shifted
    return Image.fromarray(out, "RGBA")


def process_fit(path, args):
    """Texte->image sans guide de pose : on rogne le sujet, on l'ajuste au cadre standard, on le pose en bas-centre."""
    rgb = np.array(Image.open(path).convert("RGB"))
    bg = detect_bg(rgb) if args.bg == "auto" else np.array([int(v) for v in args.bg.split(",")])
    alpha = key_out(rgb, bg, args.tolerance, not args.no_despill)
    ys, xs = np.where(alpha > 0.5)
    if ys.size == 0:
        raise ValueError("image vide après détourage (fond mal détecté ? essaie --bg 0,255,0 ou --tolerance 40)")
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    crop_rgb, crop_a = rgb[y0:y1, x0:x1].astype(np.float32), alpha[y0:y1, x0:x1]
    h, w = crop_a.shape
    box = args.frame - args.margin
    scale = min(box / h, box / w)
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    col, a = to_grid(crop_rgb, crop_a, (nw, nh))
    opaque = a >= 0.5
    col = quantize(col, opaque, args.colors)
    out = np.zeros((args.frame, args.frame, 4), dtype=np.uint8)
    ox, oy = (args.frame - nw) // 2, args.frame - nh                 # bas-centre
    sub = out[oy:oy + nh, ox:ox + nw]
    sub[..., :3] = np.where(opaque[..., None], col, 0)
    sub[..., 3] = np.where(opaque, 255, 0)
    return Image.fromarray(out, "RGBA")


def find(name, folder):
    for pat in (f"{name}.png", f"{name}_*.png", f"*{name}*.png"):
        hits = sorted(Path(folder).glob(pat))
        if hits:
            return hits[-1]                                   # le plus récent (ComfyUI numérote _00001_)
    return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--names", nargs="*", default=[], help="frames à convertir (ex: idle_0 walk_0)")
    ap.add_argument("--input", default=None, help="dossier des sorties ComfyUI (défaut : comfy_output/<categorie>_raw)")
    ap.add_argument("--file"); ap.add_argument("--name")
    ap.add_argument("--category", default="hero")
    ap.add_argument("--frame", type=int, default=64, help="taille du cadre de sortie (px) : 64 = norme héros")
    ap.add_argument("--colors", type=int, default=24)
    ap.add_argument("--bg", default="auto", help="'auto' (coins) ou R,G,B ex. 0,255,0")
    ap.add_argument("--tolerance", type=int, default=36)
    ap.add_argument("--no-despill", action="store_true")
    ap.add_argument("--align", choices=["baseline", "bottom"], default="baseline")
    ap.add_argument("--fit", action="store_true", help="monstres/alliés : rogne le sujet et l'ajuste au cadre (pas de guide de pose)")
    ap.add_argument("--margin", type=int, default=2, help="--fit : marge en px (30 px utiles dans un cadre 32)")
    ap.add_argument("--install", action="store_true")
    args = ap.parse_args()

    geo = load_json(PACK / "characters" / "hero_poses.json")["geometry"]
    if args.input is None:
        args.input = str(PACK / "comfy_output" / ("hero_raw" if args.category == "hero" else f"{args.category}_raw"))
    if args.fit and args.frame == 64 and args.category in ("enemies", "allies"):
        args.frame = 32                                           # cadre standard monstre/allié ; boss : --frame 48
    jobs = []
    if args.file:
        if not args.name:
            sys.exit("--file demande --name")
        jobs.append((args.name, Path(args.file)))
    for n in args.names:
        p = find(n, args.input)
        if p is None:
            print(f"  ✗ {n}: aucune image trouvée dans {args.input}")
            continue
        jobs.append((n, p))
    if not jobs:
        sys.exit("Rien à convertir.")

    dst = PACK / "unity_ready" / args.category
    dst.mkdir(parents=True, exist_ok=True)
    folder = {"hero": "Hero", "enemies": "Enemies", "allies": "Allies"}[args.category]
    for name, p in jobs:
        out = process_fit(p, args) if args.fit else process(p, args, geo)
        out.save(dst / f"{name}.png")
        msg = f"  ✓ {name}: {p.name} -> {out.size[0]}x{out.size[1]}"
        if args.install:
            shutil.copy(dst / f"{name}.png", UNITY_SPRITES / folder / f"{name}.png")
            msg += f"  (installé dans Sprites/{folder})"
        print(msg)
    print("Dans Unity : menu  Jeu > 0 - Importer les sprites" if args.install else "Ajoute --install pour copier dans Assets/.")


if __name__ == "__main__":
    main()
