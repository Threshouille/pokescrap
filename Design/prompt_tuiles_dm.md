# Prompt Claude Design : décor « prairie » en cases, style Pokémon Donjon Mystère

Je fais un roguelike d'action vu de dessus avec les sprites de Pokémon de *Pokémon Donjon Mystère : Explorateurs du
Ciel* (DS). Je veux un **décor original de prairie** (herbe, haies, rivière, chemin de terre, pont) **dans le style
exact des décors de ce jeu** : mêmes couleurs, même façon de dessiner les pixels, et **les mêmes formes en cases
carrées** (murs, rivières et bords qui suivent la grille, avec des coins arrondis et des angles rentrants), plus
quelques éléments posés librement par-dessus (arbres, fleurs, rochers).

## Pièces jointes
- `dm_references_x3.png` : 5 décors du jeu original (agrandis ×3) : prairie fleurie avec arbres à fruits, herbe haute
  et eau bleue, jungle, forêt de sapins, plage. **C'est le style à reproduire** ; les deux premiers sont les plus
  proches.
- `dm_palette.png` / `dm_palette.txt` : les couleurs des deux premiers. **Pars de cette palette** ; n'ajoute que les
  teintes qui manquent (bois du pont), dans le même esprit.
- `pokemon_dm_echelle_x4.png` : deux Pokémon du jeu (×4), pour l'échelle.
- `autotuile_gabarit_x8.png` (et `_x1`) : **le gabarit des auto-tuiles** (voir plus bas).

## Le style à reproduire
- Couleurs franches, saturées et lumineuses ; ombres teintées (vert bleuté, brun-rouge), jamais grises ou noires.
- Contours sombres d'un pixel autour des masses de végétation et des rochers, dans une teinte foncée de leur couleur.
- Végétation en petites boules et touffes empilées, reflet clair sur le haut de chaque boule (lumière du haut à gauche).
- Sols à petits motifs denses (brins d'herbe, mottes, fleurettes de 3 à 5 px), contrastes plus doux que la végétation.
- Eau bleue lumineuse à vaguelettes claires, comme le décor « herbe haute et eau ».
- Pixel art net à l'échelle 1 : **24 px = 1 case**, pas d'anticrénelage, alpha 0 ou 255 uniquement.

## Partie 1 : les cases

### Le format des auto-tuiles (important)
Le jeu raccorde automatiquement une matière (haie, eau, chemin) selon ses 8 voisines, en découpant chaque case en
**4 quarts de 12×12 px**. Pour chaque matière, dessine **un seul bloc de 48×72 px** (2 cases de large, 3 de haut) qui
suit exactement le gabarit joint :
- **en haut à gauche (24×24)** : la matière seule, entourée de vide de tous côtés (une case isolée, aux coins arrondis) ;
- **en haut à droite (24×24)** : la matière pleine, avec à chacun de ses 4 coins un **angle rentrant** (le petit coin du
  gabarit) : c'est ce que montre une case de matière dont seule la voisine en diagonale est vide ;
- **en bas (48×48, 2×2 cases)** : un grand carré de la matière, avec **ses bords et ses 4 coins arrondis** sur le
  pourtour et **la matière pleine au centre**. Le jeu prend les coins, les bords et le centre dans ces quarts : chaque
  quart de 12×12 doit donc se raccorder à ses voisins **quel que soit l'ordre** dans lequel le jeu les assemble (le
  centre et les bords doivent se répéter sans couture).
- Le **vide** autour de la matière est **transparent** : le jeu dessine l'herbe dessous. La limite visible de la matière
  passe à environ 3 px du bord de la case, comme sur le gabarit, avec le contour et l'ombre portée côté herbe dessinés
  **dans** la case de la matière.

### Fichiers des cases
| Fichier | Taille | Description |
|---|---|---|
| `sol_herbe_1.png` à `sol_herbe_3.png` | 24×24 | Herbe de prairie (le sol), 3 variantes qui se raccordent entre elles dans tous les sens ; la 1re est la plus calme, les autres ajoutent quelques brins ou mottes. |
| `sol_herbe_fleurs_1.png`, `sol_herbe_fleurs_2.png` | 24×24 | La même herbe avec des fleurettes rouges, roses ou jaunes, comme la prairie fleurie (variantes rares). |
| `haie.png` | 48×72 (auto-tuile) | Le mur : une haie dense de buissons et de petits arbres en boules, comme les bords de la prairie fleurie ; on ne la traverse pas. |
| `eau_1.png`, `eau_2.png`, `eau_3.png` | 48×72 (auto-tuile, ×3) | La rivière : eau bleue avec vaguelettes, bord de berge (un liseré de terre ou de pierres et d'écume) vers l'herbe. Trois images d'animation où **seules les vaguelettes et l'écume bougent** (même forme de berge), jouées en boucle à 200 ms. |
| `chemin.png` | 48×72 (auto-tuile) | Un chemin de terre battue posé sur l'herbe, bord irrégulier de quelques pixels, petits cailloux. |
| `pont_horizontal.png` | 72×24 (3 cases) | Un pont de bois franchissant la rivière de gauche à droite, posé sur l'eau : case de gauche (début avec poteaux), case du milieu (qui se répète), case de droite (fin). Planches perpendiculaires au trajet. |
| `pont_vertical.png` | 24×72 (3 cases) | Le même pont franchissant de haut en bas : case du haut, du milieu (répétée), du bas. |

## Partie 2 : les éléments posés librement
Comme avant : chacun seul sur fond transparent, **point d'ancrage au milieu du bas** (là où il touche le sol), triés
en profondeur par le jeu ; les ombres en fichiers séparés, **silhouettes #000000** (le jeu les rend semi-transparentes).
Herbes et fleurs dessinées seules et droites, sans sol ni ombre sous elles (le jeu les fait onduler au vent). Les arbres
en deux morceaux (tronc, feuillage) : le jeu fait bouger le feuillage et le rend transparent quand un Pokémon passe
derrière.

| Fichier | Taille | Description |
|---|---|---|
| `arbre_1` à `arbre_3` : `_tronc.png`, `_feuillage.png`, `_ombre.png` | 3 à 5 cases de haut en tout | Grands arbres dans le style des arbres de la prairie fleurie (feuillage en boules, quelques fruits rouges sur l'un d'eux). |
| `buisson_1.png` à `buisson_3.png` (+ `_ombre`) | 24 à 40 px de large | Buissons ronds, un avec des baies. |
| `rocher_petit.png`, `rocher_moyen.png`, `rocher_grand.png` (+ `_ombre`) | 12, 24, 40 px de large | Rochers. |
| `herbe_touffe_1.png` à `herbe_touffe_4.png` | 8 à 16 px | Touffes d'herbe haute. |
| `fleur_rouge.png`, `fleur_rose.png`, `fleur_jaune.png`, `fleur_blanche.png` | 6 à 12 px | Fleurs du style de la prairie fleurie. |
| `champignons_1.png`, `champignons_2.png` | 8 à 14 px | Petits champignons. |
| `roseau_1.png`, `roseau_2.png` | 10×24 environ | Roseaux de berge. |
| `nenuphar_1.png`, `nenuphar_2.png` | 12 à 18 px | Nénuphars (posés à plat sur l'eau, sans ombre). |
| `souche.png` (+ `_ombre`) | 24 px de large | Souche. |

## À livrer
- Une archive `.zip` avec tous les PNG, `palette.png` et un fichier **`assets.json`** listant chaque fichier avec
  `file`, `kind` (`tile`, `autotile`, `bridge`, `prop`, `shadow`), `width`, `height`, et pour les éléments posés
  `anchor` (pixels depuis le **bas gauche**), `footprint` (rayon au sol en cases, pour arbres, buissons, rochers,
  souche) et `canopy_offset` pour les feuillages ; pour l'eau, `frames` et `frame_duration_ms`.
- **`assemblage.png`** (480×264 à l'échelle 1 = 20×11 cases, plus une version ×3) : une scène construite **sur la grille**
  avec ces fichiers : de l'herbe, une haie en bordure avec un renfoncement (pour montrer coins et angles rentrants), une
  rivière qui fait un coude, un chemin de terre qui la franchit sur le pont, et les éléments posés par-dessus.
- **`comparaison.png`** : l'assemblage à côté du décor « prairie fleurie » joint, à la même échelle, pour vérifier que
  les deux semblent venir du même jeu.
