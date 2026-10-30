#!/usr/bin/env python3
"""
Ramène les images générées par ComfyUI (comfy_output/) vers des sprites prêts pour Unity (unity_ready/).

Modes
  --mode pixel (défaut) : redescend à la taille d'origine (moyenne par bloc), palette limitée optionnelle (--colors 32),
                          silhouette = celle du sprite d'origine -> remplacement 100 % compatible avec le jeu.
  --mode hd             : garde une résolution plus grande (--hd-scale 4 = 4x la taille d'origine, filtre lissé).
                          Les PPU Unity sont ajustés automatiquement (SpritePack.json) pour que la taille en jeu ne change pas.

Alpha
  --alpha mask : silhouette d'origine (défaut)      --alpha key : détourage par distance à la couleur de fond
  (les effets "fx" utilisent la luminance comme alpha)

  python tools/import_back.py --colors 32                 # pixel-art, palette 32 couleurs
  python tools/import_back.py --mode hd --hd-scale 4 --install
  python tools/import_back.py --category hero --install   # copie aussi dans Assets/_Project/Resources/Sprites
"""
import argparse
import shutil
import numpy as np
from PIL import Image
from common import PACK, UNITY_SPRITES, SPRITE_PACK_CFG, load_manifest, load_json, save_json, select


def quantize_opaque(rgb, alpha, colors):
    """Palette calculée uniquement sur les pixels opaques."""
    arr = np.array(rgb)
    opaque = arr[np.array(alpha) > 0]
    if opaque.shape[0] < 2:
        return rgb
    pal = Image.fromarray(opaque.reshape(1, -1, 3).astype(np.uint8), "RGB").quantize(
        colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    return rgb.quantize(palette=pal, dither=Image.Dither.NONE).convert("RGB")


def alpha_from_key(crop, bg, thr, feather):
    arr = np.array(crop).astype(np.int16)
    dist = np.abs(arr - np.array(bg, dtype=np.int16)).max(axis=2).astype(np.float32)
    a = np.clip((dist - thr) / max(1.0, feather), 0, 1) * 255
    return Image.fromarray(a.astype(np.uint8), "L")


def process(e, args, manifest):
    cat, name = e["category"], e["name"]
    out = PACK / "comfy_output" / cat / f"{name}.png"
    if not out.exists():
        return None
    canvas = e["canvas"]
    res = Image.open(out).convert("RGB")
    if res.size != (canvas, canvas):
        res = res.resize((canvas, canvas), Image.LANCZOS)

    w, h = e["src_size"]
    s = e["scale"]
    ox, oy = e["offset"]
    crop = res.crop((ox, oy, ox + w * s, oy + h * s))
    src = Image.open(PACK / "source" / cat / f"{name}.png").convert("RGBA")
    k = args.hd_scale if args.mode == "hd" else 1
    tw, th = w * k, h * k

    if e["alpha"] == "luma":                                   # effets : alpha = luminance
        rgb = crop.resize((tw, th), Image.BOX)
        lum = np.array(rgb).max(axis=2).astype(np.float32) / 255.0
        a = np.clip((lum - 0.02) * 1.15, 0, 1)
        if args.fx_levels > 1:                                  # look pixel-art : alpha en N niveaux (norme du projet : 4)
            a = np.round(a * (args.fx_levels - 1)) / (args.fx_levels - 1)
            a[a < 0.1] = 0
        if args.fx_color == "white":
            col = np.full((th, tw, 3), 255, dtype=np.uint8)
        else:
            col = np.clip(np.array(rgb).astype(np.float32) / np.maximum(lum[..., None], 1e-3), 0, 255).astype(np.uint8)
        return Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")

    if e["pixel"] and args.mode == "pixel":
        rgb = crop.resize((w, h), Image.BOX)
        alpha = src.split()[3] if args.alpha == "mask" else alpha_from_key(rgb, manifest["bg"], args.key_threshold, 24)
        alpha = alpha.point(lambda v: 255 if v > 127 else 0)
        if args.colors:
            rgb = quantize_opaque(rgb, alpha, args.colors)
    else:
        rgb = crop.resize((tw, th), Image.LANCZOS)
        if args.alpha == "mask":
            alpha = src.split()[3].resize((tw, th), Image.BICUBIC)
        else:
            alpha = alpha_from_key(rgb, manifest["bg"], args.key_threshold, 24)
        if args.colors and e["pixel"]:
            rgb = quantize_opaque(rgb, alpha, args.colors)
    img = rgb.convert("RGBA")
    img.putalpha(alpha)
    return img


def update_unity_cfg(folder, k, smooth):
    """Ajuste les PPU du dossier (Editor/SpritePack.json) pour que la taille en jeu reste identique."""
    cfg = load_json(SPRITE_PACK_CFG)
    for f in cfg["folders"]:
        if f["folder"] == folder:
            f["ppu"] = f["base_ppu"] * k
            f["filter"] = "Bilinear" if smooth else "Point"
    save_json(SPRITE_PACK_CFG, cfg)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mode", choices=["pixel", "hd"], default="pixel")
    ap.add_argument("--hd-scale", type=int, default=4)
    ap.add_argument("--colors", type=int, default=0, help="palette limitée (0 = aucune)")
    ap.add_argument("--alpha", choices=["mask", "key"], default="mask")
    ap.add_argument("--key-threshold", type=float, default=28)
    ap.add_argument("--fx-color", choices=["white", "keep"], default="white")
    ap.add_argument("--fx-levels", type=int, default=4, help="niveaux d'alpha des effets (4 = norme, 0 = alpha continu)")
    ap.add_argument("--category", help="hero | enemies | allies | env | fx")
    ap.add_argument("--only", nargs="*", help="noms précis, ex: idle_0 slime")
    ap.add_argument("--install", action="store_true", help="copie dans Assets/_Project/Resources/Sprites")
    ap.add_argument("--allow-partial", action="store_true", help="autorise une catégorie incomplète en mode hd")
    args = ap.parse_args()

    manifest = load_manifest()
    entries = select(manifest, args.category, args.only)
    done, missing = {}, {}
    for e in entries:
        img = process(e, args, manifest)
        if img is None:
            missing.setdefault(e["category"], []).append(e["name"])
            continue
        dst = PACK / "unity_ready" / e["category"]
        dst.mkdir(parents=True, exist_ok=True)
        img.save(dst / f"{e['name']}.png")
        done.setdefault(e["category"], []).append(e)
    total = sum(len(v) for v in done.values())
    print(f"{total} sprite(s) convertis -> unity_ready/")
    for cat, names in missing.items():
        print(f"  (pas de sortie ComfyUI pour {cat}: {', '.join(names[:6])}{'...' if len(names) > 6 else ''})")

    if args.install and done:
        cfg = load_json(SPRITE_PACK_CFG)
        installed = 0
        for cat, es in done.items():
            folder = es[0]["unity_folder"]
            entry = next(f for f in cfg["folders"] if f["folder"] == folder)
            all_names = {e["name"] for e in manifest["assets"] if e["category"] == cat}
            complete = {e["name"] for e in es} == all_names
            resolution_changes = args.mode == "hd" or entry["ppu"] != entry["base_ppu"]
            if resolution_changes and not complete and not args.allow_partial:
                print(f"  ! {cat}: installation refusée. En mode hd (ou si le dossier est déjà en hd), TOUTES les frames de la "
                      f"catégorie doivent être converties ensemble, sinon les tailles en jeu seront incohérentes. "
                      f"Retire --only ou utilise --allow-partial.")
                continue
            for e in es:
                shutil.copy(PACK / "unity_ready" / cat / f"{e['name']}.png", UNITY_SPRITES / folder / f"{e['name']}.png")
            if complete or args.allow_partial:
                update_unity_cfg(folder, args.hd_scale if args.mode == "hd" else 1, args.mode == "hd" or not es[0]["pixel"])
            installed += len(es)
            print(f"  installé : {folder} ({len(es)} fichiers)")
        if installed:
            print("Dans Unity : menu  Jeu > 0 - Importer les sprites")


if __name__ == "__main__":
    main()
