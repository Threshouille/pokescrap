# Pokescrap — Feuille de route

État : en cours de validation avec Owen. Les décisions de conception sont dans `GDD.md`.

Décidé (13/10/2026) : squelette d'abord. Les jalons 1 à 4 sont faits en version minimale, dans l'ordre, pour boucler une partie complète ; on approfondit ensuite.

## Déjà fait (prototype de combat)

Arène, équipe de 3 (1 joueur + 2 IA), changement de Pokémon, visée, 3 familles d'attaques (projectile, mêlée, zone), temps de recharge, ennemis au corps à corps et à distance avec attaques annoncées, poison, K.O. et fin de partie, effets visuels, IA qui contourne murs et nuages.

## Jalon 0 — Sécuriser le projet (fait)

- Projet sous Git en local, sans dépôt distant. Exclus : `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Rom/`, les `.nds` et `.zip`, `Tools/.venv/`.
- Les sprites, décors et données tirés de la ROM qui sont dans `Assets/` sont versionnés : ne jamais pousser ce dépôt vers un hébergement public.

## Jalon 1 — Une arène avec un début et une fin

Version minimale :
- Vagues d'ennemis et condition de victoire de l'arène.

Reporté à l'approfondissement :
- Formule de dégâts définitive : défense, table des types.
- Alliés qui esquivent les attaques annoncées.
- Équilibrage : PV, dégâts, recharges, nombre d'ennemis.
- Interface lisible : PV de l'équipe, recharges des 4 attaques, type des ennemis.
- Retours de jeu : chiffres de dégâts, animation de dégât, sons de base.

## Jalon 2 — Progression en partie

Version minimale :
- Expérience commune et niveaux (extraire les courbes de statistiques de la ROM).
- Choix d'1 amélioration parmi 3, par Pokémon, tous les 10 niveaux, avec un petit catalogue (statistiques, un ou deux modificateurs d'attaque).

Reporté à l'approfondissement :
- Catalogue étoffé, évolutions d'attaques (chaînes à concevoir).
- Évolution des Pokémon au niveau réel.

## Jalon 3 — Une partie complète sur un cycle

Version minimale :
- Enchaînement de 2 arènes puis 1 boss, réanimation entre deux arènes.
- Boss : un ennemi renforcé, sans comportement propre pour commencer.
- Écrans de victoire et de défaite.

Reporté à l'approfondissement :
- Comportements propres du boss.
- Monnaie de partie, boutique, premiers objets.

## Jalon 4 — Entre les parties

Version minimale :
- Menu : choix des 3 Pokémon parmi ceux débloqués.
- Recrutement des Pokémon battus (pourcentage de chance).
- Sauvegarde des Pokémon débloqués.

Reporté à l'approfondissement :
- Choix des attaques de départ, monnaie persistante, achat d'attaques, objet passif.

## Jalon 5 — Contenu et variété

- Nouvelles familles d'attaques (mur, ruée, amélioration temporaire, soin) et attaques de statut ; Pokémon de soutien.
- Autres statuts (brûlure, paralysie, sommeil…).
- Talents.
- Terrains spéciaux (eau, lave, trous) et types de déplacement.
- Plus de Pokémon jouables, d'ennemis, de décors.
- Les 3 cycles et le boss final.

## Jalon 6 — Coopération et finition

- Coopération locale à 2 ou 3 manettes.
- Musique et sons complets.
- Options, équilibrage final.

## Questions à trancher pour ordonner la suite

1. (Tranché : squelette d'abord.)
2. Coopération : tôt (elle touche à l'architecture des contrôles) ou en fin de projet ?
3. Son : dès le jalon 1 ou plus tard ?
4. Taille visée pour la première version « complète » : combien de Pokémon jouables et d'ennemis ?
