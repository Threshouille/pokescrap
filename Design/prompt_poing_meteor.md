# Prompt Claude Design : Poing Météor

Crée des planches d'effets en pixel art pour l'attaque **Poing Météor** (type Acier) d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Inspire-toi de l'animation de Poing Météor (Meteor Mash) des jeux Pokémon sur DS : des poings lancés comme des étoiles filantes.

En jeu, le lanceur reste immobile et tire en rafale une vingtaine de petits projectiles, un toutes les 0,08 s, au hasard dans un cône devant lui. Chaque projectile doit donc être **petit et très lisible**, même à plusieurs à l'écran.

## Références jointes (dossier `refs_poing_meteor`)
- `mach_punch_coup` et `ultimapoing_coup` : le poing iconique de Pokémon déjà en jeu (poing fermé vu de face, blanc, contour épais). Reprends ce même poing.
- `pisto_poing_impact` : un impact d'Acier déjà en jeu, pour le ton.
- Les fichiers `_x4` sont les mêmes planches agrandies quatre fois.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs, contour sombre d'une couleur.
- Livre aussi `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.
- Range les planches dans `effects_v22_poing_meteor/`, avec un dossier `apercu_x4/`.

## Palette
Étoile jaune `#b07a00 / #ffd21f / #fff6a8` ; poing acier `#5a6470 / #a8b4c0 / #f0f6ff` ; contour `#24242e`.

## Planches
| Clé | Taille de l'image | Description |
|---|---|---|
| `poing_meteor_vol` | 20×20 | Le projectile : le poing iconique (acier clair) au centre d'une **étoile jaune à 5 branches qui tourne sur elle-même** autour de lui. Le poing reste droit, seule l'étoile tourne. 6 images, environ 60 ms chacune, **en boucle** (un tour complet de l'étoile, ou un cinquième de tour puisqu'elle est symétrique). Elle ne pivote pas avec la direction du tir : le dessin doit marcher dans tous les sens. |
| `poing_meteor_impact` | 24×24 | L'étoile éclate en petites étincelles jaunes et en éclats métalliques, avec un éclair blanc au centre. 4 images, sans boucle, 200 à 250 ms au total. |

Présente les deux animations jouées en boucle, agrandies ×4, sur un fond sombre et sur un fond de sol clair, puis une vue d'une douzaine de projectiles éparpillés dans un cône, pour vérifier la lisibilité en rafale.
