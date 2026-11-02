# Norme de sprites du projet

Basée sur <https://pixor.art/fr/learn/sprite-sizes/> : **un seul pixel d'art partout**, jamais d'échelle non entière,
filtre « plus proche voisin », pivot sur un pixel. Tout est vérifiable : `python AssetPack/tools/standardize.py --check`.

## Les chiffres du projet
| | Valeur |
|---|---|
| **PPU (pixels par unité)** | **24** pour tout le pixel-art (Hero, Enemies, Allies, Env) → 1 pixel d'art = 1/24 u. Réglé dans `Assets/_Project/Editor/SpritePack.json` |
| Résolution de référence | **320×180** (×6 en 1080p, ×8 en 1440p…) |
| Héros | cadre **64×64**, debout = **48 px** de haut (2 u), pivot bas-centre |
| Ennemis / alliés | **32×32** |
| Boss | **48×48** (`brute_boss`) — un grand monstre est un sprite plus grand, **jamais** un agrandissement ×1,8 |
| Tuiles / décors | **32×32** (sol et mur se répètent par tuiles, échelle 1) |
| Effets (`Fx`) | **64×64**, alpha en 4 niveaux, PPU 64 (1 u à l'échelle 1) — exemptés de la règle d'échelle car ils grossissent/rétrécissent en jouant |
| Filtrage | Point, sans compression, sans mipmaps (réglé par l'importeur) |
| Largeur de cadre | toujours **paire** → le pivot bas-centre tombe sur un pixel entier |

## Caméra pixel-perfect
`CameraFollow` (case `pixelPerfect`, activée par défaut) utilise une caméra **orthographique** dont la taille garantit un
**nombre entier** de pixels d'écran par pixel d'art, aligne sa position et celle des sprites sur la grille de pixels, et
oriente les sprites parallèlement à l'écran. Décocher la case pour revenir à la perspective (les pixels changent alors de taille avec la profondeur).
Si tu changes le PPU, mets le même dans `CameraFollow.pixelsPerUnit` et `PixelView.PPU`.

## Sens des sprites
L'art du démon **regarde vers la droite** (mesuré : yeux et cornes décalés de +6 px dans les frames de marche/saut).
`hero.json` déclare `"facesRight": true` ; le jeu retourne l'image seulement quand le personnage regarde à gauche.
Les images de face (idle, garde, incantation) sont symétriques : le sens est porté par l'inclinaison et l'élan des clips.
Pour un nouveau héros : dessine/génère **toujours vers la droite**, sinon change `facesRight`.

## Dette connue
- Les sprites Kenney 16 px ont été agrandis en 32 px par **Scale2x** (vrais pixels ajoutés, même taille de pixel). Leurs contours font 2 px contre 1 px pour le démon : à régénérer à la taille standard avec le pipeline ComfyUI.
- Le boss est un Scale3x du cyclope : provisoire, même remarque.
- Effets pixelisés : si tu préfères les halos lisses d'origine, mets `"method": "keep"` pour `fx` dans `standard.json`.

## Ajouter un sprite aux normes
Dépose l'image brute dans `AssetPack/source_original/<cat>/`, ajoute-la dans `assets.json`, puis
`python AssetPack/tools/standardize.py` (met à la bonne taille et installe dans Unity).
Pour des images générées par ComfyUI : `tools/ingest.py` (héros) ou `tools/import_back.py` (restyle).
