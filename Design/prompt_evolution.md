# Prompt Claude Design : animation d'évolution

Crée des planches d'effets en pixel art pour l'évolution d'un Pokémon dans un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Pendant environ 2,5 s, le Pokémon reste immobile et le jeu fait alterner les silhouettes blanches de l'ancienne et de la nouvelle forme, de plus en plus vite (déjà géré par le jeu, ne pas le dessiner). Les planches viennent autour : une aura pendant la transformation, un éclair de lumière quand la nouvelle forme apparaît, puis une onde de choc qui repousse les ennemis jusqu'à 5 cases.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain. Un Pokémon occupe environ 1 case (24×24 à 32×32 px).
- Une planche PNG par animation, avec fond transparent et toutes les images sur **une seule ligne**, de gauche à droite.
- Effets centrés sur le Pokémon, symétriques (pas d'orientation : le jeu ne les fait pas pivoter).
- Pixel art net : pas d'anticrénelage ni de flou, alpha 0 ou 255 uniquement, 4 à 6 couleurs par planche, contour sombre seulement si nécessaire à la lisibilité sur sol clair.
- Livre aussi les entrées `effects.json` correspondantes : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Planches

| Clé | Taille de l'image | Images | Durée | Boucle | Description visuelle |
|---|---|---|---|---|---|
| `evolution_aura` | 48×64 | 6 | 80 ms par image | oui | Colonne de lumière douce sous et autour du Pokémon : un ovale lumineux au sol, des particules et petites étoiles qui montent en spirale, quelques rayons verticaux. Dessinée derrière le Pokémon ; le centre (24×24 autour du milieu bas) doit rester assez dégagé pour voir la silhouette. |
| `evolution_eclat` | 64×64 | 5 | 60 ms par image | non | Éclair de lumière quand la nouvelle forme apparaît : étoile à 4 ou 8 branches qui grandit très vite, cercle blanc qui s'ouvre, puis étincelles qui s'éparpillent et disparaissent. |
| `evolution_onde` | 120×120 | 6 | 60 ms par image | non | Onde de choc vue de dessus : anneau lumineux qui part du centre et s'élargit jusqu'au bord de l'image, de plus en plus fin et ajouré, avec quelques éclats et traits de vitesse orientés vers l'extérieur. Le jeu l'agrandit ×2 pour couvrir 5 cases de rayon : garde des traits de 2 px au minimum. |
| `evolution_impact` | 24×24 | 3 | 60 ms par image | non | Petit éclat blanc et doré sur chaque ennemi touché par l'onde. |

Palette commune : blanc `#ffffff`, bleu très clair `#d8f0ff`, bleu ciel `#8cc8ff`, doré pâle `#fff2a8`, doré `#ffd23a`, et un bleu profond `#3a5ab8` pour les contours ou les particules sur sol clair.

Présente les 4 planches sur une seule page, chacune jouée en boucle et agrandie ×4, sur un fond sombre et sur un fond de sol clair (sable ou cristaux roses). Ajoute une démonstration enchaînée : aura pendant 2,5 s sous une silhouette blanche de Pokémon, puis éclat, puis onde.
