# Prompt Claude Design : attaques de type, lot 2 (4 attaques)

Crée des planches d'effets en pixel art pour 4 attaques d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS), cohérentes avec le lot 1 des attaques de type (Vibraqua, Lame d'Air, Plaie-Croix, Onde Folie, Telluriforce). Ce sont des attaques puissantes : elles peuvent être spectaculaires, mais les zones au sol doivent rester assez transparentes pour qu'on voie les Pokémon dedans.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Vue de dessus ; les effets orientés pointent **vers la droite**.
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Liste

| Planche | Taille de l'image | Images | Description |
|---|---|---|---|
| `fatal_foudre_zone.png` | 144×144 | 4, en boucle, ~400 ms | zone d'orage vue de dessus, 6 cases de diamètre : cercle d'électricité jaune crépitant sur le bord, intérieur très transparent avec quelques arcs électriques ; pendant 3 s des éclairs y tombent au hasard |
| `fatal_foudre_eclair.png` | 40×40 | 5, sans boucle, ~250 ms | impact d'éclair vu de dessus : flash blanc-jaune, étoile électrique, puis petite onde de choc circulaire qui s'élargit (1,6 case de diamètre) |
| `blizzard_zone.png` | 144×144 | 4, en boucle, ~500 ms | tempête de neige vue de dessus, 6 cases de diamètre : tourbillon de flocons et de rafales blanc-bleu qui tourne, bord givré, intérieur semi-transparent |
| `blizzard_impact.png` | 24×24 | 3, sans boucle, ~200 ms | éclats de givre sur un ennemi touché (une fois par seconde) |
| `draco_meteor_ombre.png` | 48×48 | 4, en boucle, ~300 ms | ombre ronde au sol (2 cases) qui s'assombrit et rétrécit, avec un halo bleu-violet : avertissement avant la chute du météore |
| `draco_meteor_impact.png` | 64×64 | 6, sans boucle, ~400 ms | gros météore bleu-violet enflammé qui s'écrase (vu de dessus) : flash, explosion de flammes draconiques, cratère et éclats de roche |
| `pouv_antique_pierre.png` | 16×16 | 4, en boucle, ~300 ms | pierre ancienne gris-beige anguleuse avec une lueur dorée, qui tourne sur elle-même (5 pierres orbitent autour du Pokémon, puis sont lancées) |
| `pouv_antique_impact.png` | 24×24 | 3, sans boucle, ~200 ms | pierre qui éclate en fragments avec un éclat doré |

Palettes : Électrik `#3a2600 / #d68a00 / #ffd21f / #fff6a8 / #ffffff` ; Glace `#1a2f5a / #4f8fd8 / #a6dcff / #e6f8ff / #ffffff` ; Dragon `#1a0c3c / #4a2ea8 / #7038f8 / #b49aff / #ffd8a0` ; Roche `#3a2e1c / #7c6a48 / #b8a070 / #e8d8a8 / #ffd860`.

Présente les animations en boucle, agrandies ×4 (×2 pour les zones de 144 px), sur un fond sombre et sur un fond de sol clair.
