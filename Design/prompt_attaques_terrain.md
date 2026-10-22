# Prompt Claude Design : attaques à effet de terrain (14 attaques)

Crée des planches d'effets en pixel art pour 14 attaques d'un roguelike d'action vu de dessus, dans le style de Pokémon Donjon Mystère : Explorateurs du Ciel (DS). Ce sont des attaques **fortes** (pas des attaques de base) : elles doivent avoir du poids et se distinguer au premier coup d'œil, tout en restant lisibles quand trois Pokémon combattent. Chacune laisse aussi une trace au sol (braises, flaque, ronces, sol chargé, verglas, poison, boue) que le jeu dessine lui-même : **ne dessine pas la trace**, seulement l'attaque.

## Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case de terrain.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Le projectile et le coup **pointent vers la droite** (le jeu les fait pivoter dans la direction de l'attaque). Les ondes autour du lanceur sont vues de dessus, centrées.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement**, 4 à 6 couleurs par attaque, contour sombre d'une couleur.
- Planches : `<clé>_vol.png` (projectile en vol, en boucle, 3 ou 4 images), `<clé>_impact.png` (sans boucle, 3 à 5 images, 200 à 350 ms au total), `<clé>_coup.png` (coup au corps à corps, sans boucle, 3 ou 4 images), `<clé>_onde.png` (onde autour du lanceur, sans boucle, 5 ou 6 images, ~450 ms au total).
- Livre aussi les entrées `effects.json` correspondantes : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms` (une valeur par image), `loop`, `palette`.
- La trajectoire est gérée par le jeu : dessine seulement le projectile lui-même.

## Liste

| Clé | Attaque | Type | Planches (taille d'une image) | Description visuelle |
|---|---|---|---|---|
| lance_flamme | Lance-Flamme | Feu | vol 24×16, impact 24×24 | jet de flammes allongé et rapide, cœur jaune vif, langues orange qui ondulent vers l'arrière ; impact : gerbe de flammes qui s'écrase |
| deflagration | Déflagration | Feu | vol 24×24, impact 32×32 | grosse boule de feu dense qui tourne, avec 5 pointes de flamme (la forme d'étoile « 大 » des jeux) ; impact : explosion en étoile à 5 branches |
| hydrocanon | Hydrocanon | Eau | vol 24×16, impact 24×24 | puissant jet d'eau sous pression, avant arrondi, écume blanche, traînée courte ; impact : grosse éclaboussure en couronne |
| hydroblast | Hydroblast | Eau | vol 24×24, impact 32×32 | sphère d'eau tourbillonnante avec un anneau d'écume, très lourde ; impact : explosion d'eau avec gouttes projetées |
| canon_graine | Canon Graine | Plante | vol 12×12, impact 16×16 | grosse graine brune et dure, reflet, qui tourne ; impact : la graine éclate et quelques pousses vertes jaillissent |
| megafouet | Mégafouet | Plante | coup 32×32 | deux lianes épaisses qui fouettent vers l'avant en arc (de haut en bas puis de bas en haut), feuilles qui volent |
| vege_attak | Végé-Attak | Plante | vol 24×24, impact 32×32 | énorme racine épineuse qui jaillit vers l'avant, bois sombre et épines claires ; impact : racines qui surgissent du sol en étoile |
| tonnerre | Tonnerre | Électrik | onde 88×88 | 6 à 8 éclairs jaune-blanc qui partent du centre vers l'extérieur, crépitement, cercle d'étincelles au bord (rayon ≈ 1,8 case) |
| fatal_foudre | Fatal-Foudre | Électrik | vol 24×16, impact 32×32 | décharge électrique massive en zigzag, cœur blanc, bords jaunes ; impact : la foudre s'abat, étincelles en étoile |
| laser_glace | Laser Glace | Glace | vol 24×12, impact 24×24 | faisceau de glace bleu pâle, segment net qui se répète, cristaux qui scintillent ; impact : éclatement de cristaux |
| blizzard | Blizzard | Glace | onde 96×96 (le jeu l'agrandit ×2) | tempête de neige circulaire qui tourbillonne autour du centre, flocons et rafales blanc-bleu qui s'étendent puis se dissipent |
| detritus | Détritus | Poison | vol 16×16, impact 24×24 | paquet de boue toxique violette qui tremble, bulles vert acide ; impact : la boue éclabousse en étoile |
| bomb_beurk | Bomb-Beurk | Poison | vol 20×20, impact 32×32 | grosse bombe de boue violette, contour sombre, bulles qui éclatent ; impact : explosion de boue toxique |
| boue_bombe | Boue-Bombe | Sol | vol 16×16, impact 24×24 | boule de boue brune compacte qui tourne, mottes ; impact : la boue s'écrase et gicle |

Palettes : reste cohérent avec les couleurs de type déjà utilisées (Feu `#c42418 / #f0741a / #ffd23a`, Eau `#1846c8 / #2f82f2 / #8ccaff`, Plante `#2b6e2a / #4fae38 / #9fe05a`, Électrik `#d68a00 / #ffd21f / #fff6a8`, Glace `#4f8fd8 / #a6dcff / #e6f8ff`, Poison `#5b2585 / #9447c2 / #a8ff3e`, Sol `#4a2610 / #7c4620 / #b0763a`).

Présente les 14 attaques sur une seule page, chaque animation jouée en boucle, agrandie ×3, sur un fond sombre et sur un fond de sol clair.
