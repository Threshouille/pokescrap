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
- 4 emplacements par Pokémon. On commence la partie avec 3 ou 4 attaques choisies avant la partie ; on ne suit pas les niveaux d'apprentissage du jeu d'origine.
- Certaines attaques évoluent en cours de partie, lors des choix d'amélioration (ex. : Écume → Pistolet à O → Hydrocanon). L'évolution d'attaque est tirée au hasard comme les autres propositions, mais avec une fréquence plus élevée. Les chaînes sont à concevoir, elles n'existent pas dans les données.
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
- Tous les 5 ou 10 niveaux : 1 choix parmi 3 améliorations aléatoires par Pokémon (statistique, modification d'une famille d'attaques comme +50 % de taille de projectile, évolution d'une attaque). Les 3 Pokémon montant de niveau ensemble, une seule coupure sert à faire les 3 choix.
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

### Entre les parties
- Recrutement : un Pokémon battu a un pourcentage de chance de rejoindre le joueur (comme dans le jeu d'origine).
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
- Les ennemis visent le membre de l'équipe le plus proche.
- Déplacement des IA (alliés et ennemis) : en ligne droite si la voie est libre, sinon recherche de chemin sur la grille de l'arène pour contourner les murs. Les zones nocives pour leur équipe (nuage de gaz) coûtent cher à traverser, donc elles sont contournées ; une IA qui se trouve dedans en sort avant toute autre action. Une IA n'attaque que si elle a une ligne dégagée vers sa cible (sauf attaques de zone), et ne fonce pas dans un nuage.
- Limite : les IA évitent les zones déjà posées, pas les attaques encore en cours d'annonce (bande ou disque rouge).
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
- Jouables (6) : Bulbizarre (soutien : Vampigraine, Rugissement, Poudre Toxik, Tranch'Herbe), Salamèche (distance), Carapuce (distance), Pikachu (distance : Éclair, Cage-Éclair, Vive-Attaque, Rugissement), Goinfrex (corps à corps : Charge, Léchouille, Boul'Armure), Riolu (corps à corps : Vive-Attaque, Forte-Paume). Équipe de 3 tirée au hasard à chaque partie, en attendant le menu.
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
10. Évolutions sans niveau (pierre, échange, bonheur).
11. Talents : un par Pokémon, passif permanent ou à déclencher ?
12. Recrutement des Pokémon à taux nul (légendaires, boss).
13. Rythme de l'expérience : régulier, ou rapide au début puis plus lent ?

### Contenu
14. Terrains spéciaux (trous, eau, lave) : dans la première version ou plus tard ? Les spectres traversent-ils les murs ?
15. Boss : lesquels, et avec quels comportements spécifiques ?
16. Objets et boutiques : quels objets, quels effets ?
17. Liste des chaînes d'évolution d'attaques et des familles pour la première version.
