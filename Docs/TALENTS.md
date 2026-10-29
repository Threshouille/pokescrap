# Talents : statistiques

Généré par `Tools/pmd/abilities_report.py` d'après `abilities_rt.json`, `pokemon_rt.json` et `megas.json`. Comptes par espèce (n° de Pokédex : les formes de la ROM sont réunies), par lignée (`baseForm`) et par méga.

## Vue d'ensemble

- Catalogue : 314 talents de la série principale, dont 122 présents dans la ROM.
- Talents portés par au moins un Pokémon du projet : 163 (122 talents normaux de la ROM, 41 seulement en talent caché ou sur une méga).
- Espèces : 492 ; lignées : 246 ; mégas : 27.
- Talents normaux par espèce : 1 talent : 230, 2 talents : 262.
- Sans talent caché distinct : 46 espèces (légendaires surtout, ou caché identique à un talent normal).
- Sans aucun talent : 0.
- Avec un effet en jeu (`ability_effects.json`, ✓ ci-dessous) : 109 talents.

## Les plus communs (talents normaux, 25 premiers)

| Talent | Espèces | Lignées | En caché | Mégas | Famille | En jeu |
|---|---:|---:|---:|---:|---|:---:|
| Lévitation (Levitate) | 28 | 20 | 0 | 2 | type | ✓ |
| Glissade (Swift Swim) | 25 | 15 | 7 | 0 | weather | ✓ |
| Fermeté (Sturdy) | 22 | 11 | 2 | 0 | hit | ✓ |
| Chlorophylle (Chlorophyll) | 21 | 9 | 4 | 0 | weather | ✓ |
| Pression (Pressure) | 20 | 19 | 2 | 0 | none | ✓ |
| Intimidation (Intimidate) | 19 | 13 | 1 | 1 | stats | ✓ |
| Regard Vif (Keen Eye) | 18 | 12 | 5 | 0 | stats | ✓ |
| Tête de Roc (Rock Head) | 18 | 9 | 0 | 0 | hit | ✓ |
| Benêt (Oblivious) | 16 | 8 | 3 | 0 | status | ✓ |
| Isograisse (Thick Fat) | 16 | 8 | 3 | 0 | type | ✓ |
| Fuite (Run Away) | 15 | 11 | 7 | 0 | none | ✓ |
| Attention (Inner Focus) | 14 | 8 | 8 | 1 | status | ✓ |
| Cran (Guts) | 13 | 8 | 4 | 0 | status | ✓ |
| Matinal (Early Bird) | 13 | 7 | 2 | 0 | status | ✓ |
| Médic Nature (Natural Cure) | 13 | 7 | 0 | 0 | none | ✓ |
| Brasier (Blaze) | 12 | 4 | 0 | 0 | type | ✓ |
| Engrais (Overgrow) | 12 | 4 | 0 | 0 | type | ✓ |
| Essaim (Swarm) | 12 | 9 | 0 | 0 | type | ✓ |
| Mue (Shed Skin) | 12 | 9 | 0 | 0 | status | ✓ |
| Statik (Static) | 12 | 5 | 1 | 0 | status | ✓ |
| Torrent (Torrent) | 12 | 4 | 0 | 0 | type | ✓ |
| Point Poison (Poison Point) | 11 | 5 | 0 | 0 | status | ✓ |
| Synchro (Synchronize) | 11 | 5 | 0 | 0 | status | ✓ |
| Tempo Perso (Own Tempo) | 11 | 6 | 4 | 0 | status | ✓ |
| Voile Sable (Sand Veil) | 11 | 5 | 6 | 0 | weather | ✓ |

## Uniques à une lignée (21, talent normal ou caché compris)

- Absentéisme (Truant) : lignée de Parecool (other)
- Air Lock (Air Lock) : lignée de Rayquaza (weather)
- Alerte Neige (Snow Warning) : lignée de Blizzi (weather)
- Annule Garde (No Guard) : lignée de Machoc (other)
- Coloforce (Huge Power) : lignée de Azurill (stats)
- Don Floral (Flower Gift) : lignée de Ceribou (weather)
- Début Calme (Slow Start) : lignée de Regigigas (none)
- Filtre (Filter) : lignée de Mime Jr. (stats)
- Force Pure (Pure Power) : lignée de Méditikka (stats)
- Frein (Stall) : lignée de Ténéfix (none)
- Garde Mystik (Wonder Guard) : lignée de Ningale (type)
- Homochromie (Color Change) : lignée de Kecleon (hit)
- Ignifugé (Heatproof) : lignée de Archéomire (type)
- Maladresse (Klutz) : lignée de Laporeille (item)
- Marque Ombre (Shadow Tag) : lignée de Okéoké (none)
- Mauvais Rêve (Bad Dreams) : lignée de Darkrai (status)
- Motorisé (Motor Drive) : lignée de Elekid (type)
- Météo (Forecast) : lignée de Morphéo (weather)
- Normalise (Normalize) : lignée de Skitty (type)
- Télécharge (Download) : lignée de Porygon (none)
- Écran Fumée (White Smoke) : lignée de Chartor (stats)

## Seulement en talent caché ou sur une méga (41)

- Sans Limite (Sheer Force), 5e gén. : caché de 16 espèces (Nidoqueen, Nidoking, Krabby, Krabboss…) ; méga : Méga-Camérupt (stats)
- Tension (Unnerve), 5e gén. : caché de 12 espèces (Abo, Arbok, Miaouss, Persian…) (item)
- Phobique (Rattled), 5e gén. : caché de 10 espèces (Magicarpe, Coxy, Simularbre, Insolourdo…) (hit)
- Télépathe (Telepathy), 5e gén. : caché de 10 espèces (Qulbutoké, Tarsal, Kirlia, Gardevoir…) (team)
- Force Sable (Sand Force), 5e gén. : caché de 8 espèces (Taupiqueur, Triopikeur, Tarinor, Sancoki…) ; méga : Méga-Steelix (weather)
- Infiltration (Infiltrator), 5e gén. : caché de 9 espèces (Nosferapti, Nosferalto, Nostenfer, Granivol…) (other)
- Analyste (Analytic), 5e gén. : caché de 8 espèces (Magnéti, Magnéton, Stari, Staross…) (stats)
- Armurouillée (Weak Armor), 5e gén. : caché de 8 espèces (Onix, Amonita, Amonistar, Kabuto…) (hit)
- Envelocape (Overcoat), 5e gén. : caché de 7 espèces (Kokiyas, Crustabri, Pomdepik, Foretress…) (weather)
- Lunatique (Moody), 5e gén. : caché de 7 espèces (Rémoraid, Octillery, Queulorior, Stalgamin…) (stats)
- Régé-Force (Regenerator), 5e gén. : caché de 7 espèces (Ramoloss, Flagadoss, Saquedeneu, Roigada…) (none)
- Farceur (Prankster), 5e gén. : caché de 5 espèces (Cornèbre, Ténéfix, Muciole, Lumivole…) ; méga : Méga-Branette (none)
- Herbivore (Sap Sipper), 5e gén. : caché de 6 espèces (Marill, Azumarill, Girafarig, Cerfrousse…) (type)
- Impudence (Moxie), 5e gén. : caché de 6 espèces (Scarabrute, Léviator, Scarhino, Grahyèna…) (stats)
- Cœur Noble (Justified), 5e gén. : caché de 5 espèces (Caninos, Arcanin, Absol, Lucario…) (stats)
- Garde-Ami (Friend Guard), 5e gén. : caché de 5 espèces (Mélofée, Rondoudou, Mélo, Toudoudou…) (team)
- Heavy Metal (Heavy Metal), 5e gén. : caché de 5 espèces (Galekid, Galegon, Galeking, Archéomire…) (none)
- Light Metal (Light Metal), 5e gén. : caché de 5 espèces (Cizayox, Terhal, Métang, Métalosse…) (none)
- Miroir Magik (Magic Bounce), 5e gén. : caché de 3 espèces (Natu, Xatu, Mentali) ; méga : Méga-Ténéfix, Méga-Absol (other)
- Pickpocket (Pickpocket), 5e gén. : caché de 5 espèces (Farfuret, Grainipiot, Pifeuil, Tengalice…) (item)
- Acharné (Defiant), 5e gén. : caché de 4 espèces (Férosinge, Colossinge, Canarticho, Chaffreux) (stats)
- Cœur de Coq (Big Pecks), 5e gén. : caché de 4 espèces (Roucool, Roucoups, Roucarnage, Pijako) (stats)
- Toxitouche (Poison Touch), 5e gén. : caché de 4 espèces (Tadmorv, Grotadmorv, Cradopaud, Coatox) (status)
- Battant (Competitive), 6e gén. : caché de 3 espèces (Tiplouf, Prinplouf, Pingoléon) (stats)
- Corps Maudit (Cursed Body), 5e gén. : caché de 3 espèces (Polichombr, Branette, Momartik) (other)
- Cœur Soin (Healer), 5e gén. : caché de 3 espèces (Leveinard, Joliflor, Leuphorie) (status)
- Peau Miracle (Wonder Skin), 5e gén. : caché de 3 espèces (Aéromite, Skitty, Delcatty) (stats)
- Récolte (Harvest), 5e gén. : caché de 3 espèces (Noeunoeuf, Noadkoko, Tropius) (item)
- Baigne Sable (Sand Rush), 5e gén. : caché de 2 espèces (Sabelette, Sablaireau) (weather)
- Contestation (Contrary), 5e gén. : caché de 2 espèces (Caratroc, Spinda) (stats)
- Griffe Dure (Tough Claws), 6e gén. : méga : Méga-Dracaufeu X, Méga-Ptéra (contact)
- Multiécaille (Multiscale), 5e gén. : caché de 2 espèces (Dracolosse, Lugia) (hp)
- Peau Féérique (Pixilate), 6e gén. : méga : Méga-Gardevoir, Méga-Altaria (type)
- Rage Brûlure (Flare Boost), 5e gén. : caché de 2 espèces (Baudrive, Grodrive) (status)
- Amour Filial (Parental Bond), 6e gén. : méga : Méga-Kangourex (stats)
- Imposteur (Imposter), 5e gén. : caché de 1 espèces (Métamorph) (none)
- Nerfs d’Acier (Stalwart), 8e gén. : méga : Méga-Airmure (other)
- Peau Gelée (Refrigerate), 6e gén. : méga : Méga-Oniglali (type)
- Protéen (Protean), 6e gén. : caché de 1 espèces (Kecleon) (other)
- Rage Poison (Toxic Boost), 5e gén. : caché de 1 espèces (Mangriff) (status)
- Souffle Delta (Delta Stream), 6e gén. : méga : Méga-Rayquaza (type)

## Familles d'adaptation au temps réel

Classement automatique d'après l'effet anglais de PokéAPI (`Tools/pmd/ability_families.json`, `byId` pour corriger un talent) ; poids = espèces qui portent le talent (normal + caché) + mégas.

### Non transposable (hors combat, PP, poids, priorité, changement de Pokémon) : 21 talents, poids 140

- ✓ Fuite (Run Away) [22] : Ensures success fleeing from wild battles.
- ✓ Pression (Pressure) [22] : Increases the PP cost of moves targetting the Pokémon by one.
- ✓ Médic Nature (Natural Cure) [13] : Cures any major status ailment upon switching out.
- Fouille (Frisk) [11] : Reveals an opponent's held item upon entering battle.
- ✓ Ramassage (Pickup) [9] : Picks up other Pokémon's used and Flung held items.  May also pick up an item after battle.
- Régé-Force (Regenerator) [7] : Heals for 1/3 max HP upon switching out.
- Calque (Trace) [6] : Copies an opponent's ability upon entering battle.
- Anticipation (Anticipation) [6] : Notifies all trainers upon entering battle if an opponent has a super-effective move, Self-Destruct, Explosion, or a one-hit KO move.
- Farceur (Prankster) [6] : Raises non-damaging moves' priority by one stage.
- ✓ Magnépiège (Magnet Pull) [5] : Prevents Steel opponents from fleeing or switching out.
- Lumiattirance (Illuminate) [5] : Doubles the wild encounter rate.
- ✓ Heavy Metal (Heavy Metal) [5] : Doubles the Pokémon's weight.
- ✓ Light Metal (Light Metal) [5] : Halves the Pokémon's weight.
- Prédiction (Forewarn) [4] : Reveals the opponents' strongest move upon entering battle.
- ✓ Piège Sable (Arena Trap) [3] : Prevents opponents from fleeing or switching out.  Eluded by Flying-types and Pokémon in the air.
- ✓ Marque Ombre (Shadow Tag) [3] : Prevents opponents from fleeing or switching out.
- Télécharge (Download) [3] : Raises the attack stat corresponding to the opponents' weaker defense one stage upon entering battle.
- ✓ Cherche Miel (Honey Gather) [2] : The Pokémon may pick up Honey after battle.
- Frein (Stall) [1] : Makes the Pokémon move last within its move's priority bracket.
- Début Calme (Slow Start) [1] : Halves Attack and Speed for five turns upon entering battle.
- Imposteur (Imposter) [1] : Transforms upon entering battle.

### Météo / terrain : 21 talents, poids 172

- ✓ Glissade (Swift Swim) [32] : Doubles Speed during rain.
- ✓ Chlorophylle (Chlorophyll) [25] : Doubles Speed during strong sunlight.
- ✓ Voile Sable (Sand Veil) [17] : Increases evasion to 1.25× during a sandstorm.  Protects against sandstorm damage.
- ✓ Cuvette (Rain Dish) [11] : Heals for 1/16 max HP after each turn during rain.
- ✓ Hydratation (Hydration) [11] : Cures any major status ailment after each turn during rain.
- ✓ Feuille Garde (Leaf Guard) [11] : Protects against major status ailments during strong sunlight.
- ✓ Corps Gel (Ice Body) [9] : Heals for 1/16 max HP after each turn during hail.  Protects against hail damage.
- ✓ Force Sable (Sand Force) [9] : Strengthens Rock, Ground, and Steel moves to 1.3× their power during a sandstorm.  Protects against sandstorm damage.
- ✓ Force Soleil (Solar Power) [7] : Increases Special Attack to 1.5× but costs 1/8 max HP after each turn during strong sunlight.
- Envelocape (Overcoat) [7] : Protects against damage from weather.
- Ciel Gris (Cloud Nine) [6] : Negates all effects of weather, but does not prevent the weather itself.
- ✓ Rideau Neige (Snow Cloak) [6] : Increases evasion to 1.25× during hail.  Protects against hail damage.
- ✓ Peau Sèche (Dry Skin) [5] : Causes 1/8 max HP in damage each turn during strong sunlight, but heals for 1/8 max HP during rain.  Increases damage from Fire moves to 1.25×, but absorbs Water moves, healing for 1/4 max HP.
- ✓ Sable Volant (Sand Stream) [4] : Summons a sandstorm that lasts indefinitely upon entering battle.
- ✓ Sécheresse (Drought) [3] : Summons strong sunlight that lasts indefinitely upon entering battle.
- ✓ Crachin (Drizzle) [2] : Summons rain that lasts indefinitely upon entering battle.
- ✓ Alerte Neige (Snow Warning) [2] : Summons hail that lasts indefinitely upon entering battle.
- ✓ Baigne Sable (Sand Rush) [2] : Doubles Speed during a sandstorm.  Protects against sandstorm damage.
- Air Lock (Air Lock) [1] : Negates all effects of weather, but does not prevent the weather itself.
- Météo (Forecast) [1] : Changes Castform's type and form to match the weather.
- Don Floral (Flower Gift) [1] : Increases friendly Pokémon's Attack and Special Defense to 1.5× during strong sunlight.

### Immunité / absorption / renfort de type : 22 talents, poids 188

- ✓ Lévitation (Levitate) [30] : Evades Ground moves.
- ✓ Isograisse (Thick Fat) [19] : Halves damage from Fire and Ice moves.
- ✓ Absorbe-Eau (Water Absorb) [14] : Absorbs Water moves, healing for 1/4 max HP.
- ✓ Paratonnerre (Lightning Rod) [13] : Redirects single-target Electric moves to this Pokémon where possible.  Absorbs Electric moves, raising Special Attack one stage.
- ✓ Torche (Flash Fire) [13] : Protects against Fire moves.  Once one has been blocked, the Pokémon's own Fire moves inflict 1.5× damage until it leaves battle.
- ✓ Torrent (Torrent) [12] : Strengthens Water moves to inflict 1.5× damage at 1/3 max HP or less.
- ✓ Engrais (Overgrow) [12] : Strengthens Grass moves to inflict 1.5× damage at 1/3 max HP or less.
- ✓ Essaim (Swarm) [12] : Strengthens Bug moves to inflict 1.5× damage at 1/3 max HP or less.
- ✓ Brasier (Blaze) [12] : Strengthens Fire moves to inflict 1.5× damage at 1/3 max HP or less.
- Anti-Bruit (Soundproof) [11] : Protects against sound-based moves.
- ✓ Querelleur (Scrappy) [7] : Lets the Pokémon's Normal and Fighting moves hit Ghost Pokémon.
- ✓ Adaptabilité (Adaptability) [6] : Increases the same-type attack bonus from 1.5× to 2×.
- ✓ Lavabo (Storm Drain) [6] : Redirects single-target Water moves to this Pokémon where possible.  Absorbs Water moves, raising Special Attack one stage.
- ✓ Herbivore (Sap Sipper) [6] : Absorbs Grass moves, raising Attack one stage.
- ✓ Absorbe-Volt (Volt Absorb) [5] : Absorbs Electric moves, healing for 1/4 max HP.
- ✓ Ignifugé (Heatproof) [2] : Halves damage from Fire moves and burns.
- Normalise (Normalize) [2] : Makes the Pokémon's moves all act Normal-type.
- Peau Féérique (Pixilate) [2] : Turns the bearer's Normal moves into Fairy moves and strengthens them to 1.3× their power.
- Garde Mystik (Wonder Guard) [1] : Protects against damaging moves that are not super effective.
- ✓ Motorisé (Motor Drive) [1] : Absorbs Electric moves, raising Speed one stage.
- Peau Gelée (Refrigerate) [1] : Turns the bearer's Normal moves into Ice moves and strengthens them to 1.3× their power.
- Souffle Delta (Delta Stream) [1] : Creates a mysterious air current, which cannot be replaced and causes moves to never be super effective against Flying Pokémon.

### Statut (infliger, immunité, soin) : 29 talents, poids 256

- ✓ Attention (Inner Focus) [23] : Prevents flinching.
- ✓ Benêt (Oblivious) [19] : Prevents infatuation and protects against Captivate.
- ✓ Cran (Guts) [17] : Increases Attack to 1.5× with a major status ailment.
- ✓ Matinal (Early Bird) [15] : Makes sleep pass twice as quickly.
- ✓ Tempo Perso (Own Tempo) [15] : Prevents confusion.
- ✓ Statik (Static) [13] : Has a 30% chance of paralyzing attacking Pokémon on contact.
- ✓ Mue (Shed Skin) [12] : Has a 33% chance of curing any major status ailment after each turn.
- ✓ Insomnia (Insomnia) [12] : Prevents sleep.
- ✓ Synchro (Synchronize) [11] : Copies burns, paralysis, and poison received onto the Pokémon that inflicted them.
- ✓ Point Poison (Poison Point) [11] : Has a 30% chance of poisoning attacking Pokémon on contact.
- ✓ Ignifu-Voile (Water Veil) [11] : Prevents burns.
- ✓ Esprit Vital (Vital Spirit) [11] : Prevents sleep.
- ✓ Joli Sourire (Cute Charm) [10] : Has a 30% chance of infatuating attacking Pokémon on contact.
- ✓ Corps Ardent (Flame Body) [9] : Has a 30% chance of burning attacking Pokémon on contact.
- ✓ Pied Véloce (Quick Feet) [9] : Increases Speed to 1.5× with a major status ailment.
- ✓ Impassible (Steadfast) [9] : Raises Speed one stage upon flinching.
- ✓ Puanteur (Stench) [7] : Has a 10% chance of making target Pokémon flinch with each hit.
- ✓ Pieds Confus (Tangled Feet) [7] : Doubles evasion when confused.
- ✓ Échauffement (Limber) [6] : Prevents paralysis.
- ✓ Pose Spore (Effect Spore) [5] : Has a 30% chance of inflcting either paralysis, poison, or sleep on attacking Pokémon on contact.
- ✓ Toxitouche (Poison Touch) [4] : Has a 30% chance of poisoning target Pokémon upon contact.
- Écaille Spéciale (Marvel Scale) [3] : Increases Defense to 1.5× with a major status ailment.
- ✓ Armumagma (Magma Armor) [3] : Prevents freezing.
- ✓ Vaccin (Immunity) [3] : Prevents poison.
- ✓ Soin Poison (Poison Heal) [3] : Heals for 1/8 max HP after each turn when poisoned in place of damage.
- Cœur Soin (Healer) [3] : Has a 30% chance of curing each adjacent ally of any major status ailment after each turn.
- ✓ Mauvais Rêve (Bad Dreams) [2] : Damages sleeping opponents for 1/8 their max HP after each turn.
- Rage Brûlure (Flare Boost) [2] : Increases Special Attack to 1.5× when burned.
- Rage Poison (Toxic Boost) [1] : Increases Attack to 1.5× when poisoned.

### Objets tenus et baies : 7 talents, poids 46

- ✓ Gloutonnerie (Gluttony) [12] : Makes the Pokémon eat any held Berry triggered by low HP below 1/2 its max HP.
- Tension (Unnerve) [12] : Prevents opposing Pokémon from eating held Berries.
- Glu (Sticky Hold) [6] : Prevents a held item from being removed by other Pokémon.
- ✓ Délestage (Unburden) [6] : Doubles Speed upon using or losing a held item.
- Pickpocket (Pickpocket) [5] : Steals attacking Pokémon's held items on contact.
- Récolte (Harvest) [3] : Has a 50% chance of restoring a used Berry after each turn if the Pokémon has held no items in the meantime.
- Maladresse (Klutz) [2] : Prevents the Pokémon from using its held item in battle.

### Équipe / alliés : 4 talents, poids 22

- Télépathe (Telepathy) [10] : Protects against friendly Pokémon's damaging moves.
- ✓ Garde-Ami (Friend Guard) [5] : Decreases all direct damage taken by friendly Pokémon to 0.75×.
- ✓ Plus (Plus) [4] : Increases Special Attack to 1.5× when a friendly Pokémon has Plus or Minus.
- ✓ Moins (Minus) [3] : Increases Special Attack to 1.5× when a friendly Pokémon has Plus or Minus.

### Contact (coup au corps à corps) : 3 talents, poids 13

- ✓ Boom Final (Aftermath) [6] : Damages the attacker for 1/4 its max HP when knocked out by a contact move.
- ✓ Peau Dure (Rough Skin) [5] : Damages attacking Pokémon for 1/8 their max HP on contact.
- Griffe Dure (Tough Claws) [2] : Strengthens moves that make contact to 1.33× their power.

### Réaction au coup reçu / K.O. : 11 talents, poids 112

- ✓ Fermeté (Sturdy) [24] : Prevents being KOed from full HP, leaving 1 HP instead.  Protects against the one-hit KO moves regardless of HP.
- ✓ Tête de Roc (Rock Head) [18] : Protects against recoil damage.
- ✓ Coque Armure (Shell Armor) [14] : Protects against critical hits.
- ✓ Sniper (Sniper) [12] : Strengthens critical hits to inflict 3× damage rather than 2×.
- ✓ Phobique (Rattled) [10] : Raises Speed one stage upon being hit by a Dark, Ghost, or Bug move.
- ✓ Armurbaston (Battle Armor) [8] : Protects against critical hits.
- Armurouillée (Weak Armor) [8] : Raises Speed and lowers Defense by one stage each upon being hit by a physical move.
- ✓ Téméraire (Reckless) [7] : Strengthens recoil moves to 1.2× their power.
- ✓ Chanceux (Super Luck) [6] : Raises moves' critical hit rates one stage.
- ✓ Colérique (Anger Point) [4] : Raises Attack to the maximum of six stages upon receiving a critical hit.
- Homochromie (Color Change) [1] : Changes type to match when hit by a damaging move.

### Seuil de PV / soin : 2 talents, poids 6

- ✓ Suintement (Liquid Ooze) [4] : Damages opponents using leeching moves for as much as they would heal.
- ✓ Multiécaille (Multiscale) [2] : Halves damage taken from full HP.

### Passif de stats : 30 talents, poids 208

- ✓ Regard Vif (Keen Eye) [23] : Prevents accuracy from being lowered.
- ✓ Intimidation (Intimidate) [21] : Lowers opponents' Attack one stage upon entering battle.
- ✓ Sans Limite (Sheer Force) [17] : Strengthens moves with extra effects to 1.3× their power, but prevents their extra effects.
- ✓ Agitation (Hustle) [13] : Strengthens physical moves to inflict 1.5× damage, but decreases their accuracy to 0.8×.
- ✓ Technicien (Technician) [12] : Strengthens moves of 60 base power or less to 1.5× their power.
- Rivalité (Rivalry) [10] : Increases damage inflicted to 1.25× against Pokémon of the same gender, but decreases damage to 0.75× against the opposite gender.
- ✓ Hyper Cutter (Hyper Cutter) [9] : Prevents Attack from being lowered by other Pokémon.
- ✓ Turbo (Speed Boost) [8] : Raises Speed one stage after each turn.
- ✓ Corps Sain (Clear Body) [8] : Prevents stats from being lowered by other Pokémon.
- ✓ Lentiteintée (Tinted Lens) [8] : Doubles damage inflicted with not-very-effective moves.
- Analyste (Analytic) [8] : Strengthens moves to 1.3× their power when moving last.
- Lunatique (Moody) [7] : Raises a random stat two stages and lowers another one stage after each turn.
- ✓ Garde Magik (Magic Guard) [6] : Protects against damage not directly caused by a move.
- ✓ Impudence (Moxie) [6] : Raises Attack one stage upon KOing a Pokémon.
- ✓ Œil Composé (Compound Eyes) [5] : Increases moves' accuracy to 1.3×.
- Poing de Fer (Iron Fist) [5] : Strengthens punch-based moves to 1.2× their power.
- Inconscient (Unaware) [5] : Ignores other Pokémon's stat modifiers for damage and accuracy calculation.
- ✓ Cœur Noble (Justified) [5] : Raises Attack one stage upon taking damage from a Dark move.
- Coloforce (Huge Power) [4] : Doubles Attack in battle.
- ✓ Acharné (Defiant) [4] : Raises Attack two stages upon having any stat lowered.
- ✓ Cœur de Coq (Big Pecks) [4] : Protects against Defense drops.
- Force Pure (Pure Power) [3] : Doubles Attack in battle.
- Simple (Simple) [3] : Doubles the Pokémon's stat modifiers.  These doubled modifiers are still capped at -6 or 6 stages.
- Peau Miracle (Wonder Skin) [3] : Lowers incoming non-damaging moves' base accuracy to exactly 50%.
- Battant (Competitive) [3] : Raises Special Attack by two stages upon having any stat lowered.
- ✓ Solide Roc (Solid Rock) [2] : Decreases damage taken from super-effective moves by 1/4.
- ✓ Filtre (Filter) [2] : Decreases damage taken from super-effective moves by 1/4.
- Contestation (Contrary) [2] : Inverts stat changes.
- ✓ Écran Fumée (White Smoke) [1] : Prevents stats from being lowered by other Pokémon.
- Amour Filial (Parental Bond) [1] : Lets the bearer hit twice with damaging moves.  The second hit has half power.

### Autres (à voir au cas par cas) : 13 talents, poids 64

- ✓ Moiteur (Damp) [16] : Prevents Self-Destruct, Explosion, and Aftermath from working while the Pokémon is in battle.
- ✓ Sérénité (Serene Grace) [9] : Doubles the chance of moves' extra effects occurring.
- Infiltration (Infiltrator) [9] : Bypasses Light Screen, Reflect, and Safeguard.
- ✓ Écran Poudre (Shield Dust) [5] : Protects against incoming moves' extra effects.
- Miroir Magik (Magic Bounce) [5] : Reflects most non-damaging moves back at their user.
- Multi-Coups (Skill Link) [4] : Extends two-to-five-hit moves and Triple Kick to their full length every time.
- ✓ Ventouse (Suction Cups) [3] : Prevents being forced out of battle by other Pokémon's moves.
- Brise Moule (Mold Breaker) [3] : Bypasses targets' abilities if they could hinder or prevent a move.
- ✓ Annule Garde (No Guard) [3] : Ensures all moves used by and against the Pokémon hit.
- ✓ Corps Maudit (Cursed Body) [3] : Has a 30% chance of Disabling any move that hits the Pokémon.
- Absentéisme (Truant) [2] : Skips every second turn.
- Protéen (Protean) [1] : Changes the bearer's type to match each move it uses.
- Nerfs d’Acier (Stalwart) [1] : Ignores moves and abilities that draw in moves.

