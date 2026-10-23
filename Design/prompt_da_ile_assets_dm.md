# Prompt Claude Design : assets de l'île, style Pokémon Donjon Mystère (étape 2, deuxième essai)

Je refais les assets de mon île, mais cette fois **dans le style des décors de *Pokémon Donjon Mystère : Explorateurs
du Ciel* (DS)**, le plus fidèlement possible : mêmes couleurs, même façon de dessiner les pixels. Le premier essai
(style *Eastward*) ne collait pas avec les Pokémon du jeu, qui sont les sprites de Donjon Mystère.

## Pièces jointes
- `dm_references_x3.png` : 5 décors du jeu original (agrandis ×3), dans l'ordre : prairie fleurie avec arbres à fruits,
  herbe haute et eau bleue, jungle, forêt de sapins, plage et eau turquoise. **C'est le style à reproduire.** Les deux
  premiers sont les plus proches de ma scène.
- `dm_palette.png` / `dm_palette.txt` : les couleurs des deux premiers décors. **Pars de cette palette** ; ajoute
  seulement les teintes qui manquent (bois du pont, roche du dessous de l'île, nuages), dans le même esprit.
- `pokemon_dm_echelle_x4.png` : deux Pokémon du jeu (agrandis ×4), pour l'échelle et la cohérence.
- `prompt_da_ile_assets.md` : le prompt du premier essai. **La liste des fichiers, leurs noms, leurs tailles, le
  fonctionnement dans le jeu (sol en textures sans raccord, éléments posés librement avec point d'ancrage, vent,
  arbres en tronc + feuillage, ombres en silhouettes, eau animée par le jeu) et ce qu'il faut livrer (`assets.json`,
  `palette.png`, `assemblage.png`) restent exactement les mêmes.** Seul le style change.

## Le style de Donjon Mystère à reproduire
Observe les décors joints et reprends leurs caractéristiques :
- **Couleurs** franches et saturées, lumineuses, peu de gris ; ombres teintées (vert bleuté sous l'herbe, brun-rouge
  sous la terre) plutôt que noircies.
- **Contours sombres** d'un pixel autour des masses de végétation (buissons, feuillages, touffes) et des rochers,
  dans une teinte foncée de leur couleur (vert très sombre, brun très sombre), pas en noir pur.
- **Végétation en petites boules et touffes** bien découpées, empilées, avec un reflet clair sur le haut de chaque
  boule (lumière du haut à gauche), comme les buissons et arbres des décors joints.
- **Sols texturés** par de petits motifs répétés (brins d'herbe en V, mottes, petits cailloux, fleurettes de 3 à 5 px),
  assez denses, avec des contrastes plus doux que la végétation pour que les Pokémon restent lisibles.
- **Eau** bleue lumineuse avec des vaguelettes claires en arcs ou en écailles, comme le décor « herbe haute et eau ».
- Petits **accents de couleur** (fleurs rouges, roses, jaunes, fruits) qui ponctuent le vert, comme dans la prairie
  fleurie.
- Même **taille de pixel** que les décors et les Pokémon joints : 24 px = 1 case, aucun pixel agrandi ni lissé.
- Pas de dégradés doux, pas de lumière volumétrique ni de particules diffuses : un rendu net et « carte de DS ».

## Ce qui ne change pas par rapport au premier essai
- Les mêmes fichiers, avec **les mêmes noms et à peu près les mêmes tailles**, pour que je puisse remplacer les anciens
  sans rien changer dans le jeu.
- Toujours **organique** : aucune grille visible, éléments posés librement (le jeu fait les bords irréguliers).
- Pixel art net à l'échelle 1, alpha 0 ou 255, ombres en silhouettes #000000 séparées, herbes et fleurs seules et
  droites (sans sol ni ombre sous elles), textures de sol et d'eau sans raccord sur les 4 bords.

## À livrer
- Une archive `.zip` avec tous les PNG, `palette.png`, `assets.json` (même format que la première fois) et
  `assemblage.png` (+ ×3) : la même scène reconstruite avec ces fichiers.
- En plus : `comparaison.png`, l'assemblage à côté du décor « prairie fleurie » joint, à la même échelle, pour vérifier
  que les deux semblent venir du même jeu.
