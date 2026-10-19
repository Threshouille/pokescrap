# Prompt Claude Design : obstacles faits main (6 tuiles)

Crée des tuiles en pixel art pour les **obstacles** d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). L'arène contient de l'eau profonde, de la lave et du vide (dessinés avec les décors du jeu d'origine : eau bleue, lave rouge-orangé, vide noir). Ces 6 tuiles complètent : des ponts qui franchissent l'eau ou le vide, ce qu'il en reste quand ils brûlent, de la végétation qui pousse sur l'eau, de la roche de lave refroidie et des murs de glace. Elles se posent case par case et doivent se **répéter sans couture**.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case. **Une tuile = 24 × 24 px.**
- Une planche PNG par tuile, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement**, 4 à 6 couleurs par tuile, contour sombre d'une couleur quand c'est utile.
- **Raccord parfait** sur les bords indiqués (les ponts se posent en ligne et sur 2 cases de large).
- Vue de dessus, éclairage venant du haut à gauche.
- Nom des fichiers : `obstacle_<clé>.png`. Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Liste

| Clé | Élément | Images | Description visuelle | Transparence |
|---|---|---|---|---|
| bridge | Pont | 1 | tablier de planches de bois **horizontales** (le pont part vers le haut : on marche du bas vers le haut), clous sombres, bois brun chaud ; les bouts des planches débordent légèrement à gauche et à droite et sont plus sombres pour suggérer les bords | aucune sur la surface, raccord haut/bas **et** gauche/droite (2 tuiles côte à côte forment un pont large) |
| burnt_bridge | Pont brûlé | 4 | les mêmes planches, calcinées : poutres noires fendues, quelques braises rougeoyantes qui pulsent, des **trous** entre les poutres (on voit le vide ou l'eau dessous) | trous transparents (≈ 50 %) |
| vine_bridge | Pont de lianes | 1 | lianes vertes tressées en travers, feuilles, nœuds plus clairs, qui remplacent les planches | petits trous entre les lianes (≈ 20 %) |
| lily | Végétation flottante | 4 | grands nénuphars et feuilles flottantes sur lesquels on peut marcher, petites fleurs blanches ; léger balancement | l'eau apparaît entre les feuilles (≈ 35 %) |
| rock | Roche refroidie | 4 | basalte sombre et rugueux, fissures où rougeoie encore un peu de chaleur qui faiblit puis revient lentement | aucune, raccord sur les 4 bords |
| ice_wall | Mur de glace | 1 | bloc de glace épais vu de dessus : dessus clair et brillant avec un reflet, face avant (bas de la tuile) plus sombre sur 6 px pour donner de la hauteur, fissures fines | aucune, raccord gauche/droite et haut/bas (les murs font 3 cases en ligne) |

Rythme des animations : braises du pont brûlé ~150 ms par image, nénuphars ~300 ms, roche ~400 ms.

Palettes : bois `#5a3a1e / #9a6a3a / #c89a5a`, charbon `#1a1412 / #2c2420` + braises Feu `#c42418 / #f0741a`, Plante `#2b6e2a / #4fae38 / #9fe05a`, roche `#2a2226 / #4a3a36 / #6a564e` + lueur `#c4481c`, Glace `#4f8fd8 / #a6dcff / #e6f8ff`.

Présente les 6 tuiles sur une seule page, agrandies ×4 : le pont, le pont brûlé et le pont de lianes en bande de 2 × 5 cases posée sur du vide noir et sur de l'eau bleue ; les nénuphars en zone de 4 × 4 sur de l'eau bleue ; la roche en zone de 4 × 4 au milieu de lave rouge ; le mur de glace en ligne de 3 cases sur un sol de donjon gris-brun.
