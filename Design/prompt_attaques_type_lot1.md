# Prompt Claude Design : attaques de type, lot 1 (5 attaques)

Crée des planches d'effets en pixel art pour 5 attaques d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS), cohérentes avec les livraisons précédentes (attaques de base). Ces attaques sont plus rares et plus fortes que les attaques de base : elles peuvent être un peu plus spectaculaires, tout en restant lisibles.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- L'effet **pointe vers la droite** (le jeu le fait pivoter dans la direction de l'attaque).
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Liste

| Planche | Taille de l'image | Images | Description |
|---|---|---|---|
| `vibraqua_vol.png` | 32×32 | 4, en boucle, ~400 ms | anneau d'eau vu de dessus, épais, avec reflets blancs, qui ondule (le jeu le fait aussi grossir et rétrécir) ; vide au centre |
| `vibraqua_impact.png` | 32×32 | 4, sans boucle, ~250 ms | éclaboussure circulaire et gouttes |
| `lame_d_air_vol.png` | 24×32 | 4, en boucle, ~200 ms | grande lame d'air en croissant, blanc bleuté translucide, avec traits de vitesse derrière |
| `lame_d_air_impact.png` | 24×24 | 3, sans boucle, ~200 ms | entaille d'air qui se dissipe |
| `plaie_croix_trainee.png` | 40×24 | 4, en boucle, ~200 ms | traînée verte effilée derrière le Pokémon pendant sa ruée |
| `plaie_croix_impact.png` | 32×32 | 4, sans boucle, ~250 ms | double entaille en X vert vif lumineux sur l'ennemi traversé |
| `onde_folie_coup.png` | 72×72 | 5, sans boucle, ~400 ms | onde en cône (70°) qui part du bord gauche de l'image et s'élargit vers la droite sur 3 cases : anneaux concentriques violets et roses, petites spirales, aspect hypnotique |
| `telluriforce_marque.png` | 48×48 | 4, en boucle, ~400 ms | marque au sol vue de dessus, 2 cases de diamètre : fissures brunes qui s'ouvrent, lueur orangée, petits cailloux qui tressautent (avertissement avant l'éruption) |
| `telluriforce_colonne.png` | 48×48 | 5, sans boucle, ~350 ms | colonne de terre et de roche qui jaillit du sol (vue de dessus, éclatement circulaire), poussière |

Palettes : Eau `#1846c8 / #2f82f2 / #8ccaff / #f0faff` ; Vol `#4a6e9a / #a8c8e8 / #e8f4ff` ; Insecte `#2e5a12 / #6aa81e / #b4e83c / #f0ffb0` ; Spectre `#2a0c3c / #7a2ea8 / #c062b4 / #f0a6dc` ; Sol `#4a2610 / #7c4620 / #b0763a / #ffa640`.

Présente les animations en boucle, agrandies ×4, sur un fond sombre et sur un fond de sol clair.
