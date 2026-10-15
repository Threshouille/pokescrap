# Pokescrap — Feuille de route

État : validée avec Owen le 13/10/2026. Les décisions de conception sont dans `GDD.md`.

Décidé (13/10/2026) : squelette d'abord. Les jalons 1 à 4 sont faits en version minimale, dans l'ordre, pour boucler une partie complète ; on approfondit ensuite.

## Déjà fait (prototype de combat)

Arène, équipe de 3 (1 joueur + 2 IA), changement de Pokémon, visée, 3 familles d'attaques (projectile, mêlée, zone), temps de recharge, ennemis au corps à corps et à distance avec attaques annoncées, poison, K.O. et fin de partie, effets visuels, IA qui contourne murs et nuages.

## Jalon 0 — Sécuriser le projet (fait)

- Projet sous Git en local, sans dépôt distant. Exclus : `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Rom/`, les `.nds` et `.zip`, `Tools/.venv/`.
- Les sprites, décors et données tirés de la ROM qui sont dans `Assets/` sont versionnés : ne jamais pousser ce dépôt vers un hébergement public.

## Jalon 1 — Une arène avec un début et une fin

Version minimale (faite le 13/10/2026) :
- Vagues d'ennemis et condition de victoire de l'arène : 3 vagues réglables sur l'objet `Enemies`, 2 s de pause entre deux vagues, message de victoire et relance.

Reporté à l'approfondissement :
- Formule de dégâts : la défense est prise en compte depuis le 13/10/2026 ; reste la table des types.
- Alliés qui esquivent les attaques annoncées.
- Équilibrage : PV, dégâts, recharges, nombre d'ennemis.
- Interface : PV de l'équipe et recharges faits ; reste le type des ennemis.
- Retours de jeu : chiffres de dégâts, animation de dégât, sons de base.

## Jalon 2 — Progression en partie

Version minimale (faite le 13/10/2026) :
- Expérience commune et niveaux, avec les gains de statistiques par niveau tirés de la ROM.
- Choix d'1 amélioration parmi 3, par Pokémon, tous les 10 niveaux, dans un catalogue de 8 améliorations (5 de statistiques, 3 par famille d'attaques). Jeu en pause pendant les 3 choix.

À reprendre à l'approfondissement :
- Rythme de l'expérience : la formule actuelle (20 + 4 × niveau par niveau) est provisoire, sans lien avec l'objectif « niveau 100 en 30 minutes ».
- Niveaux des ennemis : fixés par étape (1, 5, 10) depuis le jalon 3, sans lien avec le niveau réel de l'équipe.

Reporté à l'approfondissement :
- Catalogue étoffé, évolutions d'attaques (chaînes à concevoir).
- Évolution des Pokémon au niveau réel.

## Jalon 3 — Une partie complète sur un cycle

Version minimale (faite le 13/10/2026) :
- Enchaînement Arène 1 → Arène 2 → Boss dans la même partie, avec fondu, nouveau décor et nouvelle disposition à chaque étape.
- Entre deux étapes : K.O. réanimés à 50 %, 30 % de PV rendus aux autres, effets effacés.
- Ennemis de niveau 1, 5 puis 10 selon l'étape.
- Boss : Groudon, renforcé (PV × 8, agrandi, barre de vie en haut de l'écran), avec une escorte en deux vagues.
- Écrans de victoire et de défaite avec temps, niveau et ennemis battus.

À faire ensuite (décidé par Owen le 13/10/2026) : les boss sont des légendaires, chacun avec 4 attaques fixes pour que le joueur apprenne ses schémas. Groudon a ses 4 attaques et ses annonces par bulle numérotée (fait le 13/10/2026).

Reporté à l'approfondissement :
- Comportements propres du boss.
- Monnaie de partie, boutique, premiers objets.

## Jalon 4 — Entre les parties

Version minimale (faite le 15/10/2026) :
- Menu : choix des 3 Pokémon parmi ceux débloqués.
- Recrutement des Pokémon battus (pourcentage de chance).
- Sauvegarde des Pokémon débloqués.
- Fait : scène `Menu`, recrutement au taux de la ROM (première forme jouable de la lignée), sauvegarde JSON, écran de fin « rejouer / menu » avec les recrues.

À reprendre à l'approfondissement :
- Taux : environ 8 % par K.O., tout se débloque en une ou deux parties. Piste : une seule recrue possible par arène.
- Ennemis recrutés jouables sans équilibrage.
- Fait depuis (15/10/2026) : choix des 2 attaques de départ au menu.

Reporté à l'approfondissement :
- Monnaie persistante, achat d'attaques, objets à effets passifs (trouvés au hasard ou chez des marchands, piste d'Owen).

## Jalon 5 — Tous les Pokémon, joueurs comme ennemis

Objectif décidé par Owen : à terme, les 558 Pokémon de la ROM sont jouables et peuvent tous apparaître comme ennemis.

- Import automatique des sprites de tous les Pokémon (aujourd'hui copiés à la main, un par un).
- Fait (15/10/2026) : génération automatique des attaques apprenables par niveau (`generate_moves.py`, ROM + PokéAPI : 343 générées, 108 écartées) ; les attaques faites à la main gardent la priorité.
- Fait (15/10/2026) : attaques de départ choisies dans la liste par niveau de la forme de base ; nouvelles attaques proposées en partie ; ennemis à 2 attaques tirées au hasard ; rôle d'IA déduit des attaques.
- Fait (15/10/2026) : statuts brûlure, sommeil, gel, confusion, piège, bâillement, vampigraine ; recul ; bonus au lanceur.
- Fait (15/10/2026) : évolution en partie au niveau de la ROM (évolutions par niveau, si les sprites existent).
- **Suivant (décidé par Owen) : reprise des attaques au cas par cas** pour leur donner un aspect unique, en commençant par les 108 écartées et les effets visuels.
- Reste : évolutions sans niveau (non décidé), nouvelles familles (mur…), talents.
- Terrains spéciaux (eau, lave, trous) et types de déplacement.
- Plus de décors, les 3 cycles et le boss final.

Conséquence dès maintenant : ne rien écrire qui suppose une liste fermée de Pokémon ou d'attaques.

## Jalon 6 — Coopération et finition (décidé : après tout le reste)

- Coopération locale à 2 ou 3 manettes.
- Musique et sons complets.
- Options, équilibrage final.

## Décisions d'ordonnancement (13/10/2026)

1. Squelette d'abord : jalons 1 à 4 en version minimale, puis approfondissement.
2. Coopération : plus tard (jalon 6).
3. Son : plus tard (jalon 6).
4. Périmètre final : tous les Pokémon jouables, et tous utilisables comme ennemis (jalon 5). Le squelette se fait avec le petit effectif actuel.
