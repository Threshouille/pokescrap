# Prompt Claude Design : éclair et météore qui tombent du ciel (Fatal-Foudre, Draco Météor)

Crée deux planches d'effets en pixel art pour un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS), cohérentes avec le lot 2 des attaques de type (`fatal_foudre_eclair`, `draco_meteor_ombre`, `draco_meteor_impact`).

- **Fatal-Foudre** (Électrik) : pendant 3 s, des éclairs frappent des endroits au hasard autour du lanceur. Il n'y a plus de zone électrifiée au sol : ce sont les éclairs qui montrent l'attaque. L'onde de choc au sol (`fatal_foudre_eclair`, déjà livrée) apparaît au pied de chaque éclair.
- **Draco Météor** (Dragon) : 5 gros météores tombent l'un après l'autre en ligne devant le lanceur. Au sol, une ombre (`draco_meteor_ombre`, déjà livrée) prévient de la chute pendant 0,6 s ; le météore tombe pendant les 0,45 dernières secondes, en diagonale (il arrive d'en haut à gauche), puis l'explosion (`draco_meteor_impact`, déjà livrée) prend le relais à l'atterrissage.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur une seule ligne, de gauche à droite.
- Pixel art net, sans anticrénelage ; 4 ou 5 couleurs ; contour sombre d'une couleur.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Planches

| Fichier | Taille de l'image | Images | Description |
|---|---|---|---|
| `fatal_foudre_chute.png` | 32×96 (4 cases de haut) | 4, sans boucle, 150 à 200 ms au total | **ne pivote pas** : éclair vertical zigzagant blanc-jaune qui frappe du haut vers le bas, **point d'impact au milieu du bord bas** de l'image. 1re image : l'éclair entier, très lumineux ; 2e : flash plus large avec des ramifications ; 3e et 4e : il s'amincit et s'éteint, quelques étincelles au pied. |
| `draco_meteor_chute.png` | 48×32 | 4, en boucle, ~200 ms | **pointe vers la droite** (le jeu le fait pivoter dans le sens de la chute) : gros rocher bleu-violet enflammé à droite de l'image, longue traînée de flammes draconiques derrière lui (vers la gauche), qui crépite. Assez gros pour se lire : le rocher fait environ 1 case. |

Palettes : Électrik `#3a2600`, `#d68a00`, `#ffd21f`, `#fff6a8`, `#ffffff` ; Dragon `#1a0c3c`, `#4a2ea8`, `#7038f8`, `#b49aff`, `#ffd8a0`.

Présente les animations en boucle, agrandies ×4, sur un fond sombre et sur un fond de sol clair : l'éclair avec l'onde de choc `fatal_foudre_eclair` à son pied, le météore en diagonale qui finit sur l'ombre `draco_meteor_ombre` suivie de l'explosion `draco_meteor_impact`.
