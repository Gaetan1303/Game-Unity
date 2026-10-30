#!/usr/bin/env python3
"""
Génère les frames d'animation du HÉROS avec TON ComfyUI : référence verrouillée + guide de pose OpenPose (img2img + ControlNet),
comme dans ton workflow opencontrol.json, mais pour toutes les frames d'un coup.

Pré-requis : ComfyUI lancé ; reference/villain_mercenary_locked_reference.png ; python tools/make_pose_guides.py déjà fait.

  python tools/run_pose.py --dry-run --frames walk_0                    # affiche le prompt sans rien envoyer
  python tools/run_pose.py                                              # toutes les frames de characters/hero_poses.json
  python tools/run_pose.py --frames walk_0 walk_1 walk_2 walk_3 --variants 4
  python tools/run_pose.py --ckpt autreModele.safetensors --denoise 0.5

Sorties : comfy_output/hero_raw/<frame>.png (384 px, 24 couleurs, fond vert). Étape suivante :
  python tools/ingest.py --names walk_0 walk_1 ... --install
"""
import argparse
import copy
import json
import sys
import urllib.error
import urllib.parse
import uuid
from pathlib import Path

from common import PACK, load_json
from run_comfy import by_title, http_bytes, http_json, remove_node, upload_image, wait_result


def build(template, cfg, a, frame, desc, ref_name, pose_name, seed, prefix):
    wf = copy.deepcopy(template)
    m = cfg["model"]
    ck = by_title(wf, "Checkpoint")
    wf[ck]["inputs"]["ckpt_name"] = a.ckpt or m["ckpt"]
    lora = by_title(wf, "Lora")
    lora_name = a.lora if a.lora is not None else m.get("lora", "")
    if lora_name:
        wf[lora]["inputs"].update(lora_name=lora_name, strength_model=m["lora_strength"], strength_clip=m["lora_strength"])
    else:
        remove_node(wf, lora, {0: [ck, 0], 1: [ck, 1]})
    wf[by_title(wf, "ControlNet Loader")]["inputs"]["control_net_name"] = a.controlnet or m["controlnet"]
    cn = wf[by_title(wf, "ControlNet Apply")]["inputs"]
    cn["strength"] = a.cn_strength if a.cn_strength is not None else m["cn_strength"]
    cn["end_percent"] = m["cn_end"]
    positive = cfg["positive"].replace("{pose}", desc)
    wf[by_title(wf, "Positive Prompt")]["inputs"]["text"] = positive
    wf[by_title(wf, "Negative Prompt")]["inputs"]["text"] = cfg["negative"]
    wf[by_title(wf, "Reference Image")]["inputs"]["image"] = ref_name
    wf[by_title(wf, "Pose Image")]["inputs"]["image"] = pose_name
    s = wf[by_title(wf, "Sampler")]["inputs"]
    s.update(seed=seed, steps=m["steps"], cfg=m["cfg"], sampler_name=m["sampler"], scheduler=m["scheduler"],
             denoise=a.denoise if a.denoise is not None else m["denoise"])
    out = cfg["output"]
    sc = wf[by_title(wf, "Output Scale")]["inputs"]; sc["width"] = sc["height"] = a.out_size or out["scale"]
    wf[by_title(wf, "Quantize")]["inputs"]["colors"] = a.colors or out["colors"]
    wf[by_title(wf, "Save")]["inputs"]["filename_prefix"] = prefix
    return wf, positive


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--server", default="127.0.0.1:8188")
    ap.add_argument("--character", default=str(PACK / "characters" / "hero.json"))
    ap.add_argument("--workflow", default=str(PACK / "comfyui" / "workflows" / "hero_pose.json"))
    ap.add_argument("--reference", help="image de référence (défaut : celle de hero.json)")
    ap.add_argument("--frames", nargs="*")
    ap.add_argument("--ckpt"); ap.add_argument("--lora"); ap.add_argument("--controlnet")
    ap.add_argument("--cn-strength", type=float); ap.add_argument("--denoise", type=float)
    ap.add_argument("--out-size", type=int); ap.add_argument("--colors", type=int)
    ap.add_argument("--seed", type=int); ap.add_argument("--variants", type=int, default=1)
    ap.add_argument("--force", action="store_true"); ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--timeout", type=int, default=900)
    a = ap.parse_args()

    cfg = load_json(a.character)
    poses = load_json(PACK / "characters" / "hero_poses.json")
    size = poses["geometry"]["canvas"]
    template = load_json(a.workflow)
    frames = a.frames or list(poses["frames"].keys())
    unknown = [f for f in frames if f not in poses["frames"]]
    if unknown:
        sys.exit(f"Frames inconnues : {unknown}. Disponibles : {', '.join(poses['frames'])}")
    ref = Path(a.reference) if a.reference else PACK / cfg["reference"]
    if not a.dry_run and not ref.exists():
        sys.exit(f"Référence introuvable : {ref}\nDépose ton image dans AssetPack/reference/ (voir reference/README.md).")
    base_seed = a.seed if a.seed is not None else cfg["seed"]

    todo = []
    for f in frames:
        out = PACK / "comfy_output" / "hero_raw" / f"{f}.png"
        guide = PACK / "pose_guides" / f"poseguide_{f}_{size}.png"
        if out.exists() and not a.force and not a.dry_run:
            print(f"  = {f} déjà généré (--force pour refaire)"); continue
        if not guide.exists() and not a.dry_run:
            sys.exit(f"Guide manquant : {guide}  -> python tools/make_pose_guides.py")
        todo.append((f, guide, out))
    print(f"{len(todo)} frame(s) — serveur {a.server} — seed {base_seed}")

    ref_name = None
    for i, (f, guide, out) in enumerate(todo, 1):
        desc = poses["frames"][f]["desc"]
        for v in range(a.variants):
            seed = base_seed + v
            prefix = f"assetpack/hero_{f}_v{v}"
            if a.dry_run:
                _, pos = build(template, cfg, a, f, desc, "ref.png", "pose.png", seed, prefix)
                print(f"[{i}/{len(todo)}] {f} seed={seed}\n   guide: {guide.name}\n   prompt: …{pos[-230:]}")
                continue
            try:
                if ref_name is None:
                    ref_name = upload_image(a.server, ref, "pack_hero_reference.png")
                pose_name = upload_image(a.server, guide, f"pack_pose_{f}.png")
                wf, _ = build(template, cfg, a, f, desc, ref_name, pose_name, seed, prefix)
                pid = http_json(f"http://{a.server}/prompt", {"prompt": wf, "client_id": uuid.uuid4().hex})["prompt_id"]
                img = wait_result(a.server, pid, a.timeout)
                q = urllib.parse.urlencode({"filename": img["filename"], "subfolder": img.get("subfolder", ""), "type": img.get("type", "output")})
                data = http_bytes(f"http://{a.server}/view?{q}")
            except (urllib.error.URLError, ConnectionError) as ex:
                sys.exit(f"Impossible de joindre ComfyUI sur {a.server} ({ex}). Est-il lancé ?")
            except urllib.error.HTTPError as ex:
                sys.exit(f"ComfyUI a refusé la requête : {ex.read().decode(errors='replace')[:600]}")
            dst = out if v == 0 else PACK / "comfy_output" / "_variants" / "hero_raw" / f"{f}__v{v}.png"
            dst.parent.mkdir(parents=True, exist_ok=True)
            dst.write_bytes(data)
            print(f"  [{i}/{len(todo)}] {f} v{v} -> {dst.relative_to(PACK)}")
    print("Terminé. Étape suivante :  python tools/ingest.py --names " + " ".join(f for f, _, _ in todo[:3]) + " ... --install")


if __name__ == "__main__":
    main()
