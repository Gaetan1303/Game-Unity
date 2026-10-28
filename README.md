# Jeu-Unity — Le Vilain Isekai

Action RPG façon **Tales of Symphonia** (LMBS : *Linear Motion Battle System*) en sprites 2D.
Un vilain réincarné dans un autre monde conquiert monstres, donjons et boss de monde en monde.

## Mise en route (Unity 6)
1. Copier le dossier `Assets/` dans un projet Unity 6 (URP ou Built-in).
2. **Project Settings > Player > Active Input Handling** = `Input System Package (New)` (ou `Both`).
3. Menu **Jeu > 2 - Créer l'arène de test** (réimporte les sprites, crée les artes et la scène), puis **Play**.
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

## Assets (CC0)
Héros démon (OpenGameArt), monstres/décors Tiny Dungeon et effets Particle Pack (Kenney). Voir `Assets/_Project/CREDITS.md`.
Les sprites pixel art partagent 24 pixels par unité : les personnages d'environ 48 px restent proches de 2 unités et les tuiles de 16 px gardent la même densité. Le filtrage est au plus proche voisin ; les effets haute résolution restent bilinéaires.

## Pas encore fait
Mystic arte d'Overlimit, menu (équipement/stratégie/objets), carte du monde et sauvegarde, donjons à plusieurs salles, changement de personnage contrôlé.
