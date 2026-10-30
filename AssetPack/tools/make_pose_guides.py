#!/usr/bin/env python3
"""
Génère les GUIDES DE POSE OpenPose (squelette COCO-18 coloré sur fond noir) pour chaque frame du héros,
au format attendu par ControlNet OpenPose (ex. xinsir_openpose_sdxl_1.0) : un fichier  pose_guides/poseguide_<frame>_768.png  par pose.

Les poses viennent de characters/hero_poses.json (angles faciles à retoucher). Le personnage regarde vers la DROITE.
Le personnage debout mesure 75 % du canvas (576 px à 768) : divisé par 12 => 48 px dans le jeu (norme 48 px, PPU 24).

  python tools/make_pose_guides.py                 # toutes les poses
  python tools/make_pose_guides.py --only walk_0 attack1_1
  python tools/make_pose_guides.py --sheet         # + une planche d'aperçu pose_guides/_sheet.png
"""
import argparse
import math
from PIL import Image, ImageDraw
import numpy as np
from common import PACK, load_json

# couleurs OpenPose (controlnet_aux) et liaisons (indices COCO-18, 0-based)
COLORS = [(255,0,0),(255,85,0),(255,170,0),(255,255,0),(170,255,0),(85,255,0),(0,255,0),(0,255,85),(0,255,170),
          (0,255,255),(0,170,255),(0,85,255),(0,0,255),(85,0,255),(170,0,255),(255,0,255),(255,0,170),(255,0,85)]
LIMBS = [(1,2),(1,5),(2,3),(3,4),(5,6),(6,7),(1,8),(8,9),(9,10),(1,11),(11,12),(12,13),(1,0),(0,14),(14,16),(0,15),(15,17)]
# 0 nez, 1 cou, 2 épaule D, 3 coude D, 4 poignet D, 5 épaule G, 6 coude G, 7 poignet G, 8 hanche D, 9 genou D, 10 cheville D,
# 11 hanche G, 12 genou G, 13 cheville G, 14 oeil D, 15 oeil G, 16 oreille D, 17 oreille G   (D = côté proche de la caméra)

# proportions (fraction de la hauteur du personnage H)
THIGH, SHIN, ANKLE_H, TORSO, HEAD_UP, UPPER, FORE, SHOULDER_HALF, HIP_HALF = .235, .235, .035, .29, .075, .165, .150, .045, .03


def down(a):   # vecteur unitaire (image : y vers le bas) à l'angle a depuis "bas", + = vers la droite
    r = math.radians(a); return np.array([math.sin(r), math.cos(r)])
def up(a):
    r = math.radians(a); return np.array([math.sin(r), -math.cos(r)])


def skeleton(p, S, geo):
    H = geo["figure_height"] * S
    ground = geo["baseline"] * S
    cx = S * 0.5
    hip_h = (THIGH + SHIN + ANKLE_H) * H
    hip = np.array([cx + p["hip"][0] * H, ground - hip_h + p["hip"][1] * H])
    if p.get("lying"):                      # allongé : le corps est à l'horizontale, au sol
        hip = np.array([cx + 0.12 * H, ground - 0.07 * H + (p["hip"][1] - 0.30) * H * 0.0])
    t = p["torso"]
    neck = hip + up(t) * TORSO * H
    head_c = neck + up(t + p["head"]) * HEAD_UP * H
    kp = np.zeros((18, 2))
    kp[1] = neck
    kp[0] = head_c + up(t + p["head"] + 90) * 0.035 * H * (-1 if p.get("lying") else 1) * 0 + np.array([0.035 * H * math.cos(math.radians(t + p["head"])), 0])
    kp[14] = head_c + np.array([0.030 * H, -0.015 * H]); kp[15] = head_c + np.array([0.012 * H, -0.015 * H])
    kp[16] = head_c + np.array([-0.020 * H, 0.0]);       kp[17] = head_c + np.array([-0.035 * H, 0.0])
    # épaules / hanches (vue 3/4 : côté D proche = légèrement en arrière, côté G = en avant)
    perp = up(t + 90)
    kp[2] = neck - perp * SHOULDER_HALF * H * 0 + np.array([-SHOULDER_HALF * H, 0.012 * H])
    kp[5] = neck + np.array([SHOULDER_HALF * H, 0.0])
    kp[8] = hip + np.array([-HIP_HALF * H, 0]); kp[11] = hip + np.array([HIP_HALF * H, 0])
    for sh, el, wr, arm in ((2, 3, 4, p["arm_r"]), (5, 6, 7, p["arm_l"])):
        kp[el] = kp[sh] + down(arm[0]) * UPPER * H
        kp[wr] = kp[el] + down(arm[1]) * FORE * H
    for hp_, kn_, an_, leg in ((8, 9, 10, p["leg_r"]), (11, 12, 13, p["leg_l"])):
        kp[kn_] = kp[hp_] + down(leg[0]) * THIGH * H
        kp[an_] = kp[kn_] + down(leg[1]) * SHIN * H
    if p.get("lying"):                      # on tourne tout le corps à plat (tête à gauche, pieds à droite... vers l'avant = droite)
        pass
    return kp


def draw(kp, S):
    img = Image.new("RGB", (S, S), (0, 0, 0))
    canvas = np.zeros((S, S, 3), dtype=np.float32)
    stick = max(2, round(4 * S / 512))
    for i, (a, b) in enumerate(LIMBS):
        (x0, y0), (x1, y1) = kp[a], kp[b]
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        length = math.hypot(x1 - x0, y1 - y0)
        ang = math.atan2(y1 - y0, x1 - x0)
        t = np.linspace(0, 2 * math.pi, 40)
        ex, ey = (length / 2) * np.cos(t), stick * np.sin(t)
        poly = [(mx + ex[k] * math.cos(ang) - ey[k] * math.sin(ang), my + ex[k] * math.sin(ang) + ey[k] * math.cos(ang)) for k in range(len(t))]
        layer = Image.new("L", (S, S), 0); ImageDraw.Draw(layer).polygon(poly, fill=255)
        m = np.array(layer) > 0
        canvas[m] = COLORS[i]
    canvas *= 0.6                                              # même atténuation que controlnet_aux
    img = Image.fromarray(canvas.astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)
    r = max(2, round(4 * S / 512))
    for i in range(18):
        x, y = kp[i]; d.ellipse([x - r, y - r, x + r, y + r], fill=COLORS[i])
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*"); ap.add_argument("--size", type=int); ap.add_argument("--sheet", action="store_true")
    a = ap.parse_args()
    cfg = load_json(PACK / "characters" / "hero_poses.json")
    geo = cfg["geometry"]; S = a.size or geo["canvas"]
    out = PACK / "pose_guides"; out.mkdir(exist_ok=True)
    made = []
    for name, p in cfg["frames"].items():
        if a.only and name not in a.only:
            continue
        img = draw(skeleton(p, S, geo), S)
        img.save(out / f"poseguide_{name}_{S}.png"); made.append((name, img))
    print(f"{len(made)} guides de pose -> pose_guides/ ({S}px)")
    if a.sheet and made:
        cols = 6; w = 192; rows = (len(made) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * w, rows * (w + 14)), (30, 30, 30)); d = ImageDraw.Draw(sheet)
        for i, (n, im) in enumerate(made):
            sheet.paste(im.resize((w, w), Image.LANCZOS), ((i % cols) * w, (i // cols) * (w + 14) + 14)); d.text(((i % cols) * w + 3, (i // cols) * (w + 14)), n, fill=(255, 255, 255))
        sheet.save(out / "_sheet.png")


if __name__ == "__main__":
    main()
