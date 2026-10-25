# Prompt Claude Design : sceaux des grottes (2 lots)

Les deux lots s'envoient séparément. Le lot 1 suffit pour remplacer les formes provisoires actuelles ; le lot 2 habille les portes selon le biome.

---

## Lot 1 : mécanismes et repères communs (12 planches)

Crée des planches en pixel art pour un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Sur une île, de **petites grottes scellées** se dressent sur un plateau de roche brune ; leur entrée est **murée**. Devant chaque grotte, un **mécanisme** fait comprendre l'énigme qui l'ouvre : une stèle à frapper avec une attaque du bon type, des braseros à allumer tous en même temps, des dalles de pression où se tenir, des coquillages à frapper du plus petit au plus grand. Quand l'énigme est résolue, le mur s'effondre dans un nuage de poussière et un escalier apparaît au fond de la grotte. Le jeu pose ces éléments case par case (1 case = 24 px), vue de 3/4 (on voit le dessus et la face avant des objets).

### Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement** (sauf mention), 4 à 6 couleurs par planche, contour sombre d'une couleur.
- Éclairage venant du haut à gauche. Les objets qui se dressent ont leur **pied au milieu du bord bas** de l'image (le jeu les trie par le pied avec les Pokémon).
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms` (une valeur par image), `loop`, `palette`.

### Liste

| Fichier | Élément | Taille d'une image | Images | Description visuelle | Raccord / transparence |
|---|---|---|---|---|---|
| `seal_stele.png` | Stèle (mécanisme à frapper) | 24×36 | 1 | petite stèle de pierre grise trapue, sur un socle, avec un **creux rond** au sommet (le jeu y pose l'icône de la condition) ; quelques fissures et de la mousse | fond transparent, pied au bas de l'image |
| `seal_stele_hit.png` | Stèle touchée (bon type) | 24×36 | 4 (~60 ms) | la stèle vibre, des éclats lumineux blancs jaillissent du creux | idem, jouée une fois |
| `seal_stele_open.png` | Stèle résolue | 24×36 | 4 (boucle, ~150 ms) | la stèle, le creux brille d'une lueur dorée qui pulse doucement | idem |
| `seal_no_effect.png` | Sans effet | 24×24 | 5 (~70 ms) | petit nuage gris terne et deux étoiles grises qui retombent (mauvais type, mauvais coquillage) | fond transparent, jouée une fois |
| `seal_icons.png` | Icônes de condition | 16×16 | 9 | dans l'ordre : soleil, nuage de pluie, tempête de sable, grêlon, croissant de lune, sablier, rune (spirale), trou de serrure, épée (gardien) ; style gravé et lumineux, pour se poser dans le creux de la stèle | fond transparent |
| `seal_type_icons.png` | Icônes de types | 16×16 | 17 | les 17 types dans l'ordre : Normal, Feu, Eau, Plante, Électrik, Glace, Combat, Poison, Sol, Vol, Psy, Insecte, Roche, Spectre, Dragon, Ténèbres, Acier ; un symbole simple par type, dans sa couleur habituelle | fond transparent |
| `seal_brazier.png` | Brasero éteint | 24×36 | 1 | vasque de pierre sur un pied, cendres grises et charbons éteints dedans | fond transparent, pied au bas de l'image |
| `seal_brazier_lit.png` | Brasero allumé | 24×36 | 6 (boucle, ~90 ms) | le même brasero, une flamme orange et jaune vive qui danse et dépasse de la vasque, quelques braises qui montent | idem |
| `seal_plate.png` | Dalle de pression | 24×24 | 2 | dalle carrée gravée d'un cercle, posée **au sol** (vue de dessus) ; image 1 : au repos, gravure sombre ; image 2 : enfoncée, la gravure s'allume en doré | fond transparent autour de la dalle |
| `seal_shells.png` | Coquillages | 24×24 | 6 | trois coquillages nacrés de **tailles nettement différentes** (petit, moyen, grand), chacun en deux états : fermé, terne (images 1 à 3) ; ouvert, perle lumineuse (images 4 à 6) | fond transparent, pied au bas de l'image |
| `seal_mark.png` | Repère des grottes scellées | 24×24 | 6 (boucle, ~140 ms) | le même symbole sur **toutes** les grottes scellées, gravé sur la paroi au-dessus de la porte : un sceau circulaire en forme d'œil fermé, lueur violette qui pulse lentement | fond transparent, se pose sur la roche brune |
| `seal_dust.png` | Effondrement de la porte | 72×48 | 8 (~70 ms) | gros nuage de poussière beige et éclats de roche qui roulent, sur toute la largeur de la porte (3 × 2 cases) ; se dissipe à la fin | fond transparent, ≈ 70 % opaque, jouée une fois |

Palettes : pierre `#3a3836 / #6a6560 / #9a948c / #c8c0b4`, mousse `#3a6a2a / #6a9a3a`, or `#a07010 / #f0c030 / #fff0a0`, flamme `#c42418 / #f0741a / #ffd23a / #fff6c0`, nacre `#c89aa0 / #f0d0d4 / #ffffff`, sceau violet `#4a2a6a / #8a5ac8 / #d0b0ff`, poussière `#8a7a64 / #c0b090 / #e8dcc4`.

Présente les planches sur une seule page, agrandies ×4 : une petite grotte de roche brune (paroi de 7 cases de large, porte murée de 3 cases au milieu) avec le repère violet au-dessus de la porte ; devant elle, une stèle avec l'icône Feu dans son creux ; à côté, la même scène avec trois braseros (un allumé), puis trois dalles (une enfoncée), puis trois coquillages (le petit ouvert) ; en dessous, la porte qui s'effondre dans la poussière.

---

## Lot 2 : portes selon le biome (13 planches)

Même jeu, même format que le lot 1. Chaque grotte scellée a une **porte** de 3 cases de large sur 2 de haut (72×48 px), creusée dans une paroi de roche brune vue de face. Le jeu pose la planche de la porte **par-dessus** la paroi tant que la grotte est fermée ; les planches à plusieurs états montrent l'usure de la porte à chaque coup réussi. Le bas de l'image touche le sol, le haut rejoint la roche du plateau.

| Fichier | Biome | Taille d'une image | Images | Description visuelle |
|---|---|---|---|---|
| `seal_door.png` | Tous (porte générique) | 72×48 | 1 | mur de gros blocs de pierre grise ajustés, bouchant l'arche de la grotte, joints sombres |
| `seal_door_lava.png` | Volcan (coulée figée) | 72×48 | 3 | coulée de lave refroidie, croûte noire veinée de rouge qui pulse ; image 2 : en partie changée en pierre grise fissurée ; image 3 : pierre grise très fissurée, prête à céder |
| `seal_door_fumaroles.png` | Volcan (fumerolles) | 72×48 | 6 (boucle, ~100 ms) | fissures dans la roche d'où jaillissent des jets de vapeur blanche brûlante, lueur orange au fond |
| `seal_door_ice.png` | Glacier (mur de glace) | 72×48 | 3 | bloc de glace bleutée translucide (l'obscurité de la grotte se devine derrière) ; images 2 et 3 : de plus en plus fondu, gouttes et flaque au pied |
| `seal_door_icicles.png` | Glacier (stalactites) | 72×48 | 1 | rideau de grosses stalactites de glace pendues à l'arche jusqu'au sol |
| `seal_door_sea.png` | Côte (grotte noyée) | 72×48 | 4 (boucle, ~150 ms) | l'arche à moitié remplie d'eau de mer sombre, vaguelettes et écume qui clapotent contre la voûte |
| `seal_door_vines.png` | Forêt, jungle (lianes) | 72×48 | 1 | lianes épaisses et entremêlées, feuilles larges, quelques fleurs, qui ferment complètement l'arche |
| `seal_door_tree.png` | Forêt (arbre endormi) | 72×48 | 4 (boucle, ~300 ms) | grosses racines et écorce qui ferment l'arche, un visage endormi dans le bois (yeux fermés), un petit « Z » qui monte |
| `seal_door_flowers.png` | Prairie (fleurs closes) | 72×48 | 1 | massif de très grandes fleurs aux pétales fermés (boutons roses et jaunes) qui bouchent l'arche |
| `seal_door_sand.png` | Désert (entrée ensevelie) | 72×48 | 1 | dune de sable qui remplit l'arche, seul le haut d'un linteau de pierre gravé dépasse |
| `seal_door_runes.png` | Ruines (porte à runes) | 72×48 | 2 | porte de pierre taillée couverte de glyphes ; image 1 : glyphes éteints ; image 2 : glyphes allumés en bleu-violet |
| `seal_door_lock.png` | Tous (serrure) | 72×48 | 1 | lourde porte de bois cerclée de fer, un grand cadenas avec un trou de serrure au milieu |
| `seal_door_shells.png` | Côte (coquillages) | 72×48 | 1 | mur de corail et de coquillages incrustés dans la roche, teintes roses et nacrées |

Palettes : roche du plateau `#3a2a20 / #6a4a32 / #9a7050 / #c8a078`, lave `#1a1010 / #6a1810 / #d03018 / #ff8a30`, glace `#3a6aa0 / #7ab0e0 / #c0e4ff / #ffffff`, eau `#1a3a6a / #2a6ab0 / #6ab0e8 / #e0f4ff`, lianes `#1a3a1a / #3a6a2a / #6aa03a / #e05a8a`, bois `#3a2410 / #6a4420 / #9a6a3a`, sable `#a07a40 / #d0b070 / #f0dca0`, runes `#4a4a5a / #8a8a9a / #6a7aff / #c0c8ff`, fer `#2a2a30 / #6a6a74`.

Présente les 13 portes sur une seule page, agrandies ×4, chacune dans la même paroi de roche brune de 7 cases de large, avec le repère violet au-dessus ; les portes à plusieurs états montrées côte à côte.
