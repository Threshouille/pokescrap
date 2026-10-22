# Prompt Claude Design : assets de l'île, style du concept A (étape 2)

Parfait, je retiens le **concept A** (sans contours, façon *Eastward*). Produis maintenant les **assets séparés** qui
permettront de reconstruire ce coin d'île dans le jeu, en gardant exactement le même style, la même palette, la même
lumière (venant du haut à gauche) et la même densité de pixels.

## Comment le jeu va les assembler (important pour le format)
- Le **sol** n'est pas fait de tuiles : le jeu répète des **textures sans raccord** et les mélange avec des contours
  irréguliers calculés par un shader (prairie, chemin, berge). Les textures doivent donc être calmes, sans motif qui
  saute aux yeux quand il se répète, et se raccorder parfaitement sur les 4 bords.
- Les **éléments de décor** (herbes, fleurs, arbres, rochers…) sont posés librement par le jeu, triés en profondeur
  (ce qui est plus bas à l'écran passe devant). Chacun a un **point d'ancrage** à son pied, là où il touche le sol.
- Le **vent** est fait par le jeu : il fait onduler le haut des herbes, des fleurs et des feuillages, le pied restant
  fixe. Les herbes et fleurs doivent donc être dessinées **seules, droites, sans sol ni ombre sous elles**.
- Les **arbres** sont en **deux morceaux** : tronc et feuillage. Le jeu fait bouger le feuillage au vent et le rend
  transparent quand un Pokémon passe derrière.
- Les **ombres** portées sont des fichiers à part : une **silhouette d'une seule couleur** (#000000), que le jeu
  affiche en semi-transparence. Aucune ombre semi-transparente dessinée dans les sprites eux-mêmes.
- L'**eau** est animée par le jeu (écoulement, scintillement, écume le long des berges) à partir de textures.

## Contraintes communes
- Pixel art net à l'échelle 1 : **24 px = 1 case**. Pas d'anticrénelage ni de flou, alpha 0 ou 255 uniquement.
- **Palette du concept A**, partagée par tous les fichiers (livre-la aussi en `palette.png`, une case de 4×4 px par
  couleur, et en liste hexadécimale).
- Sans contour noir ; ombrage intégré cohérent avec une lumière du haut à gauche.
- Vue de 3/4 dessus : sol vu de dessus, face avant visible pour les éléments hauts.
- Fond transparent pour tout sauf les textures de sol et d'eau.

## Liste des fichiers

### Textures de sol (96×96 px, raccord parfait sur les 4 bords, opaques)
| Fichier | Description |
|---|---|
| `sol_herbe_a.png` | Herbe de prairie, teinte principale, petits brins et variations douces, peu contrastée. |
| `sol_herbe_b.png` | Même herbe, un peu plus claire et chaude (zones ensoleillées), pour varier la prairie. |
| `sol_herbe_c.png` | Même herbe, plus sombre et fraîche (sous-bois, ombre des arbres). |
| `sol_chemin.png` | Terre battue du chemin, quelques cailloux épars. |
| `sol_berge.png` | Terre humide et galets de berge. |

### Eau (96×96 px, raccord parfait sur les 4 bords)
| Fichier | Description |
|---|---|
| `eau_fond.png` | Couleur de l'eau vue de dessus, profondeur légère, opaque. |
| `eau_reflets.png` | Reflets et rides de surface seuls (traits clairs) sur fond transparent ; le jeu les fait défiler par-dessus le fond. |
| `eau_ecume.png` | Écume claire et mousseuse, sur fond transparent ; le jeu l'affiche le long des berges. |
| `eau_scintillement.png` | Planche de 4 images de 8×8 px (une ligne, 32×8) : petit éclat de lumière qui apparaît et disparaît. |

### Herbes et fleurs (seules, droites, sans sol ni ombre ; ancrage au milieu du bas)
| Fichier | Taille | Description |
|---|---|---|
| `herbe_touffe_1.png` à `herbe_touffe_4.png` | 10 à 16 px de large, 8 à 14 px de haut | Touffes d'herbe plus hautes que le sol, formes variées. |
| `herbe_haute_1.png`, `herbe_haute_2.png` | 16×20 px environ | Herbes hautes et fines, quelques épis. |
| `fleur_<sorte>_1.png`, `fleur_<sorte>_2.png` | 6 à 12 px | 4 sortes de fleurs du concept (par exemple `blanche`, `jaune`, `rose`, `bleue`), 2 variantes chacune (une fleur seule, un petit bouquet), tige comprise. |
| `roseau_1.png`, `roseau_2.png` | 10×24 px environ | Roseaux de berge. |

### Arbres (ancrage au milieu du bas du tronc)
Pour chacun des 3 arbres `arbre_1` à `arbre_3` (tailles et silhouettes différentes, 3 à 5 cases de haut au total) :
| Fichier | Description |
|---|---|
| `arbre_N_tronc.png` | Tronc avec l'évasement des racines, environ 24 à 40 px de large. |
| `arbre_N_feuillage.png` | Feuillage seul, généreux, environ 72 à 120 px de large. |
| `arbre_N_ombre.png` | Silhouette #000000 de l'ombre portée au sol (vers le bas à droite), vue de dessus. |

### Autres éléments (ancrage au milieu du bas ; chacun avec son `_ombre.png` en silhouette #000000)
| Fichier | Taille | Description |
|---|---|---|
| `buisson_1.png` à `buisson_3.png` | 24 à 40 px de large | Buissons arrondis, deux avec quelques baies ou fleurs. |
| `rocher_petit.png`, `rocher_moyen.png`, `rocher_grand.png` | environ 12, 24 et 40 px de large | Rochers moussus. |
| `galets_1.png`, `galets_2.png` | 12 à 20 px | Petits groupes de galets (sans ombre séparée). |
| `souche.png`, `tronc_couche.png` | 24 et 48 px de large | Souche et tronc couché moussus. |
| `champignons_1.png`, `champignons_2.png` | 8 à 14 px | Petits groupes de champignons féeriques (sans ombre séparée). |
| `nenuphar_1.png`, `nenuphar_2.png` | 12 à 18 px | Nénuphars, l'un fleuri, posés à plat sur l'eau (sans ombre). |

### Pont (franchit une rivière de 3 cases de large)
| Fichier | Description |
|---|---|
| `pont_horizontal.png` | Pont de bois vu de 3/4 dessus, franchissant de gauche à droite (planches perpendiculaires au trajet), environ 5 cases de long sur 2 de large, rambardes comprises. |
| `pont_vertical.png` | Le même pont franchissant de haut en bas. |
| `pont_horizontal_ombre.png`, `pont_vertical_ombre.png` | Ombres sur l'eau, silhouettes #000000. |

### Bord de l'île et ciel
| Fichier | Taille | Description |
|---|---|---|
| `bord_levre.png` | 96×16 px, raccord gauche-droite | Le rebord du terrain au bord du vide : herbe qui déborde un peu, terre visible. |
| `bord_roche.png` | 96×96 px, raccord gauche-droite et haut-bas | La roche du dessous de l'île qui plonge, de plus en plus sombre vers le bas, quelques strates et pierres. |
| `bord_racines_1.png` à `bord_racines_3.png` | 8 à 16 px de large, 24 à 48 px de haut | Racines et lianes qui pendent sous le rebord. |
| `nuage_loin_1.png` à `nuage_loin_3.png` | 48 à 96 px de large | Nuages lointains, pâles, peu détaillés. |
| `nuage_milieu_1.png` à `nuage_milieu_3.png` | 96 à 160 px de large | Nuages intermédiaires. |
| `nuage_proche_1.png`, `nuage_proche_2.png` | 160 à 240 px de large | Gros nuages proches, juste sous l'île, plus détaillés. |

### Petites animations (une ligne d'images, de gauche à droite)
| Fichier | Images | Taille d'image | Durée | Description |
|---|---|---|---|---|
| `papillon_1.png`, `papillon_2.png` | 4 | 8×8 px | 90 ms | Papillon qui bat des ailes, vu de dessus, deux couleurs. |
| `luciole.png` | 4 | 6×6 px | 120 ms | Petite lumière féerique qui pulse. |
| `pollen.png` | 4 | 3×3 px | 150 ms | Grain de pollen lumineux qui scintille. |

## À livrer
- Une archive `.zip` avec tous les PNG, `palette.png` et un fichier **`assets.json`** qui donne pour chaque fichier :
  `file`, `kind` (`texture`, `prop`, `shadow`, `anim`), `width`, `height`, `anchor` (point d'ancrage en pixels depuis
  le **bas gauche** de l'image), et selon le cas `frame_count` / `frame_duration_ms` (animations), `footprint` (rayon au
  sol en cases, pour les collisions des arbres, buissons, rochers, souche et tronc couché), `canopy_offset` (pour un
  feuillage : décalage en pixels de son ancrage par rapport à celui du tronc).
- Une image **`assemblage.png`** (480×270 à l'échelle 1, plus une version ×3) qui recompose une scène proche du concept A
  **uniquement avec ces fichiers**, pour vérifier qu'ils suffisent et qu'ils vont bien ensemble.
