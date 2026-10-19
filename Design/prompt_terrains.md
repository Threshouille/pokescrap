# Prompt Claude Design : tuiles de terrain (8 terrains)

Crée des tuiles animées en pixel art pour les **effets de terrain** d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Certaines attaques laissent une trace au sol, case par case : du verglas, des braises, une flaque, etc. Ces tuiles se posent **par-dessus le sol du donjon** (roche, sable, cristaux) et doivent le laisser deviner. Elles couvrent parfois une seule case, parfois une zone de 5 × 5 cases : elles doivent se **répéter sans couture** et rester lisibles quand trois Pokémon se battent dessus.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case. **Une tuile = 24 × 24 px.**
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement** (le jeu gère lui-même la transparence et le fondu de fin), 4 à 6 couleurs par terrain.
- **Raccord parfait** : les bords gauche/droite et haut/bas se prolongent, aucune bordure ni contour autour de la tuile (posées côte à côte, on ne doit pas voir la grille).
- **Ne pas tout couvrir** : laisser des trous transparents pour que le sol du donjon reste visible (taux de couverture indiqué pour chaque terrain).
- Deux planches par terrain :
  - `terrain_<clé>.png` : la tuile principale, **4 images en boucle** ;
  - `terrain_<clé>_b.png` : une variante de la même tuile (motif décalé, autres détails), mêmes images et même rythme, mélangée au hasard avec la principale pour casser la répétition.
- Livre aussi les entrées `effects.json` correspondantes : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop` (true), `palette`.
- Vue de dessus, éclairage venant du haut à gauche comme le reste du jeu. Pas d'ombre portée hors de la tuile.

## Liste

| Clé | Terrain | Couverture | Description visuelle | Animation (4 images) |
|---|---|---|---|---|
| ice | Verglas | 80 % | plaque de glace lisse bleu très pâle, quelques fissures fines blanches et un reflet en diagonale | un reflet blanc qui glisse lentement en diagonale (~250 ms par image) |
| embers | Braises | 50 % | charbons rougeoyants épars sur sol noirci, petites flammèches orange | les braises pulsent (rouge sombre → orange → jaune), une flammèche apparaît et disparaît (~150 ms) |
| water | Flaque | 85 % | eau peu profonde bleue, reflets clairs, bord irrégulier à l'intérieur de la tuile mais raccord sur les bords | ondulations concentriques douces, reflets qui bougent (~200 ms) |
| electric | Sol chargé | 40 % | sol parcouru d'arcs électriques jaunes en zigzag, quelques étincelles blanches | les arcs changent de forme à chaque image, comme un crépitement (~90 ms) |
| electrified | Eau électrifiée | 85 % | la même flaque bleue que `water`, parcourue d'arcs électriques jaune vif à sa surface | arcs qui sautent d'un endroit à l'autre, eau qui scintille (~90 ms) ; le jeu la fait aussi clignoter bleu / jaune |
| brambles | Ronces | 70 % | enchevêtrement de tiges vert sombre avec épines claires et quelques feuilles | les tiges ondulent très légèrement, une feuille bouge (~250 ms) |
| mud | Boue | 85 % | boue brune épaisse, flaques plus sombres, quelques bulles | une bulle gonfle puis éclate, surface qui frémit (~200 ms) |
| toxic | Flaque toxique | 80 % | liquide violet épais, reflets vert acide, bulles | des bulles vertes montent et éclatent (~180 ms) |

Palettes : reste cohérent avec les couleurs de type déjà utilisées (Feu `#c42418 / #f0741a / #ffd23a`, Eau `#1846c8 / #2f82f2 / #8ccaff`, Plante `#2b6e2a / #4fae38 / #9fe05a`, Électrik `#d68a00 / #ffd21f / #fff6a8`, Glace `#4f8fd8 / #a6dcff / #e6f8ff`, Poison `#5b2585 / #9447c2 / #a8ff3e`, Sol `#4a2610 / #7c4620 / #b0763a`). Les braises peuvent ajouter un noir charbon (`#2a1a14`).

Présente les 8 terrains sur une seule page : pour chacun, une zone de 4 × 4 tuiles (principale et variante mélangées) qui joue l'animation en boucle, agrandie ×3, posée sur un sol de donjon gris-brun, puis la même zone sur un sol clair (sable).
