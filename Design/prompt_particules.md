# Prompt Claude Design : particules d'ambiance (8 planches)

Crée de toutes petites planches en pixel art pour les **particules d'ambiance** d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Le jeu fait naître ces particules par dizaines autour du joueur selon le biome et l'heure : lucioles la nuit en forêt, feuilles qui tombent, pétales dans la prairie, braises au-dessus de la lave, neige, poussière du désert, reflets et écume qui suivent le courant des rivières, bulles qui éclatent sur la lave. Le moteur les déplace, les fait tourner et les fait apparaître et disparaître en fondu : chaque planche ne contient que l'aspect de la particule.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case ; ces particules sont minuscules (4 à 12 px).
- Une planche PNG par particule, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255** (sauf mention), 2 à 4 couleurs par planche, contour sombre seulement si la particule fait 8 px ou plus.
- Les couleurs sont celles de la particule elle-même (le jeu ne les teinte pas).
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms` (une valeur par image), `loop`, `palette`. Pour les planches à **variantes** (`loop: false`), le jeu tire une image au hasard par particule ; pour les planches **animées** (`loop: true`), il les joue pendant la vie de la particule.

## Liste

| Fichier | Particule | Taille d'une image | Images | Description visuelle | Transparence |
|---|---|---|---|---|---|
| `particle_firefly.png` | Luciole | 8×8 | 4 (boucle, ~120 ms) | point lumineux jaune-vert au cœur blanc, halo de 1 px qui pulse (petit, grand, petit, éteint presque) | halo ≈ 50 % transparent autorisé |
| `particle_leaf.png` | Feuille | 10×10 | 4 variantes | feuilles d'arbre vues de dessus : verte ovale, vert clair dentelée, jaune-orangé, brune d'automne ; nervure centrale visible | alpha 0 / 255 |
| `particle_petal.png` | Pétale | 8×8 | 4 variantes | pétales de fleur : rose, blanc, rose pâle, jaune ; forme de goutte arrondie | alpha 0 / 255 |
| `particle_ember.png` | Braise | 6×6 | 4 (boucle, ~90 ms) | petite braise carrée orange au cœur jaune qui scintille (jaune, orange, rouge, orange) | alpha 0 / 255 |
| `particle_snow.png` | Flocon | 7×7 | 3 variantes | flocon blanc bleuté : point rond, petite croix, étoile à 6 branches | alpha 0 / 255 |
| `particle_dust.png` | Poussière | 8×8 | 3 variantes | grain de sable beige, petit nuage de poussière irrégulier, deux grains collés | nuage ≈ 50 % transparent autorisé |
| `particle_foam.png` | Reflet / écume | 10×6 | 4 variantes | reflets et écume à la surface de l'eau, **allongés horizontalement** : trait blanc, deux tirets, petite vaguelette en croissant, écume en grappe | alpha 0 / 255 |
| `particle_bubble.png` | Bulle de lave | 12×12 | 6 (une fois, ~180 ms) | une bulle orange qui gonfle à la surface de la lave, reflet jaune, puis éclate en quatre gouttes qui retombent | alpha 0 / 255 |

Palettes : luciole `#c8ff50 / #eaff70 / #ffffff`, feuilles `#3a6a2a / #5aa03a / #7cbc40 / #c89030 / #8a5a28`, pétales `#ffb0d0 / #ffd0e0 / #ffffff / #ffe080`, braise `#ff5a20 / #ff8a30 / #ffd040`, neige `#ffffff / #e0f0ff / #a8c8e8`, poussière `#c8b080 / #e0c890`, eau `#d8f4ff / #ffffff / #8ac8f0`, lave `#c42418 / #f0741a / #ffd23a`.

Présente les planches sur une seule page, agrandies ×6, chacune posée sur un fond qui lui correspond (sous-bois sombre de nuit pour les lucioles, herbe pour les feuilles et pétales, roche volcanique pour les braises, neige pour les flocons, sable pour la poussière, eau d'une rivière pour les reflets, lave pour les bulles), avec une petite scène où quelques dizaines de chaque particule sont dispersées.
