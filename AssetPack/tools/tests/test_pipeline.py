"""
Tests des outils AssetPack (standardize, import_back, ingest, run_comfy, run_pose, run_txt2img).
Aucun ComfyUI n'est nécessaire : un faux serveur local imite l'API. Chaque test travaille sur une COPIE du projet
(dossier temporaire) : le vrai repo n'est jamais modifié.

    python -m unittest discover -s AssetPack/tools/tests -v        (depuis la racine du repo)
"""
import http.server
import json
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import unittest
from pathlib import Path
from urllib.parse import parse_qs, urlparse

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[3]


# ----------------------------------------------------------------------------- faux ComfyUI
class MockComfy:
    """Imite /upload/image, /prompt, /history, /view. Vérifie que tous les liens du graphe sont valides."""

    def __init__(self, generated_png=None, fail=False):
        self.uploads, self.prompts, self.history = {}, [], {}
        self.generated, self.fail = generated_png, fail
        outer = self

        class H(http.server.BaseHTTPRequestHandler):
            def log_message(self, *a):
                pass

            def _send(self, code, body, ctype="application/json"):
                self.send_response(code); self.send_header("Content-Type", ctype)
                self.send_header("Content-Length", str(len(body))); self.end_headers(); self.wfile.write(body)

            def do_POST(self):
                raw = self.rfile.read(int(self.headers["Content-Length"]))
                if self.path == "/upload/image":
                    m = re.search(rb'filename="([^"]+)"\r\nContent-Type: image/png\r\n\r\n(.*?)\r\n--', raw, re.S)
                    outer.uploads[m.group(1).decode()] = m.group(2)
                    return self._send(200, json.dumps({"name": m.group(1).decode(), "subfolder": "", "type": "input"}).encode())
                if self.path == "/prompt":
                    wf = json.loads(raw)["prompt"]
                    for nid, node in wf.items():
                        for k, v in node["inputs"].items():
                            if isinstance(v, list):
                                assert v[0] in wf, f"lien cassé {nid}.{k} -> {v}"
                    outer.prompts.append(wf)
                    pid = f"p{len(outer.prompts)}"
                    loads = [v["inputs"]["image"] for v in wf.values() if v["class_type"] == "LoadImage"]
                    img = loads[-1] if loads else "gen.png"
                    if not loads:
                        outer.uploads["gen.png"] = outer.generated
                    status = {"status_str": "error"} if outer.fail else {"status_str": "success"}
                    outer.history[pid] = {"outputs": {"x": {"images": [{"filename": img, "subfolder": "", "type": "input"}]}}, "status": status}
                    return self._send(200, json.dumps({"prompt_id": pid}).encode())

            def do_GET(self):
                u = urlparse(self.path)
                if u.path.startswith("/history/"):
                    pid = u.path.split("/")[-1]
                    return self._send(200, json.dumps({pid: outer.history[pid]} if pid in outer.history else {}).encode())
                if u.path == "/view":
                    return self._send(200, outer.uploads[parse_qs(u.query)["filename"][0]], "image/png")

        self.srv = http.server.HTTPServer(("127.0.0.1", 0), H)
        self.port = self.srv.server_address[1]
        threading.Thread(target=self.srv.serve_forever, daemon=True).start()

    def close(self):
        self.srv.shutdown()


# ----------------------------------------------------------------------------- base
def png_bytes(img):
    import io
    b = io.BytesIO(); img.save(b, "PNG"); return b.getvalue()


def green_sprite(src_png, size, k, bottom_frac=0.92, noise=True):
    """Imite une sortie ComfyUI : sprite agrandi sur fond vert (bruité), 24 couleurs."""
    src = Image.open(src_png).convert("RGBA")
    fig = src.crop(src.getbbox())
    up = fig.resize((fig.width * k, fig.height * k), Image.NEAREST)
    cv = Image.new("RGB", (size, size), (0, 255, 0))
    cv.paste(up.convert("RGB"), ((size - up.width) // 2, int(bottom_frac * size) - up.height), up.split()[3])
    a = np.array(cv).astype(int)
    if noise:
        bg = (a[..., 0] == 0) & (a[..., 1] == 255) & (a[..., 2] == 0)
        a[bg] += np.random.default_rng(1).integers(-5, 6, (bg.sum(), 3))
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).quantize(colors=24, dither=Image.Dither.NONE).convert("RGB")


class Base(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="assetpack_test_"))
        shutil.copytree(REPO / "AssetPack", self.root / "AssetPack", ignore=shutil.ignore_patterns("__pycache__", "tests"))
        (self.root / "Assets/_Project/Resources").mkdir(parents=True)
        shutil.copytree(REPO / "Assets/_Project/Resources/Sprites", self.root / "Assets/_Project/Resources/Sprites")
        (self.root / "Assets/_Project/Editor").mkdir(parents=True)
        shutil.copy(REPO / "Assets/_Project/Editor/SpritePack.json", self.root / "Assets/_Project/Editor/SpritePack.json")
        self.pack = self.root / "AssetPack"
        self.sprites = self.root / "Assets/_Project/Resources/Sprites"

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def run_tool(self, *args, expect=0):
        r = subprocess.run([sys.executable, *map(str, args)], capture_output=True, text=True, cwd=self.root)
        self.assertEqual(r.returncode, expect, f"code {r.returncode} (attendu {expect})\nSTDOUT:{r.stdout}\nSTDERR:{r.stderr}")
        return r


# ----------------------------------------------------------------------------- tests
class TestStandard(Base):
    def test_sprites_du_repo_respectent_la_norme(self):
        self.run_tool(self.pack / "tools/standardize.py", "--check")

    def test_aller_retour_comfyui_identique(self):
        """Renvoyer les images d'entrée telles quelles à import_back doit redonner exactement les sprites de la norme."""
        self.run_tool(self.pack / "tools/prepare.py")
        for c in ["hero", "enemies", "allies", "env"]:
            (self.pack / "comfy_output" / c).mkdir(parents=True, exist_ok=True)
            for f in (self.pack / "comfy_input" / c).glob("*.png"):
                if not f.name.endswith("_mask.png"):
                    shutil.copy(f, self.pack / "comfy_output" / c / f.name)
        self.run_tool(self.pack / "tools/import_back.py")
        for c in ["hero", "enemies", "allies", "env"]:
            for f in (self.pack / "unity_ready" / c).glob("*.png"):
                a = np.array(Image.open(f).convert("RGBA")).astype(int)
                b = np.array(Image.open(self.pack / "source" / c / f.name).convert("RGBA")).astype(int)
                self.assertEqual(a.shape, b.shape, f.name)
                self.assertTrue((a[..., 3] == b[..., 3]).all(), f"alpha {f.name}")
                op = b[..., 3] > 0
                self.assertEqual(int(np.abs(a[op][:, :3] - b[op][:, :3]).max()), 0, f"couleurs {f.name}")

    def test_standardize_ne_declasse_pas_un_sprite_modifie(self):
        target = self.sprites / "Hero/idle_0.png"
        img = Image.open(target).convert("RGBA"); img.putpixel((10, 40), (1, 2, 3, 255)); img.save(target)   # "frame générée"
        modified = target.read_bytes()
        r = self.run_tool(self.pack / "tools/standardize.py")
        self.assertEqual(target.read_bytes(), modified, "le sprite modifié a été écrasé !")
        self.assertIn("NON écrasés", r.stdout)
        self.run_tool(self.pack / "tools/standardize.py", "--force")
        self.assertEqual(target.read_bytes(), (self.pack / "source/hero/idle_0.png").read_bytes())


class TestIngest(Base):
    def test_ingest_heros_48px_en_cadre_64(self):
        raw = self.root / "raw"; raw.mkdir()
        green_sprite(self.pack / "source/hero/idle_0.png", 768, 11).resize((384, 384), Image.NEAREST).quantize(24, dither=Image.Dither.NONE).convert("RGB").save(raw / "villain_00001_.png")
        self.run_tool(self.pack / "tools/ingest.py", "--names", "villain", "--input", raw)
        o = np.array(Image.open(self.pack / "unity_ready/hero/villain.png").convert("RGBA")).astype(int)   # int : en uint8, o + 50 déborderait et fabriquerait de faux pixels verts
        op = o[..., 3] > 0; ys, xs = np.where(op)
        self.assertEqual(o.shape[:2], (64, 64))
        self.assertTrue(46 <= ys.max() - ys.min() + 1 <= 49, "le personnage debout doit faire ~48 px")
        self.assertEqual(ys.max(), 63, "les pieds touchent le bas du cadre")
        self.assertTrue(30 <= (xs.min() + xs.max()) / 2 <= 33, "centré")
        self.assertEqual(int(((o[..., 3] > 0) & (o[..., 3] < 255)).sum()), 0, "alpha net")
        self.assertLessEqual(len({tuple(p) for p in o[op][:, :3]}), 24)
        self.assertEqual(int(((o[..., 1] > o[..., 0] + 50) & (o[..., 1] > o[..., 2] + 50) & op).sum()), 0, "plus de vert")

    def test_ingest_fit_monstre_32px(self):
        raw = self.root / "raw"; raw.mkdir()
        green_sprite(self.pack / "source/enemies/slime.png", 512, 11, 0.85).save(raw / "slime_00001_.png")
        self.run_tool(self.pack / "tools/ingest.py", "--category", "enemies", "--fit", "--names", "slime", "--input", raw)
        o = np.array(Image.open(self.pack / "unity_ready/enemies/slime.png").convert("RGBA"))
        op = o[..., 3] > 0; ys, xs = np.where(op)
        self.assertEqual(o.shape[:2], (32, 32))
        self.assertEqual(ys.max(), 31, "posé en bas")
        self.assertLessEqual(max(ys.max() - ys.min() + 1, xs.max() - xs.min() + 1), 30)

    def test_ingest_image_vide_message_clair(self):
        raw = self.root / "raw"; raw.mkdir()
        Image.new("RGB", (384, 384), (0, 255, 0)).save(raw / "vide_00001_.png")
        r = subprocess.run([sys.executable, self.pack / "tools/ingest.py", "--names", "vide", "--input", raw], capture_output=True, text=True, cwd=self.root)
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("image vide", r.stderr + r.stdout)


class TestRunners(Base):
    def setUp(self):
        super().setUp()
        self.mock = None

    def tearDown(self):
        if self.mock:
            self.mock.close()
        super().tearDown()

    def start(self, **kw):
        gen = png_bytes(green_sprite(self.pack / "source/enemies/slime.png", 512, 11, 0.85))
        self.mock = MockComfy(generated_png=gen, **kw)
        return f"127.0.0.1:{self.mock.port}"

    def test_run_comfy_img2img(self):
        srv = self.start()
        self.run_tool(self.pack / "tools/prepare.py")
        self.run_tool(self.pack / "tools/run_comfy.py", "--server", srv, "--only", "idle_0", "slime", "--ckpt", "a.safetensors")
        self.assertTrue((self.pack / "comfy_output/hero/idle_0.png").exists())
        self.assertTrue((self.pack / "comfy_output/enemies/slime.png").exists())
        self.assertFalse(any(n["class_type"] == "LoraLoader" for n in self.mock.prompts[0].values()), "sans --lora, le noeud LoRA est retiré")

    def test_run_pose_variantes_et_lora(self):
        srv = self.start()
        (self.pack / "reference").mkdir(exist_ok=True)
        Image.new("RGB", (512, 512), (120, 30, 30)).save(self.pack / "reference/villain_mercenary_locked_reference.png")
        self.run_tool(self.pack / "tools/run_pose.py", "--server", srv, "--frames", "walk_0", "attack1_1", "--variants", "2")
        self.assertEqual(len(self.mock.prompts), 4)
        self.assertTrue((self.pack / "comfy_output/hero_raw/walk_0.png").exists())
        self.assertTrue((self.pack / "comfy_output/_variants/hero_raw/walk_0__v1.png").exists())
        w = self.mock.prompts[0]
        self.assertEqual(w["10"]["class_type"], "ControlNetApplyAdvanced")
        self.assertIn("character facing right", w["3"]["inputs"]["text"])
        self.run_tool(self.pack / "tools/run_pose.py", "--server", srv, "--frames", "idle_0", "--lora", "", "--force")
        self.assertFalse(any(n["class_type"] == "LoraLoader" for n in self.mock.prompts[-1].values()))

    def test_run_txt2img_puis_ingest(self):
        srv = self.start()
        self.run_tool(self.pack / "tools/run_txt2img.py", "--server", srv, "--category", "enemies", "--only", "slime")
        self.assertEqual(self.mock.prompts[0]["6"]["inputs"]["denoise"], 1.0)
        self.run_tool(self.pack / "tools/ingest.py", "--category", "enemies", "--fit", "--names", "slime")
        self.assertEqual(Image.open(self.pack / "unity_ready/enemies/slime.png").size, (32, 32))

    def test_erreur_comfyui_message_lisible(self):
        srv = self.start(fail=True)
        r = self.run_tool(self.pack / "tools/run_txt2img.py", "--server", srv, "--only", "slime", expect=1)
        self.assertIn("ComfyUI a signalé une erreur", r.stderr)
        self.assertNotIn("Traceback", r.stderr)

    def test_workflow_sans_noeud_attendu_message_lisible(self):
        srv = self.start()
        wf = self.pack / "comfyui/workflows/sprite_txt2img.json"
        d = json.loads(wf.read_text()); del d["9"]; wf.write_text(json.dumps(d))                  # retire le noeud « Save »
        r = self.run_tool(self.pack / "tools/run_txt2img.py", "--server", srv, "--only", "slime", expect=1)
        self.assertIn("intitulé « Save »", r.stderr)
        self.assertNotIn("Traceback", r.stderr)


if __name__ == "__main__":
    unittest.main()
