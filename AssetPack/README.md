# AssetPack — sprites modifiables avec ComfyUI

Les 48 sprites du jeu, prêts à être **restylisés avec ton ComfyUI** puis **réinjectés dans Unity** sans casser le jeu
(mêmes noms de fichiers, même silhouette, même taille en jeu).

```
AssetPack/
  source/        sprites AUX NORMES (voir SPRITE_STANDARD.md) — générés par tools/standardize.py
  source_original/  images brutes d'origine (CC0 + ta palette du démon)
  comfy_input/   ce qu'on envoie à ComfyUI : sprite agrandi sur fond uni (+ <nom>_mask.png)   [généré]
  comfy_output/  TES résultats ComfyUI (<categorie>/<nom>.png)
  unity_ready/   sprites convertis pour Unity                                                  [généré]
  comfyui/workflows/   img2img.json  et  img2img_controlnet.json  (format API)
  assets.json    sujet de chaque sprite (pour les prompts) — à éditer
  styles.json    presets de style (pixel_dark, anime_cel, painted_hd) — ajoute le tien
  manifest.json  tailles / échelles / décalages (généré par prepare.py)
  tools/         prepare.py · run_comfy.py · import_back.py
```
Catégories : `hero` (12 frames du démon) · `enemies` (10 dont le boss) · `allies` (3) · `env` (5) · `fx` (18 effets).
Norme de tailles, PPU et caméra : **`SPRITE_STANDARD.md`**.

## A. Restyler les sprites existants (img2img)
Voir `tools/run_comfy.py` + `comfyui/workflows/img2img*.json`, puis `tools/import_back.py --install`.
```
python AssetPack/tools/prepare.py                         # déjà fait ; régénère comfy_input/ après un changement de norme
python AssetPack/tools/run_comfy.py --ckpt monModele.safetensors --style pixel_dark
python AssetPack/tools/import_back.py --colors 24 --install
```
`import_back.py` remet les résultats **exactement à la taille de la norme** (moyenne par blocs, palette limitée, silhouette d'origine).

## B. Créer les frames d'animation du héros (ton workflow OpenPose, automatisé)
Ton `opencontrol.json` (référence verrouillée + ControlNet OpenPose) est repris dans `comfyui/workflows/hero_pose.json`.
Le réglage du personnage (modèle, LoRA, prompt, seed) est dans `characters/hero.json`, les poses dans `characters/hero_poses.json`.

1. **Référence** : dépose `reference/villain_mercenary_locked_reference.png`.
2. **Guides de pose** (26 déjà générés dans `pose_guides/`, squelettes OpenPose 768 px, personnage vers la **droite**) :
   `python AssetPack/tools/make_pose_guides.py --sheet` (aperçu : `pose_guides/_sheet.png`). Ce sont des poses de départ : retouche les angles dans `hero_poses.json` et relance.
3. **Génération** (ComfyUI lancé) :
   ```
   python AssetPack/tools/run_pose.py --dry-run --frames walk_0           # voir le prompt
   python AssetPack/tools/run_pose.py --frames walk_0 walk_1 walk_2 walk_3 --variants 4
   ```
   Sorties : `comfy_output/hero_raw/<frame>.png` (384 px, 24 couleurs, fond vert) ; les variantes sont dans `comfy_output/_variants/`.
4. **Mise aux normes** : `python AssetPack/tools/ingest.py --names walk_0 walk_1 walk_2 walk_3 --install`
   → détourage du fond vert, réduction à la grille (cadre 64×64, personnage debout = **48 px**), 24 couleurs, alpha net, posé en bas du cadre.
5. Dans Unity (menu **Jeu > 0 - Importer les sprites** si besoin) : les clips de `hero.json` prennent **automatiquement** les nouvelles frames.

Frames reconnues : `idle_0..2, walk_0..3, run_0..3, jump_0..2, guard, cast, attack1_0/1, attack2_0/1, attack3_0/1, cast_wave, hurt, down, victory`.
Un clip écrit `"attack1_1|jump_0"` = « utilise `attack1_1` s'il existe, sinon `jump_0` » : tu remplaces les images au fil de l'eau.

Géométrie à connaître : le canvas ComfyUI entier ↔ cadre 64×64 ; le personnage debout fait 75 % du canvas (576 px à 768 → ÷12 = 48 px).
Garde `ImageScale` à 384 (ou 768) et le fond **vert uni** comme dans ton workflow.

## C. Régénérer les monstres et alliés (texte → image, comme ton pixelArt.json)
Remplace les sprites Kenney agrandis (contours de 2 px) par de vrais sprites à la taille de la norme.
```
python AssetPack/tools/run_txt2img.py --dry-run --only slime                  # voir le prompt
python AssetPack/tools/run_txt2img.py --category enemies --variants 4         # 4 essais par monstre
python AssetPack/tools/ingest.py --category enemies --fit --names slime bat spider --install              # cadre 32x32
python AssetPack/tools/ingest.py --category enemies --fit --frame 48 --names brute_boss --install         # boss 48x48
python AssetPack/tools/ingest.py --category allies --fit --names knight mage healer --install
```
Les sujets sont dans `assets.json`, les réglages (modèle, LoRA, prompt, seed) dans `characters/sprites_txt2img.json`, le graphe dans
`comfyui/workflows/sprite_txt2img.json`. Le **fond vert uni** est indispensable : le détourage en dépend. `--fit` rogne le sujet et l'ajuste au cadre
(30 px utiles dans 32), pose les pieds en bas, limite la palette et rend l'alpha net. Choisis la meilleure variante dans
`comfy_output/_variants/<cat>_raw/` et copie-la sur `comfy_output/<cat>_raw/<nom>.png` avant d'ingérer.

## Réglages qui comptent
| Réglage | Conseil |
|---|---|
| `denoise` (styles.json / `--denoise`) | 0.35 = fidèle au sprite, 0.5 = bon compromis, 0.7 = très libre (la pose peut dériver) |
| `--seed` | garde-la **fixe** : même seed + même préfixe de prompt = style cohérent entre tous les sprites |
| ControlNet canny/lineart | `img2img_controlnet.json` + `--controlnet` : garde la silhouette → indispensable pour les **frames d'animation** (walk_0..3, idle_0..2) qui doivent rester proches les unes des autres |
| Pixel-art | Stable Diffusion ne fait pas de vrai pixel-art : utilise un checkpoint/LoRA pixel-art, puis `--colors 16..32` pour re-quantifier proprement |
| Alpha | par défaut la silhouette d'origine est conservée. Si tu changes la forme (cornes plus grandes...) : `--alpha key` détoure d'après le fond gris |
| Tuiles (`env`) | le sol et le mur doivent être *seamless* : active le tiling dans ComfyUI (ex. nœud « Seamless Tile ») ou garde les originaux |
| Effets (`fx`) | lumière blanche sur fond noir ; l'alpha = luminance et Unity les colore. Garde-les blancs (`--fx-color white`) pour pouvoir les teinter par arte |
| Mode `hd` | oblige à convertir **toute la catégorie d'un coup** (sinon tailles incohérentes) ; les PPU sont ajustés tout seuls |

## Ajouter ou renommer un sprite
1. Dépose le PNG dans `source/<cat>/` et dans `Assets/_Project/Resources/Sprites/<Dossier>/` (même nom).
2. Ajoute sa ligne dans `assets.json`, relance `prepare.py`.
Les scripts du jeu chargent les sprites **par nom** : `Hero/idle_0`, `Enemies/slime`, `Fx/slash_03`...

## Ajouter ton style
Dans `styles.json`, copie un preset, renomme-le (ex. `"slayer"`), mets ton préfixe de prompt, et lance `--style slayer`.
Un LoRA entraîné sur le style voulu donnera bien plus de cohérence que le prompt seul.

## Licences
Sources : Kenney (Tiny Dungeon, Particle Pack) et OpenGameArt (Icy Tower Demon) — **CC0**, voir `Assets/_Project/CREDITS.md`.
Les images générées dépendent aussi de la licence de **ton checkpoint / LoRA** : vérifie-la avant une sortie commerciale.
