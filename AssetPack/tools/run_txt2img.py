#!/usr/bin/env python3
"""
Génère les MONSTRES / ALLIÉS avec TON ComfyUI en texte->image (comme ton pixelArt.json), un sprite par entrée de assets.json.
Sujets : assets.json  |  réglages : characters/sprites_txt2img.json  |  graphe : comfyui/workflows/sprite_txt2img.json

  python tools/run_txt2img.py --dry-run --only slime
  python tools/run_txt2img.py --category enemies --variants 4
  python tools/run_txt2img.py --category allies --ckpt autreModele.safetensors

Sorties : comfy_output/<categorie>_raw/<nom>.png  (fond vert). Ensuite, mise aux normes :
  python tools/ingest.py --category enemies --fit --names slime bat --install          (32x32)
  python tools/ingest.py --category enemies --fit --frame 48 --names brute_boss --install   (boss 48x48)
"""
import argparse
import copy
import sys
import urllib.error
import urllib.parse
import uuid

from common import PACK, load_json
from run_comfy import by_title, http_bytes, http_json, remove_node, wait_result


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--server", default="127.0.0.1:8188")
    ap.add_argument("--category", choices=["enemies", "allies"])
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--ckpt"); ap.add_argument("--lora"); ap.add_argument("--seed", type=int)
    ap.add_argument("--variants", type=int, default=1)
    ap.add_argument("--extra", default="", help="texte ajouté à tous les prompts")
    ap.add_argument("--force", action="store_true"); ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--timeout", type=int, default=600)
    a = ap.parse_args()

    cfg = load_json(PACK / "characters" / "sprites_txt2img.json")
    desc = load_json(PACK / "assets.json")
    template = load_json(PACK / "comfyui" / "workflows" / "sprite_txt2img.json")
    m = cfg["model"]
    jobs = []
    for cat in ("enemies", "allies"):
        if a.category and cat != a.category:
            continue
        for name, subject in desc[cat]["assets"].items():
            if a.only and name not in a.only:
                continue
            out = PACK / "comfy_output" / f"{cat}_raw" / f"{name}.png"
            if out.exists() and not a.force and not a.dry_run:
                print(f"  = {cat}/{name} déjà généré (--force pour refaire)"); continue
            jobs.append((cat, name, subject, out))
    if not jobs:
        sys.exit("Rien à générer (filtre --category / --only ?).")
    base_seed = a.seed if a.seed is not None else cfg["seed"]
    print(f"{len(jobs)} sprite(s) — serveur {a.server} — seed {base_seed}")

    for i, (cat, name, subject, out) in enumerate(jobs, 1):
        for v in range(a.variants):
            seed = base_seed + v
            wf = copy.deepcopy(template)
            ck = by_title(wf, "Checkpoint"); wf[ck]["inputs"]["ckpt_name"] = a.ckpt or m["ckpt"]
            lora = by_title(wf, "Lora"); lora_name = a.lora if a.lora is not None else m.get("lora", "")
            if lora_name:
                wf[lora]["inputs"].update(lora_name=lora_name, strength_model=m["lora_strength"], strength_clip=m["lora_strength"])
            else:
                remove_node(wf, lora, {0: [ck, 0], 1: [ck, 1]})
            pos = cfg["positive"].replace("{subject}", ", ".join(x for x in [subject, a.extra] if x))
            wf[by_title(wf, "Positive Prompt")]["inputs"]["text"] = pos
            wf[by_title(wf, "Negative Prompt")]["inputs"]["text"] = cfg["negative"]
            lat = wf[by_title(wf, "Latent")]["inputs"]; lat["width"] = lat["height"] = m["size"]
            wf[by_title(wf, "Sampler")]["inputs"].update(seed=seed, steps=m["steps"], cfg=m["cfg"], sampler_name=m["sampler"], scheduler=m["scheduler"])
            wf[by_title(wf, "Quantize")]["inputs"]["colors"] = m["colors"]
            wf[by_title(wf, "Save")]["inputs"]["filename_prefix"] = f"assetpack/{cat}_{name}_v{v}"
            if a.dry_run:
                print(f"[{i}/{len(jobs)}] {cat}/{name} seed={seed}\n   prompt: {pos}"); continue
            try:
                pid = http_json(f"http://{a.server}/prompt", {"prompt": wf, "client_id": uuid.uuid4().hex})["prompt_id"]
                img = wait_result(a.server, pid, a.timeout)
                q = urllib.parse.urlencode({"filename": img["filename"], "subfolder": img.get("subfolder", ""), "type": img.get("type", "output")})
                data = http_bytes(f"http://{a.server}/view?{q}")
            except (urllib.error.URLError, ConnectionError) as ex:
                sys.exit(f"Impossible de joindre ComfyUI sur {a.server} ({ex}). Est-il lancé ?")
            except urllib.error.HTTPError as ex:
                sys.exit(f"ComfyUI a refusé la requête : {ex.read().decode(errors='replace')[:600]}")
            dst = out if v == 0 else PACK / "comfy_output" / "_variants" / f"{cat}_raw" / f"{name}__v{v}.png"
            dst.parent.mkdir(parents=True, exist_ok=True); dst.write_bytes(data)
            print(f"  [{i}/{len(jobs)}] {cat}/{name} v{v} -> {dst.relative_to(PACK)}")
    print("Terminé. Étape suivante :  python tools/ingest.py --category enemies --fit --names <noms> --install")


if __name__ == "__main__":
    main()
