#!/usr/bin/env python3
"""
Prépare les sprites pour ComfyUI.

Pour chaque sprite de  source/<categorie>/<nom>.png  on génère dans  comfy_input/<categorie>/ :
  - <nom>.png       : sprite agrandi (nearest-neighbor pour le pixel-art) centré sur un canvas carré à fond uni
  - <nom>_mask.png  : masque de la silhouette d'origine (blanc = sprite)
et on écrit manifest.json (taille d'origine, échelle, décalage, sujet du prompt...) utilisé par run_comfy.py et import_back.py.

  python tools/prepare.py                      # canvas 768 (SD 1.5 / SDXL ok)
  python tools/prepare.py --canvas 1024        # recommandé pour SDXL
"""
import argparse
from PIL import Image
from common import PACK, load_json, save_json


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--canvas", type=int, default=768, help="taille du canvas carré envoyé à ComfyUI")
    ap.add_argument("--fill", type=float, default=0.72, help="part du canvas occupée par le sprite (0-1)")
    ap.add_argument("--bg", default="127,127,127", help="couleur de fond R,G,B (gris neutre par défaut)")
    args = ap.parse_args()
    bg = tuple(int(v) for v in args.bg.split(","))

    desc = load_json(PACK / "assets.json")
    manifest = {"canvas": args.canvas, "bg": list(bg), "assets": []}
    n = 0

    for cat, cd in desc.items():
        if cat.startswith("_"):
            continue
        (PACK / "comfy_input" / cat).mkdir(parents=True, exist_ok=True)
        for name, subject in cd["assets"].items():
            src_path = PACK / "source" / cat / f"{name}.png"
            if not src_path.exists():
                print(f"  ! source manquante : {src_path}")
                continue
            im = Image.open(src_path).convert("RGBA")
            w, h = im.size
            pixel = cd["pixel"]
            s = max(1, int(args.canvas * args.fill // max(w, h)))
            up = im.resize((w * s, h * s), Image.NEAREST if pixel else Image.LANCZOS)
            ox, oy = (args.canvas - up.width) // 2, (args.canvas - up.height) // 2
            alpha = up.split()[3]

            if cd["alpha"] == "luma":
                # effets lumineux : lumière blanche sur fond noir (l'alpha sera la luminance au retour)
                glow = Image.new("RGB", up.size, (0, 0, 0))
                glow.paste(up.convert("RGB"), mask=alpha)
                base = Image.new("RGB", (args.canvas, args.canvas), (0, 0, 0))
                base.paste(glow, (ox, oy))
                mask = None
            else:
                base = Image.new("RGB", (args.canvas, args.canvas), bg)
                base.paste(up.convert("RGB"), (ox, oy), alpha)
                mask = Image.new("L", (args.canvas, args.canvas), 0)
                mask.paste(alpha.point(lambda v: 255 if v > 8 else 0), (ox, oy))

            base.save(PACK / "comfy_input" / cat / f"{name}.png")
            if mask is not None:
                mask.save(PACK / "comfy_input" / cat / f"{name}_mask.png")

            manifest["assets"].append({
                "id": f"{cat}/{name}", "category": cat, "name": name,
                "unity_folder": cd["unity_folder"],
                "src_size": [w, h], "canvas": args.canvas, "scale": s, "offset": [ox, oy],
                "pixel": pixel, "alpha": cd["alpha"], "tileable": bool(cd.get("tileable", False)),
                "subject": subject,
            })
            n += 1

    save_json(PACK / "manifest.json", manifest)
    print(f"{n} sprites préparés dans comfy_input/  (canvas {args.canvas}px) — manifest.json écrit.")


if __name__ == "__main__":
    main()
