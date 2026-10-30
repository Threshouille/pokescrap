# Pokescrap — Game Design Document

État : brouillon. Les sections « Décidé » viennent d'Owen ; les « Points ouverts » sont à trancher.

## 1. Vision

Roguelike d'action en temps réel, jouable à la manette, dans une arène. Le jeu reprend les données de Pokémon Donjon Mystère (Pokémon, statistiques, attaques, talents) et les adapte au temps réel, sans chercher à reproduire le jeu d'origine. Références : Children of Morta, Wizard of Legend. Projet privé.

## 2. Décidé

### Équipe et contrôle
- 1 à 3 Pokémon choisis avant la partie (3 au départ, 1 à 3 depuis le 17/10/2026), tous présents en même temps dans l'arène.
- Les Pokémon non joués sont contrôlés par l'IA ou par d'autres joueurs (coopération).
- En solo, le joueur change à la volée le Pokémon qu'il contrôle.
- Un Pokémon K.O. : le joueur bascule sur un Pokémon encore en vie. La partie se termine quand les 3 sont K.O.
- Réanimation : en restant quelques secondes sur un allié K.O. avec le Pokémon contrôlé (3 s à moins d'1 case, jauge au-dessus de l'allié, retour à 50 % des PV avec une courte protection ; la jauge se vide deux fois plus vite si on s'éloigne). Aussi prévue entre deux arènes et par objet.
- Interface : en bas à gauche, portrait du Pokémon contrôlé (tiré de la ROM), nom, barre de vie, et 4 cadres pour ses attaques (bouton, nom abrégé, couleur du type) qui se grisent pendant la recharge avec le temps restant. Au-dessus, les petits portraits des deux alliés avec leur barre de vie, dans l'ordre de la croix (gauche / droite) ; un allié K.O. est assombri, un allié empoisonné a un coin violet.
- Des Pokémon « support » sont envisagés, pour donner un rôle aux attaques de statut.

### Effets de terrain (décidé le 19/10/2026)
- Certaines attaques laissent une trace au sol qui change le combat. Grille de cases calée sur l'arène : une case porte un seul terrain, avec une durée, et s'efface pendant sa dernière seconde.
- Le terrain agit sur tout le monde, les deux camps, sauf les Pokémon du type correspondant et ceux qui lévitent ou traversent les murs (types de déplacement de la ROM).
- Première série (une attaque de base qui laisse une petite trace courte, puis une plus forte, uniquement des attaques existantes) : Verglas (Glace : glissade, poussées plus longues ; Poudreuse, Vent Glace), Braises (Feu : brûlure, petits dégâts ; Flammèche, Lance-Flamme), Flaque (Eau : ralentit, soigne la brûlure, Pokémon Eau plus rapides ; Pistolet à O, Hydrocanon), Sol chargé (Électrik : paralysie, immunisés Électrik et Sol ; Étincelle, Tonnerre), Ronces (Plante : ralentit, petits dégâts ; Fouet Lianes, Végé-Attak), Boue (Sol : ralentit fortement ; Coud'Boue, Séisme), Flaque toxique (Poison : poison, immunisés Poison et Acier ; Acide, Détritus).
- Les IA contournent les cases nocives et en sortent.
- Retours de jeu (19/10/2026, Robin) : braises plus dangereuses (brûlure à 80 %/s qui dure 4 s après en être sorti, 4 %/s de dégâts) ; paralysie plus sensible (sol chargé : 60 %/s, étourdissement de 0,3 s chaque seconde ; eau électrifiée : 80 %/s, 0,45 s, dégâts) ; l'eau nettoie la boue, la boue ne détrempe une flaque que sur 2 cases ; verglas plus glissant (accélération 2 cases/s², poussées × 2,2), tuiles opaques qui cachent le sol.
- Le lanceur n'est pas affecté par sa propre trace (19/10/2026, Robin), ni par ce qu'elle devient quand elle déclenche une affinité ; ses alliés, eux, le sont. Exceptions (19/10/2026, Robin) : le verglas et la flaque agissent aussi sur leur lanceur (`sparesOwner: false`).
- Alliés du lanceur (19/10/2026, Robin) : ils subissent les effets de mouvement (glissade, ralentissements, poussées plus longues) mais pas les effets offensifs (statut, dégâts, étourdissement), réservés à l'équipe adverse. Une affinité appartient à l'équipe qui la déclenche. Les soins et la guérison de la brûlure valent pour tous.
- Sélection (19/10/2026, Robin) : 24 attaques existantes, 3 ou 4 par type, choisies parce qu'elles touchent le sol de façon logique (pas Séisme) ; taille et durée selon la puissance (bloc `sizing`).
- Affinités (19/10/2026, Robin) : le feu brûle les ronces (propagation case par case, 0,15 s), fait fondre le verglas en flaque, sèche la boue, consume le poison, s'éteint dans l'eau ; l'eau éteint les braises et dilue le poison ; la glace fond en flaque sur les braises. Une flaque est transformée en entier, en vague rapide (0,03 à 0,05 s par case) : gelée par la glace, envahie par les ronces (1,5 × plus longtemps), électrifiée par l'Électrik (nouveau terrain Eau électrifiée : paralysie 35 %/s, petits dégâts, immunisés Électrik et Sol), contaminée par le poison, détrempée par la boue.
- Soin (19/10/2026) : les Pokémon Eau récupèrent 1,5 % de leurs PV max par seconde dans une flaque ; les autres n'y sont que soignés de la brûlure.
- IA (19/10/2026) : préfère une attaque dont la trace réagit sous l'ennemi en terrain nocif, renonce à une trace qui toucherait un allié non immunisé (y compris sur la même flaque quand la réaction se propage). Repli : sous 35 % de PV et sans ennemi à moins de 2 cases, va vers un terrain qui l'aide à moins de 6 cases (soin, statut soigné, terrain nocif auquel il est immunisé), y reste jusqu'à 60 % de PV ou 8 s, puis pas avant 12 s.

### Obstacles (décidé le 19/10/2026, Robin)
- Couche durable sous le terrain : eau profonde, lave, vide, ponts, murs de glace, surfaces gelées. Testés d'abord seuls, dans la salle des obstacles du terrain de test (escalier), avant de les mettre dans les arènes.
- Passage selon le type de déplacement de la ROM (nage, lave, lévitation, traverse-murs). On s'arrête au bord en marchant ; poussé, glissant ou en ruée, on tombe : retour sur la terre ferme la plus proche (le dernier point sûr s'il l'est encore : une glace a pu fondre entre-temps) et dégâts (eau 10 %, vide 20 %, lave 25 % + brûlure), puis 1 s d'invincibilité ; K.O. sur la rive si la chute prend ses derniers PV (19/10/2026, Robin).
- Interactions selon le type de l'attaque : Glace gèle l'eau (8 s) et refroidit la lave ; Plante végétalise l'eau (10 s) et reconstruit un pont brûlé ; Électrik électrifie toute l'eau ; Feu fait fondre glace et murs de glace, brûle ponts et végétation ; Eau refroidit la lave (pour de bon).
- Salle : 2 ou 3 obstacles tirés au hasard à chaque visite (vide avec ponts, rivière, rivière glacée, lacs d'eau, de lave ou de glace, murs de glace), jamais de quoi couper la salle en deux.
- Génération (19/10/2026, Robin) : l'eau et la lave ne se touchent jamais (une case de sol entre les deux) ; les rivières vont d'un mur à l'autre sans case praticable ; le vide n'est pas forcément continu (vide d'un mur à l'autre avec ponts, ravin sur une partie de la salle, gouffres) ; ponts droits sur une portion rectiligne ; îles (vide ou eau autour) reliées par 2 ou 3 ponts, dont la terre et l'arrivée des ponts restent du sol (rien ne s'y pose ensuite).
- Retours (19/10/2026, Robin) : les traces de terrain agissent aussi sur les obstacles, posées dessus ou sur une case voisine (sol chargé au bord de l'eau : toute l'eau s'électrifie ; braises sur un pont : il prend feu) ; cycle du pont : en feu 2,5 s (bloqué, le feu gagne tout le pont), puis brûlé (bloqué), reconstruit en lianes par une attaque ou des ronces Plante. Planches Claude Design livrées le 19/10/2026 (`Design/effects_v12_obstacles/`).
- Nouveaux obstacles (20/10/2026, Robin) : rochers (murs isolés des décors 9 et 18, solides, brisés par le type Combat, gros rochers en 2 coups, gravats ralentissants) ; le Combat brise aussi les murs de glace (éclats glissants) ; choc contre un obstacle solide après une poussée ou une glissade : 0,5 s d'étourdissement et 5 % des PV, jamais en marchant, en esquivant ou en fonçant volontairement (20/10/2026, Robin) ; plaques de glace placées contre le vide, l'eau ou les rochers pour favoriser glissades et chutes. Pylônes par paires : arc cyclique (3 s de repos, 0,8 s d'avertissement, 1 s actif) qui blesse (6 %), paralyse et étourdit, immunisés Électrik et Sol, Vol ×1,5, Spectre touchés ; Électrik surcharge (4 s), Sol met à la terre (6 s) ; l'arc électrifie l'eau et le métal qu'il survole ; l'IA évite l'arc. Métal (compromis plaques + aimants) : plaques conductrices (électricité dans toutes les plaques reliées, Feu : brûlantes, Glace : glissantes), Acier ancré dessus (pas de recul, +20 % de défense, pas électrocuté) ; bornes aimantées qui attirent les Acier et happent les tirs Acier et Électrik, et électrifient les plaques voisines une fois chargées.
- Visuels Claude Design des pylônes (normal, surchargé, à la terre), de l'arc et de son avertissement, des plaques de métal, des bornes et du métal chargé ou brûlant livrés le 20/10/2026 (`Design/effects_v13_electrique_metal/`).
- Combinaison des effets (20/10/2026, Robin) : une trace posée sur une case qui a déjà un effet se combine avec lui (eau sur métal ou sol chargé : eau électrifiée ; plaques électrifiées sous une flaque : toute la flaque électrifiée ; eau ou glace sur métal brûlant : vapeur ou flaque), y compris quand une interaction peint tout un obstacle ; les traces apparaissent en fondu (0,25 s) par-dessus les obstacles.
- Sable mouvant (20/10/2026, Robin) : ralentit (×0,4), enlise après 2 s (statut piège 1,5 s), sans effet sur Vol, Sol, Spectre et les Pokémon qui lévitent ; arène 2 surtout.
- En partie (20/10/2026, Robin) : 1 ou 2 obstacles par arène selon le décor (cristaux : eau, un peu plus de vide et de rivières, rochers ; sable : ravins, gouffres, sable mouvant, rochers ; volcan du boss : lave, rochers, arène dégagée), 4 blocs de mur au lieu de 8 (2 au boss), eau dessinée avec celle du décor ; pylônes et métal rares, réservés à de futures salles à thème (et au boss si cohérent) ; point d'arrivée dégagé, ennemis seulement sur des cases sûres reliées à l'équipe.
- Franchissement selon le type (20/10/2026, Robin), en plus du type de déplacement de la ROM : Eau nage dans l'eau profonde ; Feu marche sur la lave ; Vol survole eau, vide et lave (3 %/s et 30 %/s de risque de brûlure au-dessus de la lave, sauf s'il passe aussi par un autre type) ; Spectre traverse tous les obstacles, solides compris (les tirs non), seul l'arc des pylônes le touche ; Glace ne glisse pas sur la glace, Plante n'est pas ralenti par la végétation, Sol et Roche par les gravats. Valable pour les ennemis ; l'IA en tient compte (péage compté dans ses chemins).
- Visuels : couches secondaires des décors de la ROM avec leurs berges (eau : décor 5, vide : 54, lave : 57, glace : 111, eau pâle bordée de neige ; le 71 faisait des briques) ; Claude Design pour ponts (intact, en feu, brûlé, de lianes), végétation, roche refroidie, murs de glace (`Design/effects_v12_obstacles/`, prompt `Design/prompt_obstacles.md`).

### Météos et sports (décidé le 20/10/2026, Robin)
- Zénith, Danse Pluie, Grêle, Tempêtesable : toute l'arène, 20 s, pour les deux camps, une seule à la fois ; elles agrandissent ou réduisent les traces de certains terrains (y compris celles déjà au sol), modifient les dégâts de certains types (comme dans les jeux), la grêle et la tempête de sable blessent (0,4 %/s, soit 8 % sur 20 s ; 2 % puis 0,75 %/s jugés trop forts par Robin le 20/10/2026 ; météos rechargées en 40 s, sports en 25 s, pour qu'aucune ne dure en permanence ; l'IA ne lance une météo blessante que si l'adversaire y perd nettement plus) sauf les types immunisés, et quelques petites traces apparaissent au hasard (flaques, braises, verglas, boue).
- Tourniquet et Lance-Boue : zone de 4 cases autour du lanceur, 15 s ; braises (ou électricité) effacées et interdites dedans, attaques Feu (ou Électrik) à moitié contre les Pokémon dedans.
- L'IA lance une météo quand elle favorise les types de son équipe, un sport contre des adversaires proches du type visé. Réglages : `climates.json`.
- Tirs et vent (20/10/2026, Robin) : la météo change la vitesse et la portée des tirs de certains types (pluie : Feu ×0,7, Eau ×1,15 ; soleil : Feu ×1,2, Eau ×0,8 ; grêle : Glace ×1,2) ; la grêle (15 %) et la tempête de sable (25 %) ont un vent de direction fixe, tirée au lancement, qui accélère et allonge les tirs dans son sens et les freine contre lui (Vol ×2, Roche et Sol insensibles) ; il déporte les Pokémon Vol (0,8 case/s sous la grêle, 1,5 dans la tempête de sable, même à l'arrêt), pas les autres (20/10/2026, Robin).

### Objets spéciaux : méga-gemmes, cristaux Z, Téra-Éclats (décidé le 23/10/2026, Robin)
- Trois familles : 27 méga-gemmes (une par méga ayant ses sprites), 17 cristaux Z et 17 Téra-Éclats (un par type ; pas
  de Fée, absente de la table de la 4e génération).
- Un emplacement par famille et par Pokémon ; un seul objet actif par partie, choisi avant la partie (ex. Dracaufeu :
  méga, Z ou Téra). Les encoches de l'écran de choix montrent les familles disponibles pour ce Pokémon (3 pour
  Dracaufeu, 2 pour un Pokémon sans méga). Pas de changement d'objet en cours de partie (pour l'instant).
- Conservation : méga-gemmes permanentes et uniques (une gemme obtenue ne retombe plus) ; cristaux Z et Téra-Éclats en
  collection permanente à niveaux (le premier exemplaire débloque, chaque doublon fait monter de niveau, jusqu'à 3).
- Compatibilité : méga-gemme pour sa lignée (utile une fois la bonne forme atteinte) ; cristal Z si le Pokémon connaît
  une attaque offensive du type ; Téra-Éclat pour tout Pokémon. Rayquaza méga-évolue sans gemme s'il connaît Draco
  Ascension.
- Les trois effets sont des ultimes alimentés par la jauge commune de la méga : seul le Pokémon joué déclenche le sien,
  la jauge se vide. Capacité Z (revu le 23/10/2026, Robin) : tous les ennemis
  visibles à l'écran sont frappés directement (dégâts de sa plus forte attaque du type, multipliés selon le niveau) et
  subissent un effet propre au type (ex. Électrik : éclair + paralysie ; Feu : brûlure + braises ; Eau : poussée +
  flaque ; Acier : bonus de défense à l'équipe ; détail dans `items.json`). Persos de dev : Dev Mew a un cristal Z Feu,
  Dev Métamorph un Téra-Éclat Spectre. Téracristal : le Pokémon prend le type de l'éclat pendant 40 à 60 s selon le niveau (défense,
  terrains, immunités ; attaques de ce type renforcées).
- Ultimes ennemis (décidé le 23/10/2026, Robin) : comme tout Pokémon, un élite ou un boss porte un seul objet, donc un
  seul ultime, une seule fois ; l'objet est choisi d'après l'équipe du joueur : Téracristal défensif quand ils sont blessés et qu'un type encaisse mieux les attaques
  de l'équipe, capacité Z quand l'équipe est groupée ou qu'une cible est presque K.O. (bandeau d'alerte pendant la pose,
  dégâts plafonnés). Le terrain d'une capacité Z n'agit jamais sur l'équipe de son lanceur. Les élites modifient aussi le terrain comme les alliés (gèlent une rivière, brûlent un pont).
- Seconde phase (décidé le 25/10/2026, Robin) : la méga et la Téra d'un ennemi ouvrent une seconde phase à 50 % de ses
  PV (plus jamais au premier contact) ; une élite ou un boss ne peut pas franchir ce seuil d'un seul coup. À la
  transformation (immobile et invincible un instant, onde qui repousse l'équipe) : +30 % des PV max (boss +20 %, ennemi
  d'un rang à objet +10 %), recharges × 0,8, en plus des statistiques ou du type. La capacité Z ne soigne pas ; la méga
  et la Téra du joueur rendent 15 % des PV max (`ultimateHeal`, décidé le 26/10/2026). En contrepartie, le champion passe de 6 à 5 fois les PV.
- Obtention (à affiner) : boss (méga-gemme non possédée, compatible avec l'équipe en priorité), coffre de fin d'étape
  (Z ou Téra pondérés vers les types de l'équipe), rare ennemi d'élite ; plus tard défis, quêtes, îlots, boutique.

### Méga-évolutions (décidé le 20/10/2026, Robin)
- Une sorte d'ultime : une **jauge commune à l'équipe** se remplit au K.O. des ennemis (plus pour un boss) et se garde
  d'une étape à l'autre ; pleine, le Pokémon contrôlé qui le peut méga-évolue (L3 + R3, F au clavier : L1 et R1 sont des attaques), la jauge
  se vide.
- La méga dure 45 à 60 s (réglage), puis le Pokémon revient à sa forme ; les PV gardent leur proportion.
- Effets : bonus de stats = écart entre les stats officielles de la méga et celles de la forme de base, type de la
  méga, et un passif inspiré de son talent officiel, adapté au temps réel (27 passifs, par lots).
- Animation d'évolution existante en version courte, puis aura pendant la durée. Les alliés IA ne méga-évoluent pas.
- **Méga-gemmes** : seules les 27 mégas qui ont des sprites ont leur pierre. Elles tombent en partie (boss surtout),
  sans doublon (une pierre obtenue ne retombe plus), et se gardent d'une partie à l'autre. Elles s'équipent hors
  partie, dans le menu d'équipe ; l'écran de choix du Pokémon montre un emplacement de pierre (vide si aucune). Une
  pierre posée sur une forme de base (ex. Dracaufite X sur Salamèche) ne sert qu'une fois la bonne forme atteinte.
- Ordre : 1) jauge, méga, retour, stats et type (sans pierres ; en `devMode`, tout Pokémon compatible y a accès ;
  fait et testé le 20/10/2026 ; tâche en pause, reprise par l'étape 2) ;
  2) pierres (sauvegarde, menu, obtention) ; 3) passifs. Plus tard : boss qui méga-évoluent. Capacités Z : sujet à part.

### IA (décidé le 20/10/2026, Robin)
- Alliés et ennemis profitent des mêmes améliorations. Pas des maîtres stratèges : évoluer dans l'environnement de
  manière propice et sûre.
- Lot A (fait et testé, fusionné dans `dev` le 22/10/2026) : cible choisie par score (K.O. possible, types, menace sur un coéquipier, concentration,
  distance) ; les alliés suivent le Pokémon joué quand il recule, le rejoignent par un chemin sûr, sont plus offensifs
  sinon, et restent près de lui quand ils sont blessés.
- Lot B : placement et attaques combinées selon les types de l'équipe (préparer une flaque pour un coéquipier
  Électrik ou Plante, se rapprocher d'un coéquipier complémentaire, préférer les terrains qui avantagent).
- Lot C (alliés d'abord, commencé) : couper le chemin d'un ennemi au corps à corps (brûler un pont, faire fondre la
  glace) et réparer un chemin pour rejoindre le Pokémon joué (geler l'eau, nénuphars, refroidir la lave).
- Les IA ne doivent pas être parfaites : elles se font surprendre par les pylônes de temps en temps, et ne prévoient
  pas le sol métallique (elles marchent dessus sans s'en soucier).

### Outils de test
- Terrain de test (19/10/2026) : depuis le menu en `devMode` (Select / T), arène ouverte où des émetteurs entretiennent chaque terrain et deux démonstrations d'affinités, sans ennemis (vagues en boucle en option), rien de recruté ni compté.
- Persos de dev (décidé le 19/10/2026) : 3 personnages réservés au développement (Dev Mew, Dev Métamorph, Dev Porygon), visibles en tête du menu quand `devMode` est actif. Ils peuvent apprendre toutes les attaques du jeu (4 au départ, filtre par type au menu), n'ont pas de type (pas de STAB), se déplacent normalement (le terrain s'applique à eux) et ont des statistiques de starter (un physique, un spécial, un équilibré). Ils ne sont ni recrutables, ni sauvegardés, ni ennemis. But : tester les attaques et les effets de terrain.

### Attaques
- 4 emplacements par Pokémon. On commence la partie avec 2 attaques choisies avant la partie parmi toutes celles que la forme de base apprend par niveau (1 à 100) ; CT et attaques par œufs exclues (décidé le 15/10/2026).
- Pendant la partie, tous les 10 niveaux, l'une des 3 propositions du choix d'amélioration est une nouvelle attaque tirée au hasard dans la liste du Pokémon, jusqu'à 4 attaques ; ensuite cette proposition n'apparaît qu'une fois sur deux et sert à remplacer une attaque apprise (« Garder » renonce à la nouvelle attaque, 17/10/2026). Après une évolution, les attaques de la forme évoluée ont plus de chances d'être proposées (15/10/2026).
- Rôle de l'IA déduit des attaques équipées (15/10/2026) : soutien avec un soin ou un bonus aux alliés, distance si projectiles et zones dominent, corps à corps sinon.
- Ennemis : 2 attaques tirées au hasard dans leur liste, dont au moins une offensive si possible (15/10/2026). Les boss gardent leurs attaques fixes.
- Évolution en partie (15/10/2026) : au niveau du jeu d'origine, statistiques de base de la nouvelle forme, niveaux gagnés conservés ; les attaques de la forme actuelle comptent double au tirage des nouvelles attaques. Évolutions sans niveau : pas encore décidé.
- Attaques de base (15 au 17/10/2026) : une attaque à distance et une au corps à corps par type (2 × 17), à recharge courte, pour taper en continu entre les grosses attaques. Pas de place réservée : les attaques de base des types du Pokémon (des deux styles, donc jusqu'à 4 pour un double type) s'ajoutent à sa liste au menu ; celles du style correspondant à ses statistiques (Attaque > Attaque Spé → corps à corps, sinon distance) sont présélectionnées. On commence avec 2 attaques choisies dans cette liste, et elles se remplacent comme les autres.
  - Distance : recharge d'environ 1 s, temps de lancement de 0,25 s pendant lequel le Pokémon est immobile ; le tir part dans la direction visée à la fin du lancement. Chaque attaque a sa trajectoire, son nombre de projectiles et sa taille (Météores en orbite, Flammèche 3 flammes successives, Pistolet à O grosse goutte, Tranch'Herbe 2 feuilles en ligne droite l'une après l'autre, qui ralentissent et traversent les Pokémon, Éclair rapide en ligne droite, Poudreuse en éventail, Onde Vide qui grandit, Acide 3 gouttes en cloche de plus en plus loin, Coud'Boue en cloche, Tornade lente qui grossit, Choc Mental qui accélère, Dard-Nuée en rafale, Jet-Pierres en cloche, Vent Mauvais lent et perçant, Draco-Rage en boomerang, Poursuite à tête chercheuse, Miroi-Tir en rafale qui rebondit). Les attaques en cloche laissent une flaque (ou des fissures) de la taille de la zone touchée, qui s'efface en 1,5 s.
  - Corps à corps : recharge de 1,1 à 2 s (doublée le 17/10/2026), lancement de 0,15 s (plus pour Morsure, Crocs Givre, Roulade, Ampleur). Chaque coup étourdit la cible 0,4 s (0,7 s pour Ampleur ; boss insensibles) et lui fait jouer son animation de dégâts, comme un recul. Formes : Griffe, Crocs Givre, Coupe Psycho, Taillade (coups en arc décalés de 0,3 à 0,65 case devant le Pokémon ; Morsure grande, jusqu'à 2 cases) (dégâts +25 % par coup réussi en moins de 3 s, jusqu'à ×2, multiplicateur affiché au-dessus de la cible), Dracogriffe (en X, grande), Morsure en arc ; Fouet Lianes (deux lianes qui partent des côtés et se croisent au milieu), Dard-Venin, Picpic en estoc ; Mach Punch en 3 coups ; Ampleur autour de soi (rayon au hasard entre 1,2 et 2 cases, immobilise le lanceur 0,25 s après) ; Roue de Feu, Aqua-Jet en ruée ; Étincelle ruée + décharge ; Roulade bond puis ruée de 4,5 cases, les deux Pokémon repoussés ; les ruées (et le coup de Pisto-Poing) repoussent la cible de 1,5 case en 0,5 s, en ralentissant ; Ombre Portée disparaît 0,3 s puis réapparaît plus loin en frappant ; Pisto-Poing ruée puis coup de poing large qui repousse, lancé vers la visée du moment.
  - Cible (17/10/2026) : l'ennemi le mieux placé par rapport à la direction visée au stick droit (sinon à la direction regardée), dans un cône de 45° et 8 cases, en favorisant les plus proches (chaque case de distance compte comme 12° d'écart, 18/10/2026), est marqué d'un cercle rouge (le Pokémon joué a un cercle bleu), recalculé en continu ; la cible actuelle est gardée tant qu'elle reste dans un cône de 60° (verrouillage, pour qu'elle ne saute pas d'un ennemi à l'autre dans un groupe). Toutes les attaques du joueur vont vers lui : tirs qui anticipent son déplacement, attaques en cloche qui atterrissent sur lui s'il est à portée, corps à corps, zones. Sans cible, l'attaque part dans la direction visée ou regardée.
  - Ennemis : une attaque de base du style de leurs statistiques plus une attaque de leur liste ; recharge × 2,5 et dégâts × 1,5 sur l'attaque de base (`meta.json`).
- Attaques de type (décidées le 19/10/2026) : une attaque par type un cran au-dessus des attaques de base (recharge de 4 à 8 s, mécanique propre), faites par lots :
  - Normal **Plaquage** : bond de 3 cases sur la cible, intouchable en l'air, dégâts en zone à l'atterrissage, 30 % de paralysie.
  - Feu **Lance-Flamme** : jet de flammes en cône tant que le bouton est enfoncé (3 s maximum), lanceur ralenti, visée qui suit la cible.
  - Eau **Vibraqua** : anneau d'eau lent (sans tête chercheuse depuis le 19/10/2026), qui grossit et rétrécit, traverse les ennemis ; confusion 20 %.
  - Plante **Méga-Sangsue** : lien accroché à la cible 1,5 s qui la draine et soigne le lanceur ; casse si elle s'éloigne.
  - Électrik **Fatal-Foudre** : après 1 s de charge (animation de tir), pendant 6 s, un éclair tombe du ciel toutes les 0,5 s sur un ennemi pris au hasard dans les 5,5 cases (n'importe où s'il n'y en a pas), avec une onde de choc de 1,5 case de rayon (dégâts, faible chance de paralysie) qui laisse du sol chargé (terrain de Robin) ; les ennemis n'évitent pas la zone ; un léger cercle blanc marque la limite de la zone.
  - Glace **Blizzard** : même charge que Fatal-Foudre (animation de tir) ; tempête semi-transparente de 3 cases de rayon qui reste centrée sur le lanceur pendant 4 s, ralentit et blesse chaque seconde, et laisse du verglas partout où elle passe.
  - Combat **Frappe Atlas** : courte ruée, saisie, saut et écrasement au sol, onde de choc autour de l'impact.
  - Poison **Acidarmure** : le Pokémon se liquéfie 2 s (intouchable, plus rapide, traverse et empoisonne les ennemis).
  - Sol **Telluriforce** : une marque sous chaque ennemi à 5 cases ou moins du lanceur (0,6 case de rayon) ; les ennemis pris dedans sont figés (animation de dégâts) jusqu'à la colonne de terre, 0,5 s plus tard ; un cercle montre la zone d'action à chaque utilisation.
  - Vol **Lame d'Air** : grande lame rapide qui traverse ; 20 % de chance d'étourdir 1 s.
  - Psy **Psyko** : soulève la cible 0,8 s puis la projette (dégâts si elle heurte un mur).
  - Insecte **Plaie-Croix** : ruée de 4,5 cases qui traverse les ennemis ; figés (animation de dégâts) dès qu'elle les touche, ils sont tranchés d'un coup 0,25 s après la fin de la ruée (entaille en X), comme un coup de sabre à retardement.
  - Roche **Pouv. Antique** : 5 pierres en orbite (bloquent les tirs) puis lancées une à une sur la cible.
  - Spectre **Onde Folie** : cône qui rend confus 4 s (sans dégâts) ; et **Ball'Ombre** : charge tant que le bouton est enfoncé (immobile, maximum après 2 s, peut être tenue indéfiniment), la charge augmente taille et dégâts ; rapide, courte portée, s'arrête et flotte en bout de course.
  - Dragon **Draco Météor** (les ennemis n'évitent pas les ombres, sinon les météores ne touchent jamais ; lancée dans la direction pointée par le joueur, sans verrouillage de cible ; impacts de 1,2 case de rayon, un météore toutes les 0,1 s, ennemis projetés à l'opposé du centre de l'impact) : gros météores qui tombent du ciel en diagonale, en ligne en partant du lanceur (ombre d'avertissement puis chute), puis Attaque Spé du lanceur en baisse.
  - Ténèbres **Coup Bas** : garde de 1 s ; si un ennemi attaque le lanceur, il se téléporte derrière lui, frappe très fort et le projette à environ 4 cases, sinon petit coup devant.
  - Acier **Gyroballe** : toupie pendant 2 s, déplacement libre, blesse ce qu'elle touche, plus fort contre les ennemis rapides.
  - Lots : 1 Vibraqua, Lame d'Air, Plaie-Croix, Onde Folie, Telluriforce ; 2 Fatal-Foudre, Blizzard, Draco Météor, Pouv. Antique ; 3 (bouton maintenu) Lance-Flamme, Ball'Ombre, Méga-Sangsue ; 4 (états du corps, prises) Plaquage, Frappe Atlas, Acidarmure, Gyroballe, Psyko, Coup Bas.
- Abandonné (15/10/2026) : l'évolution des attaques en cours de partie.
- Les attaques plus puissantes ont plus de temps de recharge ou d'autres inconvénients (annonce plus longue, charge, recul).
- Méthode (15/10/2026) : toutes les attaques sont d'abord ajoutées de façon générique (`generate_moves.py`, à partir de la ROM et de PokéAPI), puis la plupart seront reprises au cas par cas pour leur donner un aspect unique.
- Statuts (décidés le 15/10/2026, réglages dans `statuses.json`) : poison ; paralysie (vitesse ÷2) ; brûlure (dégâts réguliers, moitié du poison, attaque physique −30 %) ; sommeil (ni déplacement ni attaque, réveil au premier coup) ; gel (immobilisé, dégelé par une attaque Feu) ; confusion (déplacement et visée déviés jusqu'à 60°) ; piège (immobile mais peut attaquer, petits dégâts) ; Bâillement (sommeil au bout de 2 s) ; Vampigraine (dégâts qui soignent le lanceur). Sommeil et gel durent moitié moins sur l'équipe du joueur ; les boss subissent un quart des dégâts réguliers et des statuts handicapants deux fois plus courts. Aussi : recul, bonus au lanceur à l'impact, chance de malus. Les effets non transposables (Attraction, Requiem, Entrave…) sont ignorés : les attaques offensives gardent leurs dégâts, les attaques de statut pur sont à faire à la main.
- Ennemis : piste retenue, les mêmes attaques que les joueurs, avec plus de temps de recharge ou moins de dégâts (pas encore tranché).
- Chaque attaque a son propre temps de recharge.
- Visée libre au stick droit ; le Pokémon se tourne dans la direction du stick quand il attaque.
- Les attaques sont regroupées en familles paramétrables (projectile, corps à corps, zone, ruée, mur, amélioration temporaire…), avec quelques attaques faites sur mesure (ex. : Mur Lumière devient un vrai mur posé sur le terrain).
- Attaques de statut : on en garde un maximum, traitées au cas par cas (plus fortes ou transformées).
- Talents réadaptés au temps réel. Données (décidé le 29/10/2026, Robin) : les 1 ou 2 talents de la ROM pour chaque
  Pokémon, plus son talent caché (PokéAPI, 5e génération et après) gardé à part, et le talent officiel de chaque méga ;
  catalogue de tous les talents de la série principale (314, dont 122 dans la ROM). Les talents changés après la
  4e génération restent ceux de la ROM. Statistiques et familles d'adaptation : `Docs/TALENTS.md`. Pas encore d'effet
  en jeu ; choix des talents à adapter et rôle du talent caché à venir.
- Talents en jeu (décidé le 29/10/2026, Robin) : un seul talent actif, passif, **jamais choisi** : tiré au hasard
  parmi les talents normaux au début de la partie (le caché avec une petite chance, 5 %), gardé à l'évolution ; celui
  de la méga pendant la méga. Seuls les persos de dev choisissent (menu, J en partie). Ennemis : talent normal au
  hasard ; caché pour les boss, une élite sur deux, 25 % des rares, 50 % des épiques. Pas de coup critique : les talents
  de critique seront adaptés en « coups dans le dos » (lot 2). Valeurs adoucies comme la table des types (Isograisse
  × 0,6, Filtre × 0,8, Adaptabilité : bonus de type 1,5). Fermeté : survit à 1 PV à un coup reçu à PV pleins, sans
  limite. Talents de météo d'arrivée : équipe, boss et élites seulement. Lot 1 fait (70 talents : types, absorption,
  bas PV, statuts, contact, météo, Intimidation, Fuite, Pression, Technicien…). Lot 2 fait (39 talents, 29/10/2026) :
  pas de « coups dans le dos » ; Sniper = tirs plus rapides, plus longs, jusqu'à × 1,3 en bout de course ; Chanceux =
  tirs plus gros, coups plus larges ; Coque Armure / Armurbaston = corps à corps reçu × 0,85, pas de bonus de série ;
  Colérique = attaque × 1,5 après un coup de plus de 25 % des PV ; Lévitation = Sol × 0,25 ; réactions (Synchro,
  Cœur Noble, Phobique, Acharné, Corps Maudit, Suintement), combat (Impudence, Turbo, Agitation, Annule Garde,
  Garde Magik, Multiécaille, poussée, pièges qui empêchent d'esquiver, Moiteur, Mauvais Rêve), équipe et butin
  (Garde-Ami, Plus / Moins, Ramassage, Cherche Miel, Gloutonnerie, Délestage). ≈ 89 % de chances que le talent tiré
  agisse. Absorption aussi sur les terrains de leur élément (Robin, 29/10) : aucun mal, + 3 % des PV par seconde pour
  Absorbe-Eau / Absorbe-Volt / Peau Sèche, bonus de stat ou de puissance gardé tant qu'il y reste pour les autres.
  Lot 3 fait (46 talents, 29/10/2026) : tous les talents des Pokémon ont un effet, sauf Imposteur et Maladresse,
  abandonnés : le tirage ne tombe jamais sur un talent sans effet quand la forme en a un autre. Adaptations : Rivalité
  (même type, pas de genre), Glu (ralentit au corps à corps), Anticipation (esquive rechargée par un coup super
  efficace, 5 % d'ignorer un coup), Calque (copie à la rencontre), Lumiattirance (expérience), Météo, Homochromie,
  Protéen (changement de type, un seul, jamais en Téra ni en méga), Télépathe (zones), Tension (soins des adversaires),
  Régé-Force (hors contrôle), Coloforce / Force Pure (× 1,5)…
- Passifs des mégas (29/10/2026, Robin) : pendant la méga, son talent officiel et un effet propre en plus
  (`ability_effects.json`, `megas`) : Dracaufeu X braises au corps à corps, Alakazam esquive 60 % plus longue,
  Ectoplasma piégés drainés, Kangourex second coup qui étourdit, Ptéra Roche × 1,25 et vitesse, Mewtwo Y tirs
  ennemis ralentis, Steelix et Démolosse lancent leur météo, Airmure peau dure, Tyranocif inébranlable, Gardevoir
  protège les alliés, Ténéfix renvoie 20 % des tirs, Mysdibule draine, Charmina sans statut, Élecsprint traînée
  électrique, Camérupt braises, Altaria soigne les alliés, Branette statuts plus longs, Absol anticipe, Oniglali gèle,
  Latias protège des statuts, Latios tirs perçants, Rayquaza Dragon / Vol, Lockpin vitesse et poussée, Lucario tirs
  qui suivent, Gallame coups plus larges, Darkrai endort. Peau Féérique : attaques Normal → type de la méga.
- Protégés des mégas (30/10/2026, Robin) : Gardevoir (dégâts reçus), Latias (statuts) et Altaria (soin) ne touchent plus
  tous les alliés proches mais 2 protégés choisis à la méga-évolution (`wards`), marqués d'un halo, tant qu'ils restent
  à moins de 8 cases (`wardLeash`) : côté joueur ses coéquipiers, côté boss ou élite deux sbires de son groupe (soutiens
  puis les plus proches) ; un protégé K.O. est remplacé. Les talents permanents (Garde-Ami…) gardent leur rayon.
  Méga-Alakazam : l'esquive devient une téléportation. Tension : aucun soin (« +0 »).

### Progression en partie
- Chaque partie commence au niveau 1 et vise environ le niveau 100 après 30 minutes.
- Les statistiques suivent les données réelles : un joueur qui connaît Pokémon doit reconnaître qui est résistant, qui frappe fort, au corps à corps ou à distance.
- Expérience commune à l'équipe.
- Tous les 5 ou 10 niveaux : 1 choix parmi 3 améliorations aléatoires par Pokémon (statistique, modification d'une famille d'attaques comme +50 % de taille de projectile, nouvelle attaque). Les 3 Pokémon montant de niveau ensemble, une seule coupure sert à faire les 3 choix.
- Les Pokémon évoluent pendant la partie. On débloque toujours la première forme d'une lignée.
- Évolution (plan validé par Owen le 17/10/2026, fait le même jour ; à tester à la manette) :
  - Manuelle : quand les conditions sont réunies (y compris par niveau), le joueur appuie sur Triangle. Animation : silhouettes blanches de l'ancienne et de la nouvelle forme qui alternent (environ 2,5 s, Pokémon immobile et invincible), puis onde de choc de 5 cases (recul de 2 cases, dégâts 15 + niveau).
  - Évolutions de jour ou de nuit : selon l'horloge du jour (voir « Jour et nuit »).
  - QI : +1 par seconde quand le Pokémon est contrôlé par le joueur (remplace le bonheur).
  - Genre et hasard (Chenipotte) tirés en début de partie.
  - Beauté (Barpau) : +1 par seconde sans être touché, −10 par coup reçu, évolution à 100.
  - Pierres et objets : une carte remplace l'une des 3 propositions d'amélioration, 1 fois sur 2, seulement si le Pokémon peut s'en servir ; la carte choisit la branche d'évolution.
  - Échange avec objet : la carte de l'objet, puis une carte « Échange » ; échange seul : une carte « Échange ».
  - Babimanta : avec Rémoraid dans l'équipe.
  - Équipe de 1 à 3 Pokémon ; Munja rejoint l'équipe si une place est libre quand Ningale évolue.
  - Menu : Rond retire le dernier Pokémon choisi.
  - Mise en œuvre : la nuit assombrit l'arène et s'affiche dans l'indicateur d'étape ; le HUD affiche « Triangle : évoluer » et un bandeau prévient quand une évolution devient possible ; Debugant compare Attaque et Défense actuelles (égales à 10 % près) ; les formes alternatives d'une même espèce (Cheniselle) comptent une fois. Le genre est tiré à 50/50 (taux réels absents des données).
- Données : champ `evolution` de `pokemon_rt.json` (méthode `level`, `iq`, `item`, `party`, `trade`, et `condition` éventuelle : `move`, `day`, `night`, `beauty`, `male`, `female`, `randomA`/`randomB`, `atkAboveDef`…), produit par `extract_data.py`.
- Vitesse : reprise des jeux principaux (absente de la ROM), elle détermine la vitesse de déplacement.

### Structure d'une partie
- 2 arènes puis 1 boss, répété 3 fois, puis un boss final (modèle Wizard of Legend). Soit 6 arènes et 4 boss.
- Carte : grande arène carrée, murs au bord, quelques obstacles.
- Une arène = plusieurs vagues d'ennemis (à confirmer).
- Boutiques possibles entre les niveaux, avec un système d'objets (à préciser).

### Étages et salles (décidé le 22/10/2026, Robin)
- Étages façon Binding of Isaac : plusieurs salles reliées, dont des salles optionnelles (défis) ; une salle de boss
  par étage : tous les 1 ou 2 étages un gros boss (légendaire), sinon une équipe de 3 Pokémon un peu forts ; sous-boss.
- Salles inspirées de Donjon Mystère (décors de la ROM), de tailles variées ; instances tirées au hasard (plusieurs
  salles de lave différentes). Portes fermées pendant le combat. Pas de butin pour l'instant.
- Ordre : 1) catalogue et générateur de salles (fait le 22/10/2026) ; 2) plan procédural de l'étage et mini-carte (fait le 22/10/2026 façon Isaac, puis remplacé par les niveaux continus, voir ci-dessous) ;
  3) règles des salles (portes, embuscade, défis, sous-boss, boss, cadence des légendaires).

### L'île flottante (décidé le 22/10/2026, Robin) — remplace les niveaux continus comme structure de la run
- Une run = une île flottante : 4 quartiers (un quart d'île chacun) + une zone centrale finale. Ordre imposé, sens des
  aiguilles d'une montre ; chaque quartier s'ouvre avec un Engrenage du Temps, les 4 ouvrent le centre.
- Quartier : région principale ouverte / semi-ouverte, biomes générés (champs d'altitude, d'humidité, de température :
  volcans, grandes rivières, lacs, forêts, falaises), variations de sous-biomes fluides et organiques ; plus des chaînes
  d'îlots flottants au thème en rupture (code des niveaux continus réutilisé, quitte à le revoir).
- Durée : trame principale ≈ 30 min sans les îlots ; les îlots sont une voie optionnelle qui rallonge la run.
- Engrenages du Temps : par run, deux quartiers donnent le leur par un boss, un en 3 fragments dispersés dans trois
  sous-biomes de la région (gardés : mini-gardien, énigme de décor ou recoin ; indice sur la mini-carte), un par une
  quête du Tableau des missions tirée parmi Sauvetage (ramener un Pokémon perdu), Avis de recherche (traquer un Pokémon
  qui fuit), Défense du sanctuaire (tenir 3 vagues) ; chaque quête se termine par un boss. Le 4e quartier finit
  toujours par un boss. Les îlots ne donnent pas d'engrenage : récompenses de détour (plus tard).
- Centre : série de salles défis et combats, structure de donjon (salles du catalogue), boss final.
- Engrenages et boss de quartier (décidé le 25/10/2026, Robin ; `feature/engrenages`, `time_gears.json`) : passage
  suivant fermé jusqu'à l'engrenage du quartier, centre avec les quatre. Le boss est sur un îlot à part, par un portail
  au bord de la côte qui ne s'ouvre qu'avec la clé de l'îlot, au fond d'un donjon propre au boss (une embuscade, puis un
  gardien près d'un coffre qui contient la clé) ; dans l'îlot, la sortie reste fermée jusqu'à sa chute (engrenage et
  coffre). Le boss est plus grand qu'un Pokémon ordinaire (× 1,6), a plus de PV et une barre de vie qui n'annonce
  pas sa seconde phase (surprise). Accès (26/10/2026) : le donjon de la clé est sur le point d'intérêt du
  quartier (grotte du sommet, plus haut plateau ou clairière) ; un pont de corde part de la côte vers l'îlot, aperçu
  au large (rochers flottants), fermé par un portail de lumière qui s'ouvre avec la clé ; avancer sur le pont mène
  directement à l'arène. Objets importants à l'écran (engrenages, clé, fragments). Art tiré de la ROM : entrée du donjon = la grotte du sommet (y entrer mène au donjon), sinon une faille dimensionnelle au sol, portail animé qui s'ouvre à l'approche avec la clé, pont vertical sur la côte arrondie ; boss d'îlot sans bulles numérotées ; arène gardée après sa chute (pas de nouveau combat). Équilibrage (26/10/2026) : niveau de l'équipe + 2 (jamais sous celui du
  quartier), × 22 PV, un coup ne retire jamais plus de 35 % des PV max de sa cible, annonces de ses attaques de zone
  allongées de 0,3 s ; arène Vol : archipel de petites îles au-dessus du vide et de l'eau, beaucoup de ponts. À mi-PV, le combat s'arrête : la caméra se centre sur lui pendant qu'il
  méga-évolue (animation rapide, environ 1,5 s, personne ne bouge ni n'attaque), puis le combat reprend (26/10/2026). Roster limité par zone (26/10/2026) : forêt Altaria, Gardevoir, Gallame, Lockpin ;
  volcan Dracaufeu, Démolosse, Camérupt, Tyranocif ; glacier Oniglali, Absol, Altaria ; côte Ptéra, Airmure,
  Dracaufeu ; désert Steelix, Mysdibule, Camérupt, Tyranocif ; ruines Alakazam, Ectoplasma, Branette, Ténéfix,
  Charmina, Lucario. Légendaire à 3 % (Téracristal d'un type avantageux à sa seconde phase) : forêt Celebi, volcan
  Groudon, côte Kyogre ; les trios légendaires (oiseaux, Regi, chiens, lacs) sont gardés pour les îlots secondaires,
  les mégas légendaires pour le centre. Pas de duo. Pas de passif de zone : l'arène est pensée pour le boss (Vol :
  îlots reliés par des ponts au-dessus du vide, 2 sbires Feu qui les brûlent ; Feu : archipel de lave, sbires Poison
  ou Électrik qui piègent les zones qui ne brûlent pas ; Glace : patinoire ; Roche, Sol, Acier : éboulis ; Spectre,
  Psy, Ténèbres : colonnade, sbires Spectre ; Normal, Combat : réseau électrique, sbires Électrik). Chaque boss a 4
  attaques fixes, une par numéro de bulle (`movesets` de `time_gears.json`, choisies le 26/10/2026 : au moins une
  attaque de statut ou de météo, des attaques de zone à éviter). Le donjon de la clé prend le thème et les Pokémon des types du boss ; son gardien est le
  Pokémon le plus évolué de ces types, une élite qui se téracristallise (pas de méga), avec 2 sbires qui portent
  chacun un cristal Z. Fragments : 3 dans des sous-biomes différents, gardés par une élite endormie ou cachés dans le
  recoin le plus haut, indice sur la mini-carte. Quêtes du Tableau des missions : plus tard (un boss en attendant).
  - Retours de test (26/10/2026, Robin), à faire :
    - Fait le 26/10 : PV, barre en deux phases, arène sur mesure, gardien évolué et ses sbires, donjon au thème du
      boss, roster par zone. Clé = Clé de Donjon Mystère, engrenage tiré de la ROM.
- Zones séparées (décidé le 22/10/2026, Owen) : chaque quartier et le centre sont des instances à part (une carte
  chacune), séparées par de grands murs plutôt que par des frontières naturelles, avec un écran de chargement à chaque
  passage. On peut revenir dans un quartier déjà visité. Le centre s'ouvre depuis le dernier quartier. Passages ouverts
  pour l'instant (verrou par Engrenage du Temps à l'étape « sceaux et engrenages »).
- Relief et hauteur (décidé le 24/10/2026, Owen) : la hauteur donne un avantage. Les attaques à distance tirées d'un
  palier passent par-dessus les falaises de ce palier ou plus basses et s'écrasent contre celles qui sont plus hautes
  (d'en bas, on tire dans la paroi) ; le corps à corps ne passe jamais une falaise. Les IA en tiennent compte. Les
  collisions des falaises suivent leur épaisseur dessinée (rebord fin vu de dessus, moitié basse de la rangée du haut
  d'une paroi). Plateaux aux contours droits (rectangles sur une grille de 3 cases), falaises dessinées avec des
  morceaux des cartes de l'histoire.
- Végétation (décidé le 24/10/2026, Owen) : peu de décor épars (fleurs, buissons, rochers) ; les arbres servent aux
  forêts. Chaque sous-biome a des taches : champ de fleurs, fougeraie, éboulis, forêt. Une forêt est un massif plein
  traversé de sentiers vers une clairière centrale, pensé pour le level design (lisière praticable sous les couronnes).
- Peuplement des quartiers (décidé le 25/10/2026, Robin) : mélange de camps (surtout des gangs, parfois une équipe
  ou un solitaire ; endormis jusqu'à l'approche ou une attaque, laisse autour du camp), de patrouilles (surtout des
  meutes, plus rares) et de points d'apparition spontanée selon des conditions (nuit, météo, temps passé, équipe
  proche mais hors de vue) ; densité moyenne (8 à 12 rencontres par quartier au départ) ; un groupe battu ne revient
  pas, mais un autre lot peut réapparaître à sa place au bout d'un moment. Populations par sous-biome
  (`populations.json` : types pondérés, types de la nuit), plus fortes avec le quartier et la distance à l'arrivée.
  Étapes : 1) populations (fait le 25/10/2026) ; 2) camps, patrouilles, apparitions et retour sur la carte (fait le
  25/10/2026, `island_population.json`) ; 3) contenu
  des escaliers secrets et des grottes (trésor, embuscade, gardien, décor du sous-biome ; fait le 25/10/2026,
  `room_contents.json`) ; 4) tests.
- Escaliers secrets (décidé le 25/10/2026, Owen ; génération à faire, Robin) : un escalier caché dans un quartier mène à
  une salle à part (écran de chargement, comme un changement de zone), dont un escalier ramène exactement au même
  endroit ; l'escalier secret disparaît ensuite. Tirage avec une probabilité, comme les Monster Houses de DM (0 ou 1
  par quartier, pas systématique), surprise totale : aucun indice sur la mini-carte ni avant d'entrer.
  - Cachettes retenues : au centre d'une clairière de forêt (`IslandPlan.clearings`) ; sous un rocher fissuré à briser
    (obstacle dédié, brisé par les types Combat, Sol, Roche, Acier par exemple : à régler pour qu'une équipe sans ces
    types n'en soit pas toujours privée). Idées écartées pour l'instant : fond de grotte, palier isolé atteint par un
    rebord, escalier invisible révélé à l'approche.
  - Contenus retenus : **trésor** (un coffre : cristal Z ou Téra-Éclat pondéré vers l'équipe, méga-gemme rare) ;
    **embuscade** (façon Monster House : sortie fermée, 2 vagues ou plus, coffre à la fin) ; **gardien** (un seul
    Pokémon rare et costaud, élite, taux de recrutement augmenté ; il garde un coffre). Poids de tirage à régler.
  - Idées pour plus tard : faux trésor (coffre qui libère une embuscade, reconnaissable à un détail), source (soin,
    réanimation, jauge méga), énigme de terrain (coffre derrière eau, lave ou lianes à franchir par les interactions),
    défi (survivre à la lave qui monte, ramasser des éclats en temps limité), marchand Kecleon (quand les boutiques
    existeront).
  - Thèmes logiques (demande d'Owen pour Robin) : la salle prend le décor et les obstacles du sous-biome de l'escalier
    (forêt → salle de forêt, montagne → grotte…) ; ses Pokémon (embuscade, gardien) sont tirés dans une population
    cohérente avec ce sous-biome (et plus largement : peuplement des quartiers par biome, à définir dans les données).
  - Briques déjà en place : catalogue de salles `rooms.json` (`RoomCatalog.Pick(type, rng, thème, variante)` ; types
    `monster_house`, `subboss`, `rest`…), construction d'une zone et retour (`IslandRun.Build(zone, arrivée)`,
    `LoadingScreen.Run`), coffre et objets (`Loot.SpawnChest`, `RollTyped`, `RollStone` ; rien n'est donné dans le
    terrain de test), ennemis (`EnemySpawner.SpawnFighter`, `Loot.MakeElite`, `GameData.RandomMoves`), recrutement
    (`Recruitment`, taux de la ROM × `recruitMultiplier`), rochers et interactions (`obstacles.json`, `boulder*`),
    clairières des forêts du quart de test. Données à prévoir : un fichier du genre `secret_rooms.json` (probabilité,
    poids des cachettes et des contenus, vagues, niveaux, populations).
  - Fait le 25/10/2026 (Owen) : escalier, cachettes (clairière, rocher fissuré brisé par Combat, Sol, Roche ou Acier),
    salle vide du catalogue et retour, dans le quart de test (`secret_rooms.json`, `SecretStairs`). Reste (Robin) : le
    contenu des salles (trésor, embuscade, gardien) et les thèmes logiques.
- Grottes scellées (décidé le 25/10/2026, Owen) : une petite grotte, point d'intérêt d'un quartier, fermée par un
  sceau qui ne s'ouvre qu'à une condition ; dedans, un escalier vers une salle à événement, comme l'escalier secret
  (même retour au même endroit). Contrairement à l'escalier secret, elle se voit : c'est une énigme, pas une surprise.
  - Sceau tiré d'après le biome quand c'est possible (sinon un sceau générique) :
    volcan : coulée de lave figée (Eau ou Glace la change en pierre qui se fissure), fumerolles (éteintes par la pluie) ;
    glacier : mur de glace (Feu ou Zénith, fond en 3 états), stalactites (Roche ou Combat les font tomber) ;
    côte : grotte marine noyée (marée basse la nuit, ou Plante qui absorbe), coquillages à frapper par ordre de taille ;
    forêt : racines ou lianes (Feu, Insecte, Plante), arbre creux endormi (s'éveille la nuit) ;
    désert : entrée ensevelie (Vol ou Tempêtesable), cadran solaire (Zénith) ;
    ruines : porte à runes (un Zarbi ou une Psy dans l'équipe), braseros (Feu sur chacun, ils s'éteignent après
    quelques secondes), plaques de pression (les 3 Pokémon dessus en même temps).
    Génériques : gardien (élite devant l'entrée), clé (coffre ou élite du quartier), nettoyer le quartier, temps passé.
  - Faisabilité : la plupart des sceaux tirés sont réalisables par l'équipe (types, attaques apprises) ; quelques-uns
    rares peuvent ne pas l'être (raison de rejouer).
  - Lisibilité (point délicat) : un repère commun à toutes les grottes scellées (même symbole, même lueur) ; l'icône de
    la condition gravée sur le sceau (icônes de types, soleil, lune, nuage) ; progression visible (états successifs,
    secousse, particules ; « cling » ou petit nuage gris quand l'attaque n'a pas d'effet) ; ouverture marquée (secousse
    de caméra, dissipation, escalier qui apparaît, signalé sur la mini-carte) ; bulle d'indice optionnelle à l'approche.
    Entrée tirée des éléments des cartes de l'histoire (`map_props`, entrée de grotte du style `rock`) ; états du sceau
    en planches Claude Design (comme `obstacle_*`).
  - Forme retenue (Owen) : une petite grotte comme celle du sommet des montagnes, sur un plateau isolé (pas creusée
    dans une falaise ni une colline) ; la porte est une partie de sa paroi qui disparaît ; un mécanisme devant la grotte
    fait comprendre l'énigme. L'escalier reste après usage.
  - Plaques de pression : seuls les Pokémon joués par des humains comptent, jamais les IA (énigme pour jouer entre amis).
  - Fait le 25/10/2026 : génération, porte, mécanisme provisoire, conditions type d'attaque, météo, équipe, braseros,
    plaques, coquillages, temps ; escalier vers une salle vide. Reste : nuit sur l'île, clé, gardien, nettoyer le
    quartier, planches Claude Design, contenu des salles.
  - Étapes : 1) données (`cave_seals.json`) et placement dans `IslandPlan` ; 2) `CaveSeal` (conditions type d'attaque,
    météo, braseros ou plaques ; repère et icône) ; 3) escalier vers une salle du catalogue (comme `SecretStairs`) ;
    4) nuit sur l'île, clé, gardien, planches.

### Niveaux continus (décidé le 22/10/2026, Robin) — devenu la base des chaînes d'îlots — remplace l'étage façon Isaac
- Génération plus organique, semi-ouverte, proche de Minecraft Dungeons : un niveau = une seule grande carte parcourue
  sans fondu ; les salles deviennent des zones aux contours naturels (ou construites : ruines, labyrinthe) reliées par
  des chemins sinueux ; chemin principal du départ au boss, branches optionnelles (défis, recoins), quelques boucles.
- Taille : 10 à 14 zones, 5 à 8 minutes de traversée sans combat. Mini-carte avec brouillard qui se révèle.
- Biomes : un thème par niveau et une suite de sous-biomes cohérents le long du chemin (`biomes.json`), changement de
  décor sur une frontière naturelle (goulet, rivière, ravin, arche) et dégradé d'obstacles avant la frontière.
- Exploration : passages secrets ouverts par les interactions du décor (rocher brisé, lianes brûlées, rivière gelée,
  pont refait), points de repère.
- Combats libres en général ; sortie bloquée (barrière de glace ou de ronces) pour les défis et le boss.
- Ordre : 1) carte continue (zones, chemins, branches, mini-carte), 2) sous-biomes et transitions, 3) exploration,
  4) rencontres (ennemis par zone, défis, sous-boss, boss).

### Jour et nuit (décidé le 25/10/2026, Owen ; branche `feature/jour-nuit`)
- Un cycle de 10 minutes réelles (aube 1 min, jour 5, crépuscule 1, nuit 3), arrêté pendant les pauses ; une partie
  commence à une heure tirée au hasard. Il remplace l'alternance jour / nuit par étape.
- Éclairage : la lumière globale change de couleur et d'intensité selon la phase (orangé, blanc, rose-orangé, bleu
  sombre mais jouable) ; barres de vie, nombres, visées, icônes et effets d'attaque restent non éclairés.
- La nuit compte pour les évolutions de nuit et les sceaux de nuit des grottes.
- **Pour Robin (rencontres)** : la nuit doit influer sur les Pokémon qui apparaissent : types Spectre et Ténèbres plus
  fréquents, une liste de Pokémon nocturnes favorisés (Hoothoot, Nosferapti, Mimigal, Ténéfix, Feuforêve…), et
  éventuellement des Pokémon de jour seulement ou de nuit seulement ; poids dans `encounters.json`, phase lue par
  `DayNight.IsNight` (ou `DayNight.Now`).
- Lumières ponctuelles (fait le 25/10/2026) : Pokémon Feu (et Loupio, Lanturn, Muciole, Lumivole), tirs et impacts Feu et
  Électrik, lave et braises, braseros, repère et entrée des grottes, escaliers, arcs des pylônes ; presque invisibles le
  jour, pleines la nuit.
- Eau animée (fait le 25/10/2026) : l'animation de palette de la ROM (miroitement de l'eau, lueur de la lave, cristaux)
  sur les sols, murs et obstacles des décors. Sens du courant des rivières : avec les particules.
- Particules d'ambiance (fait le 25/10/2026, planches Claude Design) : lucioles (forêts,
  prairie, crépuscule et nuit), feuilles, pétales, pollen, braises (volcan, lave), neige, poussière (désert, ruines),
  courant des rivières (reflets qui descendent vers la côte), bulles de lave.
- Vent dans la végétation (fait le 25/10/2026) : arbres, palmiers, buissons, fougères et fleurs ondulent au pixel près
  (pied fixe), plus fort et penchés pendant une tempête ; les feuilles et la neige dérivent avec le vent.
- Post-traitement (fait le 25/10/2026) : bloom (lave, feu, lucioles et lumières brillent la nuit), couleurs et vignette
  par phase, en fondu avec l'horloge ; coupable dans `meta.json` (`postProcessing`).

### Types et environnement
- La table des types est conservée (avantages et faiblesses), celle de la 4e génération (époque de la ROM).
- Multiplicateurs adoucis (17/10/2026, `meta.json`) : super efficace × 1,5, pas très efficace × 0,6, sans effet × 0,25 (pas d'immunité totale) ; une faiblesse et une résistance s'annulent ; résultat borné entre × 0,2 et × 2. Bonus d'attaque du même type (STAB) × 1,25.
- Chiffres de dégâts (17/10/2026) : au-dessus du Pokémon touché, en pixel art. Blanc normal, orange et plus gros si super efficace, gris-bleu et plus petit si pas très efficace, gris foncé sans effet, couleur du statut pour les dégâts de statut, vert précédé de « + » pour les soins.
- L'environnement réagit au type ou à la nature du Pokémon : les Pokémon volants survolent les trous, les Pokémon aquatiques vont dans l'eau.

### Rencontres (décidé le 24/10/2026, Robin ; étape 1 faite sur `feature/rencontres`)
- Les vagues ne sont plus écrites à la main : chaque vague tire des **rencontres** jusqu'à épuiser un budget de
  menace qui grandit avec l'étape et la vague. Lots (précisés le 24/10/2026, Robin) : **solitaire** (un Pokémon fort,
  commun à épique), **équipe** (corps à corps, distance, soutien, le 2e choisi pour que son type se combine avec celui
  du 1er : Eau + Électrik, Plante + Feu… ; commun à épique), **meute** (4 à 6 fois le même Pokémon, niveau bas, commun à
  rare), **gang** (trois solitaires, chacun avec un objet, donc trois ultimes ; rare à épique, à partir de l'étape 3).
- Combinaisons : un lot peut venir avec un autre dans le même groupe (meute menée par un solitaire rare ou épique,
  équipe et solitaire, meute et équipe ; probabilités dans `encounters.json`).
- Téra ennemi cohérent avec l'environnement : le type choisi aide aussi à se déplacer parmi les obstacles autour (Eau ou
  Vol sur un lac, Spectre entre les rochers…) ; utilisé dès le début du combat quand c'est pour le terrain.
- **Élites** (étape 2, 24/10/2026) : au plus un par étape, plus souvent en avançant (15 % à l'étape 1, 60 % à
  l'étape 4), pendant une vague, en plus de son budget, annoncé par un bandeau. **Champion** : un Pokémon très fort
  (plus évolué, +5 niveaux, PV × 6, 4 attaques puissantes, objet et méga si possible, plus grand). **Escouade
  d'élite** : trois complémentaires, rares ou épiques, seul le chef porte un objet. Anneau doré, butin garanti (à la
  chute du champion, quand toute l'escouade est tombée) ; ils modifient le terrain comme les alliés. Les élites tirés
  au hasard parmi les ennemis ordinaires disparaissent des rencontres.
- À faire plus tard : un usage plus stratégique des méga-évolutions par l'ennemi.
- Chaque ennemi a un **rang** (commun, peu commun, rare, épique), plus fréquent en avançant : niveaux, PV et attaques en
  plus, attaques prises parmi les plus puissantes, objet et ultime pour les épiques ; anneau de couleur sous lui.
- Les Pokémon vont avec le décor (types) et leur stade d'évolution avec l'étape ; légendaires exclus.
- Suite : 2) élites (champion solitaire ou escouade), 3) comportements de groupe (rôles, encerclement, combos de
  terrain, obstacles), 4) intégration et équilibrage.

### Objets de partie (décidé le 24/10/2026, Robin ; étape 1 faite sur `feature/sac`)
- Distincts des objets spéciaux (méga-gemmes, cristaux Z, Téra-Éclats). Deux familles : **consommables** (baies,
  graines, orbes) dans un **sac partagé** par l'équipe, et **objets tenus** (un par Pokémon, passifs ; étape 2).
- Rien ne se garde d'une partie à l'autre (esprit roguelike).
- Commandes : 1 à 4 au clavier ; à la manette, croix haut / bas pour choisir et □ pour utiliser.
- Étape 1 (faite) : 4 emplacements, piles de 9 ; Baie Oran / Sitrus (soin), Ceriz / Pêcha / Fraive (statuts),
  Graine Explo (dégâts en zone), Dodo (sommeil), Rappel (relève un K.O.), Vive (vitesse), Orbe Pétrif (fige les
  ennemis visibles), Orbe Gel (gèle l'eau de la salle) ; lâchés par les ennemis selon leur rang (élites : toujours) et
  dans le coffre de fin d'étape ; les alliés s'en servent (soin sous 35 % des PV, statut, K.O.).
- Étape 2 (faite) : **objets tenus**, un par Pokémon : Bandeau Choix, Lunettes Choix, Mouchoir Choix, Restes, Orbe
  Vie, Grelot Coque, Casque Brut, Ceinture Force, et liés au terrain : Ballon (flotte au-dessus de l'eau, du vide et de
  la lave jusqu'au premier coup), Bottes de Magma (marche sur la lave), Cape Sable (terrains au sol sans effet). Les
  ennemis rares, épiques et les élites peuvent en tenir un (il agit pour eux) et le lâchent ; le coffre parfois. Le
  Pokémon joué le prend en marchant dessus, l'ancien tombe à ses pieds (pour le donner à un autre membre).
- **Tiers** (24/10/2026) : les objets tenus à effet chiffré (Bandeaux, Lunettes, Mouchoir, Restes, Orbe Vie, Grelot,
  Casque) existent en I, II, III (anneau bronze, argent ou or) ; un ennemi rare en lâche un I ou II, épique II ou III,
  élite III ; ramasser le même objet du même tier le monte d'un tier. Ajustements : Graine Explo et Casque Brut
  plafonnés contre un boss (5 % et 2 % de ses PV), Orbe Pétrif ralentit un boss, Restes seulement hors combat (1 %/s),
  Orbe Vie une fois par attaque, Orbe Gel gardé s'il n'y a pas d'eau.
- Sprites des objets : ceux de Donjon Mystère (PMDCollab RawAsset). Suite : boutique Kecleon et monnaie.

### Boss
- Chaque boss est un Pokémon légendaire (Groudon en premier), avec 4 attaques toujours les mêmes, pour que le joueur apprenne ses schémas.
- Mort du boss : ses gardes disparaissent sans rapporter d'expérience ni de recrue (17/10/2026).
- Annonce (décidé le 13/10/2026) : avant chaque attaque, le boss affiche une bulle avec un chiffre de 1 à 4. C'est la seule indication : aucune zone rouge au sol. L'attaque est tirée au hasard ; la visée est verrouillée à l'apparition de la bulle. Sous 50 % de PV, les annonces durent 60 % de leur temps normal.
- Groudon : 1 Tir de Boue (3 projectiles en éventail sur 40°, ralentissent), 2 Tranche (ruée de 5 cases), 3 Séisme (dégâts dans un rayon de 4 cases autour de lui), 4 Éruption (une flaque de lave sous chaque membre de l'équipe pendant 4 s, premier dégât après 0,6 s puis chaque seconde). Annonces : 0,8 s, 0,9 s, 1,4 s et 1,0 s.
- Résistance des boss aux statuts (décidé le 13/10/2026) : le poison ne leur retire qu'un quart des dégâts normaux (1 % des PV max par tic au lieu de 4 %) et la paralysie dure moitié moins longtemps sur eux. Sans cela, le poison usait un boss aussi vite qu'un ennemi ordinaire.

### Entre les parties
- Recrutement : un Pokémon battu a un pourcentage de chance de rejoindre le joueur (comme dans le jeu d'origine).
- Un Pokémon recruté est débloqué dans la première forme de sa lignée (décidé le 15/10/2026) : battre un Dracaufeu débloque Salamèche, puisque chaque partie commence au niveau 1 et qu'on évolue en jouant. Rien ne change pour les Pokémon sans évolution.
- Mise en œuvre (15/10/2026) : le taux appliqué est celui de la forme débloquée (dans la ROM, 186 formes évoluées sur 312 ont un taux négatif, Dracaufeu −12 %) ; taux nul ou négatif = jamais recruté (légendaires). Tirage seulement pour une espèce pas encore débloquée ; la recrue est gardée même en cas de défaite. Provisoire : on remonte la lignée jusqu'à la première forme jouable (Raichu → Pichu), en attendant la décision sur les évolutions sans niveau (point 10).
- Menu avant la partie (15/10/2026) : choix de 3 Pokémon parmi les débloqués ; les autres apparaissent en silhouette. Seule la première forme jouable de chaque lignée se choisit (pas d'évolution ni de forme alternative) ; la grille défile (17/10/2026). Débloqués au départ : les 6 jouables d'origine (`meta.json`). Fin de partie : rejouer avec la même équipe ou revenir au menu.
- Deux monnaies : une gagnée et dépensée pendant la partie (boutiques), une autre conservée entre les parties pour acheter des attaques équipables au départ, et peut-être un objet donnant un passif pendant la partie (modèle Wizard of Legend).

### Périmètre final
- Tous les Pokémon de la ROM sont jouables, et tous peuvent apparaître comme ennemis. Cela impose de générer automatiquement les attaques temps réel à partir des données, avec des retouches à la main au cas par cas.

### Première version jouable
- 3 Pokémon jouables, 5 ou 6 ennemis, une quinzaine d'attaques réparties sur 4 familles.

## 2 bis. État du prototype

- Fait : déplacement au stick gauche, visée au stick droit, 4 emplacements d'attaque sur R1 / R2 / L1 / L2 avec temps de recharge, changement de Pokémon sur la croix (gauche / droite), familles « projectile » et « mêlée », barres de vie, vitesse de déplacement tirée de la vitesse de base (ramenée entre 3 et 6 cases/s).
- Flèche de visée aux pieds du Pokémon, visible seulement quand on vise (stick droit ou souris).
- Attaques à distance : recharge d'environ 3 s (jugées trop efficaces face au corps à corps). Charge projette l'attaquant de 2,5 cases vers l'avant, joueur comme ennemi.
- Ennemis : 6 Rattata poursuivent le joueur, annoncent leur attaque par une zone rouge pendant 0,6 s (direction verrouillée, donc esquivable), frappent (Charge), puis marquent une pause.
- Équipe : les 3 Pokémon sont présents en même temps. Le joueur en contrôle un et en change avec la croix (gauche / droite) ; les deux autres sont joués par l'IA : ils restent à 2 cases du joueur, attaquent l'ennemi le plus proche (jusqu'à 7 cases) avec la première attaque prête, reculent pendant les recharges et reviennent s'ils s'éloignent de plus de 7 cases. Ils n'esquivent pas les attaques annoncées.
- Les ennemis visent le membre de l'équipe le plus proche (remplacé le 15/10/2026 par la cible de groupe).
- Déplacement des IA (alliés et ennemis) : en ligne droite si la voie est libre, sinon recherche de chemin sur la grille de l'arène pour contourner les murs. Les zones nocives pour leur équipe (nuage de gaz) coûtent cher à traverser, donc elles sont contournées ; une IA qui se trouve dedans en sort avant toute autre action. Une IA n'attaque que si elle a une ligne dégagée vers sa cible (sauf attaques de zone), et ne fonce pas dans un nuage.
- Limite : les IA évitent les zones déjà posées, pas les attaques encore en cours d'annonce (bande ou disque rouge). Levée le 15/10/2026 pour les alliés (voir esquive).
- Esquive (15/10/2026) : ✕ à la manette, Ctrl gauche au clavier. Ruée de 2,5 cases en 0,2 s dans la direction du déplacement (sinon celle du regard), recharge de 3 s. On traverse les ennemis mais pas les murs, et on n'est pas invincible : les attaques touchent pendant la ruée. Joueur et alliés seulement. Les alliés esquivent les attaques annoncées (bande ou disque rouge) et les projectiles qui arrivent sur eux ; si l'esquive recharge, ils sortent à pied. Un allié corps à corps l'utilise aussi pour foncer sur une cible entre 3 et 6 cases s'il a plus de 50 % de PV. Réglages dans `meta.json`.
- Ennemis en groupe (15/10/2026) : chaque vague est répartie en groupes de 3 (le reste forme un petit groupe), qui apparaissent ensemble. Chaque groupe d'au moins 2 a forcément un soutien (un ennemi seul garde le rôle de ses attaques, 17/10/2026) ; les deux autres sont corps à corps ou distance au hasard (deux du même rôle possible). Les attaques sont tirées pour donner ce rôle quand la liste du Pokémon le permet. Si aucun membre ne peut être soutien, l'un est remplacé par un Pokémon de l'étape (sinon de la partie) qui le peut. Le groupe vise le membre de l'équipe le plus proche de son centre, réévalué toutes les 2 s ou dès qu'il tombe.
- Ennemis (17/10/2026) : ils bougent dès que leur attaque part (plus de pause immobile) ; les 1,5 s qui suivent ne servent qu'à retarder l'annonce suivante. Ils ont aussi l'esquive (recharge de 5 s, `enemyDodgeCooldown`) : distance et soutien fuient un adversaire à moins de 2,5 cases (sauf acculés), corps à corps foncent sur leur cible entre 3 et 6 cases. Le boss garde sa pause et n'esquive pas.
- Équilibrage (15/10/2026) : ennemis à 50 % des dégâts normaux (25 % avant), PV des boss × 16 au lieu de × 8.
- Sortie de zone corrigée (15/10/2026) : une IA dans une zone nocive tournait en rond (chaque pas vers la sortie était refusé parce qu'encore dans la zone). Elle sort maintenant au plus court, même quand plusieurs zones se chevauchent.
- K.O. : un Pokémon K.O. reste au sol ; si c'était celui du joueur, le contrôle passe au suivant encore debout ; quand les trois sont K.O., la partie s'arrête et se relance avec Options ou Entrée.
- Caméra : elle glisse vers le nouveau Pokémon quand on change de contrôle (environ un tiers de seconde).
- Ennemis : 5 Rattata (corps à corps) et 2 Smogo (distance), avec des PV triplés (Rattata 45, Smogo 126). Ils choisissent la première attaque prête qui porte ; un ennemi à distance garde 4 cases d'écart pendant ses recharges.
- Smogo : Purédpois (projectile lent, 40 % de chance d'empoisonner) et Gaz Toxik (nuage posé sur la cible pendant 4 s, qui empoisonne ceux qui s'y trouvent). Annonce par une bande rouge (tir) ou un disque rouge (nuage).
- Famille « zone » : une aire qui reste au sol et applique son statut.
- Statut empoisonné : 4 % des PV max par seconde pendant 5 s, sans tenir compte de l'invulnérabilité ; teinte violette et icône au-dessus de la barre de vie. Icône originale faite avec Claude Design (celle du jeu d'origine n'a pas été localisée dans la ROM).
- Progression : expérience commune (chaque ennemi battu rapporte son rendement d'expérience de la ROM), niveau commun affiché avec une barre bleue ; à chaque niveau, chaque Pokémon gagne les statistiques prévues par la ROM. Tous les 10 niveaux, le jeu se met en pause et propose 1 amélioration parmi 3 à chaque Pokémon, l'un après l'autre (croix ou stick pour choisir, ✕ ou Entrée pour valider). Seules les améliorations utiles au Pokémon lui sont proposées.
- Rôles (13/10/2026) : chaque Pokémon est corps à corps, distance ou soutien (champ `role` de ses attaques de départ), et son IA en découle, allié comme ennemi. Corps à corps : va au contact. Distance : garde 4 cases pendant ses recharges. Soutien : soigne d'abord l'allié sous 70 % de PV, pose ses bonus et malus, fuit un adversaire à moins de 2,5 cases, reste à 3 cases de ses alliés, n'attaque qu'ensuite.
- Placement des IA (13/10/2026) : un Pokémon à distance reste entre 4 et 6 cases de sa cible et recule en dessous de 3,5 ; un soutien reste entre 5 et 7 cases et recule en dessous de 4. Trop près avec la place de reculer, ils reculent avant de tirer ; acculés, ils se battent sur place. Leurs attaques de mêlée ne servent qu'en défense (adversaire à moins de 1,5 case). Toutes les IA gardent 1,5 case entre membres d'un même camp, y compris au repos autour du joueur.
- Rugissement a un rayon de 4,5 cases ; un soutien ne s'avance pas pour le lancer. Para-Spore et Poudre Toxik portent à 6 cases, pour rester utilisables depuis la bande des soutiens.
- Jouables (6) : Bulbizarre (soutien : Vampigraine, Rugissement, Poudre Toxik, Tranch'Herbe), Salamèche (distance), Carapuce (distance), Pichu (remplace Pikachu le 15/10/2026 : seules les premières formes se choisissent au menu), Goinfrex (corps à corps : Charge, Léchouille, Boul'Armure), Riolu (corps à corps : Vive-Attaque, Forte-Paume). Équipe de 3 choisie dans le menu depuis le 15/10/2026 ; les 6 ennemis sont recrutables.
- Ennemis (6, deux par rôle, six types) : Rattata et Machoc (corps à corps), Smogo et Stalgamin (distance), Granivol (soigneur) et Tarsal (affaiblisseur).
- Mécaniques ajoutées : soin (part des PV max de l'allié le plus blessé à portée), drain (Vampigraine rend à l'allié le plus blessé ce qu'elle inflige), bonus et malus temporaires d'attaque, de défense ou de vitesse (même type sur la même statistique : rafraîchi, pas cumulé), paralysie (vitesse divisée par deux).
- Icônes d'effets : au-dessus de chaque Pokémon, statuts puis bonus et malus (attaque, défense, vitesse), 4 par ligne puis une seconde ligne ; les mêmes à droite de la barre de vie du Pokémon contrôlé. Une icône clignote pendant sa dernière seconde. Provisoirement, bonus et malus sont des flèches colorées (rouge attaque, bleu défense, vert vitesse), remplacées dès qu'une planche `status_attack_up`, `status_speed_down`, etc. existe.
- Dégâts : bruts × 30 / (30 + défense de la cible), défense ou défense spéciale selon la catégorie de l'attaque ; un bonus de défense divise encore les dégâts reçus. Le poison ignore la défense.
- Constat avec ces réglages : équipe laissée sans joueur, les 5 Rattata tombent en 15 s mais les 2 Smogo mettent ensuite toute l'équipe K.O. Les alliés n'esquivent pas.
- Ancien constat (avant renforcement) : sans aucune action du joueur, l'équipe élimine les 6 Rattata en moins de 10 secondes. Les ennemis actuels sont réglés pour un seul Pokémon.
- Équilibrage provisoire : dégâts = puissance + statistique d'attaque (ni types ni défense) ; les ennemis infligent 25 % de ces dégâts ; 0,8 s d'invulnérabilité après chaque coup reçu par le joueur. Immobile au milieu des six Rattata, un Pokémon tient 6 à 7 s.
- Provisoire aussi : projectiles affichés comme des disques de la couleur du type ; affichage des recharges en texte.
- Attaques en place : Tranch'Herbe et Charge (Bulbizarre), Flammèche et Griffe (Salamèche), Pistolet à O, Écume et Charge (Carapuce), Charge (Rattata), Purédpois et Gaz Toxik (Smogo).

## 3. Ce que disent les données

- 524 attaques, dont 222 de statut (42 %), 197 physiques, 105 spéciales. 457 sont apprenables par au moins un Pokémon.
- Un Pokémon apprend en médiane 14 attaques par niveau et environ 50 en comptant CT et œufs. La dernière s'apprend en médiane au niveau 50.
- Bulbizarre : 14 attaques par niveau, dont 8 de statut. Niveaux d'apprentissage de 1 à 37.
- Statistiques de niveau 1 très basses (Bulbizarre : 22 PV, 3 en attaque). Courbes par niveau pas encore extraites.
- Aucune statistique de vitesse dans la ROM : les données n'ont que PV, attaque, attaque spéciale, défense, défense spéciale, et la vitesse de déplacement vaut 1 pour les 558 Pokémon.
- Taux de recrutement : 208 Pokémon sur 558 ont un taux nul ou négatif ; la plupart des autres sont entre 5 et 15 % (8,2 % pour les trois starters).
- 187 évolutions par niveau, niveau médian 28, maximum 55.
- Chaque attaque a des champs d'IA (poids, portée visée) utilisables pour les alliés et les ennemis.
- 124 talents, 558 Pokémon (122 talents réellement portés ; 230 espèces ont 1 talent, 262 en ont 2 ; aucune sans talent).
- Chaque Pokémon a déjà un type de déplacement : standard (305), survol des trous (104), traversée des murs (14, spectres), marche sur la lave (34), nage (101). Les décors contiennent aussi les blocs d'eau et de lave.

## 4. Points ouverts

Revus le 25/10/2026 : les points tranchés depuis la première version sont regroupés à la fin.

### Structurants
2. Périmètre de la première version : un seul cycle (2 arènes + 1 boss) ? Et quelle structure de partie : étapes,
   île flottante, niveaux continus (branches d'Owen) ?
3. Échelle de vitesse : provisoirement 3 à 6 cases/s pour une vitesse de base de 5 à 160. À ajuster en jouant.
17. Cibles d'équilibrage : durée d'une partie, niveau visé, taux de victoire (objectif d'origine : niveau 100 en
    30 minutes).

### Combat
7. IA alliée : ordres possibles ?
8. Équilibrage solo / 2 joueurs / 3 joueurs.
9. Lisibilité du type d'un ennemi à l'écran (en partie réglée par la couleur des chiffres de dégâts).

### Progression
11. Talents : passifs des mégas à valider ; débloquer le talent caché par une amélioration en partie ? Valeurs à
    régler après le test en jeu.
12. Recrutement des Pokémon à taux nul (légendaires, boss).
13. Rythme de l'expérience : régulier, ou rapide au début puis plus lent ?

### Contenu
15. Boss : lesquels, et avec quels comportements spécifiques ? (Groudon seul pour l'instant ; la seconde phase des
    élites peut servir de modèle.)
16. Boutiques et monnaies : quoi acheter, où, avec quelle monnaie (une en partie, une entre les parties) ? Boutique
    Kecleon envisagée.

### Tranchés depuis
1. Boutons : attaques sur les gâchettes, changement de Pokémon sur la croix gauche / droite, esquive sur ✕.
4. Fréquence des choix : tous les 10 niveaux.
5. Ennemis : mêmes attaques que les joueurs, annoncées avant de partir (zone rouge, bulle numérotée pour les boss).
6. Formule de dégâts : bruts × 30 / (30 + défense), table des types adoucie, STAB.
10. Évolutions sans niveau : décidé le 17/10/2026 (voir Progression en partie).
14. Terrains spéciaux : eau, lave, vide, rochers et autres obstacles en partie, franchis selon le type de déplacement
    ou le type du Pokémon (voir Obstacles).
16 (en partie). Objets : sac partagé de consommables et objets tenus à tiers (voir Objets de partie).
