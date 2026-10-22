# Prompt Claude Design : recherche de direction artistique pour l'île (étape 1)

Je cherche la direction artistique des décors d'un roguelike d'action en temps réel vu de dessus, dont les
personnages sont des Pokémon en pixel art (sprites joints : `pokemon_dm_echelle_x4.png`, agrandis ×4 ; à l'échelle
réelle, un Pokémon fait 17 à 25 px de haut). Le jeu se passe sur une **île flottante** au-dessus des nuages. Je veux
m'éloigner du style des décors de Pokémon Donjon Mystère (tuiles carrées, aplats, bords en escalier) pour un monde
**féerique, organique et plein de profondeur**.

Avant de produire les assets, je veux **3 images de concept** du même coin d'île, dans 3 variantes de style, pour
choisir une direction.

## Références (à interpréter, pas à copier)
- *Eastward* : pixel art très riche et détaillé, lumière chaude, végétation dense, profondeur par superposition de plans.
- *Minecraft Dungeons* : vue plongeante de 3/4, formes de terrain franches et lisibles, éclairage marqué (ombres
  portées, lumière d'ambiance colorée), bords de terrain qui tombent dans le vide.
- *Pokémon Ranger* (DS) : nature Pokémon lumineuse et accueillante, couleurs fraîches, lisibilité en action.

## Contraintes communes
- **Vue de 3/4 dessus** (pas d'isométrique) : on voit le sol de dessus et la face avant des éléments hauts (troncs,
  rochers, falaises). La lumière vient du haut à gauche.
- **Pixel art net à l'échelle 1** : 24 px = 1 case de terrain (un Pokémon tient dans environ une case). Pas
  d'anticrénelage ni de flou, pas de pixels de tailles différentes. Palette riche autorisée (plus de 6 couleurs),
  mais cohérente.
- **Organique** : aucune grille visible. Bords de chemins, de berges et de prairies irréguliers et doux ; éléments
  posés librement.
- **Profondeur** : grands arbres de 3 à 5 cases de haut dont le feuillage passe devant ce qui est derrière, ombres
  portées au sol, léger dénivelé (un talus ou une petite falaise de 1 à 2 cases), et surtout le **bord de l'île** :
  on voit la roche du dessous de l'île qui plonge, des racines qui pendent, et des **nuages en contrebas** (plusieurs
  couches, plus claires et plus floues au loin, par aplats de pixel art).
- **Lisibilité** : les Pokémon doivent rester bien visibles sur le sol (sol plus calme et moins contrasté que les
  personnages ; détails denses réservés aux bords et aux éléments de décor).

## La scène (identique dans les 3 variantes)
Image de **480×270 px** à l'échelle 1 (20 × 11 cases), livrée aussi agrandie ×3 (1440×810, au plus proche voisin).
- Une prairie d'herbe avec des touffes plus hautes et des **fleurs** de 3 ou 4 sortes, en massifs naturels.
- Un petit bois de **2 ou 3 grands arbres** (tronc épais, feuillage généreux) sur un côté.
- Un **cours d'eau** qui traverse la scène en serpentant, avec berges de terre et de galets, quelques nénuphars ou
  roseaux, de l'écume claire le long des bords et des reflets.
- Un **petit pont de bois** au-dessus du cours d'eau, et un chemin de terre qui y mène.
- Dans un coin, le **bord de l'île** qui tombe dans le vide, avec les nuages en contrebas.
- Les **deux Pokémon joints** posés dans la scène à l'échelle 1 (sans les modifier), l'un sur la prairie, l'autre
  près du pont, avec leur ombre.
- Ambiance **féerique** : lumière de fin de matinée dorée, quelques particules lumineuses ou pollens en suspension,
  une ou deux lucioles ou papillons.

## Les 3 variantes
| Variante | Fichier | Description |
|---|---|---|
| A | `concept_a_sans_contour.png` | Façon *Eastward* : aucun contour noir, formes définies par la lumière et les ombres, détail fin et textures riches. |
| B | `concept_b_contours_colores.png` | Façon *Pokémon Ranger* : contours colorés (plus sombres que la couleur qu'ils entourent, jamais noirs purs), couleurs fraîches et franches, un peu moins de détail. |
| C | `concept_c_formes_franches.png` | Façon *Minecraft Dungeons* : grandes formes simples et lisibles, éclairage très marqué (ombres portées longues, lumière colorée), contrastes forts entre plans. |

Pour chaque variante, ajoute sous l'image (ou dans un court texte) la palette utilisée et 2 ou 3 phrases sur ce qui
la distingue. Livre une archive `.zip` avec les 3 images à l'échelle 1, leurs versions ×3 et ce texte.
