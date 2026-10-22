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
  - Électrik **Fatal-Foudre** : après 1 s de charge, zone autour du lanceur où tombent des éclairs au hasard, avec de petites ondes de choc (dégâts, faible chance de paralysie).
  - Glace **Blizzard** : même charge et même zone que Fatal-Foudre, mais ralentit et blesse chaque seconde.
  - Combat **Frappe Atlas** : courte ruée, saisie, saut et écrasement au sol, onde de choc autour de l'impact.
  - Poison **Acidarmure** : le Pokémon se liquéfie 2 s (intouchable, plus rapide, traverse et empoisonne les ennemis).
  - Sol **Telluriforce** : une marque sous chaque ennemi à 5 cases ou moins du lanceur (0,6 case de rayon) ; les ennemis pris dedans sont figés (animation de dégâts) jusqu'à la colonne de terre, 0,5 s plus tard ; un cercle montre la zone d'action à chaque utilisation.
  - Vol **Lame d'Air** : grande lame rapide qui traverse ; 20 % de chance d'étourdir 1 s.
  - Psy **Psyko** : soulève la cible 0,8 s puis la projette (dégâts si elle heurte un mur).
  - Insecte **Plaie-Croix** : ruée de 4,5 cases qui traverse les ennemis ; figés (animation de dégâts) dès qu'elle les touche, ils sont tranchés d'un coup 0,25 s après la fin de la ruée (entaille en X), comme un coup de sabre à retardement.
  - Roche **Pouv. Antique** : 5 pierres en orbite (bloquent les tirs) puis lancées une à une sur la cible.
  - Spectre **Onde Folie** : cône qui rend confus 4 s (sans dégâts) ; et **Ball'Ombre** : charge tant que le bouton est enfoncé (immobile, maximum après 2 s, peut être tenue indéfiniment), la charge augmente taille et dégâts ; rapide, courte portée, s'arrête et flotte en bout de course.
  - Dragon **Draco Météor** : gros météores qui tombent en ligne en partant du lanceur, puis Attaque Spé du lanceur en baisse.
  - Ténèbres **Coup Bas** : garde de 1 s ; si un ennemi attaque le lanceur, il se téléporte derrière lui et frappe très fort, sinon petit coup devant.
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
- Talents réadaptés au temps réel.

### Progression en partie
- Chaque partie commence au niveau 1 et vise environ le niveau 100 après 30 minutes.
- Les statistiques suivent les données réelles : un joueur qui connaît Pokémon doit reconnaître qui est résistant, qui frappe fort, au corps à corps ou à distance.
- Expérience commune à l'équipe.
- Tous les 5 ou 10 niveaux : 1 choix parmi 3 améliorations aléatoires par Pokémon (statistique, modification d'une famille d'attaques comme +50 % de taille de projectile, nouvelle attaque). Les 3 Pokémon montant de niveau ensemble, une seule coupure sert à faire les 3 choix.
- Les Pokémon évoluent pendant la partie. On débloque toujours la première forme d'une lignée.
- Évolution (plan validé par Owen le 17/10/2026, fait le même jour ; à tester à la manette) :
  - Manuelle : quand les conditions sont réunies (y compris par niveau), le joueur appuie sur Triangle. Animation : silhouettes blanches de l'ancienne et de la nouvelle forme qui alternent (environ 2,5 s, Pokémon immobile et invincible), puis onde de choc de 5 cases (recul de 2 cases, dégâts 15 + niveau).
  - Jour et nuit alternent à chaque étape (évolutions de jour ou de nuit).
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

### Types et environnement
- La table des types est conservée (avantages et faiblesses), celle de la 4e génération (époque de la ROM).
- Multiplicateurs adoucis (17/10/2026, `meta.json`) : super efficace × 1,5, pas très efficace × 0,6, sans effet × 0,25 (pas d'immunité totale) ; une faiblesse et une résistance s'annulent ; résultat borné entre × 0,2 et × 2. Bonus d'attaque du même type (STAB) × 1,25.
- Chiffres de dégâts (17/10/2026) : au-dessus du Pokémon touché, en pixel art. Blanc normal, orange et plus gros si super efficace, gris-bleu et plus petit si pas très efficace, gris foncé sans effet, couleur du statut pour les dégâts de statut, vert précédé de « + » pour les soins.
- L'environnement réagit au type ou à la nature du Pokémon : les Pokémon volants survolent les trous, les Pokémon aquatiques vont dans l'eau.

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
- 124 talents, 558 Pokémon.
- Chaque Pokémon a déjà un type de déplacement : standard (305), survol des trous (104), traversée des murs (14, spectres), marche sur la lave (34), nage (101). Les décors contiennent aussi les blocs d'eau et de lave.

## 4. Points ouverts


### Structurants
1. Boutons : avec la visée au stick droit, les 4 attaques vont sur les gâchettes (L1, L2, R1, R2). Quel bouton pour changer de Pokémon ?
2. Périmètre de la première version : un seul cycle (2 arènes + 1 boss) ?
3. Échelle de vitesse : provisoirement 3 à 6 cases/s pour une vitesse de base de 5 à 160. À ajuster en jouant.
4. Fréquence des choix : tous les 5 niveaux (environ 20 coupures et 60 choix par partie) ou tous les 10 (10 coupures, 30 choix) ?

### Combat
5. Ennemis : confirmer le partage des attaques avec les joueurs ; prévoir un temps d'annonce avant leurs attaques pour qu'elles soient esquivables.
6. Formule de dégâts.
7. IA alliée : niveau d'autonomie, ordres possibles ?
8. Équilibrage solo / 2 joueurs / 3 joueurs.
9. Lisibilité du type d'un ennemi à l'écran (en partie réglée par la couleur des chiffres de dégâts).

### Progression
10. Évolutions sans niveau : décidé le 17/10/2026 (voir Progression en partie).
11. Talents : un par Pokémon, passif permanent ou à déclencher ?
12. Recrutement des Pokémon à taux nul (légendaires, boss).
13. Rythme de l'expérience : régulier, ou rapide au début puis plus lent ?

### Contenu
14. Terrains spéciaux (trous, eau, lave) : dans la première version ou plus tard ? Les spectres traversent-ils les murs ?
15. Boss : lesquels, et avec quels comportements spécifiques ?
16. Objets et boutiques : quels objets, quels effets ? Piste d'Owen (15/10/2026) : objets trouvés au hasard ou chez des marchands, qui donnent des effets passifs aux Pokémon.

### Refonte visuelle du terrain (décidé le 23/10/2026, Owen)
- But : s'éloigner de la direction artistique de Donjon Mystère pour un monde féerique, organique et profond ;
  remplacer les décors de la ROM biome par biome si le test est concluant.
- Vue de 3/4 dessus (pas d'isométrique : commandes, formes d'attaque, effets et sprites des Pokémon inchangés), faux
  3D par la profondeur : tri par profondeur, grands éléments qui passent devant, ombres portées, dénivelés décoratifs,
  dessous de l'île et nuages en contrebas qui défilent à des vitesses différentes, lumière et étalonnage.
- Pixel art riche à 24 px par case, compatible avec les sprites de DM. Références : Eastward, Minecraft Dungeons,
  Pokémon Ranger. Contours : à choisir sur les images de concept.
- Organique à l'écran, grille invisible pour le gameplay : sol mélangé par shader (transitions irrégulières), éléments
  posés librement ; écart d'environ une demi-case accepté entre le bord visible et la collision.
- Animations par shader (vent dans l'herbe, les fleurs et les feuillages ; eau qui coule), quelques animations dessinées
  (papillons, reflets). Un arbre devient transparent quand un Pokémon passe derrière. Jour/nuit après le test.
- Étapes : 1) recherche visuelle (3 concepts, `Design/prompt_da_ile_recherche.md`) ; 2) liste des assets et prompts ;
  3) vitrine technique (petit bout d'île : prairie, fleurs, grands arbres, rivière, pont, bord de l'île) ; 4) bilan.

