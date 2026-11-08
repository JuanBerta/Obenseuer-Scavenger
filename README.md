# Obenseuer - Scavenger

Version 0.6.0 (formerly **Scavenge Timer**). Adds a scavenging mode to the game's own time picker (WaitingUI), a stash to collect what you find, loot from container spawns, and a **Scavenging skill** that levels up as you scavenge.

## How to use

1. Press **F8** to open the game's time picker. Its header shows "Scavenging". The hour slider is shown even without an alarm clock, and starts at 1 hour. Use the left and right arrow keys to change it.
2. Pick a duration (10 minutes to `MaxHours`, default 12 hours) and press the picker's sleep button.
3. Wait. Time, needs and random events work as in the normal wait.
4. **ESC** stops the scavenge early. You keep the items found in the time that already passed.
5. When the scavenge ends, the stash opens. Press **F9** to open it at any time.

## Stash

- Items stay in the stash until you take them (**Take**, **Take page**, or **Take all**).
- **Shift + [** and **Shift + ]** change page. The stash has an unlimited number of pages.
- The stash resets at the start of each in-game day. A scavenge that crosses midnight keeps its items.
- The stash is saved next to the save files, so it survives quitting the game during the same day.

## Scavenging skill

You gain Scavenging XP by:

- **Timed scavenging**: XP per hour (`XpPerHour`) and per item found (`XpPerItem`).
- **Digging through real trash**: the first time each day you open a trash container (dumpster, trash bin, trash can, crate sack...), and every time you pick up an item from a trash pile.

Higher levels give:

- **More items** per hour of timed scavenging (+5% per level).
- **Better items**: a higher chance of the rare pool (+1.5% per level, up to 75%).
- **Bigger finds**: more multi-item finds (+1% per level) and, from level 10, larger stacks.
- **Scavenger instinct**: while searching trash in the world, a chance to find something extra (5% at level 1, +2% per level, up to 60%). Extra items appear in the container you opened, or in your inventory when you pick up from a trash pile.

The level and XP are shown in the time picker header ("Scavenging (Lv 4)"), in the stash title, and after each scavenge. Levels go up to `MaxLevel` (default 20); level L to L+1 needs `40 × L^1.6` XP. XP is saved in your save game with the stash.

## Loot

Items come from the container categories in `Categories` (config). They are the same data as the viewer's container spawns page. Each find rolls one category at random, then one item from its common pool, or its rare pool with probability `RareChance`.

Some finds are tenement upgrade materials (`TenementChance`, default 30%). These are the items the tenement upgrades in the viewer's `data/tenement.json` ask for, such as Plank, Cement, Plywood and ScrapMetal. The list is in `TenementItemIds`.

## Config

Config file: `BepInEx/config/JuanBerta.Obenseuer.Scavenger.cfg` (settings from the old `JuanBerta.Obenseuer.ScavengeTimer.cfg` are imported on first start).

| Key | Default | Meaning |
|---|---|---|
| OpenKey | F8 | Opens the time picker in scavenge mode |
| StashKey | F9 | Opens the stash |
| MaxHours | 12 | Longest scavenge, in hours (1-24) |
| ItemsPerHour | 4 | Average items per hour |
| RareChance | 0.2 | Chance of a rare-pool item |
| Categories | Dumpster,TrashBin,Trashcan,CrateSack,Cupboard,KitchenDrawer | Container categories to loot from |
| TenementChance | 0.3 | Chance that a find is a tenement upgrade material |
| MultiAmountChance | 0.3 | Chance that a find gives several of the same item |
| MaxAmountPerFind | 3 | Largest amount of one item a single find can give (2-10) |
| TenementItemIds | (38 item IDs) | Item IDs of tenement upgrade materials |
| Skill: Enabled | true | Turn the Scavenging skill on or off |
| Skill: MaxLevel | 20 | Highest level |
| Skill: XpMultiplier | 1 | Multiplies all Scavenging XP |
| Skill: XpPerHour / XpPerItem | 8 / 2 | XP from timed scavenging |
| Skill: XpPerTrashContainer / XpPerTrashPickup | 5 / 2 | XP from searching trash in the world |
| Skill: ItemsPerHourBonusPerLevel | 0.05 | Extra items per hour per level |
| Skill: RareChanceBonusPerLevel | 0.015 | Added rare chance per level |
| Skill: MultiAmountBonusPerLevel | 0.01 | Added multi-item chance per level |
| Skill: TrashProcChance / TrashProcChancePerLevel | 0.05 / 0.02 | Chance of an extra find in trash |
| Skill: TrashCategories | Dumpster,TrashBin,Trashcan,... | Categories that count as trash |

## Known limits

- ESC is also the game's menu key. While a scavenge is running, ESC stops it; otherwise it behaves as before.
- Shift+[ and Shift+] are US-layout keys. On other layouts, change `PreviousPageKey` and `NextPageKey` in the source.
- The picker's sleep button keeps the game's "Sleep" text. Only the header says "Scavenging". Renaming the button made its icon overlap the text.
- The stash UI uses IMGUI (not native game UI), because no native list was found.

## Upgrading from Scavenge Timer

Delete `BepInEx/plugins/Obenseuer-ScavengeTimer` (or its DLL) before installing `Obenseuer-Scavenger.dll`, so the two don't run together. Your settings and today's stash carry over.

## Build

Requires BepInEx 6 and the game's DLLs. Set `ObenseuerDir` if the game is not in the default Steam folder.
