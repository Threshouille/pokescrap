# Prompt Claude Design : talents et mégas (2 lots)

Les deux lots s'envoient séparément. Le lot 1 habille les talents qui agissent sur les autres (marques sur les
ennemis touchés, déclenchements) ; le lot 2 donne de la prestance aux mégas (aura commune, signature de chaque méga).

---

## Lot 1 : marques et déclenchements des talents (16 planches)

Crée des planches en pixel art pour un roguelike d'action en temps réel vu de dessus, dans le style de Pokémon Donjon
Mystère : Explorateurs du Ciel (DS). Chaque Pokémon a un **talent** passif. Certains agissent sur les adversaires :
un sceau d'ombre au sol empêche les ennemis proches d'esquiver, un regard menaçant baisse leur attaque, un cadenas
bloque l'attaque qui vient de frapper… Le joueur doit **reconnaître d'un coup d'œil** qui est touché et par quoi, au
milieu d'un combat chargé. Les effets doivent donc être nets, lisibles à petite taille, et ne jamais cacher le
Pokémon : marques **au sol** sous ses pieds, ou **petites icônes** au-dessus de sa tête.

### Format (identique aux livraisons précédentes)
- Échelle : 24 px = 1 case ; un Pokémon fait environ une case.
- Une planche PNG par animation, fond transparent, toutes les images sur **une seule ligne**, de gauche à droite.
- Pixel art net : pas d'anticrénelage ni de flou, **alpha 0 ou 255 uniquement** (sauf mention : les marques au sol
  peuvent être à ≈ 70 % d'opacité pour laisser voir le décor), 4 à 6 couleurs par planche, contour sombre d'une couleur.
- Éclairage venant du haut à gauche. Les marques au sol sont vues **de dessus** (ellipse aplatie : 2 fois plus large
  que haute). Les icônes au-dessus de la tête sont vues de face, centrées.
- Livre aussi les entrées `effects.json` : `file`, `frame_width`, `frame_height`, `frame_count`, `frame_duration_ms`
  (une valeur par image), `loop`, `palette`, et `below: true` pour les marques au sol (dessinées sous les Pokémon).

### Liste

| Fichier | Talent | Taille d'une image | Images | Description visuelle | Pose / boucle |
|---|---|---|---|---|---|
| `ability_trap_shadow.png` | Marque Ombre, Méga-Ectoplasma | 48×24 | 6 (boucle, ~120 ms) | sceau d'ombre violet-noir au sol, cercle de runes qui tourne lentement, petites mains d'ombre qui dépassent du bord | au sol sous chaque ennemi piégé |
| `ability_trap_shadow_drain.png` | Méga-Ectoplasma (drain) | 16×24 | 6 (~80 ms) | volute d'ombre violette qui monte depuis l'ennemi piégé et se dissipe vers le haut | au-dessus de l'ennemi, jouée à chaque perte de PV |
| `ability_trap_sand.png` | Piège Sable | 48×24 | 6 (boucle, ~120 ms) | petit tourbillon de sable ocre au sol, grains qui spiralent vers le centre | au sol sous chaque ennemi piégé |
| `ability_trap_magnet.png` | Magnépiège | 48×24 | 4 (boucle, ~120 ms) | cercle magnétique gris acier au sol, arcs bleu électrique entre deux pôles rouge et bleu | au sol sous chaque ennemi Acier piégé |
| `ability_intimidate.png` | Intimidation | 16×16 | 5 (~90 ms) | œil rouge menaçant aux sourcils froncés qui s'ouvre d'un coup, puis une petite flèche rouge vers le bas | au-dessus de la tête de l'ennemi touché, jouée une fois |
| `ability_unnerve.png` | Tension | 16×16 | 4 (boucle, ~150 ms) | petit cœur vert barré d'une croix rouge qui tremble (soins réduits) | au-dessus de la tête, tant qu'il est près du porteur |
| `ability_curse.png` | Corps Maudit | 16×16 | 5 (~100 ms) | cadenas violet qui se referme avec un éclat, chaîne spectrale | au-dessus de la tête de l'attaquant, jouée une fois |
| `ability_nightmare.png` | Mauvais Rêve | 24×24 | 6 (boucle, ~140 ms) | volute de cauchemar noire et magenta, avec un œil rouge qui s'ouvre au milieu, au-dessus d'un ennemi endormi | au-dessus de la tête de l'endormi |
| `ability_absorb_water.png` | Absorbe-Eau, Lavabo, Peau Sèche | 32×32 | 8 (~60 ms) | gouttes bleues qui convergent en spirale vers le centre, puis petit éclat blanc | centrée sur le porteur, jouée une fois |
| `ability_absorb_electric.png` | Absorbe-Volt, Paratonnerre, Motorisé | 32×32 | 8 (~60 ms) | étincelles jaunes qui convergent vers le centre, puis petit éclat blanc | idem |
| `ability_absorb_fire.png` | Torche | 32×32 | 8 (~60 ms) | flammèches orange qui convergent vers le centre, puis le centre s'embrase brièvement | idem |
| `ability_absorb_grass.png` | Herbivore | 32×32 | 8 (~60 ms) | feuilles vertes qui convergent vers le centre, puis petit éclat blanc | idem |
| `ability_endure.png` | Fermeté | 32×32 | 6 (~70 ms) | bouclier doré qui apparaît d'un coup devant le Pokémon et se fissure sans se briser | centrée sur le porteur, jouée une fois |
| `ability_copy.png` | Calque | 32×32 | 8 (~70 ms) | deux silhouettes lumineuses qui échangent un petit orbe, comme une photocopie qui scintille | centrée sur le porteur, jouée une fois |
| `ability_reflect.png` | Synchro, Miroir Magik | 32×32 | 6 (~60 ms) | miroir hexagonal bleu pâle qui apparaît, renvoie un éclat, puis se brise en éclats | centrée sur le porteur, jouée une fois |
| `ability_evade.png` | Anticipation, Méga-Absol | 32×32 | 6 (~60 ms) | image fantôme du Pokémon qui se décale, traînée bleu nuit, petit « ! » blanc | centrée sur le porteur, jouée une fois |

Palettes : ombre `#1a1024 / #4a2a6a / #8a5ac8 / #d0b0ff`, sable `#7a5a2a / #c09a50 / #f0d890`, acier
`#2a2a34 / #6a6a7a / #b0b4c0`, aimant `#c42418 / #2a5ad0 / #8ac8ff`, menace `#6a1010 / #d02020 / #ff8a6a`, soin
`#2a7a3a / #6ad07a`, malédiction `#2a1040 / #7a3ab0 / #c890ff`, cauchemar `#140a14 / #6a1a4a / #e03a8a / #ff4040`,
eau `#1a3a6a / #2a6ab0 / #6ab0e8 / #e0f4ff`, électrik `#a07010 / #f0c030 / #fff6a0`, feu `#c42418 / #f0741a /
#ffd23a`, plante `#2a6a2a / #6aa03a / #b0e070`, or `#a07010 / #f0c030 / #fff0a0`, miroir `#3a6aa0 / #a0d0ff /
#ffffff`.

Présente les planches sur une seule page, agrandies ×4 : un Pokémon gris neutre (silhouette ronde d'une case) répété,
chacun montrant une marque : trois ennemis piégés sur le sceau d'ombre, l'un avec la volute de drain ; un ennemi avec
l'œil d'Intimidation, un autre avec le cœur barré, un attaquant avec le cadenas, un endormi avec le cauchemar ; en
dessous, les quatre absorptions côte à côte, puis Fermeté, Calque, le miroir et l'esquive fantôme.

---

## Lot 2 : prestance des mégas (17 planches)

Même jeu, même format que le lot 1. Pendant 45 à 60 secondes, un Pokémon méga-évolue : il devient plus fort et gagne
un **effet propre**. Une méga doit se voir de loin et donner envie : une **aura commune** sous ses pieds dit
« méga » à coup sûr, et chaque méga a une **signature** visuelle liée à son effet. Les effets sur les alliés ou sur les
tirs doivent rester discrets pour ne pas noyer l'écran quand trois Pokémon combattent ensemble.

| Fichier | Méga | Taille d'une image | Images | Description visuelle | Pose / boucle |
|---|---|---|---|---|---|
| `mega_aura.png` | Toutes | 48×24 | 8 (boucle, ~100 ms) | anneau au sol aux couleurs de l'arc-en-ciel de la méga-évolution (violet, bleu, rose, or), qui tourne, avec le symbole ADN de la méga-évolution qui pulse au centre | au sol sous la méga, tant qu'elle dure (`below`) |
| `mega_aura_rise.png` | Toutes | 32×48 | 8 (boucle, ~90 ms) | fines particules lumineuses arc-en-ciel qui montent autour du Pokémon et s'éteignent en haut | centrée sur la méga, par-dessus, ≈ 70 % d'opacité permise |
| `mega_alakazam_out.png` | Méga-Alakazam | 32×48 | 6 (~50 ms) | le Pokémon s'efface en se pliant en une ligne verticale violette, petites cuillères qui scintillent | à l'endroit qu'il quitte, jouée une fois |
| `mega_alakazam_in.png` | Méga-Alakazam | 32×48 | 6 (~50 ms) | l'inverse : une ligne violette s'ouvre, éclat psy, le Pokémon réapparaît | à l'endroit où il arrive, jouée une fois |
| `mega_charizardx_aura.png` | Méga-Dracaufeu X | 48×48 | 6 (boucle, ~90 ms) | flammes **bleues** et noires qui lèchent le corps vers le haut, étincelles bleu ciel | centrée sur la méga, par-dessus |
| `mega_kangaskhan_echo.png` | Méga-Kangourex | 24×24 | 5 (~50 ms) | petit poing de bébé Kangourex qui frappe avec un éclat jaune et des étoiles (le second coup) | sur la cible, jouée une fois |
| `mega_mewtwo_field.png` | Méga-Mewtwo Y | 96×96 | 8 (boucle, ~110 ms) | bulle psychique violette translucide aux bords ondulés, quelques runes qui flottent dedans | centrée sur la méga ; alpha ≈ 40 % permis à l'intérieur, bord opaque |
| `mega_ally_guard.png` | Méga-Gardevoir | 32×16 | 6 (boucle, ~120 ms) | petit halo protecteur vert et rose en forme de cœur stylisé au-dessus de la tête | au-dessus de chaque allié protégé |
| `mega_ally_shield.png` | Méga-Latias | 32×16 | 6 (boucle, ~120 ms) | petit halo protecteur rouge et blanc, triangle qui tourne doucement (immunité aux statuts) | au-dessus de chaque allié protégé |
| `mega_altaria_cloud.png` | Méga-Altaria | 72×36 | 8 (boucle, ~130 ms) | nuage de coton blanc et bleu ciel au sol, petites notes de musique et paillettes vertes qui montent | au sol sous la méga (`below`) |
| `mega_manectric_sparks.png` | Méga-Elecsprint | 32×32 | 6 (boucle, ~70 ms) | éclairs jaunes et bleus qui crépitent autour des pattes | centrée sur la méga, par-dessus |
| `mega_sableye_gem.png` | Méga-Ténéfix | 32×32 | 6 (~60 ms) | grosse gemme rouge qui apparaît devant lui, renvoie un éclat, puis se referme en scintillant | centrée sur la méga quand elle renvoie un tir, jouée une fois |
| `mega_mawile_bite.png` | Méga-Mysdibule | 32×32 | 6 (~60 ms) | mâchoires d'acier noires qui claquent, gouttes de vie rouges qui partent vers l'arrière | sur la cible, jouée une fois |
| `mega_glalie_frost.png` | Méga-Oniglali | 32×32 | 6 (~60 ms) | éclat de givre en étoile, cristaux de glace qui se figent puis se brisent | sur la cible, jouée une fois |
| `mega_darkrai_dream.png` | Méga-Darkrai | 32×32 | 6 (~70 ms) | brume noire et rouge qui enveloppe la cible, un croissant de lune rouge au-dessus | sur la cible, jouée une fois |
| `mega_lucario_aura.png` | Méga-Lucario | 24×24 | 4 (boucle, ~80 ms) | aura bleue en flammèches qui entoure un tir, petite traînée vers l'arrière | par-dessus chacun de ses tirs, orientée vers la droite |
| `mega_latios_trail.png` | Méga-Latios | 24×12 | 4 (boucle, ~80 ms) | traînée psychique bleu et blanc, motif répétable de gauche à droite | derrière ses tirs perçants, répétée le long de la course (raccord gauche-droite) |

Palettes : méga `#5a2a8a / #2a6ad0 / #e05ab0 / #f0c030 / #ffffff`, psy `#4a2a6a / #8a5ac8 / #d0b0ff`, flammes bleues
`#101830 / #2a4ab0 / #4ab0ff / #c0f0ff`, coup `#a07010 / #f0c030 / #fff6a0`, halo vert `#2a7a4a / #6ad08a / #f0a0c8`,
halo rouge `#a02020 / #f05050 / #ffffff`, nuage `#a0c8e8 / #e0f0ff / #ffffff / #6ad07a`, électrik `#a07010 / #f0c030 /
#6ab0ff`, gemme `#6a1020 / #d02040 / #ff8aa0`, acier `#1a1a20 / #4a4a54`, givre `#3a6aa0 / #a0d0ff / #ffffff`,
cauchemar `#140a14 / #6a1a2a / #e03a3a`, aura `#1a3a8a / #4a8aff / #c0e0ff`.

Présente les planches sur une seule page, agrandies ×4 : une méga grise neutre (silhouette d'une case et demie) sur
l'anneau arc-en-ciel avec les particules qui montent ; à côté, Méga-Alakazam qui disparaît puis réapparaît deux cases
plus loin ; puis les signatures : flammes bleues, bulle de Mewtwo Y avec un tir ennemi ralenti dedans, deux alliés avec
leurs halos (vert et rouge), le nuage d'Altaria, les éclairs d'Elecsprint ; en dessous, les effets sur une cible
(poing, gemme, morsure, givre, brume) et un tir avec l'aura de Lucario, un autre avec la traînée de Latios.
