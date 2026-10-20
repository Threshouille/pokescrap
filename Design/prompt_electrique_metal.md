# Prompt Claude Design : pylônes électriques, métal et aimants (9 planches)

Crée des planches en pixel art pour des **obstacles** d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Dans l'arène, des **pylônes électriques** se dressent par paires et lancent périodiquement un **arc électrique** de l'un à l'autre ; des **plaques de métal** couvrent une partie du sol (elles conduisent l'électricité, chauffent au feu) et des **bornes aimantées** attirent les attaques électriques et métalliques. Le jeu pose ces éléments case par case (1 case = 24 px) et tend l'arc entre les deux pylônes, quelle que soit leur distance.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement**, 4 à 6 couleurs par planche, contour sombre d'une couleur.
- Vue de dessus, éclairage venant du haut à gauche.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms` (une valeur par image), `loop`, `palette`.

## Liste

| Fichier | Élément | Taille d'une image | Images | Description visuelle | Raccord / transparence |
|---|---|---|---|---|---|
| `obstacle_pylon.png` | Pylône électrique | 24×24 | 4 (boucle, ~150 ms) | pylône métallique vu de dessus : socle carré gris acier boulonné, tête avec un isolateur en céramique et une bobine cuivrée au centre ; une petite étincelle jaune saute sur la bobine | fond transparent autour du socle |
| `obstacle_pylon_charged.png` | Pylône surchargé | 24×24 | 4 (boucle, ~80 ms) | le même pylône, bobine incandescente blanc-jaune, éclairs qui crépitent tout autour | idem |
| `obstacle_pylon_grounded.png` | Pylône à la terre | 24×24 | 1 | le même pylône éteint, couvert de terre et de boue brune, bobine grise | idem |
| `obstacle_arc.png` | Arc électrique | 24×12 | 4 (boucle, ~60 ms) | segment d'arc **horizontal** : éclair épais en zigzag, cœur blanc, bords jaune vif, petites étincelles ; chaque image change le zigzag | **raccord parfait gauche/droite** (le jeu répète le segment sur toute la longueur) |
| `obstacle_arc_warn.png` | Avertissement | 24×6 | 4 (boucle, ~90 ms) | fil électrique fin et discontinu qui crépite, quelques étincelles jaunes isolées (l'arc va partir) | raccord gauche/droite, ≈ 60 % transparent |
| `obstacle_metal.png` | Plaque de métal | 24×24 | 1 | plaque d'acier gris-bleu striée en losanges, rivets aux coins, léger reflet | **raccord sur les 4 bords** (zones de plusieurs plaques), opaque |
| `obstacle_magnet.png` | Borne aimantée | 24×24 | 4 (boucle, ~200 ms) | borne en fer à cheval rouge et argent vue de dessus, posée sur un socle gris ; de petites ondes magnétiques bleues pulsent autour | fond transparent autour du socle |
| `terrain_charged_metal.png` | Métal chargé | 24×24 | 4 (boucle, ~80 ms) | arcs électriques jaunes qui courent à la surface d'une plaque (posés **par-dessus** la plaque de métal) | raccord sur les 4 bords, ≈ 60 % transparent |
| `terrain_hot_metal.png` | Métal brûlant | 24×24 | 4 (boucle, ~200 ms) | lueur de chaleur orange-rouge et vapeur qui ondule à la surface d'une plaque (par-dessus la plaque) | raccord sur les 4 bords, ≈ 50 % transparent |

Palettes : métal `#3a3f4a / #6a727e / #9aa4b0 / #d0d8e0`, cuivre `#8a4a20 / #c8783a`, Électrik `#d68a00 / #ffd21f / #fff6a8` + blanc, aimant rouge `#a01818 / #e04040`, ondes `#4f8fd8 / #a6dcff`, chaleur Feu `#c42418 / #f0741a / #ffd23a`, boue `#4a2610 / #7c4620`.

Présente les planches sur une seule page, agrandies ×4 : deux pylônes à 5 cases d'écart sur un sol de donjon gris-brun, l'arc tendu entre eux (version active, puis avertissement) ; une zone de 4 × 3 plaques de métal, avec une borne aimantée sur le bord ; la même zone avec le métal chargé, puis brûlant, par-dessus.
