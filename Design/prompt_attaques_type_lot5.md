# Prompt Claude Design : lot 5, crocs, poings et griffes (17 attaques)

Crée des planches d'effets en pixel art pour 17 attaques au corps à corps d'un roguelike vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Pour chaque attaque, inspire-toi de son animation dans les jeux Pokémon sur DS (Diamant / Perle / Platine, Explorateurs du Ciel), tout en restant cohérent avec les planches déjà livrées.

## Références jointes (dossier `refs_lot5_crocs_poings_griffes`)
Ces planches sont déjà en jeu ; les nouvelles doivent avoir **la même lisibilité, le même trait, les mêmes tailles et le même rythme** :
- Crocs : `crocs_givre_coup` (24×24, 4 images : 40, 40, 60, 70 ms), `crocs_givre_impact` (16×16, 3 images), `morsure_coup`, `morsure_impact` (mêmes formats).
- Poings : `mach_punch_coup` (20×20, 3 images : 40, 50, 70 ms), `mach_punch_impact` (16×16), `pisto_poing_coup` (32×24, 4 images), `pisto_poing_impact`.
- Griffes : `griffe_coup` (36×48, 4 images : 30, 50, 60, 70 ms, arc de 100°), `griffe_impact` (16×16), `dracogriffe_coup` (28×36), `dracogriffe_impact` (24×24), `tranche_coup` (40×56, 2 entailles).
- Les fichiers `_x4` sont les mêmes planches agrandies quatre fois, pour lire le détail.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- L'effet **pointe vers la droite** (le jeu le fait pivoter dans la direction du coup) ; le Pokémon qui frappe est à gauche de l'image.
- Pixel art net : pas d'anticrénelage ni de flou, 4 ou 5 couleurs par attaque, contour sombre d'une couleur.
- Planches sans boucle, courtes : 3 à 5 images, 150 à 300 ms au total.
- Livre aussi `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`, `loop`, `palette`.
- Range les planches dans `effects_v21_type_lot5/`, avec un dossier `apercu_x4/`.

## Palettes de type
Feu `#c42418 / #f0741a / #ffd23a` ; Glace `#4f8fd8 / #a6dcff / #e6f8ff` ; Électrik `#d68a00 / #ffd21f / #fff6a8` ; Poison `#5b2585 / #9447c2 / #a8ff3e` ; Ténèbres `#1e1a24 / #4a3f5c / #c8c0d8` ; Spectre `#3a2266 / #6a48b0 / #b89aff` ; Acier `#5a6470 / #a8b4c0 / #f0f6ff` ; Combat `#8a2a12 / #d0562a / #ffb070` ; Normal `#6a6a6a / #c8c8c8 / #ffffff`.

## Liste

### Crocs (modèle : `crocs_givre_coup`, mâchoires qui se referment)
| Clé | Attaque | Type | Planches | Description |
|---|---|---|---|---|
| crocs_feu | Crocs Feu | Feu | `crocs_feu_coup` 24×24 ; `crocs_feu_impact` 16×16 | mâchoires enflammées, flammèches qui jaillissent à la fermeture |
| crocs_eclair | Crocs Éclair | Électrik | `crocs_eclair_coup` 24×24 ; `crocs_eclair_impact` 16×16 | crocs jaunes crépitants, petits arcs électriques entre les dents |
| crochetvenin | Crochetvenin | Poison | `crochetvenin_coup` 24×24 ; `crochetvenin_impact` 16×16 | deux longs crochets violets, gouttes de venin vert vif |
| machouille | Mâchouille | Ténèbres | `machouille_coup` 28×28 ; `machouille_impact` 16×16 | grandes mâchoires sombres, plus massives que Morsure, claquement net |
| croc_de_mort | Croc de Mort | Normal | `croc_de_mort_coup` 28×28 ; `croc_de_mort_impact` 16×16 | deux énormes incisives blanches qui se plantent |

### Poings (modèle : le **poing iconique de Pokémon**, celui de l'icône du type Combat, que l'on voit aussi dans les animations d'attaque des jeux GBA / DS : poing fermé vu de face, blanc, contour épais, éclat en étoile derrière ; colorié selon le type)
| Clé | Attaque | Type | Planches | Description |
|---|---|---|---|---|
| poing_de_feu | Poing de Feu | Feu | `poing_de_feu_coup` 24×24 ; `poing_de_feu_impact` 24×24 | poing iconique enveloppé de flammes, gerbe de feu à l'impact |
| poinglace | Poinglace | Glace | `poinglace_coup` 24×24 ; `poinglace_impact` 24×24 | poing iconique givré, éclats de glace qui volent à l'impact |
| poing_eclair | Poing-Éclair | Électrik | `poing_eclair_coup` 24×24 ; `poing_eclair_impact` 24×24 | poing iconique chargé, éclair en zigzag à l'impact |
| poing_ombre | Poing Ombre | Spectre | `poing_ombre_coup` 24×24 ; `poing_ombre_impact` 16×16 | poing iconique fantôme violet, semi-transparent, qui surgit d'une ombre au sol |
| vampipoing | Vampipoing | Combat | `vampipoing_coup` 24×24 ; `vampipoing_impact` 16×16 ; `vampipoing_soin` 16×16 | poing iconique orange, petites sphères vertes aspirées vers le lanceur (`_soin` : jouée sur le lanceur) |
| marto_poing | Marto-Poing | Combat | `marto_poing_coup` 48×48 ; `marto_poing_impact` 64×64 | un long manche avec un gros poing iconique au bout, abattu par le lanceur comme un marteau (vu de dessus : le manche part de la gauche et le poing s'écrase au centre-droit de l'image) ; impact : large onde au sol, fissures et poussière qui partent du centre |
| mitra_poing | Mitra-Poing | Combat | `mitra_poing_charge` 32×32 (en boucle, 4 images, centrée sur le lanceur) ; `mitra_poing_coup` 24×24 ; `mitra_poing_impact` 16×16 | concentration : aura orange qui pulse ; puis rafale d'une dizaine de petits poings iconiques très rapides (`_coup` : 3 images très courtes, jouée à chaque coup, légèrement décalée au hasard), petit éclat à chaque coup |
| ultimapoing | Ultimapoing | Normal | `ultimapoing_charge` 32×32 (en boucle, centrée sur le lanceur) ; `ultimapoing_coup` 32×32 ; `ultimapoing_impact` 32×32 ; `ultimapoing_onde` 72×72 | charge : lueur blanche qui grossit autour du poing ; coup : gros poing iconique blanc ; onde : onde de choc en cône de 90° qui part du lanceur vers la droite (arcs blancs qui s'élargissent) |

### Griffes (modèle : `griffe_coup` / `dracogriffe_coup`, traits en arc)
| Clé | Attaque | Type | Planches | Description |
|---|---|---|---|---|
| combo_griffe | Combo-Griffe | Normal | `combo_griffe_coup` 28×36 ; `combo_griffe_impact` 16×16 | 3 traits blancs fins et rapides, à jouer plusieurs fois de suite (2 à 5 coups) : variante en miroir `combo_griffe_coup_b` pour alterner |
| griffe_acier | Griffe Acier | Acier | `griffe_acier_coup` 36×48 ; `griffe_acier_impact` 16×16 | 3 griffes métalliques argentées, reflet blanc qui glisse le long des traits |
| griffe_ombre | Griffe Ombre | Spectre | `griffe_ombre_coup` 36×48 ; `griffe_ombre_impact` 16×16 | grande griffe d'ombre violette qui sort du sol en arc |
| eclategriffe | Éclategriffe | Normal | `eclategriffe_coup` 36×48 ; `eclategriffe_impact` 24×24 | 3 griffes larges qui écrasent, éclats de carapace à l'impact |

Présente les 17 attaques sur une seule page, chaque animation jouée en boucle, agrandie ×4, sur un fond sombre et sur un fond de sol clair, à côté de la référence correspondante pour comparaison.
