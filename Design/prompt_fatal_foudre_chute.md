# Prompt Claude Design : éclair qui tombe du ciel (Fatal-Foudre)

Crée une planche d'effet en pixel art pour Fatal-Foudre (type Électrik), dans un roguelike vu de dessus au style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS), cohérente avec `fatal_foudre_eclair` (l'onde de choc au sol, livrée dans le lot 2).

Pendant 3 s, des éclairs frappent des endroits au hasard autour du lanceur. Cette planche montre l'éclair qui **tombe du ciel** sur l'endroit frappé ; l'onde de choc `fatal_foudre_eclair` apparaît au même moment au pied de l'éclair. Il n'y a plus de zone électrifiée au sol : ce sont les éclairs qui montrent l'attaque.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG, fond transparent, toutes les images sur une seule ligne, de gauche à droite.
- **La planche ne pivote pas** : l'éclair est vertical, il descend du haut de l'image, et **son point d'impact est au milieu du bord bas** de l'image.
- Pixel art net, sans anticrénelage ; 4 ou 5 couleurs ; contour sombre d'une couleur.
- Livre aussi l'entrée `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Planche
| Fichier | Taille de l'image | Images | Description |
|---|---|---|---|
| `fatal_foudre_chute.png` | 32×96 (4 cases de haut) | 4, sans boucle, 150 à 200 ms au total | éclair zigzagant blanc-jaune qui frappe du haut vers le bas : 1re image, l'éclair apparaît en entier, très lumineux ; 2e, flash plus large avec des ramifications ; 3e et 4e, il s'amincit et s'éteint, quelques étincelles au pied |

Palette : Électrik `#3a2600`, `#d68a00`, `#ffd21f`, `#fff6a8`, `#ffffff`.

Présente l'animation en boucle, agrandie ×4, sur un fond sombre et sur un fond de sol clair, avec l'onde de choc `fatal_foudre_eclair` jouée au pied de l'éclair.
