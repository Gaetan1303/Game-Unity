#!/usr/bin/env python3
"""
Envoie tout le pack à TON ComfyUI local (img2img), sprite par sprite, et récupère les résultats dans comfy_output/.

Prérequis : ComfyUI lancé (par défaut http://127.0.0.1:8188), `python tools/prepare.py` déjà fait.

  python tools/run_comfy.py --ckpt monModele.safetensors --style pixel_dark
  python tools/run_comfy.py --ckpt monModele.safetensors --lora monStyle.safetensors --category hero
  python tools/run_comfy.py --ckpt xl.safetensors --workflow comfyui/workflows/img2img_controlnet.json --controlnet canny.safetensors
  python tools/run_comfy.py --dry-run --only idle_0          # affiche le prompt sans rien envoyer

Le sujet de chaque sprite vient de assets.json, le style de styles.json (ajoute le tien !).
Plusieurs essais par sprite : --variants 4 (le n°0 va dans comfy_output/, les autres dans comfy_output/_variants/).
"""
import argparse
import copy
import io
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

from common import PACK, load_json, load_manifest, select


# ---------------------------------------------------------------- HTTP minimal (sans dépendance)
def http_json(url, payload=None, timeout=60):
    data = json.dumps(payload).encode() if payload is not None else None
    headers = {"Content-Type": "application/json"} if payload is not None else {}
    with urllib.request.urlopen(urllib.request.Request(url, data=data, headers=headers), timeout=timeout) as r:
        return json.loads(r.read())


def http_bytes(url, timeout=120):
    with urllib.request.urlopen(url, timeout=timeout) as r:
        return r.read()


def upload_image(server, path, name):
    boundary = uuid.uuid4().hex
    body = io.BytesIO()

    def w(s):
        body.write(s.encode())

    w(f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="{name}"\r\nContent-Type: image/png\r\n\r\n')
    body.write(Path(path).read_bytes())
    w("\r\n")
    w(f'--{boundary}\r\nContent-Disposition: form-data; name="overwrite"\r\n\r\ntrue\r\n')
    w(f"--{boundary}--\r\n")
    req = urllib.request.Request(f"http://{server}/upload/image", data=body.getvalue(),
                                 headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())["name"]


# ---------------------------------------------------------------- manipulation du workflow
def by_title(wf, title):
    for nid, n in wf.items():
        if n.get("_meta", {}).get("title") == title:
            return nid
    return None


def remove_node(wf, nid, mapping):
    """Supprime un noeud et rebranche ses sorties : mapping = {index_sortie: [id, index]}."""
    del wf[nid]
    for n in wf.values():
        for k, v in n["inputs"].items():
            if isinstance(v, list) and len(v) == 2 and v[0] == nid:
                n["inputs"][k] = mapping[v[1]]


def build_workflow(template, a, style, entry, image_name, seed, prefix):
    wf = copy.deepcopy(template)
    ck = by_title(wf, "Checkpoint")
    if a.ckpt:
        wf[ck]["inputs"]["ckpt_name"] = a.ckpt

    lora = by_title(wf, "Lora")
    if lora:
        if a.lora:
            wf[lora]["inputs"]["lora_name"] = a.lora
            wf[lora]["inputs"]["strength_model"] = a.lora_strength
            wf[lora]["inputs"]["strength_clip"] = a.lora_strength
        else:
            remove_node(wf, lora, {0: [ck, 0], 1: [ck, 1]})

    cnl = by_title(wf, "ControlNet Loader")
    if cnl:
        if not a.controlnet:
            sys.exit("Ce workflow utilise un ControlNet : ajoute --controlnet NOM.safetensors")
        wf[cnl]["inputs"]["control_net_name"] = a.controlnet
        apply_ = by_title(wf, "ControlNet Apply")
        wf[apply_]["inputs"]["strength"] = a.cn_strength

    pos = ", ".join(x for x in [style["positive"], entry["subject"], a.prompt_extra] if x)
    wf[by_title(wf, "Positive Prompt")]["inputs"]["text"] = pos
    wf[by_title(wf, "Negative Prompt")]["inputs"]["text"] = style["negative"]
    wf[by_title(wf, "Input Image")]["inputs"]["image"] = image_name
    s = wf[by_title(wf, "Sampler")]["inputs"]
    s.update(seed=seed, steps=a.steps or style.get("steps", 28), cfg=a.cfg or style.get("cfg", 6.5),
             denoise=a.denoise if a.denoise is not None else style.get("denoise", 0.5),
             sampler_name=a.sampler, scheduler=a.scheduler)
    wf[by_title(wf, "Save")]["inputs"]["filename_prefix"] = prefix
    return wf, pos


def wait_result(server, pid, timeout):
    t0 = time.time()
    while time.time() - t0 < timeout:
        hist = http_json(f"http://{server}/history/{pid}")
        if pid in hist:
            h = hist[pid]
            if h.get("status", {}).get("status_str") == "error":
                raise RuntimeError(f"ComfyUI a signalé une erreur : {json.dumps(h['status'])[:400]}")
            for out in h.get("outputs", {}).values():
                if out.get("images"):
                    return out["images"][0]
        time.sleep(0.7)
    raise TimeoutError("ComfyUI n'a pas répondu à temps")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--server", default="127.0.0.1:8188")
    ap.add_argument("--workflow", default=str(PACK / "comfyui" / "workflows" / "img2img.json"))
    ap.add_argument("--style", default="pixel_dark")
    ap.add_argument("--ckpt", help="nom du checkpoint tel qu'affiché dans ComfyUI")
    ap.add_argument("--lora"); ap.add_argument("--lora-strength", type=float, default=0.8)
    ap.add_argument("--controlnet"); ap.add_argument("--cn-strength", type=float, default=0.6)
    ap.add_argument("--denoise", type=float); ap.add_argument("--steps", type=int); ap.add_argument("--cfg", type=float)
    ap.add_argument("--sampler", default="dpmpp_2m"); ap.add_argument("--scheduler", default="karras")
    ap.add_argument("--seed", type=int, default=1234, help="seed fixe = style cohérent entre les sprites")
    ap.add_argument("--variants", type=int, default=1)
    ap.add_argument("--prompt-extra", default="", help="texte ajouté à tous les prompts")
    ap.add_argument("--category"); ap.add_argument("--only", nargs="*")
    ap.add_argument("--force", action="store_true", help="refait aussi les sprites déjà générés")
    ap.add_argument("--timeout", type=int, default=600)
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()

    styles = load_json(PACK / "styles.json")
    if a.style not in styles:
        sys.exit(f"Style inconnu '{a.style}'. Disponibles : {', '.join(k for k in styles if not k.startswith('_'))}")
    style = styles[a.style]
    template = load_json(a.workflow)
    manifest = load_manifest()
    entries = select(manifest, a.category, a.only)
    if not entries:
        sys.exit("Aucun sprite sélectionné.")

    todo = []
    for e in entries:
        out = PACK / "comfy_output" / e["category"] / f"{e['name']}.png"
        if out.exists() and not a.force and not a.dry_run:
            print(f"  = {e['id']} déjà généré (--force pour refaire)")
            continue
        todo.append(e)

    print(f"{len(todo)} sprite(s) à traiter — style '{a.style}' — serveur {a.server}")
    for i, e in enumerate(todo, 1):
        src = PACK / "comfy_input" / e["category"] / f"{e['name']}.png"
        for v in range(a.variants):
            seed = a.seed + v
            prefix = f"assetpack/{e['category']}_{e['name']}_v{v}"
            if a.dry_run:
                wf, pos = build_workflow(template, a, style, e, "dry.png", seed, prefix)
                print(f"[{i}/{len(todo)}] {e['id']} seed={seed}\n   prompt: {pos}")
                continue
            try:
                name = upload_image(a.server, src, f"pack_{e['category']}_{e['name']}.png")
                wf, _ = build_workflow(template, a, style, e, name, seed, prefix)
                pid = http_json(f"http://{a.server}/prompt", {"prompt": wf, "client_id": uuid.uuid4().hex})["prompt_id"]
                img = wait_result(a.server, pid, a.timeout)
                q = urllib.parse.urlencode({"filename": img["filename"], "subfolder": img.get("subfolder", ""), "type": img.get("type", "output")})
                data = http_bytes(f"http://{a.server}/view?{q}")
            except (urllib.error.URLError, ConnectionError) as ex:
                sys.exit(f"Impossible de joindre ComfyUI sur {a.server} ({ex}). Est-il lancé ?")
            except urllib.error.HTTPError as ex:
                sys.exit(f"ComfyUI a refusé la requête : {ex.read().decode(errors='replace')[:600]}")
            dst = (PACK / "comfy_output" / e["category"] / f"{e['name']}.png") if v == 0 else \
                  (PACK / "comfy_output" / "_variants" / e["category"] / f"{e['name']}__v{v}.png")
            dst.parent.mkdir(parents=True, exist_ok=True)
            dst.write_bytes(data)
            print(f"  [{i}/{len(todo)}] {e['id']} v{v} -> {dst.relative_to(PACK)}")
    print("Terminé. Étape suivante :  python tools/import_back.py --install")


if __name__ == "__main__":
    main()
