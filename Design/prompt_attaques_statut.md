# Prompt Claude Design : attaques de statut posées dans l'arène (6 attaques)

Crée des planches d'effets en pixel art pour 6 attaques de statut d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Ces attaques posent quelque chose dans l'arène (un mur, une bulle, un clone, des pointes, une étoile). Pour chacune, inspire-toi de son animation dans les jeux Pokémon sur DS (Diamant / Perle / Platine, Explorateurs du Ciel).

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Livre aussi `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.
- Range les planches dans `effects_v23_statut/`, avec un dossier `apercu_x4/`.

## Palettes
Psy `#7a2ea8 / #d04ab0 / #ff92d6` ; lumière jaune `#b07a00 / #ffd21f / #fff6a8` ; verre bleu `#2f5fb0 / #6fb0ff / #d8f0ff` ; Normal `#6a6a6a / #c8c8c8 / #ffffff` ; Sol `#4a2610 / #7c4620 / #b0763a`.

## Planches
| Clé | Attaque | Taille | Description |
|---|---|---|---|
| `mur_lumiere_mur` | Mur Lumière | 72×16, en boucle, 4 images | Un mur de lumière **vu de dessus**, couché à l'horizontale (3 cases de long, fin) : panneau translucide jaune doré avec un reflet qui glisse d'un bout à l'autre. Le jeu le tourne pour qu'il se tienne en travers du tir. |
| `mur_lumiere_renvoi` | Mur Lumière | 24×24, 3-4 images | Éclat en losange quand un tir est renvoyé. |
| `mur_lumiere_fin` | Mur Lumière | 72×16, 4 images | Le mur se fissure et s'efface en paillettes. |
| `protection_mur` | Protection | 72×16, en boucle, 4 images | Même principe, mais un mur de **verre bleu** plus épais et opaque, avec des hexagones en relief et un reflet. |
| `protection_choc` | Protection | 24×24, 3-4 images | Onde bleue quand un tir ou un Pokémon le heurte. |
| `protection_fin` | Protection | 72×16, 4 images | Le verre se brise en éclats. |
| `abri_bulle` | Abri | 32×32, en boucle, 4 images | Bulle verte translucide autour du Pokémon (centrée), bord lumineux qui pulse, reflet en haut à gauche. |
| `clonage_fumee` | Clonage | 32×32, 5 images | Nuage de fumée blanche qui gonfle et se dissipe (apparition et disparition du clone). |
| `picots_pose` | Picots | 24×24, 3 images | Petit nuage de poussière quand une poignée de pointes touche le sol. |
| `terrain_spikes` | Picots | 24×24, en boucle lente, 2 images | Case de pointes vues de dessus : trois ou quatre petites pointes métalliques brunes et grises éparpillées, fond transparent (posé sur le sol). Variante `terrain_spikes_b` avec une autre disposition. |
| `voeu_cercle` | Vœu | 48×48, en boucle, 4 images | Cercle doré au sol (2 cases de diamètre) avec une étoile au centre, qui pulse : il annonce où l'étoile va tomber. |
| `voeu_etoile` | Vœu | 24×24, en boucle, 4 images | Étoile jaune à 5 branches qui tombe du ciel, avec une petite traînée (dessinée pointe vers le bas, elle ne pivote pas). |
| `voeu_soin` | Vœu | 48×48, 5 images | Éclat de soin à l'impact : étincelles vertes et dorées qui montent en gerbe. |

Présente les 13 planches jouées en boucle, agrandies ×4, sur un fond sombre et sur un fond de sol clair.
