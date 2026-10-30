# Jeu-Unity — Le Vilain Isekai

Action RPG façon **Tales of Symphonia** (LMBS : *Linear Motion Battle System*) en sprites 2D.
Un vilain réincarné dans un autre monde conquiert monstres, donjons et boss de monde en monde.

## Mise en route
1. Ouvrir ce dossier avec **Unity Hub** (*Add > Add project from disk*) — éditeur **6000.5.3f1** (URP 2D, Input System déjà configurés).
2. Menu **Jeu > 2 - Créer l'arène de test** (réimporte les sprites, crée les artes et la scène), puis **Play**.
   Sprites flous ? **Jeu > 0 - Importer les sprites**. Cliquer dans la fenêtre Game pour donner le focus clavier.

## Le combat (LMBS)
- **Rencontres** : approche un groupe de monstres → *ENCOUNTER !* → le combat démarre jusqu'à leur défaite → écran de **Victoire** (EXP, Gald, combo max, dégâts, rang).
- **Ligne de combat** : en combat tu es verrouillé sur une cible. `Q/D` (←/→) te font avancer/reculer le long de la ligne vers elle. **Maj** = course libre 3D. **Tab** change de cible.
- **Artes standard** — `J` + direction : neutre = attaque normale (combo x3), `→` Croc démoniaque, `↑` Lame ascendante (projette en l'air), `↓` Tempête de lames.
- **Artes techniques** (TP) — `K` + direction : neutre **Photon**, `→` Sphère démoniaque, `↑` Foudre, `↓` Soin ténébreux.
  *(→ = vers la cible, ↑ = Z, ↓ = S)*
- **Enchaînements** : un coup qui touche ouvre une fenêtre d'annulation → enchaîne un arte pendant la récupération de l'attaque ou d'un arte précédent. Un compteur de **HITS** s'affiche ; les très longs combos réduisent les dégâts.
- **Équilibre (jauge jaune)** : les ennemis vacillent quand elle tombe à zéro ; les lanceurs projettent en l'air pour **jongler**.
- **Garde** `Ctrl` (-80 % de dégâts, pas de vacillement). Garde déclenchée juste avant un coup = **garde parfaite**.
- **Overlimit** : la jauge se remplit en frappant et en encaissant → `L` : TP infinis, +30 % dégâts, +25 % vitesse pendant 12 s.
- **Groupe** : 3 alliés IA (Chevalier, Mage, Soigneuse) qui focalisent ta cible, lancent leurs artes et te soignent.
- **Objet** `H` : Pomme gel (+40 % PV). **Niveaux** : EXP à la victoire, PV/TP/dégâts augmentent.
- **Boss** : Cyclope en super armure avec onde de choc de zone télégraphée (cercle rouge).

Manette : stick = ligne, X = attaque, B = technique, A = saut, LT = garde, LB = course libre, RB = cible, RT = Overlimit, Y = objet.

## Structure
`Combat/` dégâts, équilibre, artes, jongle · `Player/` héros, TP/niveaux, cible, artes · `Allies/` IA du groupe · `Enemies/` IA · `World/` rencontres, résultats, mondes · `UI/` HUD · `Core/` entrées, caméra, sprites, FX.


## Animations (données, pas du code)
Chaque personnage est animé « pose à pose » par des clips JSON : `Assets/_Project/Resources/Animations/hero.json` et `creature.json`
(idle, marche, **marche arrière** quand on recule le long de la ligne, course, saut, réception, garde, 3 attaques, un clip par type d'arte,
touché/projeté/à terre/relevé/mort/victoire ; démarches propres à chaque monstre). Chaque frame = sprite + décalage + rotation + échelle + teinte + évènement.
Les coups de l'attaque normale sont **calés sur le moment du coup** (évènement `hit`), les artes sur `release`.
Modifier un clip = éditer le JSON (voir `AnimData.cs` pour les champs). Test automatique du lecteur : voir `Scripts/Animation/`.

## Normes de sprites
PPU **24** pour tout le pixel-art, caméra pixel-perfect (orthographique, échelle entière), cadres 64/32/48 px : voir `AssetPack/SPRITE_STANDARD.md`.

## Remplacer les graphismes (ComfyUI)
Le dossier **`AssetPack/`** contient tous les sprites, les workflows ComfyUI et les scripts pour les restylisés puis les réinjecter dans le jeu : voir `AssetPack/README.md` (restyle img2img **et** génération des frames du héros avec OpenPose).

## Assets (CC0)
Héros démon (OpenGameArt), monstres/décors Tiny Dungeon et effets Particle Pack (Kenney). Voir `Assets/_Project/CREDITS.md`.
Les sprites pixel art partagent 24 pixels par unité : les personnages d'environ 48 px restent proches de 2 unités et les tuiles de 16 px gardent la même densité. Le filtrage est au plus proche voisin ; les effets haute résolution restent bilinéaires.

## Pas encore fait
Mystic arte d'Overlimit, menu (équipement/stratégie/objets), carte du monde et sauvegarde, donjons à plusieurs salles, changement de personnage contrôlé.
