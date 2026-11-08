# Obenseuer - Scavenger

Version 0.7.0 (formerly **Scavenge Timer**). Adds a **Scavenging skill**: search the city's trash to level it up, and the higher your level, the more, and better, stuff you find in it.

## How it works

- Open a trash container (dumpster, trash bin, trash can, crate sack...) for the **first time in the in-game day** and you get Scavenging XP (10 by default). Each container gives XP once per day; at midnight they all count again.
- **Scavenger instinct**: every time you search a trash container for the first time that day, you have a chance to dig deeper and find extra items in it. The extra items appear right in the container you opened.
- Press **F8** to see your level, XP and current instinct chances.

## What levels give

| | Level 1 | Per level | Cap |
|---|---|---|---|
| Instinct chance (extra find when searching trash) | 5% | +2.5% | 60% |
| Rare-pool chance of extra finds | 20% | +2% | 75% |
| Chance an extra find is several of the same item | 20% | +1.5% | 80% |
| Extra finds per instinct | up to 1 | +1 at levels 7, 14... | |
| Largest stack per find | 3 | +1 at levels 10, 20... | |

Levels go up to 20 (`MaxLevel`). Level L to L+1 needs `25 × L^1.5` XP: about 25 XP for level 2, 1,060 XP total for level 10.

XP is saved with your save game.

## Config

Config file: `BepInEx/config/JuanBerta.Obenseuer.Scavenger.cfg`

| Key | Default | Meaning |
|---|---|---|
| General: StatusKey | F8 | Shows your level and XP |
| Skill: Enabled | true | Turn the skill on or off |
| Skill: MaxLevel | 20 | Highest level |
| Skill: XpPerTrashContainer | 10 | XP for each trash container searched for the first time that day |
| Skill: XpMultiplier | 1 | Multiplies all Scavenging XP |
| Skill: ShowXpNotifications | true | A notification each time you gain XP |
| Skill: TrashCategories | Dumpster,TrashBin,Trashcan,Trash,... | Container categories that count as trash |
| Instinct: ChanceAtLevel1 / ChancePerLevel | 0.05 / 0.025 | Chance of an extra find |
| Instinct: RareChanceAtLevel1 / RareChancePerLevel | 0.2 / 0.02 | Rare-pool chance of extra finds |
| Instinct: MultiAmountChanceAtLevel1 / MultiAmountChancePerLevel | 0.2 / 0.015 | Chance an extra find is a stack |

## Upgrading from Scavenge Timer

- Delete `BepInEx/plugins/Obenseuer-ScavengeTimer` (or its DLL) so the two don't run together.
- The timed scavenging (F8 time picker) and the scavenge stash (F9) are gone. **Take everything out of the stash before updating**: its items are not kept.
- Skill XP is stored where the stash used to be saved, so nothing else needs to move.

## Known limits

- The game's skill list is built into its code, so Scavenging is not shown on the Character tab. Use F8 to check it.
- Extra items from instinct are added to the container you opened; if it's full, the extra find is lost.

## Build

Requires BepInEx 6 and the game's DLLs. Set `ObenseuerDir` if the game is not in the default Steam folder.
