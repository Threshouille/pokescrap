# Pokescrap — Passation de session

Dernière mise à jour : 26/10/2026 (Robin : socle technique fusionné dans `feature/engrenages` ; peuplement, engrenages et boss de quartier ; voir le README). À lire en début de session avec `CLAUDE.md` (détails
techniques), `Docs/GDD.md` (décisions de conception) et `Docs/ROADMAP.md` (jalons). Bilan complet du 25/10 : avancement,
branches, axes de refonte et questions de conception (page partagée par Robin).

## Talents (29/10/2026, branche `feature/talents`)

Catalogue `abilities_rt.json`, talents ROM + caché PokéAPI par Pokémon, talent des mégas, rapport `Docs/TALENTS.md`.
Lots 1 à 3 des effets (155 talents, tous ceux des Pokémon hors passifs des mégas ; `ability_effects.json`, `Abilities.cs`, `Combatant.Ability.cs`). Suite : test
en jeu ci-dessous, réglage des valeurs, passifs des mégas ; ensuite (Robin) : adapter les IA aux talents (les leurs
et ceux des adversaires : viser autrement, rester sur un terrain absorbé, éviter Statik au corps à corps…).

Test en jeu prévu (éditeur, profil de dev) :
0. Démo de tous les talents : terrain de test, escalier rose (en bas, à droite de l'orange) ; regarder chaque talent
   (←/→, ↑/↓ par famille) et noter ceux dont la démo ne montre rien de clair.
1. Menu : un perso de dev, R2 / Tab ouvre la liste des talents (filtre par famille, description) ; un Pokémon normal
   montre ses talents et son caché. En partie, J ouvre la même liste (Maj+J : le suivant).
2. Partie normale : le nom du talent tiré s'affiche dans le HUD après le niveau ; il reste après une évolution ; une
   méga prend celui de la méga puis rend l'ancien.
3. Terrain de test (Select / T) avec un perso de dev, J pour choisir un talent :
   cibles d'entraînement de la salle 0 pour Sniper (dégâts plus forts de loin), Chanceux (tirs plus gros),
   Technicien, Brasier sous 1/3 des PV ; Statik / Peau Dure en frappant au corps à corps une cible avec le talent
   (élite de la salle 7 : J ne change que le Pokémon joué, donc tester le talent de l'élite par son tirage).
4. Météo : Sécheresse / Crachin au début d'une arène ; Glissade, Chlorophylle (vitesse), Voile Sable (pas de dégâts).
5. Intimidation : bandeau et icône de malus d'attaque sur les ennemis qui approchent.
6. Paratonnerre / Lavabo : les tirs Électrik / Eau des ennemis sont attirés et absorbés.
7. Marque Ombre / Piège Sable : esquive refusée près du porteur ; Ventouse : pas de recul.
8. Ramassage : objets qui arrivent dans le sac au fil des K.O.
9. À noter : bandeaux trop fréquents ou pas assez, valeurs trop fortes ou trop faibles.
Banc complet le 29/10 (lot 3) : 78/81 ; échecs antérieurs aux talents (toujours là avec les talents coupés), à corriger à
part : « capacité Z frappe et paralyse » (16 et 18 dégâts pour un seuil de 17,5) et « Secours : le plus apte éloigne
l'ennemi » (allié K.O. jamais réanimé). « Groupe : pas d'à-coups » a échoué une fois sur trois (instable) ; « Peuplement : camps et rondes » aussi (2 échecs sur 4 :
lieux tirés au hasard, camp parfois mal peuplé ou pas réveillé ; talents coupés pendant le banc, donc sans lien).

## Reprise (26/10/2026, Robin) : où on s'est arrêté

Branche `feature/engrenages` (contient `feature/peuplement` et `refacto/socle`), **pas poussée**, pas fusionnée dans
`dev`. Accès à l'îlot du boss terminé et validé en jeu : donjon de la clé sur le point d'intérêt, entrée = grotte
du sommet (y entrer mène au donjon), sinon faille dimensionnelle de la ROM (`rift`, animée), sur une case libre, accessible à pied et loin des autres donjons ; pont de
corde vertical sur la côte arrondie, pied praticable par tous ; portail de lumière animé (scintille terne sans la clé,
s'ouvre la première fois qu'on approche avec, puis tourbillon) ; arène et salles gardées telles quelles au retour ;
plus de bulles numérotées pour les boss d'îlot. La clé reste la Clé de Donjon Mystère (`pmd_Key_White`).

À faire ensuite :
1. Banc complet (pas lancé depuis la fusion de `refacto/socle`) avant toute fusion dans dev ; bilan de la tâche.
2. Boss plus dynamiques (ruée, renforts à mi-PV), passe d'équilibrage générale, puis l'aspect graphique.
3. Plus tard : salle rouge du terrain de test qui tire le boss au hasard à l'entrée (N reste pour choisir) ; quêtes
   du Tableau des missions ; intégrer les branches d'Owen avec lui ; outils de dev dans un assembly à part ; boutique
   Kecleon.

## Méthode de travail

- Owen et Robin écrivent en français ; réponses courtes, économie de tokens (voir `CLAUDE.md`).
- Avant toute nouvelle tâche, présenter un plan et attendre l'accord (valeurs, comportements, ce qui est provisoire,
  questions ouvertes). Signaler tout écart au plan validé. Donner un avis argumenté, pas seulement exécuter.
- Une branche par tâche **depuis `dev`** (`feature/…`, `fix/…`, `refacto/…`), poussée sur `origin`, fusionnée dans `dev`
  une fois testée en jeu et le banc vert. `main` n'est plus mis à jour depuis le 19/10 (à rattraper depuis `dev`).
- Commits non signés, seulement avec l'accord de la personne ; jamais de force push ; ne pas toucher aux branches de
  l'autre sans lui. Dépôt public : rien de la ROM (vérification avant chaque push, voir `CLAUDE.md`).
- Après chaque tâche : tests (EditMode et banc), mise à jour de GDD / ROADMAP / CLAUDE.md / README, commit.
- Effets visuels : prompt pour Claude Design (pixel art strict, 24 px = 1 case, une ligne d'images orientée vers la
  droite, `effects.json`) ; archive déposée à la racine, extraite dans `Design/effects_vN/`, contrôlée, fusionnée dans
  `Assets/Resources/Effects/`. Effets Z et Téra : ressources existantes en ligne plutôt que Claude Design.

## Où en est le projet (25/10/2026)

| Domaine | État |
|---|---|
| Combat (familles d'attaques, types, statuts, esquive, visée) | solide |
| Environnement (10 terrains, 20 obstacles, pylônes, 4 météos, 2 sports) | solide |
| IA (rôles, cible par score, dangers, réparation de chemin, secours, soins sûrs) | solide |
| Rencontres (lots, rangs, combinaisons, élites, seconde phase à mi-PV) | fait, à régler en vraie partie |
| Objets (méga, Z, Téra ; sac partagé ; objets tenus à tiers ; butin ; interface lisible) | fait |
| Progression en partie (expérience, améliorations, attaques, évolutions) | provisoire |
| Structure de partie | provisoire sur `dev` (Arène 1 → Arène 2 → Boss) ; île, salles et niveaux continus sur les branches d'Owen |
| Entre les parties (menu, recrutement, sauvegarde) | minimal ; ni monnaie ni boutique |
| Son, coopération | pas commencés (jalon 6) |
| Équilibrage | sans cible chiffrée |

## Branches pas encore dans `dev` (25/10/2026)

Toutes ont des conflits avec `dev` (fusion à blanc par `git merge-tree`) ; intégration prévue avec Owen, dans l'ordre :

## Fusion de dev dans `feature/quart-ile` (24/10/2026, Robin)

dev (objets spéciaux, ultimes ennemis, secours et soins) fusionné dans la branche d'Owen ; salles de dev renumérotées
6 à 9 dans le terrain de test (démo Z, élite, boss, secours). Corrigé : sur le quart d'île, les alliés ne refaisaient
pas un pont (chemin partiel à la limite de l'A* = cible « atteignable ») ; un allié qui ne se rapproche plus cherche
maintenant une réparation. Banc en batch : 39/39. « Hauteur » échouait aussi sur la version poussée par Owen
(`e5de12e`) : ennemi à 7 cases, la portée exacte de Puis. Cachée, donc hors de portée au moindre décalage ; ramené à
5 cases et une attaque typée.

## Session en cours : composition du quart d'île (branche `feature/quart-ile`, 23-24/10/2026)

Branche partie de `feature/salles` (Robin : salles, île flottante ; Owen : zones séparées), **ni l'une ni l'autre
fusionnées dans `dev`**. But : composer un quartier de l'île principale avec les assets de DM. Salle de test : escalier
turquoise du terrain de test (salle 5) ; R / Select : nouveau tirage. Code : `Assets/Scripts/Dungeon/IslandQuarterTest.cs`
(génération), `IslandPlan.cs` (données par case), `DungeonGenerator.GenerateIsland` / `Draw` (rendu), `IslandRun.cs`
(décor, marqueurs, mini-carte) ; réglages `Assets/Resources/Data/quarter_test.json`. Détails techniques dans `CLAUDE.md`
(paragraphes « Quart d'île de test », « Cartes de l'histoire », « Éléments de décor »).

Fait et validé par Owen : quart en éventail sans murs, rivière large et sinueuse (pointe = centre de l'île → côte),
jungle (décor 44) et prairie fleurie (décor 5) de part et d'autre, 3 ponts ; route principale (terre du décor 35 +
pierres clairsemées du décor 30) entre deux portails (marqueurs inertes) par le pont du milieu, colonnes brisées
(obstacle `column`, rocher du décor 46) ; relief vu de 3/4 : paroi sud de 2 cases par palier, rebord fin ailleurs,
escaliers de 3 colonnes, montagne en lacets, rebords à sens unique (validés), grotte au sommet sous un toit qui
s'efface (validée, sol du décor 9).

Repris et validé par Owen le 24/10 (2e session) :
1. **Falaises** : plateaux aux contours droits (rectangles sur une grille de 3 cases, encoches bouchées) ; morceaux
   refaits dans `cliff_styles.json` : prairie = rebord de la carte 224 + bas de paroi pris dans la carte 226 (zone orange
   sans pli rose : les plis sont des angles, les enchaîner donnait « une suite de coins »), roche = carte 23 (paroi
   régulière, entrée de grotte) au lieu de la 26 (gros blocs qui ne se découpent pas) ; colonnes posées dans l'ordre puis
   en miroir (sans couture) ; sol de la carte effacé en haut des morceaux, bas de paroi en vague, ombre portée au pied
   (remplace les cases assombries `footShadow`) ; dos, côtés et coins fabriqués à partir de la bande du rebord, extrémités
   de paroi avec rebord et coin du bas arrondi seulement contre un terrain plus bas ; plus de bordure du décor autour des
   falaises ; escaliers sans bande de terre ; toit de la grotte jusqu'à la roche.
2. **Collisions et hauteur** : collisions à l'épaisseur dessinée des rebords ; les tirs d'un palier passent les
   falaises de ce palier ou plus basses, s'écrasent sur les plus hautes ; IA idem (`ShotClear`) ; scénario de banc
   « Hauteur : … » (passe).
3. **Décor** : cases sombres sous le décor corrigées (`blocker` caché n'est plus peint) ; retirés : buissons 877, 879,
   905, haie `grass_909` ; décor épars réduit (fleurs, buissons, rochers) ; taches par sous-biome : champs de fleurs,
   fougeraies, éboulis, forêts en massif plein avec sentiers et clairière (`IslandPlan.clearings` pour le level design).

Escalier secret (25/10, validé par Owen en jeu) : caché dans une clairière ou sous un rocher fissuré, salle vide du
catalogue et retour au même endroit ; le contenu des salles (trésor, embuscade, gardien) et les thèmes logiques
(population des Pokémon par biome) sont pour Robin (GDD « Escaliers secrets »).

Reste à faire :
- Arbres transparents quand un Pokémon passe derrière (on marche sous les couronnes en lisière) ; plus de variété
  d'arbres (couche du dessous des cartes, à détourer).
- La roche n'a pas d'escaliers à elle (elle emprunte ceux de la prairie, roses) : en chercher dans la carte 24.
- Banc d'essai lancé dans l'éditeur (24/10) : 23/25, les deux échecs sont des pires images à ~100 ms (« Niveau : pas de
  gel… », « Île : … ») alors que l'éditeur tournait sans le focus ; à relancer en batch (`setup.py --ai-scenarios`,
  Unity fermé) pour confirmer que ce n'est pas une régression.
- Les IA ne cherchent pas encore les hauteurs ; idées de level design autour des clairières.
- Attention : un rendu de contrôle par `execute_code` dans l'éditeur laisse des objets dans la scène ouverte (`Obstacles`,
  « Tuiles d'obstacles » à la racine) qui reviennent en jeu : recharger `SampleScene` sans enregistrer après coup, et
  jamais pendant que Owen est en Play.

Abandonné : la refonte visuelle organique (branche `feature/refonte-visuelle`, non fusionnée, gardée pour mémoire :
shader de sol, vitrine, assets Claude Design) — Owen préfère les assets de DM en cases carrées.

## Prochaine session : attaques au cas par cas

Fait (15-17/10/2026) : les 34 **attaques de base** (17 à distance, 17 au corps à corps), réglées une par une avec Owen (détail dans le GDD, paramètres dans `CLAUDE.md`), avec leurs planches Claude Design (`Design/effects_v6_attaques_de_base/`, `Design/effects_v7_attaques_de_base_cac/`, prompts `Design/prompt_attaques_de_base*.md`) et les icônes des 9 statuts (`Design/effects_v5_statuts/`). Owen n'a pas encore tout testé à la manette. Ensuite : les 18 attaques de type (liste et lots dans le GDD) ; lot 1 fait le 19/10/2026 (Vibraqua, Lame d'Air, Plaie-Croix, Onde Folie, Telluriforce), planches intégrées ; lot 2 fait le 19/10/2026 (Fatal-Foudre, Blizzard, Draco Météor, Pouv. Antique), planches demandées (`Design/prompt_attaques_type_lot2.md`). Draco Météor baisse le modificateur « attack », qui touche aussi les attaques physiques. Lot 2 fusionné dans dev le 26/10/2026 ; lot 3 (Lance-Flamme, Ball'Ombre, Méga-Sangsue) fusionné le 26/10/2026 ; lot 4 (Plaquage, Frappe Atlas, Acidarmure, Gyroballe, Psyko, Coup Bas) en cours sur `feature/attaques-type-lot4`, planches demandées (`Design/prompt_attaques_type_lot4.md`). Points de départ :
- `Tools/pmd/generated_moves_report.txt` : 108 attaques écartées (75 de statut sans effet transposable comme Abri, Repos, Morphing, Métronome ; terrain, météo, K.O. en un coup, expulsion ; statuts non gérés comme Attraction, Requiem, Entrave).
- Les attaques génériques n'ont pas d'effet visuel propre (disque coloré du type) : prévoir des prompts Claude Design par famille ou par attaque.
- Une attaque se reprend à la main en l'ajoutant à `realtime_moves.json` (même `id`), qui l'emporte sur la version générée.
- Notes du générateur sur chaque attaque (`notes` : « plusieurs coups », « charge », « recharge », « une seule statistique gardée ») : pistes de reprise.
- Champs ignorés pour l'instant : chance de recul critique, coups multiples en mêlée (puissance ×2 à la place), bonus sur plusieurs statistiques.

## Effets de terrain (19/10/2026)

Faits et testés en Play par Robin : 7 terrains, 14 attaques, persos de dev, commandes clavier U I O P. Suite décidée : affiner la logique et le fonctionnement, puis les affinités entre terrains (table `reactions`). Tuiles animées des 8 terrains livrées (19/10/2026, `Design/effects_v11_terrains/`, prompt `Design/prompt_terrains.md`). Reste, reporté par Robin (19/10/2026) : planches des 14 attaques à trace qui n'ont qu'un disque coloré (Lance-Flamme, Déflagration, Hydrocanon, Hydroblast, Canon Graine, Mégafouet, Végé-Attak, Tonnerre, Fatal-Foudre, Laser Glace, Blizzard, Détritus, Bomb-Beurk, Boue-Bombe) ; prompt prêt : `Design/prompt_attaques_terrain.md`. Ces attaques sont générées : leur attribuer les planches via `move_rules.json` plutôt qu'en les recopiant dans `realtime_moves.json`.

## Méga-évolutions (20/10/2026, Robin) : en pause

Étape 1 faite (branche `feature/mega-evolutions`) : sprites de 27 mégas (SpriteCollab), galerie du terrain de test,
jauge commune, L3 + R3 / F, stats et types, onde de choc, retour au bout de 50 s ; testée en jeu par Robin, réservée
au `devMode`. Mise en pause par Robin pour passer aux IA. Fusionnée dans `dev` le 22/10/2026. Suite (GDD) : 2) méga-gemmes (sauvegarde, emplacement au menu
d'équipe, obtention en partie, sans doublon) ; 3) les 27 passifs. Plus tard : boss qui méga-évoluent.

## Correction des IA (branche `feature/ia`, fusionnée dans `dev` le 22/10/2026, Robin)

- Alliés qui suivaient le Pokémon joué jusque dans une zone nocive : ils visent la case sûre la plus proche de lui
  (`AllyAI.SafeSpotNear`) par un chemin qui contourne les dangers (`Navigator.Direction(..., avoid: true)`), et
  n'attendent au bord que s'il n'en existe aucun.
- Lot A (GDD « IA ») fait et testé : `TargetChoice` (cible par score, alliés et groupes ennemis),
  alliés qui suivent le Pokémon joué quand il recule, plus offensifs, prudents quand ils sont blessés. Suite : lot B
  (placement et combos selon les types), lot C (couper et réparer les chemins).
- Banc d'essai des IA (`AiScenarios`, touche B dans le terrain de test, `python3 Tools/setup.py --ai-scenarios`) :
  8/8 en batch le 20/10/2026. Il a révélé que l'allié restait coincé au coin d'une bande de danger (coin coupé en
  diagonale, ligne droite autorisée depuis le danger) : corrigé dans `Navigator` (mode `avoid`).
- Lot C commencé (20/10/2026) : réparer un chemin (geler, nénuphars, lianes, lave refroidie), brûler un pont sous un
  ennemi au corps à corps ; pylônes vus avec 60 % de chances par cycle ; métal ignoré par les IA. `Navigator` ne
  relance plus l'A* à chaque pas vers une cible inaccessible (problème connu, corrigé). Banc : 14 scénarios.

## Salles (branche `feature/salles`, 22/10/2026, Robin)

Étape 1 faite : catalogue `rooms.json` (types, thèmes, variantes, tailles), générateur (plans de murs, décors de la ROM,
liquide du décor), galerie des salles dans le terrain de test, `RoomCheck` (84 salles, 0 problème). Le point de départ
est encore le centre (spawnClear réduit à 2 dans les salles) : les portes le remplaceront à l'étape 2.
Retour de Robin (22/10/2026) : les obstacles des salles « ne marchaient pas ». Diagnostic (banc + captures `RoomShots`) :
la mécanique marchait, mais le liquide de certains décors trompait (eau violette, sol d'eau, glace = sol) et des salles
sortaient vides ; corrigé (aspect standard, décors écartés, nouvel essai). Banc : 22/22.
Étage façon Isaac fait puis remplacé (22/10/2026, demande de Robin) par des **niveaux continus** façon Minecraft
Dungeons : phase 1 faite (`LevelPlan`, `LevelRun`, zones naturelles, chemins sinueux et renfoncements, branches,
boucles, mini-carte avec brouillard ; l'escalier orange mène désormais à l'île) ; `LevelCheck` 20 niveaux sans problème,
exploration ≈ 2 à 4,5 min sans combat ; banc 22/22. Suite (GDD) : 2) sous-biomes (`biomes.json`, décor par case,
transitions sur frontières naturelles), 3) passages secrets, 4) rencontres. Puis brancher les niveaux sur la vraie partie.
**Nouvelle direction (22/10/2026)** : une run = une île flottante (GDD « L'île flottante ») ; les niveaux continus
serviront aux chaînes d'îlots. Étapes 1 et 2 faites (données seulement) : `IslandPlan`, `IslandFeatures`, `island.json`,
`biomes.json`, aperçu `IslandCheck`. Étape 3 faite : île jouable (escalier orange du terrain de test), décor par case,
haies aux changements de décor, ciel autour ; pire image 24 ms en courant avec les alliés (les pics de 108 ms venaient de
la réparation de chemin, rendue locale). Banc 24/24. Suite : 4) chaînes d'îlots, 5) sceaux et engrenages, 6) centre,
7) rencontres.
**Composition d'un quartier (23/10/2026, Owen, `feature/quart-ile`)** : salle de test d'un quart d'île « nature
luxuriante » (escalier turquoise) : grande zone sans murs, rivière large et sinueuse de la pointe (centre de l'île) à
la côte, jungle (décor 44) d'un côté, prairie fleurie (décor 5) de l'autre, 3 ponts. Point de départ pour composer les
quartiers de l'île principale.
**Zones séparées (22/10/2026, Owen)** : chaque quartier et le centre sont des cartes à part, murées, avec écran de
chargement aux passages (dans les deux sens ; dernier quartier → centre). `IslandCheck.Run` : 4 îles, zones toutes
accessibles depuis leur arrivée ; construction d'un quartier ≈ 0,9 s. Limite : une zone revisitée est reconstruite
telle qu'à l'origine (pont brûlé, rivière gelée non gardés).
Gels des grands niveaux corrigés par une session parallèle (pire image 167 → 31 ms ; banc 23/23), à confirmer en jeu.
Retour de Robin sur l'étage de test : plus d'obstacles (pylônes dans les labyrinthes…) et obstacles fondus dans le décor
(capture : vide standard bordé de bleu sur la Grotte Abîme aux murs noirs). Fait : liquide du décor par thème
(`liquidDecors`), rochers découpés dans chaque décor, décor 26 écarté, plus d'éléments par variante.
Décors de la ROM utiles, par donjon d'origine : lave 57/58 (Cratère Obscur), 110 (Caverne Brasier), 123 (Volcan Géant) ;
glace 88, 111, 137, 139, 140 ; eau 4, 39, 52, 96, 113, 126 ; forêt 5, 10, 35, 44, 128 ; caverne 1, 9, 22, 49, 103, 130 ;
désert 17, 19, 20, 85, 133 ; électrique 14 (Plaines Elek), 127 (Registeel) ; néant 26, 55 (Faille Spatiale, liquide
noir), 59, 99 ; ruines 28, 29, 30, 45/46 (Tour du Temps) ; labyrinthe 102 (Grotte Labyrinthe).

## Objets spéciaux (branche `feature/objets`, 23/10/2026, Robin)

Faits : `items.json`, collection et équipement dans la sauvegarde, encoches et écran des objets au menu, ultimes (méga
avec gemme, capacité Z, Téracristal) sur la jauge commune, butin (boss, coffre de fin d'étape, élites). Banc 23/23 (5
scénarios d'objets, 4 d'élites et d'ultimes ennemis). Ultimes ennemis (`EnemyUltimate`) et élites qui modifient le terrain : faits. À faire : tester en jeu ; icônes d'objets faites (`import_items.py`, PokeAPI + Nikostera) ; effets Z et Téra à chercher en ligne
plutôt que chez Claude Design (demande de Robin) ; vérifier les noms français des objets ; observé sur le banc : une
attaque de projectile touche un ennemi immobile du banc à de nombreuses reprises (1 035 dégâts pour ≈ 33 estimés par
coup), à vérifier en jeu.

## Branches (26/10/2026)

`feature/quart-ile` est fusionnée dans `dev` (Owen, 25/10). Pas encore dans `dev` : `feature/attaques-type-lot2`,
`feature/refonte-visuelle`, `feature/salles` (Owen), `feature/peuplement` et `feature/engrenages` (Robin, celle-ci
contient `refacto/socle`). Conflits récurrents : numéros des salles du terrain de test, liste des scénarios du banc,
`PlayerController`, journaux de modifications ; après l'intégration : registre des salles par nom, banc découpé par
domaine, outils de dev dans un assembly à part, puis découpage de `Combatant` et `Obstacles`.

## En attente

- Icônes de bonus et malus (`status_attack_up`… six fichiers) : flèches colorées provisoires.
- Planches des 14 attaques à trace qui n'ont qu'un disque coloré (prompt prêt : `Design/prompt_attaques_terrain.md`) ;
  les attribuer via `move_rules.json`.
- Attaques génériques sans effet visuel propre ; 108 attaques écartées (`Tools/pmd/generated_moves_report.txt`).
- Évolution manuelle : pas testés à la manette (Triangle), échange, Babimanta, Barpau, Debugant.
- Archives qui ne servent plus à la racine et dans `Design/` (Owen peut les supprimer) ; garder l'archive de la ROM.

## Problèmes connus et points à surveiller

- Corrigé le 22/10/2026 : gels sur les grands niveaux quand le Pokémon joué survolait l'eau, la lave ou le vide (Ptéra) :
  les alliés, sans chemin, lançaient l'A* jusqu'à sa limite puis la réparation de chemin (Dijkstra quadratique sur
  toute la carte, toutes les 0,5 s). Correctifs : cible de suivi praticable, chemin partiel à la limite de l'A*,
  réparation à tas binaire bornée, redessin incrémental des obstacles et du terrain. Sonde au banc (« Niveau : pas de
  gel… ») : pire image 167 ms → 31 ms sur un niveau 360×268. Reste à confirmer en jeu par Robin.

- Équilibrage entièrement provisoire : rythme d'expérience (20 + 4 × niveau) sans lien avec l'objectif « niveau 100 en
  30 min », soutien rare (il faut un soin ou un bonus aux alliés), équipe qui progresse vite.
- L'annonce des attaques au corps à corps ennemies reste un disque rouge, qui ne correspond pas aux arcs ni aux estocs.
- Mineurs : les zones ne déclenchent ni drain ni recul ; Attaque / Attaque Spé proposées même sans attaque de cette
  catégorie ; animation de marche pendant un lancement ou sous sommeil (IA).
- Jamais testés à la manette : navigation du menu, écran de remplacement d'attaque, Croix / Rond des écrans de fin.
- Observé sur le banc : une attaque de projectile touchait un ennemi immobile de nombreuses fois (à vérifier en jeu).
- Réglages de test : dans `meta_dev.json` (éditeur et builds de développement), plus dans `meta.json`.
- Unity s'est déjà bloqué pendant un rechargement de scripts (« The file 'MemoryStream' is corrupted ») : relancer.
  Après une recompilation, la liaison unityMCP peut manquer une dizaine de secondes : vérifier `Logs/Editor.log`.
- Version d'Unity : `ProjectSettings/ProjectVersion.txt` diffère selon les machines ; ne pas le committer.
- Machine d'Owen (Linux) : manette Battletron compatible PS4 en Bluetooth ; si Linux la refuse (« Rejected connection
  from !bonded device »), refaire l'appairage avec `bluetoothctl` jusqu'à « Bonded: yes ». Ne pas toucher à
  `ClassicBondedOnly` sans accord explicite.

## Comment je teste

- **Tests EditMode** (logique pure et données, quelques secondes) : `python3 Tools/setup.py --edit-tests` (Unity fermé ;
  sinon Window > General > Test Runner, EditMode). Fichiers dans `Assets/Editor/Tests/`.
- **Banc d'essai** (58 scénarios en mode Play, environ 10 min) : `python3 Tools/setup.py --ai-scenarios`, Unity fermé,
  lignes `AISCENARIO` dans `Logs/ai_scenarios.log` ; touche B dans le terrain de test.
- **Compilation rapide hors Unity** : reprendre le fichier `.rsp` d'Unity (`Library/Bee/artifacts/*/Assembly-CSharp.rsp`)
  et le passer au `csc` du SDK .NET livré avec Unity.
- **Terrain de test** (menu, Select / T en `devMode`) : salle 0 (émetteurs de terrain, présentoir des objets), 1
  obstacles et cibles, 2 galerie des mégas, 3 démo des capacités Z, 4 élite, 5 boss, 6 démos de secours, 20
  observation des rencontres (V / ○ : pilote automatique).
- Play lance la scène `Menu` ; `Run.Start(new[]{4,1,25})` lance une partie avec cette équipe, `Run.ToMenu()` revient au
  menu. Écran d'amélioration : `Progression.AddXp(5000)`. Statuts : `combatant.AddStatus("sleep", 3f)`.
- Sauvegarde : `save.json` dans `Application.persistentDataPath` (macOS `~/Library/Application Support/DefaultCompany/
  Pokescrap/`, Linux `~/.config/unity3d/DefaultCompany/Pokescrap/`, Windows `%USERPROFILE%\AppData\LocalLow\
  DefaultCompany\Pokescrap\`) ; menu **Pokescrap > Effacer la sauvegarde**.
- Mesurer une mécanique : figer les autres acteurs (désactiver `AllyAI`, `EnemyAI`, `BossAI`). Le temps est figé
  pendant l'écran d'amélioration (`Time.timeScale = 0`). La fenêtre Unity en arrière-plan est lente.
