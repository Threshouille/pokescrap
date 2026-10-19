# Prompt Claude Design : nuages de spores et de poudres

Crée des planches d'effets en pixel art pour cinq attaques de spores et de poudres d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Quand le Pokémon lance l'attaque, 6 petits nuages partent de lui dans toutes les directions. Chacun avance lentement sur 3 cases en grossissant jusqu'à 3 fois sa taille de départ (agrandissement fait par le jeu), comme des spores qui se déploient, puis se dissipe. Un ennemi traversé par un nuage reçoit une petite bouffée de spores.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain. Un Pokémon occupe environ 1 case (24×24 à 32×32 px).
- Une planche PNG par animation, avec fond transparent et toutes les images sur **une seule ligne**, de gauche à droite.
- Effets **sans orientation**, à peu près ronds et centrés : le jeu ne les fait pas pivoter.
- Pixel art net : pas d'anticrénelage ni de flou, alpha 0 ou 255 uniquement, 4 à 6 couleurs par planche, contour sombre seulement si nécessaire à la lisibilité sur sol clair (sol rose pâle, sable, roche volcanique sombre).
- Le jeu affiche le nuage à la moitié de sa taille au départ, puis l'agrandit progressivement jusqu'à 1,5 fois (agrandissement au plus proche voisin) : garde des formes simples et des pixels isolés assez rares pour que ça reste lisible aux deux tailles.
- Livre aussi les entrées `effects.json` correspondantes : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Planches (3 par attaque, 15 au total)

Pour chaque attaque `<clé>` :

| Planche | Taille de l'image | Images | Durée | Boucle | Description visuelle |
|---|---|---|---|---|---|
| `<clé>_spore` | 32×32 | 4 | 100 ms par image | oui | Petit nuage de spores en vol : un amas cotonneux irrégulier, plus dense au centre, entouré de quelques grains de spores qui tournoient lentement. Le contour du nuage ondule doucement d'une image à l'autre. |
| `<clé>_impact` | 24×24 | 4 | 60 ms par image | non | Bouffée de spores sur un ennemi : petit éclat de grains qui part du centre et s'éparpille en disparaissant. |
| `<clé>_fin` | 32×32 | 5 | 80 ms par image | non | Dissipation du nuage en fin de course (même dessin que `_spore` au départ) : il s'ouvre, se troue, puis s'effiloche en quelques grains qui disparaissent. |

| Clé | Attaque | Effet | Couleurs |
|---|---|---|---|
| `spore` | Spore | endort | verts tendres et beige champignon, grains crème |
| `poudre_dodo` | Poudre Dodo | endort | bleu-vert pâle et bleu ciel, petites étincelles blanches (ambiance sommeil) |
| `para_spore` | Para-Spore | paralyse | jaune vif et orangé, quelques petites étincelles électriques |
| `poudre_toxik` | Poudre Toxik | empoisonne | violet et magenta, bulles toxiques sombres |
| `spore_coton` | Spore Coton | ralentit | blanc cassé et gris très clair, flocons de coton duveteux, contour gris-beige pour rester visible |

Livre une archive `.zip` avec les 15 PNG et un `effects.json` qui les liste tous.
