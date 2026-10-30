"""Fonctions communes aux outils de l'AssetPack."""
import json
from pathlib import Path

PACK = Path(__file__).resolve().parent.parent
ROOT = PACK.parent
UNITY_SPRITES = ROOT / "Assets" / "_Project" / "Resources" / "Sprites"
SPRITE_PACK_CFG = ROOT / "Assets" / "_Project" / "Editor" / "SpritePack.json"


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save_json(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")


def load_manifest():
    p = PACK / "manifest.json"
    if not p.exists():
        raise SystemExit("manifest.json introuvable : lance d'abord  python tools/prepare.py")
    return load_json(p)


def select(manifest, category=None, only=None):
    """Filtre les entrées du manifest. 'only' = liste de noms (ex: idle_0 slime)."""
    out = []
    for e in manifest["assets"]:
        if category and e["category"] != category:
            continue
        if only and e["name"] not in only:
            continue
        out.append(e)
    return out
