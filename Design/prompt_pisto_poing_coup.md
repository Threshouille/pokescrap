# Prompt Claude Design : coup de Pisto-Poing

Crée une planche d'effet en pixel art pour le coup de poing de Pisto-Poing (type Acier), dans un roguelike vu de dessus au style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS), cohérente avec la livraison précédente des attaques au corps à corps (`pisto_poing_trainee`, `pisto_poing_impact`).

Le Pokémon vient de faire une courte ruée ; il s'arrête, puis, 0,25 s plus tard, donne un coup de poing droit devant lui qui repousse la cible.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG, fond transparent, toutes les images sur une seule ligne, de gauche à droite.
- L'effet pointe vers la droite (le jeu le fait pivoter) ; le Pokémon qui frappe est à gauche de l'image.
- Pixel art net, sans anticrénelage ; 4 ou 5 couleurs ; contour sombre d'une couleur.
- Livre aussi l'entrée `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Planche
| Fichier | Taille de l'image | Images | Description |
|---|---|---|---|
| `pisto_poing_coup.png` | 32×24 | 4, sans boucle, 180 à 220 ms au total | poing métallique argenté qui jaillit vers la droite avec des lignes de vitesse, éclat blanc au bout de la frappe, onde de choc en arc qui se dissipe |

Palette : argent et acier (`#2a2e38`, `#6a7488`, `#aab4c8`, `#e4ecf8`, `#ffffff`), comme `pisto_poing_impact`.

Présente l'animation en boucle, agrandie ×4, sur un fond sombre et sur un fond de sol clair.
