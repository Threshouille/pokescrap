# Prompt Claude Design : Tranche

Crée une planche d'effet en pixel art pour l'attaque **Tranche** (type Normal) d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Inspire-toi de l'animation de Tranche (Slash) des jeux Pokémon sur DS (Diamant / Perle / Platine, Explorateurs du Ciel) : deux grandes entailles blanches, nettes et tranchantes, qui fendent l'air en diagonale.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, avec fond transparent et toutes les images sur **une seule ligne**, de gauche à droite.
- L'effet **pointe vers la droite** (le jeu le fait pivoter dans la direction du coup) ; le Pokémon qui frappe se trouve à gauche de l'image.
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs, contour sombre d'une couleur.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Planches
| Clé | Taille de l'image | Description |
|---|---|---|
| `tranche_coup` | 40×56 (zone d'environ 1,5 case devant le lanceur, arc de 120°) | Comme `griffe_coup` (3 traits de griffe blancs en arc), mais avec **seulement 2 traits**, plus longs, plus larges et plus tranchants : deux entailles parallèles qui traversent la zone en diagonale, tracées d'un coup sec puis qui s'effacent en éclats. 4 à 5 images, 200 à 280 ms, sans boucle. Blanc pur au cœur, bord gris-bleu clair, liseré sombre. |
| `tranche_impact` | 24×24 | Éclat blanc en croix fine avec deux petites étincelles, 3 images, sans boucle. |

Présente l'animation jouée en boucle, agrandie ×4, sur un fond sombre et sur un fond de sol clair, à côté de `griffe_coup` pour comparaison.
