# Prompt Claude Design : Clé de l'îlot et Engrenage du Temps

Crée des sprites en pixel art pour deux objets clés d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Sur une île flottante, chaque quartier a un gardien qui attend sur un îlot à part ; on y accède par un portail au bord de la côte, ouvert par la **Clé de l'îlot**, trouvée au fond d'un donjon. Battre le gardien donne un **Engrenage du Temps** (l'objet de l'histoire de Donjon Mystère : Explorateurs), qui ouvre le passage vers le quartier suivant.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain. Un objet au sol occupe environ 1 case.
- Une planche PNG par animation, avec fond transparent et toutes les images sur **une seule ligne**, de gauche à droite.
- Objets **sans orientation**, centrés : le jeu ne les fait pas pivoter. Ils sont aussi affichés en icône dans l'interface (cartes de ramassage, compteur) : le dessin doit se lire en petit.
- Pixel art net : pas d'anticrénelage ni de flou, alpha 0 ou 255 uniquement, 4 à 6 couleurs par planche, contour sombre pour rester lisible sur tous les sols (herbe, sable, roche volcanique sombre, neige).
- Livre aussi les entrées `effects.json` correspondantes : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Planches (4)

| Planche | Taille de l'image | Images | Durée | Boucle | Description visuelle |
|---|---|---|---|---|---|
| `key_islet` | 24×24 | 1 | — | non | La Clé de l'îlot, posée : une grosse clé ancienne en métal doré patiné, l'anneau en forme d'île flottante stylisée (un petit disque de terre avec une touffe d'herbe), le panneton orné d'un éclat bleu ciel. Lisible en icône. |
| `key_islet_glow` | 24×24 | 6 | 100 ms par image | oui | La même clé au sol, qui attire l'œil : un reflet blanc glisse sur le métal, et quelques étincelles bleu ciel scintillent autour. |
| `time_gear` | 24×24 | 1 | — | non | L'Engrenage du Temps : une roue dentée turquoise lumineuse cerclée d'or, un moyeu doré, une lueur intérieure bleu-vert, à la manière de l'objet des Explorateurs. |
| `time_gear_glow` | 24×24 | 8 | 90 ms par image | oui | L'engrenage au sol qui tourne lentement sur lui-même (1/8 de tour par image) avec une pulsation de lumière turquoise. |

Palette : or patiné, bleu ciel, turquoise, blanc pour les reflets, contour brun très sombre.

Livre une archive `.zip` avec les 4 PNG et un `effects.json` qui les liste tous.
