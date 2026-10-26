# Prompt Claude Design : attaques de type, lot 4 (états du corps et prises)

Crée des planches d'effets en pixel art pour 6 attaques d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS), cohérentes avec les lots précédents des attaques de type.

- **Plaquage** (Normal) : le Pokémon bondit sur sa cible (3 cases) et retombe ; l'atterrissage frappe tout autour (1,3 case de rayon).
- **Frappe Atlas** (Combat) : courte ruée ; s'il touche, le Pokémon saisit l'ennemi, saute avec lui et l'écrase au sol ; une onde de choc (1,6 case de rayon) repousse les autres.
- **Acidarmure** (Poison) : le Pokémon se liquéfie 2 s (il devient violet translucide) ; les ennemis qu'il traverse sont empoisonnés.
- **Gyroballe** (Acier) : le Pokémon tourne sur lui-même comme une toupie pendant 2 s et blesse ce qu'il percute.
- **Psyko** (Psy) : la cible est soulevée 0,8 s, puis projetée au loin.
- **Coup Bas** (Ténèbres) : le Pokémon se met en garde 1 s ; s'il est frappé, il disparaît dans l'ombre, réapparaît derrière l'attaquant et le frappe très fort ; sinon, petit coup devant lui.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Les effets orientés **pointent vers la droite** (le jeu les fait pivoter).
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Liste

| Planche | Taille de l'image | Images | Description |
|---|---|---|---|
| `plaquage_onde.png` | 64×64 | 4, sans boucle, ~280 ms | onde d'atterrissage vue de dessus (le jeu l'étire à la taille de la zone) : anneau de poussière beige qui s'élargit, petites fissures |
| `plaquage_impact.png` | 24×24 | 3, sans boucle, ~200 ms | choc sur un ennemi touché, étoile blanche |
| `frappe_atlas_trainee.png` | 32×24 | 4, en boucle, ~200 ms | traînée de ruée rouge-orangé derrière le Pokémon |
| `frappe_atlas_onde.png` | 80×80 | 5, sans boucle, ~350 ms | onde de choc de l'écrasement vue de dessus (étirée à la taille de la zone) : cratère, éclats de sol, anneau rouge-orangé |
| `acidarmure_impact.png` | 24×24 | 3, sans boucle, ~250 ms | giclée d'acide violet et vert vif sur un ennemi traversé |
| `gyroballe_impact.png` | 24×24 | 3, sans boucle, ~200 ms | étincelles métalliques argentées au point de contact |
| `psyko_impact.png` | 32×32 | 4, sans boucle, ~260 ms | éclat psychique rose-violet quand la cible est projetée |
| `coup_bas_coup.png` | 24×32 | 4, sans boucle, ~200 ms | petit coup en arc devant le Pokémon (quand personne ne l'a frappé) : griffure d'ombre |
| `coup_bas_contre.png` | 32×32 | 4, sans boucle, ~240 ms | coup de la contre-attaque sur l'attaquant : grande entaille d'ombre noire bordée de rouge sombre |
| `coup_bas_impact.png` | 24×24 | 3, sans boucle, ~200 ms | éclat noir sur l'ennemi touché |
| `coup_bas_trainee.png` | 40×16 | 4, en boucle, ~200 ms | ombre au sol laissée là où le Pokémon disparaît pour réapparaître derrière l'attaquant |

Palettes : Normal `#4a4434 / #a89a74 / #d8ccaa / #ffffff` ; Combat `#3a1606 / #a81c24 / #ee4a3a / #ffc4b4` ; Poison `#1f0a2e / #5b2585 / #9447c2 / #a8ff3e` ; Acier `#2a2e38 / #6a7488 / #aab4c8 / #ffffff` ; Psy `#2a0c3c / #7a2ea8 / #d04ab0 / #ff92d6` ; Ténèbres `#0c0810 / #2a1c2c / #6a2028 / #c84040`.

Présente les animations en boucle, agrandies ×4, sur un fond sombre et sur un fond de sol clair.
