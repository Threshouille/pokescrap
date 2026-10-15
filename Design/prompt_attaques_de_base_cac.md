# Prompt Claude Design : attaques de base au corps à corps (17 types)

Crée des planches d'effets en pixel art pour 17 attaques au corps à corps d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Ce sont des attaques de base que le joueur enchaîne environ une fois par seconde. Elles doivent être **vives et très courtes** : le coup part, se voit nettement, puis disparaît.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, avec fond transparent et toutes les images sur **une seule ligne**, de gauche à droite.
- L'effet **pointe vers la droite** (le jeu le fait pivoter dans la direction du coup) ; le Pokémon qui frappe se trouve à gauche de l'image.
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Planches sans boucle, très courtes : 3 à 5 images, 150 à 250 ms au total. Les traînées de ruée (`_trainee`) sont en boucle, 4 images.
- Livre aussi les entrées `effects.json` correspondantes : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.

## Types de planche
- `_coup` : le coup lui-même, centré sur la zone touchée (arc, estoc, morsure).
- `_trainee` : traînée qui suit le Pokémon pendant une ruée, dessinée derrière lui.
- `_impact` : petit éclat sur l'ennemi touché, 16×16 ou 24×24, 3 images.

## Liste

| Clé | Attaque | Type | Planches (taille de l'image) | Description visuelle |
|---|---|---|---|---|
| griffe | Griffe | Normal | `griffe_coup` existe déjà : à refaire en arc de 100°, 36×48 ; `griffe_impact` 16×16 | 3 traits de griffe blancs en arc |
| roue_de_feu | Roue de Feu | Feu | `roue_de_feu_trainee` 32×24 ; `roue_de_feu_impact` 24×24 | anneau de flammes qui tourne autour du Pokémon, traînée de flammèches |
| aqua_jet | Aqua-Jet | Eau | `aqua_jet_trainee` 40×24 ; `aqua_jet_impact` 24×24 | gaine d'eau effilée vers l'avant, gouttelettes derrière |
| fouet_lianes | Fouet Lianes | Plante | `fouet_lianes_coup` 60×16 ; `fouet_lianes_impact` 16×16 | liane verte qui claque en ligne droite, extrémité en fouet |
| etincelle | Étincelle | Électrik | `etincelle_trainee` 32×24 ; `etincelle_impact` 24×24 ; `etincelle_decharge` 48×48 | corps entouré d'étincelles, puis décharge circulaire à l'arrivée |
| crocs_givre | Crocs Givre | Glace | `crocs_givre_coup` 24×24 ; `crocs_givre_impact` 16×16 | mâchoires de glace qui se referment, éclats de givre |
| mach_punch | Mach Punch | Combat | `mach_punch_coup` 20×20 ; `mach_punch_impact` 16×16 | poing flou avec lignes de vitesse, très rapide |
| dard_venin | Dard-Venin | Poison | `dard_venin_coup` 44×12 ; `dard_venin_impact` 16×16 | dard violet effilé, goutte vert vif à la pointe |
| ampleur | Ampleur | Sol | `ampleur_onde` 96×96 ; `ampleur_impact` 16×16 | onde de choc au sol vue de dessus, fissures et poussière, anneau qui s'élargit |
| picpic | Picpic | Vol | `picpic_coup` 32×12 ; `picpic_impact` 16×16 | bec pointu stylisé blanc-jaune, trait d'estoc |
| coupe_psycho | Coupe Psycho | Psy | `coupe_psycho_coup` 40×64 ; `coupe_psycho_impact` 16×16 | grande lame d'énergie rose-violet en arc large de 160° |
| taillade | Taillade | Insecte | `taillade_coup` 24×36 ; `taillade_impact` 16×16 | faux verte en arc, plus lumineuse si possible sur une variante (3 intensités : `taillade_coup`, `taillade_coup_2`, `taillade_coup_3`) |
| roulade | Roulade | Roche | `roulade_trainee` 32×24 ; `roulade_impact` 24×24 | boule de roche qui roule, poussière et cailloux derrière |
| ombre_portee | Ombre Portée | Spectre | `ombre_portee_trainee` 40×16 ; `ombre_portee_coup` 24×32 ; `ombre_portee_impact` 16×16 | traînée d'ombre au sol (le Pokémon se fond dans son ombre), puis griffure d'ombre violette |
| dracogriffe | Dracogriffe | Dragon | `dracogriffe_coup` 28×36 ; `dracogriffe_impact` 24×24 | 3 griffes bleu-violet lumineuses (le jeu en joue deux, croisées en X) |
| morsure | Morsure | Ténèbres | `morsure_coup` 24×24 ; `morsure_impact` 16×16 | mâchoires noires aux crocs blancs qui claquent |
| pisto_poing | Pisto-Poing | Acier | `pisto_poing_trainee` 32×16 ; `pisto_poing_impact` 24×24 | poing métallique argenté, éclair blanc au contact |

Palettes : reste cohérent avec les couleurs de type des attaques de base à distance (Feu `#c42418 / #f0741a / #ffd23a`, Eau `#1846c8 / #2f82f2 / #8ccaff`, Plante `#2b6e2a / #4fae38 / #9fe05a`, Électrik `#d68a00 / #ffd21f / #fff6a8`, Glace `#4f8fd8 / #a6dcff / #e6f8ff`, Psy `#7a2ea8 / #d04ab0 / #ff92d6`, Poison `#5b2585 / #9447c2 / #a8ff3e`, Sol `#4a2610 / #7c4620 / #b0763a`).

Présente les 17 attaques sur une seule page, chaque animation jouée en boucle, agrandie ×4, sur un fond sombre et sur un fond de sol clair.
