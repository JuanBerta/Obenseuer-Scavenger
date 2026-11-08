# Obenseuer - Scavenger

Version 0.8.2 (formerly **Scavenge Timer**). Adds a **Scavenging skill**: search the city's trash to level it up, and the higher your level, the more, and better, stuff you find in it.

## How it works

- Open a trash container (dumpster, trash bin, trash can, crate sack...) for the **first time in the in-game day** and you get Scavenging XP (20 by default). Each container gives XP once per day; at midnight they all count again.
- Like the game's skills, Scavenging starts unlearned and hidden. The first trash container you search shows the game's "New skill learned!" popup, and **Scavenging appears on the Character tab** with the game's other skills: same card, level, XP and 10-segment bar. Each level is 1000 XP, up to level 10.
- XP gains show the game's "Skill increased!" notification, and level-ups its "Scavenging increased to level N!" popup.
- **Scavenger instinct**: every time you search a trash container for the first time that day, you have a chance to dig deeper and find extra items in it. The extra items appear right in the container you opened.

## What levels give

| | Level 1 | Per level | Level 10 |
|---|---|---|---|
| Instinct chance (extra find when searching trash) | 5% | +5% | 50% |
| Rare-pool chance of extra finds | 20% | +4% | 56% |
| Chance an extra find is several of the same item | 20% | +3% | 47% |
| Extra finds per instinct | up to 1 | +1 at levels 4 and 8 | up to 3 |
| Largest stack per find | 3 | +1 at levels 5 and 10 | 5 |

XP is saved with your save game.

## Config

Config file: `BepInEx/config/JuanBerta.Obenseuer.Scavenger.cfg`

| Key | Default | Meaning |
|---|---|---|
| Skill: Enabled | true | Turn the skill on or off |
| Skill: XpPerTrashContainer | 20 | XP for each trash container searched for the first time that day (1000 XP per level) |
| Skill: XpMultiplier | 1 | Multiplies all Scavenging XP |
| Skill: ShowXpNotifications | true | The game's "Skill increased!" notification each time you gain XP |
| Skill: TrashCategories | Dumpster,TrashBin,Trashcan,Trash,... | Container categories that count as trash |
| Instinct: ChanceAtLevel1 / ChancePerLevel | 0.05 / 0.05 | Chance of an extra find |
| Instinct: RareChanceAtLevel1 / RareChancePerLevel | 0.2 / 0.04 | Rare-pool chance of extra finds |
| Instinct: MultiAmountChanceAtLevel1 / MultiAmountChancePerLevel | 0.2 / 0.03 | Chance an extra find is a stack |

## Upgrading from Scavenge Timer

- Delete `BepInEx/plugins/Obenseuer-ScavengeTimer` (or its DLL) so the two don't run together.
- The timed scavenging (F8 time picker) and the scavenge stash (F9) are gone. **Take everything out of the stash before updating**: its items are not kept.
- Skill XP is stored where the stash used to be saved, so nothing else needs to move.

## Known limits

- The game's skill list is built into its code, so Scavenging is added to the Character tab by the mod, after the game's own skills.
- Extra items from instinct are added to the container you opened; if it's full, the extra find is lost.

## Build

Requires BepInEx 6 and the game's DLLs. Set `ObenseuerDir` if the game is not in the default Steam folder.
