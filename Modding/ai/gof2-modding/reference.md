# Galaxy on Fire 2 Remake: game data reference for modders

Generated from the game's own tables (the Android economy; the Default economy's prices differ). Use these numbers
in mod files: `base`, `override`, ingredients, `alwaysSoldAt`, `system`, `looksLike`, `startStation`, `startShip`...
Mod content is referred to by key instead (`mod_id:id`).

## Races

0 Terran, 1 Vossk, 2 Nivelian, 3 Midorian, 8 Pirate, 9 Void, 10 Specter. Systems and stations belong to 0-3.

## Items

Type: primary / secondary / turret / equipment / commodity. Tech = tech level (1-10). Price = min-max (credits). Stats = the item's own stats (copy them when basing an item on it; names as in items.json's statList, which mods accept). BP = has a blueprint recipe in the original.

| # | Name | Type | Category | Tech | Price | Stats | BP |
|---|---|---|---|---|---|---|---|
| 0 | Nirai Impulse EX 1 | primary | Laser | 1 | 400-440 | damage 3, loadingTimeMs 400, range 2000, projectileSpeed 20 |  |
| 1 | Nirai Impulse EX 2 | primary | Laser | 2 | 6230-6481 | damage 5, loadingTimeMs 400, range 2000, projectileSpeed 22 |  |
| 2 | Nirai Charged Pulse | primary | Laser | 3 | 9510-10461 | damage 6, loadingTimeMs 380, range 2000, projectileSpeed 20 |  |
| 3 | V'skorr | primary | Laser | 6 | 8000-8800 | damage 7, loadingTimeMs 500, range 2000, projectileSpeed 20 |  |
| 4 | Sh'koom | primary | Laser | 6 | 13000-13747 | damage 10, loadingTimeMs 600, range 2000, projectileSpeed 20 |  |
| 5 | Berger Focus I | primary | Laser | 5 | 19100-21010 | damage 16, loadingTimeMs 900, range 2200, projectileSpeed 22 |  |
| 6 | Berger Focus II A1 | primary | Laser | 6 | 28100-30500 | damage 17, loadingTimeMs 850, range 2200, projectileSpeed 24 |  |
| 7 | Berger Retribution | primary | Laser | 7 | 35100-36200 | damage 18, loadingTimeMs 750, range 2200, projectileSpeed 23 |  |
| 8 | Berger Converge IV | primary | Laser | 8 | 81100-85300 | damage 28, loadingTimeMs 850, range 2000, projectileSpeed 22 |  |
| 9 | M6 A1 "Wolf" | primary | Laser | 7 | 35420-37000 | damage 27, loadingTimeMs 1200, range 2400, projectileSpeed 20 |  |
| 10 | M6 A2 "Cougar" | primary | Laser | 8 | 41230-44000 | damage 34, loadingTimeMs 1400, range 2600, projectileSpeed 20 |  |
| 11 | M6 A3 "Wolverine" | primary | Laser | 9 | 101629-107979 | damage 51, loadingTimeMs 1500, range 2600, projectileSpeed 20 | yes |
| 12 | N'saan | primary | Blaster | 1 | 10580-11638 | damage 8, loadingTimeMs 600, range 1500, projectileSpeed 14 |  |
| 13 | K'booskk | primary | Blaster | 2 | 14500-15950 | damage 8, loadingTimeMs 530, range 1500, projectileSpeed 14 |  |
| 14 | Sh'gaal | primary | Blaster | 4 | 22900-23604 | damage 9, loadingTimeMs 430, range 1000, projectileSpeed 17 |  |
| 15 | H'nookk | primary | Blaster | 6 | 48513-52401 | damage 9, loadingTimeMs 330, range 1500, projectileSpeed 16 | yes |
| 16 | Luna EMP Mk I | primary | Blaster | 4 | 4800-5280 | damage 0, empDamage 3, loadingTimeMs 350, range 1200, projectileSpeed 17 |  |
| 17 | Sol EMP Mk II | primary | Blaster | 5 | 10500-11550 | damage 0, empDamage 5, loadingTimeMs 450, range 2000, projectileSpeed 15 |  |
| 18 | Dia EMP Mk III | primary | Blaster | 6 | 34120-37532 | damage 0, empDamage 8, loadingTimeMs 450, range 2000, projectileSpeed 17 |  |
| 19 | Gram Blaster | primary | Blaster | 7 | 38000-40000 | damage 12, loadingTimeMs 300, range 1400, projectileSpeed 17 |  |
| 20 | Ridil Blaster | primary | Blaster | 8 | 62000-65000 | damage 12, loadingTimeMs 250, range 1000, projectileSpeed 18 |  |
| 21 | Tyrfing Blaster | primary | Blaster | 9 | 89000-94000 | damage 13, loadingTimeMs 220, range 1000, projectileSpeed 19 |  |
| 22 | Micro Gun MK I | primary | Auto-cannon | 1 | 440-484 | damage 2, loadingTimeMs 220, range 1000, projectileSpeed 18 |  |
| 23 | Micro Gun MKII | primary | Auto-cannon | 2 | 5000-5354 | damage 2, loadingTimeMs 170, range 1000, projectileSpeed 20 |  |
| 24 | 64MJ Railgun | primary | Auto-cannon | 3 | 12300-13530 | damage 2, loadingTimeMs 140, range 1200, projectileSpeed 22 |  |
| 25 | 128MJ Railgun | primary | Auto-cannon | 6 | 24615-26687 | damage 3, loadingTimeMs 120, range 1400, projectileSpeed 26 | yes |
| 26 | Scram Cannon | primary | Auto-cannon | 7 | 42400-45000 | damage 2, loadingTimeMs 100, range 1400, projectileSpeed 28 |  |
| 27 | Mass Driver MD 10 | primary | Auto-cannon | 8 | 113519-120463 | damage 4, loadingTimeMs 90, range 1400, projectileSpeed 30 | yes |
| 28 | Thermic o5 | primary | Thermo | 7 | 8000-8800 | damage 3, loadingTimeMs 300, range 2000, projectileSpeed 12 |  |
| 29 | ReHeat o10 | primary | Thermo | 8 | 34000-34269 | damage 4, loadingTimeMs 170, range 2000, projectileSpeed 14 |  |
| 30 | MaxHeat o20 | primary | Thermo | 9 | 81000-82689 | damage 5, loadingTimeMs 150, range 2000, projectileSpeed 16 |  |
| 31 | G'liissk | secondary | Rocket | 1 | 20-22 | damage 60, loadingTimeMs 1200, range 1200, projectileSpeed 12 |  |
| 32 | Jet Rocket | secondary | Rocket | 3 | 30-32 | damage 70, loadingTimeMs 900, range 1200, projectileSpeed 14 |  |
| 33 | Amour Rocket | secondary | Rocket | 5 | 97-101 | damage 72, empDamage 34, loadingTimeMs 2000, range 1000, projectileSpeed 16 | yes |
| 34 | EMP Rocket Mk I | secondary | Rocket | 6 | 50-55 | damage 10, empDamage 45, loadingTimeMs 1000, range 1000, projectileSpeed 18 |  |
| 35 | EMP Rocket Mk II | secondary | Rocket | 7 | 65-65 | damage 30, empDamage 60, loadingTimeMs 1000, range 1000, projectileSpeed 20 |  |
| 36 | Edo | secondary | Missile | 1 | 90-95 | damage 70, loadingTimeMs 2000, range 6000, projectileSpeed 8 |  |
| 37 | Intelli Jet | secondary | Missile | 5 | 259-273 | damage 100, empDamage 50, loadingTimeMs 3000, range 8000, projectileSpeed 18 | yes |
| 38 | S'koonn | secondary | Missile | 3 | 170-187 | damage 140, loadingTimeMs 3000, range 8000, projectileSpeed 10 |  |
| 39 | Mamba EMP | secondary | Missile | 7 | 90-98 | damage 0, empDamage 100, loadingTimeMs 3000, range 10000, projectileSpeed 15 |  |
| 40 | Dephase EMP | secondary | Missile | 8 | 250-268 | damage 120, empDamage 100, loadingTimeMs 3000, range 10000, projectileSpeed 17 |  |
| 41 | EMP GL I | secondary | EMP bomb | 4 | 65-72 | damage 2, empDamage 80, loadingTimeMs 6000, range 6000, projectileSpeed 7, magnitude 22000, steerable 0 |  |
| 42 | EMP GL II | secondary | EMP bomb | 5 | 180-198 | damage 2, empDamage 150, loadingTimeMs 6500, range 6500, projectileSpeed 8, magnitude 30000, steerable 0 |  |
| 43 | EMP GL DX | secondary | EMP bomb | 6 | 330-360 | damage 4, empDamage 300, loadingTimeMs 5200, range 5200, projectileSpeed 10, magnitude 37000, steerable 0 |  |
| 44 | AMR Tormentor | secondary | Nuke | 4 | 800-855 | damage 150, loadingTimeMs 6000, range 6000, projectileSpeed 6, magnitude 20000, steerable 0 |  |
| 45 | AMR Oppressor | secondary | Nuke | 5 | 1200-1305 | damage 400, loadingTimeMs 6500, range 6500, projectileSpeed 7, magnitude 30000, steerable 0 |  |
| 46 | AMR Extinctor | secondary | Nuke | 9 | 4719-4968 | damage 700, loadingTimeMs 7000, range 7000, projectileSpeed 9, magnitude 40000, steerable 0 | yes |
| 47 | Hammerhead D1 | turret | Turret | 5 | 20000-22000 | damage 6, loadingTimeMs 300, range 2000, projectileSpeed 28, automatic 0, handling 40 |  |
| 48 | Hammerhead D2A2 | turret | Turret | 6 | 68000-73769 | damage 10, loadingTimeMs 280, range 2500, projectileSpeed 32, automatic 0, handling 60 |  |
| 49 | L'ksaar | turret | Turret | 6 | 140000-142942 | damage 12, loadingTimeMs 250, range 3000, projectileSpeed 32, automatic 0, handling 100 |  |
| 50 | Targe Shield | equipment | Shield | 2 | 3750-4125 | shieldCapacity 50, shieldRegenTime 20000, gammaShielding 0 |  |
| 51 | Riot Shield | equipment | Shield | 4 | 11500-12650 | shieldCapacity 80, shieldRegenTime 46000, gammaShielding 0 |  |
| 52 | H'Belam | equipment | Shield | 6 | 30750-33825 | shieldCapacity 120, shieldRegenTime 58000, gammaShielding 0 |  |
| 53 | Beamshield II | equipment | Shield | 8 | 89500-98450 | shieldCapacity 150, shieldRegenTime 45000, gammaShielding 0 |  |
| 54 | Fluxed Matter Shield | equipment | Shield | 10 | 263825-279957 | shieldCapacity 220, shieldRegenTime 60000, gammaShielding 0 | yes |
| 55 | E2 Exoclad | equipment | Armor | 2 | 2250-2475 | armor 40 |  |
| 56 | E4 Ultra Lamina | equipment | Armor | 4 | 9500-10450 | armor 80 |  |
| 57 | E6 D-X Plating | equipment | Armor | 6 | 41000-45100 | armor 110 |  |
| 58 | D'iol | equipment | Armor | 8 | 107500-118250 | armor 160 |  |
| 59 | T'yol | equipment | Armor | 10 | 169484-183378 | armor 250 | yes |
| 60 | AMR Saber | secondary | Mine | 3 | 190-209 | damage 350, loadingTimeMs 1000, range 40000, magnitude 1000 |  |
| 61 | Neétha EMP | secondary | Mine | 3 | 340-374 | empDamage 500, loadingTimeMs 3000, range 50000, magnitude 2200 |  |
| 62 | Ksann'k | secondary | Mine | 3 | 1400-1500 | damage 700, loadingTimeMs 4000, range 60000, magnitude 5000 |  |
| 63 | ZMI Optistore | equipment | Compression | 1 | 2100-2310 | cargoBonus 15 |  |
| 64 | Autopacker 2 | equipment | Compression | 3 | 4700-5170 | cargoBonus 25 |  |
| 65 | Ultracompact | equipment | Compression | 5 | 13000-14300 | cargoBonus 40 |  |
| 66 | Shrinker BT | equipment | Compression | 7 | 24800-27280 | cargoBonus 75 |  |
| 67 | Rhoda Blackhole | equipment | Compression | 10 | 104550-110700 | cargoBonus 100 | yes |
| 68 | AB-1 "Retractor" | equipment | Tractor beam | 6 | 150000-154500 | automatic_2 0, timeToLock 4000 |  |
| 69 | AB-2 "Glue Gun" | equipment | Tractor beam | 7 | 450000-459000 | automatic_2 0, timeToLock 1800 |  |
| 70 | AB-3 "Kingfisher" | equipment | Tractor beam | 8 | 1125000-1136250 | automatic_2 1, timeToLock 0 |  |
| 71 | Linear Boost | equipment | Booster | 4 | 4800-5280 | boostSpeed 60, boostRechargeMs 8000, boostDurationMs 3000 |  |
| 72 | Cyclotron Boost | equipment | Booster | 5 | 10400-11440 | boostSpeed 80, boostRechargeMs 10000, boostDurationMs 4400 |  |
| 73 | Synchrotron Boost | equipment | Booster | 7 | 20300-21444 | boostSpeed 160, boostRechargeMs 12000, boostDurationMs 5600 |  |
| 74 | Me'al | equipment | Booster | 7 | 38700-42570 | boostSpeed 200, boostRechargeMs 16000, boostDurationMs 6000 |  |
| 75 | Ketar Repair Bot | equipment | Repair bot | 4 | 36250-39875 |  |  |
| 76 | Static Thrust | equipment | Steering nozzle | 1 | 1200-1320 | agility 20 |  |
| 77 | Pendular Thrust | equipment | Steering nozzle | 3 | 2800-2824 | agility 40 |  |
| 78 | D'ozzt Thrust | equipment | Steering nozzle | 5 | 4700-5170 | agility 70 |  |
| 79 | Mp'zzzm Thrust | equipment | Steering nozzle | 7 | 16900-17907 | agility 100 |  |
| 80 | Pulsed Plasma Thrust | equipment | Steering nozzle | 8 | 28800-29707 | agility 130 |  |
| 81 | Telta Quickscan | equipment | Scanner | 2 | 460-506 | timeToLock_2 4000, showClassAAsteroids 0, radarShowsCargo 0 |  |
| 82 | Telta Ecoscan | equipment | Scanner | 3 | 7900-8256 | timeToLock_2 3000, showClassAAsteroids 0, radarShowsCargo 1 |  |
| 83 | Hiroto Proscan | equipment | Scanner | 6 | 33500-36850 | timeToLock_2 1800, showClassAAsteroids 0, radarShowsCargo 1 |  |
| 84 | Hiroto Ultrascan | equipment | Scanner | 7 | 175200-179204 | timeToLock_2 1800, showClassAAsteroids 1, radarShowsCargo 1 |  |
| 85 | Khador Drive | equipment | Jump drive | 10 | 624803-662923 | loadingSpeed_37 5000, energyConsumption 1 | yes |
| 86 | IMT Extract 1.3 | equipment | Mining laser | 2 | 480-528 | handling_2 30, miningYield 60 |  |
| 87 | IMT Extract 2.7 | equipment | Mining laser | 6 | 20280-22308 | handling_2 40, miningYield 60 |  |
| 88 | K'yuul | equipment | Mining laser | 7 | 40820-44902 | handling_2 50, miningYield 75 |  |
| 89 | IMT Extract 4.0X | equipment | Mining laser | 8 | 85020-93522 | handling_2 60, miningYield 80 |  |
| 90 | Gunant's Drill | equipment | Mining laser | 10 | 220426-236114 | handling_2 100, miningYield 100 | yes |
| 91 | Small Cabin | equipment | Cabin | 3 | 2600-2860 | cabinSize 3 |  |
| 92 | Medium Cabin | equipment | Cabin | 6 | 5900-6195 | cabinSize 5 |  |
| 93 | Large Cabin | equipment | Cabin | 7 | 12800-13440 | cabinSize 10 |  |
| 94 | Sight Suppressor II | equipment | Cloak | 6 | 61750-67925 | effect_35 20000, loadingSpeed_36 6500, energyConsumption 2 |  |
| 95 | U'tool | equipment | Cloak | 7 | 95000-104500 | effect_35 10000, loadingSpeed_36 2000, energyConsumption 1 |  |
| 96 | Yin Co. Shadow Ninja | equipment | Cloak | 10 | 273204-288941 | effect_35 40000, loadingSpeed_36 3500, energyConsumption 5 | yes |
| 97 | Drinking Water | commodity | Commodity | 1 | 10-11 |  |  |
| 98 | Medical Supplies | commodity | Commodity | 2 | 50-55 |  |  |
| 99 | Space Waste | commodity | Commodity | 1 | 5-6 |  |  |
| 100 | Artefacts | commodity | Commodity | 5 | 1200-1320 |  |  |
| 101 | Rare Animals | commodity | Commodity | 5 | 2700-2970 |  |  |
| 102 | Rare Plants | commodity | Commodity | 5 | 3800-4180 |  |  |
| 103 | Drugs | commodity | Commodity | 1 | 20-40 |  |  |
| 104 | Luxury | commodity | Commodity | 7 | 1070-1177 |  |  |
| 105 | Premium Food | commodity | Commodity | 8 | 30-40 |  |  |
| 106 | Basic Food | commodity | Commodity | 1 | 3-4 |  |  |
| 107 | Organs | commodity | Commodity | 5 | 4200-4620 |  |  |
| 108 | Vossk Organs | commodity | Commodity | 7 | 22000-24200 |  |  |
| 109 | Buskat | commodity | Commodity | 4 | 9000-9900 |  |  |
| 110 | Collectible Figure | commodity | Commodity | 5 | 100-1000 |  |  |
| 111 | Digital Watch | commodity | Commodity | 2 | 5-7 |  |  |
| 112 | Towels | commodity | Commodity | 2 | 5-7 |  |  |
| 113 | Implants | commodity | Commodity | 8 | 6450-7095 |  |  |
| 114 | Explosives | commodity | Commodity | 3 | 370-400 |  |  |
| 115 | Documents | commodity | Commodity | 2 | 1200-1320 |  |  |
| 116 | Secure Container | commodity | Commodity | 5 | 0-0 |  |  |
| 117 | Secure Cabin | commodity | Commodity | 5 | 0-0 |  |  |
| 118 | Electronics | commodity | Commodity | 2 | 400-420 |  |  |
| 119 | Chemicals | commodity | Commodity | 7 | 130-143 |  |  |
| 120 | Plastics | commodity | Commodity | 5 | 150-165 |  |  |
| 121 | Nanotech | commodity | Commodity | 7 | 875-963 |  |  |
| 122 | Energy Cells | commodity | Commodity | 2 | 1400-1540 |  |  |
| 123 | Biowaste | commodity | Commodity | 1 | 4-6 |  |  |
| 124 | Radioactive Goods | commodity | Commodity | 3 | 260-286 |  |  |
| 125 | Mechanical Supplies | commodity | Commodity | 4 | 450-495 |  |  |
| 126 | Noble Gas | commodity | Commodity | 2 | 360-396 |  |  |
| 127 | Microchips | commodity | Commodity | 7 | 163-179 |  |  |
| 128 | Com. Devices | commodity | Commodity | 5 | 75-83 |  |  |
| 129 | Optics | commodity | Commodity | 7 | 400-440 |  |  |
| 130 | Hydraulics | commodity | Commodity | 2 | 270-297 |  |  |
| 131 | Alien Remains | commodity | Commodity | 10 | 40-44 |  |  |
| 132 | Suteo Liqueur | commodity | Commodity | 1 | 139-153 |  |  |
| 133 | Pan Whiskey | commodity | Commodity | 1 | 489-538 |  |  |
| 134 | Behén Wine | commodity | Commodity | 1 | 789-868 |  |  |
| 135 | V'ikka Moonshine | commodity | Commodity | 1 | 237-261 |  |  |
| 136 | Eanya Tonic | commodity | Commodity | 1 | 211-232 |  |  |
| 137 | S'kloptorr Rum | commodity | Commodity | 1 | 245-270 |  |  |
| 138 | Wolf-Reiser Brandy | commodity | Commodity | 1 | 99-109 |  |  |
| 139 | Aquila Cocktail | commodity | Commodity | 1 | 320-352 |  |  |
| 140 | Buntta Apéritif | commodity | Commodity | 1 | 259-285 |  |  |
| 141 | Weymire Punch | commodity | Commodity | 1 | 1073-1180 |  |  |
| 142 | Y'mirr Schnaps | commodity | Commodity | 1 | 89-98 |  |  |
| 143 | Union Draught | commodity | Commodity | 1 | 105-116 |  |  |
| 144 | Oom'bak Gin | commodity | Commodity | 1 | 113-124 |  |  |
| 145 | Vulpes Soup | commodity | Commodity | 1 | 398-438 |  |  |
| 146 | Magnetar Juice | commodity | Commodity | 1 | 1100-1210 |  |  |
| 147 | Mido Distillate | commodity | Commodity | 1 | 453-498 |  |  |
| 148 | Prospero Flip | commodity | Commodity | 1 | 262-288 |  |  |
| 149 | Nesla Brandy | commodity | Commodity | 1 | 258-284 |  |  |
| 150 | Pescal Inartu Brew | commodity | Commodity | 1 | 829-912 |  |  |
| 151 | Augmenta Fizz | commodity | Commodity | 1 | 1125-1238 |  |  |
| 152 | K'ontrr Dishwater | commodity | Commodity | 1 | 578-636 |  |  |
| 153 | Ni'mrrod Muck | commodity | Commodity | 1 | 510-561 |  |  |
| 154 | Gold | commodity | Ore | 1 | 80-88 |  |  |
| 155 | Titanium | commodity | Ore | 1 | 150-165 |  |  |
| 156 | Iron | commodity | Ore | 1 | 10-40 |  |  |
| 157 | Orichalzine | commodity | Ore | 1 | 120-132 |  |  |
| 158 | Pyresium | commodity | Ore | 1 | 80-88 |  |  |
| 159 | Sodil | commodity | Ore | 1 | 70-77 |  |  |
| 160 | Doxtrite | commodity | Ore | 1 | 35-40 |  |  |
| 161 | Cesogen | commodity | Ore | 1 | 44-48 |  |  |
| 162 | Perrius | commodity | Ore | 1 | 38-42 |  |  |
| 163 | Hypanium | commodity | Ore | 1 | 70-77 |  |  |
| 164 | Void Crystals | commodity | Ore | 1 | 40-44 |  |  |
| 165 | Golden Core | commodity | Ore core | 3 | 414-455 |  |  |
| 166 | Titanium Core | commodity | Ore core | 3 | 800-880 |  |  |
| 167 | Iron Core | commodity | Ore core | 3 | 115-127 |  |  |
| 168 | Orichalzin Core | commodity | Ore core | 3 | 600-660 |  |  |
| 169 | Pyresium Core | commodity | Ore core | 3 | 460-506 |  |  |
| 170 | Sodil Core | commodity | Ore core | 3 | 414-455 |  |  |
| 171 | Doxtrit Core | commodity | Ore core | 3 | 218-240 |  |  |
| 172 | Cesogen Core | commodity | Ore core | 3 | 240-264 |  |  |
| 173 | Perrius Core | commodity | Ore core | 3 | 257-283 |  |  |
| 174 | Hypanium Core | commodity | Ore core | 3 | 380-418 |  |  |
| 175 | Void Essence | commodity | Ore core | 3 | 6500-6500 |  |  |
| 176 | Nirai .50AS | primary | Scatter gun | 6 | 37000-40700 | damage 13, loadingTimeMs 700, range 2000, projectileSpeed 22, magnitude 2800 |  |
| 177 | Berger FlaK 9-9 | primary | Scatter gun | 7 | 113100-124410 | damage 19, loadingTimeMs 750, range 2100, projectileSpeed 25, magnitude 4400 |  |
| 178 | Icarus Heavy AS | primary | Scatter gun | 9 | 407928-438997 | damage 30, loadingTimeMs 900, range 2600, projectileSpeed 28, magnitude 6300 | yes |
| 179 | Liberator | secondary | Nuke | 8 | 80232-85313 | damage 850, loadingTimeMs 10000, range 20000, projectileSpeed 10, magnitude 25000, steerable 1 | yes |
| 180 | Berger AGT 20mm | turret | Turret | 6 | 193500-212850 | damage 4, loadingTimeMs 100, range 2000, projectileSpeed 32, automatic 1, handling 100 |  |
| 181 | Skuld AT XR | turret | Turret | 6 | 338100-371910 | damage 9, loadingTimeMs 190, range 2500, projectileSpeed 32, automatic 1, handling 125 |  |
| 182 | HH-AT "Archimedes" | turret | Turret | 6 | 500144-532463 | damage 8, loadingTimeMs 150, range 3000, projectileSpeed 32, automatic 1, handling 150 | yes |
| 183 | Disruptor Laser | primary | Laser | 9 | 173833-186554 | damage 18, loadingTimeMs 300, range 2000, projectileSpeed 28 | yes |
| 184 | Rhoda Vortex | equipment | Time Extender | 9 | 312000-343200 | effect_42 15000, loadingSpeed_43 30000 |  |
| 185 | Emergency System | equipment | Emergency System | 6 | 7000-7700 | effect_41 10000 |  |
| 186 | Nirai Overdrive | equipment | Weapon Mod | 5 | 25000-27500 | fireRateFactor 20, damageFactor -10 |  |
| 187 | Nirai Overcharge | equipment | Weapon Mod | 5 | 25000-27500 | fireRateFactor -10, damageFactor 20 |  |
| 188 | Ketar Repair Bot II | equipment | Repair bot | 7 | 118000-129800 |  |  |
| 189 | Signature: Terran | equipment | Signature | 1 | 500000-500000 |  |  |
| 190 | Signature: Vossk | equipment | Signature | 1 | 500000-500000 |  |  |
| 191 | Signature: Nivelian | equipment | Signature | 1 | 500000-500000 |  |  |
| 192 | Signature: Midorian | equipment | Signature | 1 | 500000-500000 |  |  |
| 193 | SunFire o50 | primary | Thermo | 9 | 152500-167750 | damage 5, loadingTimeMs 120, range 2200, projectileSpeed 17 |  |
| 194 | AB-4 "Octopus" | equipment | Tractor beam | 10 | 3093750-3403125 | automatic_2 2, timeToLock 0 |  |
| 195 | Polytron Boost | equipment | Booster | 8 | 72500-79750 | boostSpeed 300, boostRechargeMs 16000, boostDurationMs 6000 |  |
| 196 | Spectral Filter SA-1 | equipment | Spectral filter | 9 | 2300-2530 | showInfo 0, showOnRadar_58 0 |  |
| 197 | Ion Lambda Mk1 | secondary | Ionizing missile | 4 | 4250-4675 | damage 5, empDamage 0, loadingTimeMs 6000, range 6000, projectileSpeed 6, magnitude 10000, steerable 0, effect_56 50 |  |
| 198 | PE Proton | turret | Plasma collector | 9 | 4100-4300 | damage 0, handling 100, speed_49 16, magnitude_50 40, range_51 27500 |  |
| 199 | PE Ambipolar-5 | turret | Plasma collector | 9 | 593750-599688 | damage 0, handling 150, speed_49 28, magnitude_50 80, range_51 50000 |  |
| 200 | PE Fusion H2 | turret | Plasma collector | 9 | 1131000-1142310 | damage 0, handling 200, speed_49 32, magnitude_50 100, range_51 60000 |  |
| 201 | Green Plasma | commodity | Plasma | 9 | 35-39 |  |  |
| 202 | Blue Plasma | commodity | Plasma | 9 | 40-44 |  |  |
| 203 | Purple Plasma | commodity | Plasma | 9 | 50-55 |  |  |
| 204 | Red Plasma | commodity | Plasma | 9 | 80-88 |  |  |
| 205 | Gamma Shield I | equipment | Gamma shield | 8 | 25000-27000 | gammaShielding 40 |  |
| 206 | Gamma Shield II | equipment | Gamma shield | 8 | 533256-586582 | gammaShielding 60 | yes |
| 207 | Nirai SPP-C1 | equipment | Repair beam | 8 | 6200-6700 | range_53 15000, effect_54 60, count 1 |  |
| 208 | Nirai SPP-M50 | equipment | Repair beam | 8 | 340000-360000 | range_53 60000, effect_54 100, count 3 |  |
| 209 | K'mirkk Toad Mutagen | commodity | Commodity | 10 | 500000-525000 |  |  |
| 210 | Chromo Plasma | commodity | Commodity | 10 | 775000-786625 |  | yes |
| 211 | Berger SG-100 | secondary | Sentry Gun | 9 | 640-704 | damage 9, loadingTimeMs 430, range 1000, projectileSpeed 22 |  |
| 212 | Berger SG-400 | secondary | Sentry Gun | 9 | 1200-1320 | damage 11, loadingTimeMs 300, range 1000, projectileSpeed 25 |  |
| 213 | T'Suum | secondary | Sentry Gun | 9 | 5100-5300 | damage 14, loadingTimeMs 250, range 1000, projectileSpeed 28 |  |
| 214 | Shesha | secondary | Cluster missile | 9 | 310-341 | damage 60, loadingTimeMs 3000, range 8000, projectileSpeed 8 |  |
| 215 | Garuda-IV | secondary | Cluster missile | 9 | 670-737 | damage 75, loadingTimeMs 3000, range 10000, projectileSpeed 9 |  |
| 216 | Patala | secondary | Cluster missile | 9 | 1300-1430 | damage 90, loadingTimeMs 3000, range 10000, projectileSpeed 10 |  |
| 217 | Novanium | commodity | Ore | 1 | 400-430 |  |  |
| 218 | Novanium Core | commodity | Ore core | 3 | 5000-5150 |  |  |
| 219 | Spectral Filter ST-X | equipment | Spectral filter | 9 | 462500-467125 | showInfo 1, showOnRadar_58 0 |  |
| 220 | Spectral Filter Omega | equipment | Spectral filter | 9 | 916500-925665 | showInfo 1, showOnRadar_58 1 |  |
| 221 | Ion Lambda Mk2 | secondary | Ionizing missile | 6 | 8128-8761 | damage 5, empDamage 0, loadingTimeMs 6000, range 6000, projectileSpeed 6, magnitude 15000, steerable 0, effect_56 100 | yes |
| 222 | Crimson Drain | equipment | Transfusion beam | 8 | 8400-8567 | range_53 20000, effect_54 50, count 1 |  |
| 223 | Pandora Leech | equipment | Transfusion beam | 8 | 508045-545097 | range_53 10000, effect_54 100, count 3 | yes |
| 224 | Matador TS | turret | Turret | 9 | 470500-476000 | damage 18, loadingTimeMs 200, range 3000, projectileSpeed 32, automatic 0, handling 130 |  |
| 225 | Particle Shield | equipment | Shield | 10 | 738210-780835 | shieldCapacity 380, shieldRegenTime 55000, gammaShielding 0 | yes |
| 226 | Shock Blast | secondary | Shock Blast | 9 | 7404-7905 | damage 140, empDamage 80, loadingTimeMs 7000, range 7000, projectileSpeed 0, magnitude 80000, steerable 0 | yes |
| 227 | Phoenix SIS | equipment | Shield Injector | 9 | 881346-900606 | plasmaConsumption 30 | yes |
| 228 | M6 A4 "Raccoon" | primary | Laser | 9 | 525000-542000 | damage 120, loadingTimeMs 1300, range 2600, projectileSpeed 22 |  |
| 229 | Dark Matter Laser | primary | Laser | 9 | 500000-515000 | damage 60, loadingTimeMs 680, range 2000, projectileSpeed 24 |  |
| 230 | Mass Driver MD 12 | primary | Auto-cannon | 9 | 405000-415000 | damage 7, loadingTimeMs 100, range 1500, projectileSpeed 30 |  |
| 231 | Mimung Blaster | primary | Blaster | 9 | 350000-370000 | damage 16, loadingTimeMs 230, range 1000, projectileSpeed 20 |  |
| 232 | Fireworks | secondary | Nuke | 1 | 1550000-1573250 | damage 1, loadingTimeMs 8000, range 8000, projectileSpeed 12, magnitude 20000, steerable 0 | yes |

## Ships

Armor = hull points. Handling: 100 = average. Slots = primary / secondary / turret / equipment. Ships 13, 14 and 15 are the freighters and the Terran battleship (not sold).

| # | Name | Race | Armor | Cargo t | Handling | Price | Slots |
|---|---|---|---|---|---|---|---|
| 0 | Betty | Midorian | 95 | 25 | 120 | 16200 | 1/1/0/3 |
| 1 | Teneta | Terran | 192 | 65 | 106 | 100320 | 2/4/1/7 |
| 2 | Hiro | Pirate | 160 | 52 | 150 | 32600 | 1/2/0/4 |
| 3 | Badger | Midorian | 135 | 55 | 112 | 39100 | 2/2/0/5 |
| 4 | Dace | Nivelian | 170 | 38 | 162 | 188480 | 4/1/0/5 |
| 5 | Inflict | Terran | 150 | 45 | 125 | 30900 | 2/1/0/4 |
| 6 | Hector | Midorian | 105 | 42 | 148 | 38400 | 2/1/0/5 |
| 7 | Anaan | Terran | 220 | 240 | 65 | 141520 | 2/1/1/7 |
| 8 | VoidX | Void | 450 | 30 | 155 | 4057950 | 4/4/0/15 |
| 9 | H'Soc | Vossk | 210 | 45 | 140 | 120000 | 2/2/0/7 |
| 10 | Phantom | Terran | 200 | 52 | 150 | 966667 | 2/1/0/9 |
| 11 | Hernstein | Pirate | 210 | 180 | 75 | 262240 | 2/2/1/8 |
| 12 | Type 43 | Nivelian | 175 | 30 | 132 | 58000 | 2/3/0/6 |
| 13 | Vossk freighter (not sold) | Terran | 100 | 50 | 50 | 19700 | 1/2/0/5 |
| 14 | Terran battleship (not sold) | Terran | 100 | 50 | 50 | 19700 | 1/2/0/5 |
| 15 | freighter (not sold) | Terran | 100 | 50 | 50 | 19700 | 1/2/0/5 |
| 16 | Kinzer | Nivelian | 180 | 45 | 120 | 1418733 | 2/4/0/10 |
| 17 | Ward | Terran | 145 | 65 | 95 | 1103200 | 4/2/0/10 |
| 18 | Hatsuyuki | Nivelian | 115 | 28 | 145 | 137520 | 2/2/0/8 |
| 19 | Nuyang II | Midorian | 225 | 105 | 90 | 626467 | 4/2/0/9 |
| 20 | Cicero | Midorian | 125 | 25 | 155 | 42000 | 2/1/0/6 |
| 21 | Aegir | Nivelian | 190 | 70 | 100 | 1808600 | 4/4/0/11 |
| 22 | Groza | Terran | 160 | 130 | 118 | 201280 | 3/3/0/8 |
| 23 | Azov | Pirate | 150 | 55 | 128 | 49520 | 2/2/1/5 |
| 24 | Velasco | Pirate | 170 | 95 | 125 | 456200 | 3/2/1/8 |
| 25 | Tyrion | Pirate | 155 | 52 | 145 | 253120 | 1/4/0/9 |
| 26 | Hera | Terran | 152 | 64 | 108 | 85600 | 2/2/0/7 |
| 27 | Taipan | Terran | 176 | 50 | 113 | 80080 | 3/2/0/5 |
| 28 | Veteran | Terran | 200 | 110 | 92 | 1658933 | 3/4/1/12 |
| 29 | Mantis | Pirate | 240 | 75 | 118 | 2363886 | 4/4/0/12 |
| 30 | Berger CrossXT | Midorian | 165 | 45 | 128 | 70560 | 2/2/0/6 |
| 31 | Salvéhn | Nivelian | 156 | 110 | 102 | 75600 | 2/1/1/6 |
| 32 | Wasp | Pirate | 100 | 30 | 160 | 19500 | 1/1/0/3 |
| 33 | Furious | Terran | 176 | 108 | 112 | 60640 | 1/2/1/6 |
| 34 | Razor 6 | Terran | 135 | 60 | 130 | 235920 | 4/1/0/6 |
| 35 | Night Owl | Nivelian | 125 | 40 | 150 | 26500 | 1/3/0/4 |
| 36 | Cormorant | Terran | 200 | 350 | 45 | 135120 | 0/4/1/8 |
| 37 | Cronus | Terran | 190 | 95 | 120 | 0 | 2/2/0/7 |
| 38 | Typhon | Terran | 175 | 40 | 145 | 1666667 | 4/0/0/12 |
| 39 | S'Kanarr | Vossk | 315 | 150 | 70 | 7250000 | 4/2/1/11 |
| 40 | Nemesis | Terran | 235 | 105 | 95 | 3400000 | 4/1/0/14 |
| 41 | K'Suukk | Vossk | 255 | 55 | 125 | 1300000 | 3/2/0/12 |
| 42 | Vol Noor | Vossk | 165 | 75 | 110 | 84000 | 2/2/0/7 |
| 43 | Wraith | Nivelian | 180 | 65 | 140 | 1166667 | 4/2/0/9 |
| 44 | Specter | Vossk | 800 | 65 | 138 | 12000000 | 4/2/0/16 |
| 45 | Bloodstar | Midorian | 460 | 180 | 88 | 5400000 | 4/4/1/14 |
| 46 | Blue Fyre | Midorian | 270 | 125 | 116 | 2571429 | 3/3/0/13 |
| 47 | Gator Custom | Midorian | 335 | 320 | 95 | 2971429 | 4/2/0/12 |
| 48 | Amboss | Midorian | 305 | 80 | 110 | 3400000 | 4/4/0/14 |
| 49 | Scimitar | Vossk | 400 | 40 | 105 | 3314286 | 3/2/0/15 |
| 50 | - | Vossk | 1000 | 50 | 50 | 8165900 | 1/2/0/5 |
| 51 | Rhino | Terran | 1200 | 480 | 30 | 466667 | 0/2/1/9 |
| 52 | Gryphon | Pirate | 220 | 90 | 130 | 1400000 | 4/2/0/10 |
| 53 | - | Vossk | 255 | 65 | 120 | 2250000 | 4/4/0/10 |
| 54 | Na'srrk | Vossk | 280 | 70 | 145 | 3085714 | 4/4/0/12 |
| 55 | Groza Mk II | Terran | 450 | 90 | 122 | 3565000 | 5/1/0/11 |
| 56 | Berger Cross Special | Midorian | 410 | 55 | 130 | 3705000 | 4/4/0/14 |
| 57 | Kinzer RS | Nivelian | 420 | 65 | 125 | 4465000 | 4/4/0/15 |
| 58 | Phantom XT | Terran | 425 | 60 | 150 | 3715000 | 4/1/0/14 |
| 59 | Teneta R.E.D. | Terran | 545 | 70 | 118 | 3805000 | 4/2/1/13 |
| 60 | Darkzov | Pirate | 420 | 70 | 130 | 3618000 | 4/1/1/14 |
| 61 | Ghost | Vossk | 530 | 50 | 135 | 3333333 | 4/2/0/14 |
| 62 | Dark Angel | Midorian | 350 | 85 | 125 | 3650000 | 4/2/1/14 |
| 63 | N'Tirrk | Vossk | 280 | 80 | 128 | 200000 | 2/4/0/8 |

## Systems

Security 0 lawless .. 3 secure. Visible = on the star map from a new game's start. Gates = the systems its jumpgate reaches.

| # | Name | Race | Security | Visible | Map position | Gates | Stations |
|---|---|---|---|---|---|---|---|
| 0 | Suteo | Nivelian | 1 |  | 22, 55, 55 | 2, 8 | 0, 1, 2, 3, 4 |
| 1 | Pan | Terran | 1 |  | 22, 81, 62 | 8, 18 | 5, 6, 7, 8, 9 |
| 2 | Behén | Nivelian | 1 | yes | 24, 40, 73 | 0, 8, 9, 17 | 30, 31, 32, 33 |
| 3 | V'ikka | Vossk | 2 | yes | 52, 61, 45 | 5, 8, 12, 14, 19 | 15, 16, 17, 18, 19 |
| 4 | Eanya | Midorian | 0 | yes | 15, 15, 90 | 17, 27 | 20, 21, 22, 23, 24 |
| 5 | S'kolptorr | Vossk | 1 | yes | 64, 67, 30 | 3, 12, 20 | 25, 26, 27, 28, 29 |
| 6 | Wolf-Reiser | Terran | 3 |  | 75, 20, 35 | 7 | 10 |
| 7 | Aquila | Terran | 1 | yes | 66, 15, 47 | 6, 11, 25 | 35, 36, 37, 38, 39 |
| 8 | Buntta | Terran | 2 | yes | 37, 63, 62 | 0, 1, 2, 3, 18, 19 | 40, 41, 42, 43, 44 |
| 9 | Weymire | Nivelian | 2 | yes | 39, 29, 63 | 2, 11, 17, 19, 26 | 45, 46, 47, 48, 49 |
| 10 | Y'mirr | Vossk | 0 |  | 81, 63, 60 |  | 50, 51, 52, 53, 54 |
| 11 | Union | Terran | 2 | yes | 55, 27, 59 | 7, 9, 14, 16, 25 | 55, 56, 57 |
| 12 | Oom'bak | Vossk | 2 | yes | 67, 55, 41 | 3, 5, 13, 14 | 60, 61 |
| 13 | Vulpes | Terran | 1 | yes | 78, 47, 55 | 12, 16 | 65, 66, 67, 68, 69 |
| 14 | Magnetar | Terran | 3 | yes | 57, 42, 39 | 3, 11, 12, 19 | 70, 71, 72, 73, 74 |
| 15 | Mido | Midorian | 0 | yes | 29, 10, 90 |  | 75, 76, 77, 78, 79 |
| 16 | Prospero | Terran | 1 | yes | 77, 32, 74 | 11, 13 | 80, 81, 82, 83, 84 |
| 17 | Nesla | Nivelian | 1 | yes | 26, 21, 79 | 2, 4, 9, 26, 29 | 85, 86, 87, 88, 89 |
| 18 | Pescal Inartu | Terran | 1 | yes | 43, 85, 81 | 1, 8 | 90, 91, 92, 93, 94 |
| 19 | Augmenta | Terran | 3 | yes | 44, 46, 50 | 3, 8, 9, 14 | 95, 96, 97, 98, 99 |
| 20 | K'ontrr | Vossk | 0 | yes | 77, 79, 20 | 5, 21, 30, 31 | 58, 59, 62, 63, 64 |
| 21 | Ni'mrrod | Vossk | 0 |  | 89, 84, 10 | 20, 30, 31 | 11, 12, 13, 14, 34 |
| 22 | Beidan | Terran | 3 |  | 92, 40, 32 |  | 100 |
| 23 | Herjaza | Terran | 1 |  | 59, 82, 56 |  | 101, 102 |
| 24 | Skavac | Terran | 0 |  | 68, 93, 69 |  | 103, 104 |
| 25 | Loma | Midorian | 0 |  | 43, 7, 70 | 11, 7, 26 | 105, 106, 107 |
| 26 | Shima | Midorian | 0 | yes | 36, 16, 50 | 17, 25, 9 | 108 |
| 27 | Ginoya  | Midorian | 0 |  | 18, 8, 90 | 28, 4 | 109, 110, 111, 112, 113 |
| 28 | Talidor | Midorian | 0 |  | 24, 7, 77 | 27 | 114, 115, 116, 117, 118 |
| 29 | Paréah | Nivelian | 2 |  | 18, 31, 58 | 17 | 119, 120, 121, 122, 123 |
| 30 | Me'enkk | Vossk | 0 |  | 82, 96, 69 | 20, 21 | 124, 125, 126, 127 |
| 31 | Wah'norr | Vossk | 0 |  | 94, 59, 50 | 20, 21 | 128, 129, 130, 131 |
| 32 | Skor Terpa | Terran | 0 |  | 50, 2, 65 |  | 132 |
| 33 | Alda | Terran | 0 |  | 53, 89, 70 |  | 133, 134 |

## Stations

78 Var Hastra (Mido) is where a new game starts. 108 is the Kaamo Club. Tech = which items its shop can sell.

| # | Name | System | Tech |
|---|---|---|---|
| 0 | Nehébru | 0 Suteo | 8 |
| 1 | Mahasa | 0 Suteo | 7 |
| 2 | Anesa | 0 Suteo | 10 |
| 3 | Géhlon | 0 Suteo | 9 |
| 4 | Posého | 0 Suteo | 10 |
| 5 | Decimus | 1 Pan | 9 |
| 6 | Emisto | 1 Pan | 9 |
| 7 | Binon | 1 Pan | 10 |
| 8 | Taygete | 1 Pan | 9 |
| 9 | Coppolite | 1 Pan | 8 |
| 10 | Thynome | 6 Wolf-Reiser | 10 |
| 11 | S'porrk | 21 Ni'mrrod | 3 |
| 12 | Ukka N'ima | 21 Ni'mrrod | 2 |
| 13 | Ekrr U'rra | 21 Ni'mrrod | 1 |
| 14 | K'ane | 21 Ni'mrrod | 3 |
| 15 | T'rry T'rry | 3 V'ikka | 3 |
| 16 | N'arrki | 3 V'ikka | 9 |
| 17 | Me'tokka | 3 V'ikka | 7 |
| 18 | P'errka | 3 V'ikka | 4 |
| 19 | Ma'rrkka | 3 V'ikka | 4 |
| 20 | Kallsta Omba | 4 Eanya | 5 |
| 21 | Euclades | 4 Eanya | 1 |
| 22 | Dekato | 4 Eanya | 9 |
| 23 | Psa Tori | 4 Eanya | 8 |
| 24 | Sao Noma | 4 Eanya | 8 |
| 25 | S'inokk | 5 S'kolptorr | 5 |
| 26 | F'err | 5 S'kolptorr | 10 |
| 27 | B'akka | 5 S'kolptorr | 4 |
| 28 | B'errokk | 5 S'kolptorr | 9 |
| 29 | Ga'kkrr | 5 S'kolptorr | 6 |
| 30 | Néhma | 2 Behén | 6 |
| 31 | Neso Aehma | 2 Behén | 4 |
| 32 | Duhnu | 2 Behén | 6 |
| 33 | Néh Suhnu | 2 Behén | 4 |
| 34 | Narsaxa | 21 Ni'mrrod | 3 |
| 35 | Io Ombak | 7 Aquila | 8 |
| 36 | Dendra Ekta | 7 Aquila | 3 |
| 37 | Carme | 7 Aquila | 10 |
| 38 | Nepis | 7 Aquila | 7 |
| 39 | Tsoj Delev | 7 Aquila | 7 |
| 40 | Valadon | 8 Buntta | 3 |
| 41 | Ko-on | 8 Buntta | 5 |
| 42 | Epigome | 8 Buntta | 8 |
| 43 | Arpalys | 8 Buntta | 4 |
| 44 | Isonoma | 8 Buntta | 9 |
| 45 | Éhna | 9 Weymire | 3 |
| 46 | Himo | 9 Weymire | 6 |
| 47 | Bosméh | 9 Weymire | 7 |
| 48 | Sahi | 9 Weymire | 8 |
| 49 | Siaméh | 9 Weymire | 1 |
| 50 | P'arrenkk | 10 Y'mirr | 10 |
| 51 | Rr'ostam | 10 Y'mirr | 10 |
| 52 | G'ukkion | 10 Y'mirr | 10 |
| 53 | Ba'rrtu | 10 Y'mirr | 9 |
| 54 | K'mirkk | 10 Y'mirr | 10 |
| 55 | Kappa | 11 Union | 5 |
| 56 | Suttnar | 11 Union | 2 |
| 57 | Tornard | 11 Union | 2 |
| 58 | B'akrram | 20 K'ontrr | 10 |
| 59 | E'kkide | 20 K'ontrr | 3 |
| 60 | Kkit S'ukk | 12 Oom'bak | 6 |
| 61 | L'ikirr | 12 Oom'bak | 6 |
| 62 | Makke S'ik | 20 K'ontrr | 9 |
| 63 | A'rk Oomk | 20 K'ontrr | 6 |
| 64 | Bak W'ok | 20 K'ontrr | 8 |
| 65 | Lopat | 13 Vulpes | 7 |
| 66 | Inari Onu | 13 Vulpes | 9 |
| 67 | Orcon | 13 Vulpes | 4 |
| 68 | Sorox | 13 Vulpes | 5 |
| 69 | Oni | 13 Vulpes | 6 |
| 70 | Dis | 14 Magnetar | 7 |
| 71 | Unotok | 14 Magnetar | 2 |
| 72 | Zepar | 14 Magnetar | 4 |
| 73 | Lon Nion | 14 Magnetar | 4 |
| 74 | Kanado | 14 Magnetar | 1 |
| 75 | Deuter IV | 15 Mido | 3 |
| 76 | Yrdal Gedal | 15 Mido | 3 |
| 77 | Heinsten | 15 Mido | 2 |
| 78 | Var Hastra | 15 Mido | 2 |
| 79 | Kernstal | 15 Mido | 3 |
| 80 | Kumppa | 16 Prospero | 1 |
| 81 | Teres | 16 Prospero | 4 |
| 82 | Plural Z | 16 Prospero | 8 |
| 83 | Marktesh | 16 Prospero | 7 |
| 84 | Urda Arcturius | 16 Prospero | 7 |
| 85 | Genoh | 17 Nesla | 10 |
| 86 | Hamina | 17 Nesla | 8 |
| 87 | Geséhn | 17 Nesla | 3 |
| 88 | Aoéh | 17 Nesla | 2 |
| 89 | Ahma Ésoh | 17 Nesla | 5 |
| 90 | Festus | 18 Pescal Inartu | 7 |
| 91 | Dima | 18 Pescal Inartu | 4 |
| 92 | Fejar | 18 Pescal Inartu | 2 |
| 93 | Maissa | 18 Pescal Inartu | 2 |
| 94 | Sobotnik | 18 Pescal Inartu | 5 |
| 95 | Gome C | 19 Augmenta | 1 |
| 96 | Kalun Amir | 19 Augmenta | 8 |
| 97 | Nyrand | 19 Augmenta | 6 |
| 98 | Alioth | 19 Augmenta | 6 |
| 99 | Damarque I | 19 Augmenta | 5 |
| 100 | Kothar | 22 Beidan | 10 |
| 101 | Valkyrie | 23 Herjaza | 8 |
| 102 | Scion | 23 Herjaza | 0 |
| 103 | Coromesk | 24 Skavac | 0 |
| 104 | Nosdron | 24 Skavac | 0 |
| 105 | Var Destro | 25 Loma | 10 |
| 106 | Sao Perula | 25 Loma | 10 |
| 107 | Quineros | 25 Loma | 10 |
| 108 | Kaamo | 26 Shima | 1 |
| 109 | Naneroh | 27 Ginoya  | 4 |
| 110 | Valpatro | 27 Ginoya  | 5 |
| 111 | Luur | 27 Ginoya  | 5 |
| 112 | Var Lupra | 27 Ginoya  | 3 |
| 113 | Tadram | 27 Ginoya  | 6 |
| 114 | Midantha | 28 Talidor | 4 |
| 115 | Tergalon | 28 Talidor | 4 |
| 116 | Psa Leri | 28 Talidor | 5 |
| 117 | Merlur | 28 Talidor | 2 |
| 118 | Sao Laros | 28 Talidor | 6 |
| 119 | Lan Manéh | 29 Paréah | 7 |
| 120 | Katashán | 29 Paréah | 6 |
| 121 | Névan | 29 Paréah | 7 |
| 122 | Okana | 29 Paréah | 9 |
| 123 | Shelén | 29 Paréah | 9 |
| 124 | Vu's | 30 Me'enkk | 6 |
| 125 | Bor M'okk | 30 Me'enkk | 9 |
| 126 | Bak S'ondorr | 30 Me'enkk | 6 |
| 127 | Kan'Tarr | 30 Me'enkk | 8 |
| 128 | M'Ankk | 31 Wah'norr | 6 |
| 129 | Va'lerrm | 31 Wah'norr | 9 |
| 130 | O'muarkk | 31 Wah'norr | 6 |
| 131 | Bra'Murr | 31 Wah'norr | 8 |
| 132 | Bervegor | 32 Skor Terpa | 0 |
| 133 | Merxde | 33 Alda | 0 |
| 134 | Selbah | 33 Alda | 0 |
