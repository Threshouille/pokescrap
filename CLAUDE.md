# Pokescrap

Projet Unity (URP) : roguelike en temps réel, jouable à la manette, qui réutilise les assets et données de Pokémon Donjon Mystère sans chercher à reproduire le jeu d'origine (ex. : la carte est une grande arène carrée avec obstacles, pas un donjon à salles et couloirs). L'utilisateur parle français.

Conception : `Docs/GDD.md` (décisions d'Owen + points ouverts) ; le lire avant tout travail de gameplay et le tenir à jour quand une décision est prise.

## Économie de tokens
- Réponses courtes, pas de récapitulatif de ce qui vient d'être fait.
- Ne jamais lire `Library/`, `Temp/`, `Logs/` en entier ; pour un log, `grep`/`tail` ciblé.
- Ne pas lire les `.meta`, `.unity`, `.prefab`, `.asset` bruts : passer par les outils unityMCP.
- unityMCP : regrouper les opérations dans `batch_execute` ; paginer (`page_size`, `max_nodes`, `max_depth`) ; `generate_preview=false` ; `read_console` filtré sur les erreurs.
- Captures d'écran seulement si un contrôle visuel est nécessaire, en basse résolution (`max_resolution` ≤ 512).
- Scripts : `find_in_file` / `script_apply_edits` ciblés plutôt que relire le fichier complet.
- Pas de sous-agents sauf demande explicite.
- Données de jeu volumineuses (Pokémon, attaques) : les interroger par script (`jq`, Python), ne jamais les charger en entier dans le contexte.

## Données et assets issus de la ROM
- ROM : Explorateurs du Ciel EU (`C2SP`), `Rom/eos_eu.nds`, dépaquetée dans `Rom/files/` (hors `Assets/`, donc non importée par Unity).
- `python3 -I Tools/pmd/nds_unpack.py Rom/eos_eu.nds Rom/files` : dépaquette la ROM.
- `python3 -I Tools/pmd/extract_data.py Rom/files Assets/Data` : génère `pokemon.json`, `moves.json`, `types.json`, `abilities.json` (noms FR + `name_en`, identifiants = index internes du jeu).
- `Tools/.venv/bin/python -I Tools/pmd/extract_sprites.py Rom/eos_eu.nds Rom/export [first last]` (≈8 min pour tout, via `skytemple-files`) :
  - `Rom/export/sprites/<sprite_index sur 4 chiffres>/` : `AnimData.xml` + `<Anim>-Anim.png`, `-Offsets.png`, `-Shadow.png` (une ligne par direction, 8 directions) ; `sprite_index` vient de `pokemon.json`.
  - `Rom/export/portraits/<id Pokémon sur 4 chiffres>/<émotion>.png`.
- `Rom/export/` contient ~26 000 PNG : ne copier dans `Assets/` que les sprites réellement utilisés, jamais le dossier entier.
- `Tools/.venv/bin/python -I Tools/pmd/extract_tileset.py Rom/eos_eu.nds Assets/Tilesets <id>...` : décor de donjon → `Assets/Tilesets/<id>/chunks.png` + `tileset.json` (le décor 0 est un décor de test ; le 1 est utilisé dans la scène).
- Après ajout de fichiers : menus Unity **Pokescrap > Build Anim Sets** (sprites sous `Assets/Sprites/Pokemon/<Nom>/`) et **Pokescrap > Build Tilesets**.
- Scène : `Player` (`PlayerController` seul : crée les 3 Pokémon de l'équipe au lancement et suit celui que le joueur contrôle ; les autres ont `AllyAI`), `Dungeon` (`DungeonGenerator`, tilemaps `Floor` et `Walls` avec collisions), caméra avec `CameraFollow`. Les modifications de scène faites en mode Play sont perdues : vérifier `Application.isPlaying` avant d'éditer.
- Vitesse de base : absente de la ROM, lue dans `Tools/pmd/external/pokemon_stats.csv` (PokéAPI) par `extract_data.py`, qui écrit aussi `Assets/Resources/Data/pokemon_rt.json` (données compactes chargées en jeu).
- Combat (`Assets/Scripts/Combat/`) : `GameData` charge `pokemon_rt.json` et `realtime_moves.json` (attaques temps réel écrites à la main : famille, recharge, vitesse, portée, rayon, attaques de départ par Pokémon) ; `Combatant` (PV, attaques, recharges), `Projectile`, `EnemyAI`, `EnemySpawner`, `HealthBar`.
- Effets d'attaque : planches PNG (une ligne d'images, pointant vers la droite, 24 px = 1 case) + `effects.json` dans `Assets/Resources/Effects/`, faits avec Claude Design (originaux dans `Design/`). `EffectPlayer` les joue ; chaque attaque les référence par `effect` et `impactEffect` dans `realtime_moves.json`. Sans planche, un projectile retombe sur un disque coloré.
- `GameData` et `EffectLibrary` relisent leurs fichiers à chaque lancement de partie ; en mode édition (hors Play), leurs données peuvent être périmées tant qu'aucun script n'a été recompilé.
- Déplacement des IA : `Navigator` (A* sur la grille de `DungeonGenerator.Current`, zones nocives pénalisées via `Zone.Inside`). Toute nouvelle IA doit passer par `navigator.Direction(...)` plutôt que viser sa cible en ligne droite.
- Familles d'attaques : `projectile`, `melee` (avec `lunge` éventuel), `zone` (aire persistante). Statut : `status: "poison"` sur une attaque. Icône de statut : planche `status_<nom>` dans `Assets/Resources/Effects/` si elle existe, sinon icône de secours dans `Shapes`.
- Ennemis de l'arène : liste `waves` (chaque vague = jeux d'animations + nombres) sur l'objet `Enemies` (`EnemySpawner`), avec `healthFactor`, `damageFactor` et `delayBetweenWaves`. La vague suivante part quand il ne reste aucun ennemi ; `Victory` après la dernière.
- Feuille de route : `Docs/ROADMAP.md` (squelette d'abord : jalons 1 à 4 en version minimale). Projet sous Git local, sans dépôt distant ; ne jamais pousser vers un hébergement public (assets tirés de la ROM versionnés).
- Un nouveau Pokémon dans `Assets/Sprites/Pokemon/<Nom>/` a besoin d'un `id.txt` contenant son identifiant de `pokemon.json`, et de son portrait : `Rom/export/portraits/<id sur 4 chiffres>/00.png` copié en `Assets/Resources/Portraits/<id>.png`.
- Progression : `Progression` (ajouté à l'exécution par `PlayerController`) gère expérience, niveau commun et écran de choix (met `Time.timeScale` à 0). Gains par niveau : `LevelData` lit `Assets/Resources/Data/levels_rt.bytes`, produit par `Tools/.venv/bin/python -I Tools/pmd/extract_levels.py Rom/files Assets/Resources/Data/levels_rt.bytes`. Catalogue d'améliorations : `Assets/Resources/Data/upgrades.json` (appliquées par `Combatant.ApplyUpgrade`).
- Interface : `TeamHud` (ajouté à l'exécution par `PlayerController`), dessinée en IMGUI à l'échelle de l'écran. Réanimation des alliés K.O. gérée dans `PlayerController.UpdateRevives`.
- Tests en jeu : la fenêtre Unity tourne lentement en arrière-plan ; toute logique de jeu doit passer par `FixedUpdate`, pas par `Update`.
- Pas encore extraits : eau/lave des décors, objets, effets d'attaque du jeu d'origine, musiques.
- La ROM et tout ce qui en est extrait restent locaux, hors de tout dépôt public.
- Le jeu lit ses données depuis des fichiers (JSON / ScriptableObjects), jamais en dur dans le code.
