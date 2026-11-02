# Prompt Claude Design : entrées des donjons (3 lots)

Les lots s'envoient séparément, dans l'ordre. Le lot 1 suffit pour remplacer les carrés provisoires actuels (escaliers des salles, escaliers secrets, passages) ; les lots 2 et 3 habillent les cachettes et les grandes entrées.

---

## Contexte commun (à coller en tête de chaque lot)

Crée des planches en pixel art pour un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS) : couleurs franches de DM, **contour sombre d'une couleur**, volumes simples, végétation en boules. Le jeu se passe sur une île flottante divisée en quartiers (prairie, forêt, désert, montagne, volcan, glacier, côte, ruines). On y trouve plusieurs sortes d'**entrées** qui mènent à des salles à part (écran de chargement) : il faut les **reconnaître d'un coup d'œil**, chacune avec sa silhouette, même petites à l'écran et au milieu du décor.

Les entrées sont :
- les **escaliers** : l'escalier qui descend vers la salle suivante d'un donjon, celui qui remonte vers l'île ; il rappelle l'escalier de Donjon Mystère ;
- les **escaliers secrets** : cachés au centre d'une clairière de forêt ou sous un rocher fissuré à briser ; une surprise, on ne les voit qu'une fois découverts ;
- le **donjon de la clé** : un donjon par quartier, sur un point d'intérêt (plateau, clairière), qui mène au gardien et à la clé de l'îlot du boss ;
- les **portes de quartier** : un passage vers le quartier suivant, fermé tant que l'équipe n'a pas l'**Engrenage du Temps** du quartier (roue dentée turquoise cerclée d'or, l'objet de l'histoire de DM Explorateurs).

### Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case. Vue de 3/4 : on voit le dessus et la face avant des objets.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement** (sauf mention), 4 à 8 couleurs par planche, contour sombre d'une couleur.
- Éclairage venant du haut à gauche. Ce qui est **au sol** (trous, escaliers creusés) est centré dans l'image ; ce qui **se dresse** (arches, portes) a son **pied au milieu du bord bas** de l'image (le jeu le trie par le pied avec les Pokémon).
- Les entrées doivent se lire sur tous les sols : herbe verte, sable clair, roche brune, roche volcanique sombre, neige, dalles grises.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms` (une valeur par image), `loop`, `palette`, et `below: true` pour ce qui est au sol.

---

## Lot 1 : escaliers, repères et icônes (9 planches)

| Fichier | Élément | Taille d'une image | Images | Description visuelle | Raccord / transparence |
|---|---|---|---|---|---|
| `entrance_stairs_down.png` | Escalier qui descend | 48×48 | 1 | ouverture rectangulaire dans le sol, bordée de pierres taillées claires, **marches de pierre qui s'enfoncent** vers le bas de l'image et se perdent dans le noir ; silhouette de l'escalier de DM, lisible à 2 cases | au sol, fond transparent autour de la bordure |
| `entrance_stairs_up.png` | Escalier qui remonte | 48×48 | 1 | même bordure, **marches qui montent** vers le haut de l'image et une **lumière douce** qui tombe d'en haut (on devine la sortie) | idem |
| `entrance_stairs_locked.png` | Escalier fermé | 48×48 | 1 | l'escalier qui remonte, **barré d'une grille de pierre** grise et terne (la sortie d'une embuscade, fermée jusqu'à la fin du combat) | idem |
| `entrance_glow.png` | Lueur d'une entrée active | 48×48 | 8 (boucle, ~110 ms) | anneau de lumière blanc-doré qui pulse sur la bordure et **petites étincelles qui montent** de l'ouverture ; se pose par-dessus n'importe quel escalier | ≈ 60 % opaque autorisé, fond transparent |
| `entrance_reveal.png` | Apparition d'un escalier secret | 48×48 | 8 (~70 ms) | gros nuage de poussière beige qui s'ouvre en cercle, éclats de terre et une étoile blanche au centre ; se dissipe à la fin | ≈ 70 % opaque, jouée une fois |
| `entrance_enter.png` | Entrée prise | 48×48 | 6 (~60 ms) | tourbillon de lumière blanche qui se resserre vers le centre de l'ouverture (le Pokémon y disparaît) | ≈ 70 % opaque, jouée une fois |
| `entrance_icons.png` | Icônes des entrées | 16×16 | 8 | dans l'ordre : escalier qui descend, escalier qui remonte, escalier secret (point d'interrogation gravé), grotte scellée (œil fermé violet, le repère des grottes), donjon de la clé (clé dorée), îlot du boss (couronne), porte de quartier fermée (engrenage gris), porte de quartier ouverte (engrenage turquoise) ; pictogrammes simples, contour sombre, pour la mini-carte et une petite bulle au-dessus de l'entrée | fond transparent |
| `entrance_bubble.png` | Bulle au-dessus d'une entrée | 20×22 | 4 (boucle, ~160 ms) | petite bulle blanche à contour sombre, pointe en bas, qui flotte de 1 px vers le haut et le bas ; le jeu pose une icône de `entrance_icons` dedans | fond transparent |
| `entrance_shadow.png` | Ombre au sol | 48×24 | 1 | ombre ovale douce sous une entrée qui se dresse | ≈ 40 % opaque, noir |

Palettes : pierre `#3a3836 / #6a6560 / #9a948c / #c8c0b4`, noir du fond `#100c14 / #2a2030`, lumière `#a07010 / #f0c030 / #fff0a0 / #ffffff`, poussière `#8a7a64 / #c0b090 / #e8dcc4`, turquoise de l'engrenage `#0a6a72 / #1ec8c8 / #a0fff0`, violet du sceau `#4a2a6a / #8a5ac8 / #d0b0ff`.

Présente les planches sur une seule page, agrandies ×4 : sur une bande d'herbe, puis de sable, puis de roche brune, puis de neige, l'escalier qui descend avec sa lueur, celui qui remonte, celui qui est fermé, et un Pokémon de DM (Pikachu) à côté pour l'échelle ; au-dessus, la bulle avec chaque icône ; en dessous, l'apparition puis l'entrée prise image par image.

---

## Lot 2 : cachettes des escaliers secrets et bordures par biome (5 planches)

Les cachettes sont une surprise : l'indice doit rester **très discret** (feuilles mortes, fissure), sans éclat ni lueur qui attire l'œil.

| Fichier | Élément | Taille d'une image | Images | Description visuelle | Raccord / transparence |
|---|---|---|---|---|---|
| `secret_clearing.png` | Escalier secret de clairière | 48×48 | 1 | au centre d'une clairière : un **cercle de petits champignons** et de pierres moussues autour d'un trou où descendent des marches de bois et de racines | au sol, fond transparent |
| `secret_clearing_hidden.png` | La même, encore cachée | 48×48 | 1 | le même cercle de champignons, mais le trou est **couvert d'un tapis de feuilles mortes** et de mousse (indice discret pour un œil attentif) | idem |
| `secret_rock.png` | Rocher fissuré | 48×48 | 3 | gros rocher brun qui couvre l'escalier, **fissure lumineuse** au milieu ; images : intact, fendu (fissures plus larges), presque brisé (morceaux qui se détachent) ; un coup du bon type le fait passer d'un état à l'autre | se dresse, pied au bas de l'image |
| `secret_rock_break.png` | Rocher qui éclate | 48×48 | 7 (~60 ms) | le rocher éclate en morceaux qui roulent, nuage de poussière, l'escalier apparaît dessous à la fin | jouée une fois |
| `entrance_rim_biomes.png` | Bordures par biome | 48×48 | 6 | la bordure de l'escalier qui descend (même ouverture, mêmes marches que `entrance_stairs_down`) refaite pour chaque sol, dans l'ordre : herbe et racines (prairie, forêt), sable et grès (désert, côte), roche brune (montagne), roche noire et braises (volcan), glace et neige (glacier), dalles gravées (ruines) | au sol, fond transparent |

Présente les planches agrandies ×4 : une clairière de forêt de DM (arbres en boules autour) avec la cachette fermée puis ouverte ; un rocher fissuré dans ses trois états puis qui éclate ; dessous, les six bordures posées chacune sur son sol.

---

## Lot 3 : donjon de la clé et portes de quartier (6 planches)

| Fichier | Élément | Taille d'une image | Images | Description visuelle | Raccord / transparence |
|---|---|---|---|---|---|
| `key_dungeon.png` | Entrée du donjon de la clé | 72×72 | 3 | un **dolmen de pierre ancienne** (deux montants et une dalle posée dessus) devant un escalier qui s'enfonce dans le noir ; sur la dalle, une **clé gravée** ; trois variantes de matière, dans l'ordre : pierre moussue (prairie, forêt, côte), grès ocre (désert, ruines), roche sombre et givrée (montagne, volcan, glacier) | se dresse, pied au bas de l'image |
| `key_dungeon_glow.png` | Runes allumées | 72×72 | 8 (boucle, ~120 ms) | la clé gravée et quelques runes sur les montants **s'allument en doré** et pulsent ; à poser par-dessus `key_dungeon` | fond transparent, seulement les runes et leur lueur |
| `quarter_gate.png` | Porte de quartier fermée | 72×72 | 1 | une **arche de pierre** massive, au sommet un **logement circulaire vide** en forme de roue dentée ; entre les montants, un voile de lumière grise terne qui barre le passage | se dresse, pied au bas de l'image |
| `quarter_gate_open.png` | Porte de quartier ouverte | 72×72 | 8 (boucle, ~100 ms) | la même arche, l'**Engrenage du Temps turquoise** en place dans le logement, qui tourne lentement ; entre les montants, un **tourbillon turquoise** lumineux | idem |
| `quarter_gate_opening.png` | Ouverture de la porte | 72×72 | 10 (~80 ms) | l'engrenage descend dans le logement, s'enclenche avec un éclat, le voile gris se déchire et devient le tourbillon turquoise | idem, jouée une fois |
| `quarter_gate_refuse.png` | Porte refusée | 72×72 | 5 (~70 ms) | le voile gris ondule et repousse, petit éclair gris, le logement vide clignote (l'équipe n'a pas l'engrenage) | fond transparent, jouée une fois |

Palettes : pierre moussue `#3a3836 / #6a6560 / #9a948c / #3a6a2a / #6a9a3a`, grès `#7a4a24 / #b07840 / #e0b070`, roche givrée `#2a2a3a / #4a4a64 / #a0b0d0 / #e8f0ff`, or `#a07010 / #f0c030 / #fff0a0`, turquoise `#0a6a72 / #1ec8c8 / #a0fff0`.

Présente les planches agrandies ×4 : l'entrée du donjon de la clé dans ses trois matières sur l'herbe, le sable et la roche, avec ses runes allumées ; la porte de quartier fermée, refusée, en train de s'ouvrir puis ouverte, avec un Pokémon de DM (Pikachu) devant pour l'échelle.
