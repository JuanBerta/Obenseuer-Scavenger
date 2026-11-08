# Changelog - Scavenger (formerly Scavenge Timer)

## 1.0.0
- Gear: a Facemask or White Mask (+3%), a Gas Mask (+6%) or a Crowbar in hand (+5%) raises the Scavenger instinct chance.
- Darkness: searching trash outdoors at night (21:00-06:00) without a light halves the instinct chance. A flashlight that is on, or the Hard Hat with Headlamp, counts as a light.
- Trash King (level 10 perk): each first search of a trash container has a 10% chance of a rare-pool find, on top of instinct.
- Daily progress: the XP notification and the Scavenging card show how many trash containers you searched today.
- Settings are ordered and described for ConfigurationManager.
- Loot Map support: the map can show which trash you haven't searched today (needs Loot Map 0.4.0).
- Fixed: searched containers are tracked by position, so containers the game spawns at runtime are remembered and can't give XP twice after a reload.
- Fixed: loading a save could reset the skill (the game sometimes stores the character as a full path).

## 0.9.1
- Fixed: a new game (or another character) inherited Scavenging XP from the previously played save. Each character now starts at 0, and saves without Scavenger data start at 0.
- Fixed: with no game skills learned yet, the Character tab showed "No learned skills" and hid Scavenging.

## 0.9.0
- Your state affects Scavenging like the game's own skills: being drunk gives up to +100% XP but lowers the Scavenger instinct chance; mushrooms change your effective level, with the game's formulas.
- The Drinking skill shrinks the drunk instinct penalty (-8% per level, down to 20%).
- Container difficulty (on by default): dumpsters and other big trash give more XP, small trash cans less.
- XP notifications show what changed the XP.

## 0.8.2
- Fixed: opening a trash container gave no XP. XP is now given when the container's panel actually opens (locked containers still don't count).

## 0.8.1
- Scavenging starts unlearned and hidden, like the game's skills. Your first trash container shows the game's "New skill learned!" popup.

## 0.8.0
- Scavenging appears on the Character tab with the game's own skill card (level, XP, 10-segment bar).
- Uses the game's skill scale: 1000 XP per level, up to level 10.
- XP gains use the game's "Skill increased!" notification; level-ups its "increased to level N!" popup.
- Removed the F8 status key.

## 0.7.0
- Skill only: timed scavenging (F8 time picker) and the scavenge stash (F9) were removed.
- Trash containers give XP the first time you open them each in-game day.
- Scavenger instinct: a level-based chance to find extra items in the trash you search, more often from the rare pool and in bigger stacks at higher levels.

## 0.6.0
- Renamed from Scavenge Timer to Scavenger and added the Scavenging skill.

## 0.5.3
- Scavenge Timer: timed scavenging through the game's time picker, with a stash for what you find.
