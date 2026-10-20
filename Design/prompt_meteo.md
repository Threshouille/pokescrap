# Prompt Claude Design : météos, sports et sable mouvant (11 planches)

Crée des planches en pixel art pour les **météos** et les **zones** d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Pendant 20 secondes, une météo recouvre toute l'arène : pluie, grêle, tempête de sable ou grand soleil. Le jeu **répète le motif sur tout l'écran et le fait défiler** (vers le bas pour la pluie et le soleil, dans le sens du vent pour la grêle et la tempête de sable) : dessine des motifs **sans direction marquée** et **légers**, pour que le combat reste lisible dessous. S'y ajoutent deux zones circulaires (Tourniquet, Lance-Boue), la tuile du sable mouvant et de petites icônes pour l'écran.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement**, 3 à 5 couleurs par planche.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms` (une valeur par image), `loop`, `palette`.

## Liste

| Fichier | Élément | Taille d'une image | Images | Description visuelle | Raccord / couverture |
|---|---|---|---|---|---|
| `weather_rain.png` | Pluie | 48×48 | 4 (boucle, ~80 ms) | gouttes de pluie : petits traits **verticaux** de 3 à 5 px, bleu clair et blanc, répartis irrégulièrement ; quelques éclaboussures en anneau | **raccord sur les 4 bords**, ≈ 8 % couvert |
| `weather_hail.png` | Grêle | 48×48 | 4 (boucle, ~100 ms) | grêlons ronds de 2 à 3 px, blanc et bleu pâle, quelques éclats qui rebondissent | raccord sur les 4 bords, ≈ 6 % couvert |
| `weather_sand.png` | Tempête de sable | 48×48 | 4 (boucle, ~90 ms) | grains de sable et petits nuages de poussière ocre **sans direction** (points et volutes rondes), plus denses que la pluie | raccord sur les 4 bords, ≈ 15 % couvert |
| `weather_sun.png` | Grand soleil | 48×48 | 4 (boucle, ~200 ms) | petits éclats de lumière jaune pâle qui scintillent, quelques poussières dorées | raccord sur les 4 bords, ≈ 4 % couvert |
| `weather_icon_rain.png` | Icône pluie | 16×16 | 1 | nuage gris-bleu et gouttes | contour sombre |
| `weather_icon_hail.png` | Icône grêle | 16×16 | 1 | nuage pâle et grêlons | contour sombre |
| `weather_icon_sand.png` | Icône tempête de sable | 16×16 | 1 | tourbillon de sable ocre | contour sombre |
| `weather_icon_sun.png` | Icône soleil | 16×16 | 1 | soleil jaune à rayons | contour sombre |
| `sport_water_sport.png` | Tourniquet | 96×96 | 4 (boucle, ~150 ms) | anneau d'eau vu de dessus (le jeu l'étire sur un disque de 8 cases) : bord fait de gouttes et d'éclaboussures bleues qui tournent, intérieur presque vide (quelques gouttes) | fond transparent, ≈ 20 % couvert |
| `sport_mud_sport.png` | Lance-Boue | 96×96 | 4 (boucle, ~150 ms) | anneau de boue projetée : mottes et éclaboussures brunes qui tournent sur le bord, intérieur presque vide | fond transparent, ≈ 20 % couvert |
| `obstacle_quicksand.png` | Sable mouvant | 24×24 | 4 (boucle, ~180 ms) | sable ocre plus foncé que le sol, creusé d'une petite spirale qui tourne lentement vers le centre, quelques grains qui glissent | **raccord sur les 4 bords** (zones de plusieurs cases), opaque |

Palettes : Eau `#1846c8 / #2f82f2 / #8ccaff` + blanc, Glace `#4f8fd8 / #a6dcff / #e6f8ff`, sable `#7c5a2a / #b08040 / #d8b070 / #f0d8a0`, soleil `#ffd23a / #fff6a8`, boue `#4a2610 / #7c4620 / #b0763a`, nuage `#5a6478 / #9aa4b0`.

Présente les planches sur une seule page, agrandies ×3 : chaque météo répétée en 3 × 2 motifs par-dessus un sol de donjon gris-brun ; les deux zones posées sur ce sol ; une zone de 4 × 3 cases de sable mouvant au milieu d'un sol de sable ; les 4 icônes en ligne.
