# Obenseuer - Scavenge Timer

Version 0.5.3. Adds a scavenging mode to the game's own time picker (WaitingUI), a stash to collect what you find, and loot from container spawns.

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

## Loot

Items come from the container categories in `Categories` (config). They are the same data as the viewer's container spawns page. Each find rolls one category at random, then one item from its common pool, or its rare pool with probability `RareChance`.

Some finds are tenement upgrade materials (`TenementChance`, default 30%). These are the items the tenement upgrades in the viewer's `data/tenement.json` ask for, such as Plank, Cement, Plywood and ScrapMetal. The list is in `TenementItemIds`.

## Config

Config file: `BepInEx/config/JuanBerta.Obenseuer.ScavengeTimer.cfg`

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

## Known limits

- ESC is also the game's menu key. While a scavenge is running, ESC stops it; otherwise it behaves as before.
- Shift+[ and Shift+] are US-layout keys. On other layouts, change `PreviousPageKey` and `NextPageKey` in the source.
- The picker's sleep button keeps the game's "Sleep" text. Only the header says "Scavenging". Renaming the button made its icon overlap the text.
- The stash UI uses IMGUI (not native game UI), because no native list was found.

## Build

Requires BepInEx 6 and the game's DLLs. Set `ObenseuerDir` if the game is not in the default Steam folder.
