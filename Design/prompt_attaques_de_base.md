# Prompt Claude Design : attaques de base (17 types)

Crée des planches d'effets en pixel art pour 17 petites attaques à distance d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Ce sont des attaques de base que le joueur tire en continu, plusieurs fois par seconde. Elles doivent rester **petites, lisibles et discrètes** pour ne pas saturer l'écran quand trois Pokémon tirent en même temps.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, avec fond transparent et toutes les images sur **une seule ligne**, de gauche à droite.
- Le projectile **pointe vers la droite** (le jeu le fait pivoter dans la direction du tir).
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Deux planches par attaque : `<clé>_vol.png` (en boucle, 3 ou 4 images) et `<clé>_impact.png` (sans boucle, 3 ou 4 images, très court : 200 à 300 ms au total).
- Livre aussi les entrées `effects.json` correspondantes : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.
- La trajectoire est gérée par le jeu : dessine seulement le projectile lui-même, sans traînée qui suppose une direction courbe.

## Liste

| Clé | Attaque | Type | Image de vol | Description visuelle | Impact |
|---|---|---|---|---|---|
| meteores | Météores | Normal | 12×12 | petite étoile jaune pâle à 5 branches qui tourne sur elle-même | éclat d'étoile en croix, 16×16 |
| flammeche | Flammèche | Feu | 12×12 | petite flamme en goutte, cœur jaune, qui vacille (tirée par 3 en éventail) | petite gerbe de flammèches, 16×16 |
| pistolet_a_o | Pistolet à O | Eau | 20×16 | grosse goutte d'eau ronde et lourde, reflet blanc, légère déformation qui ondule | grosse éclaboussure en couronne, 24×24 |
| tranch_herbe | Tranch'Herbe | Plante | 12×12 | feuille fine en lame qui tourne | feuille coupée en 2 + brins, 16×16 |
| eclair | Éclair | Électrik | 16×8 | petit trait électrique crénelé, blanc-jaune | étincelle en étoile, 16×16 |
| poudreuse | Poudreuse | Glace | 10×10 | flocon cristallin à 6 branches | éclats de givre, 16×16 |
| onde_vide | Onde Vide | Combat | 24×24 | croissant d'air blanc-orangé, concave vers l'arrière (le jeu l'agrandit en vol) | souffle en arc qui se dissipe, 24×24 |
| acide | Acide | Poison | 12×12 | goutte d'acide violette avec reflet vert vif | flaque qui éclabousse en étoile, 24×24 |
| coud_boue | Coud'Boue | Sol | 10×10 | motte de boue brune irrégulière | tache de boue qui gicle, 16×16 |
| tornade | Tornade | Vol | 24×24 | petit tourbillon d'air vu de dessus, spirale blanc-bleu, qui tourne | dispersion de rafales, 24×24 |
| choc_mental | Choc Mental | Psy | 16×16 | orbe rose-violet avec anneau pulsant | anneau psychique qui s'étend, 24×24 |
| dard_nuee | Dard-Nuée | Insecte | 8×6 | minuscule dard vert-jaune pointu | petit éclat vert, 12×12 |
| jet_pierres | Jet-Pierres | Roche | 20×20 | caillou gris-beige anguleux qui roule | pierre qui éclate en 3 ou 4 fragments + poussière, 24×24 |
| vent_mauvais | Vent Mauvais | Spectre | 20×20 | volute d'ombre violette spectrale aux bords ondulants, translucide | volute qui s'évapore, 24×24 |
| draco_rage | Draco-Rage | Dragon | 16×16 | boule de flamme bleu-violet en rotation (elle reviendra vers le lanceur, donc forme symétrique) | flammes bleues en éventail, 24×24 |
| poursuite | Poursuite | Ténèbres | 12×12 | pointe d'ombre noire bordée de rouge sombre, effilée | éclat noir en étoile, 16×16 |
| miroi_tir | Miroi-Tir | Acier | 12×12 | disque de lumière métallique argenté avec reflet blanc | éclat métallique scintillant, 16×16 |

Palettes : reste cohérent avec les couleurs de type déjà utilisées (Feu `#c42418 / #f0741a / #ffd23a`, Eau `#1846c8 / #2f82f2 / #8ccaff`, Plante `#2b6e2a / #4fae38 / #9fe05a`, Électrik `#d68a00 / #ffd21f / #fff6a8`, Glace `#4f8fd8 / #a6dcff / #e6f8ff`, Psy `#7a2ea8 / #d04ab0 / #ff92d6`, Poison `#5b2585 / #9447c2 / #a8ff3e`, Sol `#4a2610 / #7c4620 / #b0763a`).

Présente les 17 attaques sur une seule page, avec chaque animation jouée en boucle, agrandie ×4, sur un fond sombre et sur un fond de sol clair.
