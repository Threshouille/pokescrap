# Pokescrap — Game Design Document

État : brouillon. Les sections « Décidé » viennent d'Owen ; les « Points ouverts » sont à trancher.

## 1. Vision

Roguelike d'action en temps réel, jouable à la manette, dans une arène. Le jeu reprend les données de Pokémon Donjon Mystère (Pokémon, statistiques, attaques, talents) et les adapte au temps réel, sans chercher à reproduire le jeu d'origine. Références : Children of Morta, Wizard of Legend. Projet privé.

## 2. Décidé

### Équipe et contrôle
- 3 Pokémon choisis avant la partie, tous présents en même temps dans l'arène.
- Les Pokémon non joués sont contrôlés par l'IA ou par d'autres joueurs (coopération).
- En solo, le joueur change à la volée le Pokémon qu'il contrôle.
- Un Pokémon K.O. : le joueur bascule sur un Pokémon encore en vie. La partie se termine quand les 3 sont K.O.
- Réanimation : en restant quelques secondes sur un allié K.O. avec le Pokémon contrôlé (3 s à moins d'1 case, jauge au-dessus de l'allié, retour à 50 % des PV avec une courte protection ; la jauge se vide deux fois plus vite si on s'éloigne). Aussi prévue entre deux arènes et par objet.
- Interface : en bas à gauche, portrait du Pokémon contrôlé (tiré de la ROM), nom, barre de vie, et 4 cadres pour ses attaques (bouton, nom abrégé, couleur du type) qui se grisent pendant la recharge avec le temps restant. Au-dessus, les petits portraits des deux alliés avec leur barre de vie, dans l'ordre de la croix (gauche / droite) ; un allié K.O. est assombri, un allié empoisonné a un coin violet.
- Des Pokémon « support » sont envisagés, pour donner un rôle aux attaques de statut.

### Attaques
- 4 emplacements par Pokémon. On commence la partie avec 2 attaques choisies avant la partie parmi toutes celles que la forme de base apprend par niveau (1 à 100) ; CT et attaques par œufs exclues (décidé le 15/10/2026).
- Pendant la partie, tous les 10 niveaux, l'une des 3 propositions du choix d'amélioration est une nouvelle attaque tirée au hasard dans la liste du Pokémon, jusqu'à 4 attaques ; ensuite cette proposition n'apparaît qu'une fois sur deux et sert à remplacer une attaque apprise. Après une évolution, les attaques de la forme évoluée ont plus de chances d'être proposées (15/10/2026).
- Rôle de l'IA déduit des attaques équipées (15/10/2026) : soutien avec un soin ou un bonus aux alliés, distance si projectiles et zones dominent, corps à corps sinon.
- Ennemis : 2 attaques tirées au hasard dans leur liste, dont au moins une offensive si possible (15/10/2026). Les boss gardent leurs attaques fixes.
- Évolution en partie (15/10/2026) : au niveau du jeu d'origine, statistiques de base de la nouvelle forme, niveaux gagnés conservés ; les attaques de la forme actuelle comptent double au tirage des nouvelles attaques. Évolutions sans niveau : pas encore décidé.
- Attaques de base (15 au 17/10/2026) : une attaque à distance et une au corps à corps par type (2 × 17), à recharge courte, pour taper en continu entre les grosses attaques. Pas de place réservée : les attaques de base des types du Pokémon (des deux styles, donc jusqu'à 4 pour un double type) s'ajoutent à sa liste au menu ; celles du style correspondant à ses statistiques (Attaque > Attaque Spé → corps à corps, sinon distance) sont présélectionnées. On commence avec 2 attaques choisies dans cette liste, et elles se remplacent comme les autres.
  - Distance : recharge d'environ 1 s, temps de lancement de 0,25 s pendant lequel le Pokémon est immobile ; le tir part dans la direction visée à la fin du lancement. Chaque attaque a sa trajectoire, son nombre de projectiles et sa taille (Météores en orbite, Flammèche 3 flammes successives, Pistolet à O grosse goutte, Tranch'Herbe en courbes croisées, Éclair rapide en ligne droite, Poudreuse en éventail, Onde Vide qui grandit, Acide 3 gouttes en cloche de plus en plus loin, Coud'Boue en cloche, Tornade lente qui grossit, Choc Mental qui accélère, Dard-Nuée en rafale, Jet-Pierres en cloche, Vent Mauvais lent et perçant, Draco-Rage en boomerang, Poursuite à tête chercheuse, Miroi-Tir en rafale qui rebondit). Les attaques en cloche visent automatiquement l'ennemi le plus proche dans un cône de 40° et laissent une flaque (ou des fissures) de la taille de la zone touchée, qui s'efface en 1,5 s.
  - Corps à corps : recharge de 1,1 à 2 s (doublée le 17/10/2026), lancement de 0,15 s (plus pour Morsure, Crocs Givre, Roulade, Ampleur). Chaque coup étourdit la cible 0,4 s (0,7 s pour Ampleur ; boss insensibles) et lui fait jouer son animation de dégâts, comme un recul. Formes : Griffe, Crocs Givre, Coupe Psycho, Taillade (dégâts croissants en enchaînant), Dracogriffe (en X), Morsure en arc ; Fouet Lianes, Dard-Venin, Picpic en estoc ; Mach Punch en 3 coups ; Ampleur autour de soi (rayon au hasard, immobilise le lanceur 0,25 s après) ; Roue de Feu, Aqua-Jet en ruée ; Étincelle ruée + décharge ; Roulade bond puis ruée, les deux Pokémon repoussés ; Ombre Portée disparaît 0,3 s puis réapparaît plus loin en frappant ; Pisto-Poing ruée puis coup de poing qui repousse.
  - Ennemis : une attaque de base du style de leurs statistiques plus une attaque de leur liste ; recharge × 2,5 et dégâts × 1,5 sur l'attaque de base (`meta.json`).
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
- Les Pokémon évoluent au niveau réel du jeu d'origine. On débloque toujours la première forme d'une lignée, qui évolue pendant la partie. Les évolutions sans niveau (pierre, échange, bonheur) restent à définir.
- Vitesse : reprise des jeux principaux (absente de la ROM), elle détermine la vitesse de déplacement.

### Structure d'une partie
- 2 arènes puis 1 boss, répété 3 fois, puis un boss final (modèle Wizard of Legend). Soit 6 arènes et 4 boss.
- Carte : grande arène carrée, murs au bord, quelques obstacles.
- Une arène = plusieurs vagues d'ennemis (à confirmer).
- Boutiques possibles entre les niveaux, avec un système d'objets (à préciser).

### Types et environnement
- La table des types est conservée (avantages et faiblesses).
- L'environnement réagit au type ou à la nature du Pokémon : les Pokémon volants survolent les trous, les Pokémon aquatiques vont dans l'eau.

### Boss
- Chaque boss est un Pokémon légendaire (Groudon en premier), avec 4 attaques toujours les mêmes, pour que le joueur apprenne ses schémas.
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
- Ennemis en groupe (15/10/2026) : chaque vague est répartie en groupes de 3 (le reste forme un petit groupe), qui apparaissent ensemble. Chaque groupe a forcément un soutien ; les deux autres sont corps à corps ou distance au hasard (deux du même rôle possible). Les attaques sont tirées pour donner ce rôle quand la liste du Pokémon le permet. Si aucun membre ne peut être soutien, l'un est remplacé par un Pokémon de l'étape (sinon de la partie) qui le peut. Le groupe vise le membre de l'équipe le plus proche de son centre, réévalué toutes les 2 s ou dès qu'il tombe.
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
9. Lisibilité du type d'un ennemi à l'écran.

### Progression
10. Évolutions sans niveau (pierre, échange, bonheur) : pas encore décidé (Owen, 15/10/2026). Touche aussi les bébés : la ROM range Pikachu sous Pichu, qui évolue par bonheur.
11. Talents : un par Pokémon, passif permanent ou à déclencher ?
12. Recrutement des Pokémon à taux nul (légendaires, boss).
13. Rythme de l'expérience : régulier, ou rapide au début puis plus lent ?

### Contenu
14. Terrains spéciaux (trous, eau, lave) : dans la première version ou plus tard ? Les spectres traversent-ils les murs ?
15. Boss : lesquels, et avec quels comportements spécifiques ?
16. Objets et boutiques : quels objets, quels effets ? Piste d'Owen (15/10/2026) : objets trouvés au hasard ou chez des marchands, qui donnent des effets passifs aux Pokémon.
