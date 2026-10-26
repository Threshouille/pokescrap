# Prompt Claude Design : attaques de type, lot 3 (attaques à bouton maintenu)

Crée des planches d'effets en pixel art pour 3 attaques d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS), cohérentes avec les lots 1 et 2 des attaques de type. Ces attaques durent tant que le joueur garde le bouton enfoncé.

- **Lance-Flamme** (Feu) : le Pokémon crache un jet de flammes continu devant lui (jusqu'à 3 s). Le jeu tire une petite flamme toutes les 0,08 s, avec un léger écart d'angle : c'est l'accumulation des flammes qui fait le jet. Chaque flamme grossit un peu en avançant et ralentit, sur 3,5 cases.
- **Ball'Ombre** (Spectre) : tant que le bouton est enfoncé, une boule d'ombre grossit devant le Pokémon (jusqu'à ×2,2 en 2 s) ; au relâchement elle part très vite, sur 4 cases, puis s'arrête et flotte un instant avant de disparaître.
- **Méga-Sangsue** (Plante) : un lien végétal relie le Pokémon à sa cible pendant 1,5 s et aspire son énergie ; le lien casse si la cible s'éloigne.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Les effets orientés **pointent vers la droite** (le jeu les fait pivoter).
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Liste

| Planche | Taille de l'image | Images | Description |
|---|---|---|---|
| `lance_flamme_vol.png` | 16×16 | 4, en boucle, ~240 ms | une flamme du jet : boule de feu rouge-orangée au cœur jaune, petite queue de flamme vers la gauche ; elle doit bien se fondre avec ses voisines quand des dizaines se chevauchent |
| `lance_flamme_impact.png` | 24×24 | 3, sans boucle, ~180 ms | gerbe de flammèches qui s'éteint |
| `ball_ombre_vol.png` | 24×24 | 4, en boucle, ~320 ms | boule d'énergie spectrale violet sombre avec un cœur noir et un halo qui ondule ; elle sert aussi pendant la charge (agrandie jusqu'à ×2,2) et quand elle flotte en fin de course |
| `ball_ombre_impact.png` | 32×32 | 4, sans boucle, ~260 ms | éclatement d'ombre violette en volutes |
| `mega_sangsue_lien.png` | 24×12 | 4, en boucle, ~320 ms | **morceau de lien répété** le long de la ligne entre le Pokémon et sa cible (le jeu l'étire en le répétant) : liane verte translucide parcourue de petites bulles lumineuses qui avancent vers la **gauche** (de la cible vers le lanceur) ; les bords gauche et droit doivent se raccorder sans couture |
| `mega_sangsue_impact.png` | 24×24 | 3, sans boucle, ~200 ms | petites particules vertes aspirées hors de la cible, à chaque prise (toutes les 0,25 s) |

Palettes : Feu `#3a0b06 / #c42418 / #f0741a / #ffd23a / #fff4c2` ; Spectre `#140820 / #2a0c3c / #5b2585 / #9447c2 / #d39af2` ; Plante `#10260e / #2e6a1e / #58a832 / #9edc5a / #e4ffb0`.

Présente les animations en boucle, agrandies ×4, sur un fond sombre et sur un fond de sol clair : le jet de Lance-Flamme (plusieurs flammes qui se suivent), Ball'Ombre petite et chargée, et le lien de Méga-Sangsue répété sur 4 cases entre deux points.
