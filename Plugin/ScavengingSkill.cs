using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace ScavengeTimerMod
{
	/// <summary>
	/// The Scavenging skill. Opening a trash container (dumpster, trash bin...) for the first time each
	/// in-game day gives XP. Higher levels make "Scavenger instinct" more likely: while searching trash you
	/// find extra items in it, more of them and more often from the rare pool.
	/// XP is saved with the save game (see ScavengeStorage).
	/// </summary>
	internal static class ScavengingSkill
	{
		internal static ConfigEntry<bool> Enabled;
		internal static ConfigEntry<float> XpPerTrashContainer;
		internal static ConfigEntry<float> XpMultiplier;
		internal static ConfigEntry<bool> ShowXpNotifications;
		internal static ConfigEntry<float> ProcBase;
		internal static ConfigEntry<float> ProcPerLevel;
		internal static ConfigEntry<float> RareBase;
		internal static ConfigEntry<float> RarePerLevel;
		internal static ConfigEntry<float> MultiBase;
		internal static ConfigEntry<float> MultiPerLevel;
		internal static ConfigEntry<string> TrashCategories;
		internal static ConfigEntry<bool> StateAffectsSkill;
		internal static ConfigEntry<float> MaxDrunkXpBonus;
		internal static ConfigEntry<bool> DrunkLowersInstinct;
		internal static ConfigEntry<bool> MushroomsAffectLevel;
		internal static ConfigEntry<bool> DrinkingSkillHelps;
		internal static ConfigEntry<bool> ContainerDifficulty;
		internal static ConfigEntry<bool> ShowDailyCount;
		internal static ConfigEntry<bool> GearAffectsInstinct;
		internal static ConfigEntry<string> GearBonuses;
		internal static ConfigEntry<bool> DarknessAffectsInstinct;
		internal static ConfigEntry<int> NightStartHour;
		internal static ConfigEntry<int> NightEndHour;
		internal static ConfigEntry<float> DarkInstinctFactor;
		internal static ConfigEntry<bool> DarknessOnlyOutdoors;
		internal static ConfigEntry<bool> TrashKing;
		internal static ConfigEntry<float> TrashKingChance;
		private static Dictionary<string, float> gear;
		internal static ConfigEntry<string> DifficultyFactors;
		private static Dictionary<string, float> difficulty;

		private static HashSet<string> trashSet;
		private static readonly HashSet<string> loggedCategories = new HashSet<string>();

		private static ConfigDescription D(string text, int order, AcceptableValueBase range = null, bool advanced = false)
		{
			return new ConfigDescription(text, range, new ConfigurationManagerAttributes { Order = order, IsAdvanced = advanced });
		}

		internal static void Bind(ConfigFile cfg)
		{
			// Skill
			Enabled = cfg.Bind("Skill", "Enabled", true, D("Turn the Scavenging skill on or off.", 100));
			XpPerTrashContainer = cfg.Bind("Skill", "XpPerTrashContainer", 20f, D("XP for each trash container you open for the first time in the in-game day. Like the game's skills, a level is 1000 XP, up to level 10.", 90, new AcceptableValueRange<float>(1f, 500f)));
			XpMultiplier = cfg.Bind("Skill", "XpMultiplier", 1f, D("Multiplies all Scavenging XP. Raise it for a faster grind.", 80, new AcceptableValueRange<float>(0.1f, 10f)));
			ShowXpNotifications = cfg.Bind("Skill", "ShowXpNotifications", true, D("Show the game's \"Skill increased!\" notification each time you gain Scavenging XP.", 70));
			ShowDailyCount = cfg.Bind("Skill", "ShowDailyCount", true, D("Show how many trash containers you have searched today, in the XP notification and on the Scavenging card.", 60));
			TrashCategories = cfg.Bind("Skill", "TrashCategories",
				"Dumpster,TrashBin,Trashcan,Trash,Trash Machineshop,Trash Organic,TrashBale,CrateSack,CrateSackMines",
				D("Container spawn categories that count as trash, comma separated.", 10, null, true));
			TrashCategories.SettingChanged += (s, e) => trashSet = null;

			// Instinct
			ProcBase = cfg.Bind("Instinct", "ChanceAtLevel1", 0.05f, D("Chance at level 1 to find something extra (Scavenger instinct) when you search a trash container.", 100, new AcceptableValueRange<float>(0f, 1f)));
			ProcPerLevel = cfg.Bind("Instinct", "ChancePerLevel", 0.05f, D("Added to the instinct chance per level above 1 (capped at 60%, plus gear).", 90, new AcceptableValueRange<float>(0f, 0.5f)));
			RareBase = cfg.Bind("Instinct", "RareChanceAtLevel1", 0.2f, D("Chance at level 1 that an extra find comes from the container's rare pool.", 80, new AcceptableValueRange<float>(0f, 1f)));
			RarePerLevel = cfg.Bind("Instinct", "RareChancePerLevel", 0.04f, D("Added to the rare chance per level above 1 (capped at 75%).", 70, new AcceptableValueRange<float>(0f, 0.5f)));
			MultiBase = cfg.Bind("Instinct", "MultiAmountChanceAtLevel1", 0.2f, D("Chance at level 1 that an extra find is several of the same item.", 60, new AcceptableValueRange<float>(0f, 1f)));
			MultiPerLevel = cfg.Bind("Instinct", "MultiAmountChancePerLevel", 0.03f, D("Added to the multi-amount chance per level above 1 (capped at 80%).", 50, new AcceptableValueRange<float>(0f, 0.5f)));

			// Gear and light
			GearAffectsInstinct = cfg.Bind("Gear", "GearAffectsInstinct", true, D("Equipped gear raises the instinct chance (see GearBonuses).", 100));
			GearBonuses = cfg.Bind("Gear", "GearBonuses", "Facemask:0.03,White Mask:0.03,Gas Mask:0.06,Crowbar:0.05",
				D("Item name:bonus pairs. The bonus is added to the instinct chance while the item is equipped or in your hand.", 90));
			GearBonuses.SettingChanged += (s, e) => gear = null;
			DarknessAffectsInstinct = cfg.Bind("Gear", "DarknessAffectsInstinct", true, D("Searching trash at night without a light (a flashlight that is on, or the Hard Hat with Headlamp) lowers the instinct chance.", 80));
			DarkInstinctFactor = cfg.Bind("Gear", "DarkInstinctFactor", 0.5f, D("Instinct chance multiplier when searching in the dark (0.5 = half).", 70, new AcceptableValueRange<float>(0f, 1f)));
			NightStartHour = cfg.Bind("Gear", "NightStartHour", 21, D("Hour when it gets dark.", 60, new AcceptableValueRange<int>(0, 23)));
			NightEndHour = cfg.Bind("Gear", "NightEndHour", 6, D("Hour when it gets light.", 50, new AcceptableValueRange<int>(0, 23)));
			DarknessOnlyOutdoors = cfg.Bind("Gear", "DarknessOnlyOutdoors", true, D("Only count darkness outdoors (buildings have their own lights).", 40));

			// State
			StateAffectsSkill = cfg.Bind("State", "StateAffectsSkill", true,
				D("Your state affects Scavenging the way it affects the game's own skills: alcohol and mushrooms. Hunger, thirst, sleep and health don't, as in the game.", 100));
			MaxDrunkXpBonus = cfg.Bind("State", "MaxDrunkXpBonus", 1f, D(
				"Extra XP when drunk, scaled by alcohol in blood (1 = up to +100% when completely drunk). In the game, alcohol raises the fail chance, and a higher fail chance gives more skill XP.",
				90, new AcceptableValueRange<float>(0f, 3f)));
			DrunkLowersInstinct = cfg.Bind("State", "DrunkLowersInstinct", true,
				D("Being drunk lowers the instinct chance (you miss things), like alcohol lowers the game's success chances.", 80));
			DrinkingSkillHelps = cfg.Bind("State", "DrinkingSkillHelps", true,
				D("Your Drinking skill makes the drunk instinct penalty smaller: -8% per Drinking level, down to 20% of the penalty (the game's own numbers for how much a drink affects you).", 70));
			MushroomsAffectLevel = cfg.Bind("State", "MushroomsAffectLevel", true,
				D("Mushroom effects change your effective Scavenging level for instinct, using the game's own formula for skills.", 60));

			// Difficulty
			ContainerDifficulty = cfg.Bind("Difficulty", "ContainerDifficulty", true,
				D("Bigger or messier trash gives more XP, smaller trash less (see DifficultyFactors).", 100));
			DifficultyFactors = cfg.Bind("Difficulty", "DifficultyFactors",
				"Dumpster:1.25,TrashBale:1.25,Trash Machineshop:1.25,CrateSackMines:1.25,CrateSack:1,TrashBin:1,Trash:1,Trashcan:0.75,Trash Organic:0.75",
				D("XP multiplier per container category, as Category:factor pairs. Categories not listed use 1.", 90));
			DifficultyFactors.SettingChanged += (s, e) => difficulty = null;

			// Level 10 perk
			TrashKing = cfg.Bind("Perk", "TrashKing", true, D("Level 10 perk: every first search of a trash container has a chance of an extra rare-pool find, on top of instinct.", 100));
			TrashKingChance = cfg.Bind("Perk", "TrashKingChance", 0.1f, D("Chance of the Trash King find.", 90, new AcceptableValueRange<float>(0f, 1f)));
		}

		// ---- XP and levels --------------------------------------------------------------------

		internal static float Xp
		{
			get { return ScavengerPlugin.Stash != null ? ScavengerPlugin.Stash.skillXp : 0f; }
			set { if (ScavengerPlugin.Stash != null) ScavengerPlugin.Stash.skillXp = Mathf.Max(0f, value); }
		}

		internal const int MaxLevel = 10;          // same as the game's skills

		// The skill on the game's scale: the whole part is the level, 1000 XP per level (1.562 = LVL 1, 1562 XP).
		// Like the game's skills it starts at 0 (not learned, hidden) and becomes LVL 1 with the first XP.
		internal static bool Learned { get { return Xp > 0f; } }
		internal static float SkillValue { get { return Learned ? Mathf.Min(MaxLevel, 1f + Xp / 1000f) : 0f; } }

		internal static int Level
		{
			get { return Enabled.Value ? Mathf.FloorToInt(SkillValue) : 0; }
		}

		internal static void AddXp(float amount) { AddXp(amount, null); }

		internal static void AddXp(float amount, string note)
		{
			if (!Enabled.Value || amount <= 0f || ScavengerPlugin.Stash == null || SkillValue >= MaxLevel) return;
			bool wasLearned = Learned;
			int before = Level;
			Xp += amount * XpMultiplier.Value;
			int after = Level;
			if (!wasLearned) GameUI.ShowNewSkill();
			else if (after > before) GameUI.ShowLevelUp(after);
			else if (ShowXpNotifications.Value) ScavengerPlugin.Notify("Skill increased!", string.IsNullOrEmpty(note) ? "Scavenging" : "Scavenging (" + note + ")");
		}

		// ---- Character state (same inputs the game uses for its skills) ------------------------------

		// Alcohol in blood, 0 to 100.
		internal static float Alcohol
		{
			get
			{
				if (!StateAffectsSkill.Value || PlayerStats.instance == null || PlayerStats.instance.alcohol == null) return 0f;
				return Mathf.Clamp(PlayerStats.instance.alcohol.AlcoholInBlood, 0f, 100f);
			}
		}

		// Recipe.CurrentFailChance multiplies the chance of success by (1 - alcohol / 100).
		internal static float Sober { get { return 1f - Alcohol / 100f; } }

		internal static int DrinkingLevel
		{
			get
			{
				try { return PlayerStats.instance != null ? PlayerStats.instance.GetSkill(PlayerStats.PlayerSkillType.Drinking) : 0; }
				catch (Exception) { return 0; }
			}
		}

		// PlayerStatsAlcohol.SetAlcohol: a drink's effect is scaled by 1 - Drinking * 0.08, never below 0.2.
		internal static float DrinkingTolerance
		{
			get { return DrinkingSkillHelps.Value ? Mathf.Clamp(1f - DrinkingLevel * 0.08f, 0.2f, 1f) : 1f; }
		}

		// What's left of the instinct chance after alcohol: the penalty shrinks with the Drinking skill.
		internal static float InstinctSober { get { return 1f - (Alcohol / 100f) * DrinkingTolerance; } }

		// PlayerStats.GetSkillWithEffects: (skill + MushroomsHigh / 10) * (1 - SMVProgression / 200), clamped.
		internal static float EffectiveLevel
		{
			get
			{
				float level = Level;
				if (StateAffectsSkill.Value && MushroomsAffectLevel.Value && PlayerStats.instance != null && PlayerStats.instance.mushrooms != null)
				{
					var m = PlayerStats.instance.mushrooms;
					level = (level + m.MushroomsHigh / 10f) * (1f - m.SMVProgression / 200f);
				}
				return Mathf.Clamp(level, 0f, MaxLevel);
			}
		}

		internal static float DrunkXpMultiplier { get { return 1f + MaxDrunkXpBonus.Value * Alcohol / 100f; } }

		internal static float DifficultyFactor(string category)
		{
			if (!ContainerDifficulty.Value || string.IsNullOrEmpty(category)) return 1f;
			if (difficulty == null)
			{
				difficulty = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
				foreach (string pair in DifficultyFactors.Value.Split(','))
				{
					int i = pair.LastIndexOf(':');
					float f;
					if (i > 0 && float.TryParse(pair.Substring(i + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f))
						difficulty[pair.Substring(0, i).Trim()] = Mathf.Max(0f, f);
				}
			}
			float v;
			return difficulty.TryGetValue(category.Trim(), out v) ? v : 1f;
		}

		// ---- Level bonuses (Scavenger instinct), from the effective level ------------------------------

		private static float Above1 { get { return Mathf.Max(0f, EffectiveLevel - 1f); } }
		// Instinct chance for a container in this scene: level, gear, darkness and alcohol.
		internal static float InstinctChance(string scene)
		{
			float chance = Mathf.Clamp(ProcBase.Value + ProcPerLevel.Value * Above1, 0f, 0.6f) + GearBonus();
			if (IsDark(scene)) chance *= DarkInstinctFactor.Value;
			if (DrunkLowersInstinct.Value) chance *= InstinctSober;
			return Mathf.Clamp(chance, 0f, 0.75f);
		}

		// ---- Gear and light ------------------------------------------------------------------

		private static IEnumerable<string> EquippedItemNames()
		{
			var inv = Inventory.instance;
			if (inv == null || inv.CharacterSlots == null) yield break;
			foreach (var sc in inv.CharacterSlots)
			{
				if (sc == null || sc.itemStack == null || sc.itemStack.itemId < 0 || sc.itemStack.itemAmount <= 0) continue;
				Item item = ItemDatabase.instance != null ? ItemDatabase.instance.FetchItemByID(sc.itemStack.itemId) : null;
				if (item != null && !string.IsNullOrEmpty(item.Title)) yield return item.Title;
			}
		}

		internal static float GearBonus()
		{
			if (!GearAffectsInstinct.Value) return 0f;
			if (gear == null)
			{
				gear = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
				foreach (string pair in GearBonuses.Value.Split(','))
				{
					int i = pair.LastIndexOf(':'); float f;
					if (i > 0 && float.TryParse(pair.Substring(i + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f))
						gear[pair.Substring(0, i).Trim()] = f;
				}
			}
			float total = 0f; float v;
			try { foreach (string name in EquippedItemNames()) if (gear.TryGetValue(name, out v)) total += v; }
			catch (Exception) { }
			return total;
		}

		internal static bool HasLight()
		{
			try
			{
				foreach (var f in UnityEngine.Object.FindObjectsByType<WieldableItem.Flashlight>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
					if (f != null && f.isActiveAndEnabled && Traverse.Create(f).Field("on").GetValue<bool>()) return true;
				foreach (string name in EquippedItemNames())
					if (name.IndexOf("Headlamp", StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			catch (Exception) { }
			return false;
		}

		internal static bool IsNight()
		{
			if (TimeOfDayAzure.instance == null || TimeOfDayAzure.instance.currentTimeAndDay == null) return false;
			float h = TimeOfDayAzure.instance.currentTimeAndDay.hours;
			int start = NightStartHour.Value, end = NightEndHour.Value;
			return start > end ? (h >= start || h < end) : (h >= start && h < end);
		}

		internal static bool IsDark(string scene)
		{
			if (!DarknessAffectsInstinct.Value || !IsNight()) return false;
			if (DarknessOnlyOutdoors.Value && (scene == null || !scene.StartsWith("Open Sewer"))) return false;
			return !HasLight();
		}
		internal static float RareChance { get { return Mathf.Clamp(RareBase.Value + RarePerLevel.Value * Above1, 0f, 0.75f); } }
		internal static float MultiChance { get { return Mathf.Clamp(MultiBase.Value + MultiPerLevel.Value * Above1, 0f, 0.8f); } }
		internal static int MaxFinds { get { return 1 + Mathf.FloorToInt(EffectiveLevel) / 4; } }              // up to 1 extra find, 2 at level 4, 3 at level 8
		internal static int MaxAmountPerFind { get { return 3 + Mathf.FloorToInt(EffectiveLevel) / 5; } }      // stacks of up to 3, 4 at level 5, 5 at level 10

		internal static bool IsTrash(string category)
		{
			if (string.IsNullOrEmpty(category)) return false;
			if (trashSet == null)
			{
				trashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string raw in TrashCategories.Value.Split(','))
					if (raw.Trim().Length > 0) trashSet.Add(raw.Trim());
			}
			return trashSet.Contains(category.Trim());
		}

		// A trash container was opened: XP the first time today, and maybe Scavenger instinct adds extra items to it.
		internal static void OnContainerOpened(Storage storage)
		{
			if (!Enabled.Value || storage == null || ScavengerPlugin.Stash == null) return;
			string cat = storage.itemSpawnCategory ?? "";
			if (loggedCategories.Add(cat))
				ScavengerPlugin.LogInfo("Opened a container with category '" + cat + "'" + (IsTrash(cat) ? " (trash: gives Scavenging XP)" : ""));
			if (!IsTrash(cat)) return;
			string sceneName = storage.gameObject.scene.name;
			Vector3 pos = storage.transform.position;
			if (!ScavengerPlugin.Stash.MarkSearched(sceneName, pos)) return;   // already searched today
			ScavengerApi.RaiseTrashSearched(sceneName, pos);

			bool high = StateAffectsSkill.Value && MushroomsAffectLevel.Value && Mathf.Abs(EffectiveLevel - Level) >= 0.5f;
			string scene = storage.gameObject.scene.name;
			bool dark = IsDark(scene);
			if (UnityEngine.Random.value < InstinctChance(scene)) Instinct(storage, high);
			if (TrashKing.Value && Level >= MaxLevel && UnityEngine.Random.value < TrashKingChance.Value) TrashKingFind(storage);

			float diff = DifficultyFactor(cat), drunk = DrunkXpMultiplier;
			var notes = new List<string>();
			if (Mathf.Abs(diff - 1f) > 0.01f) notes.Add((diff > 1f ? "+" : "") + Mathf.RoundToInt((diff - 1f) * 100f) + "% " + cat.ToLowerInvariant());
			if (drunk > 1.01f) notes.Add("drunk +" + Mathf.RoundToInt((drunk - 1f) * 100f) + "%");
			if (dark) notes.Add("too dark to see much");
			if (ShowDailyCount.Value) notes.Add(ScavengerPlugin.Stash.SearchedCount + " today");
			AddXp(XpPerTrashContainer.Value * diff * drunk, string.Join(", ", notes.ToArray()));
		}

		// Level 10 perk: a guaranteed rare-pool find on top of instinct.
		private static void TrashKingFind(Storage storage)
		{
			if (StorageSpawnCategoryDatabase.instance == null) return;
			StorageSpawnCategory cat = StorageSpawnCategoryDatabase.instance.FetchStorageSpawnCategory(storage.itemSpawnCategory);
			if (cat == null || (cat.ItemRare.Count == 0 && cat.ItemCommon.Count == 0)) return;
			Item item = cat.GetRandomItem(cat.ItemRare.Count > 0 ? 1f : 0f);
			if (item == null) return;
			if (storage.AddItemAndReturnRemaining(item, 1, -1, null) >= 1) return;
			try
			{
				if (Inventory.instance != null && Inventory.instance.storagePanelUI != null)
					Inventory.instance.storagePanelUI.UpdateStoragePanelUI(storage);
			}
			catch (Exception) { }
			ScavengerPlugin.Notify("Trash King!", "Your trained eye spots: " + item.Title);
		}

		private static void Instinct(Storage storage, bool affectedByMushrooms)
		{
			if (StorageSpawnCategoryDatabase.instance == null) return;
			StorageSpawnCategory cat = StorageSpawnCategoryDatabase.instance.FetchStorageSpawnCategory(storage.itemSpawnCategory);
			if (cat == null || (cat.ItemCommon.Count == 0 && cat.ItemRare.Count == 0)) return;

			int finds = UnityEngine.Random.Range(1, MaxFinds + 1);
			var names = new List<string>();
			for (int i = 0; i < finds; i++)
			{
				Item item = cat.GetRandomItem(RareChance);
				if (item == null) continue;
				int amount = 1;
				if (UnityEngine.Random.value < MultiChance)
					amount = UnityEngine.Random.Range(2, MaxAmountPerFind + 1);
				amount = Mathf.Min(amount, Mathf.Max(1, item.Stackable));
				int added = amount - storage.AddItemAndReturnRemaining(item, amount, -1, null);
				if (added > 0) names.Add(added > 1 ? item.Title + " x" + added : item.Title);
			}
			if (names.Count == 0) return;
			try
			{
				if (Inventory.instance != null && Inventory.instance.storagePanelUI != null)
					Inventory.instance.storagePanelUI.UpdateStoragePanelUI(storage);
			}
			catch (Exception) { }
			ScavengerPlugin.Notify("Scavenger instinct!", (affectedByMushrooms ? "With your mind wide open, you" : "You") + " dig deeper and find: " + string.Join(", ", names.ToArray()));
		}

		// The game's own skill UI: the Character tab card and the level-up popup.
		internal static class GameUI
		{
			private static readonly Dictionary<CharacterPanel, CharacterPanelSkill> cards = new Dictionary<CharacterPanel, CharacterPanelSkill>();
			private static bool hidEmptyText;

			// Add a Scavenging card from the game's skill prefab, after the game's own cards.
			internal static void AddCard(CharacterPanel panel)
			{
				if (panel == null || cards.ContainsKey(panel)) return;
				Transform prefab = Traverse.Create(panel).Field("skillPrefab").GetValue<Transform>();
				Transform list = Traverse.Create(panel).Field("skillList").GetValue<Transform>();
				if (prefab == null || list == null) return;
				Transform t = UnityEngine.Object.Instantiate(prefab, list);
				t.name = "Skill_Scavenging";
				CharacterPanelSkill card = t.GetComponent<CharacterPanelSkill>();
				if (card != null) cards[panel] = card;
			}

			internal static void UpdateCard(CharacterPanel panel)
			{
				CharacterPanelSkill card;
				if (panel == null || !cards.TryGetValue(panel, out card) || card == null) return;
				bool show = Enabled.Value && Learned;
				card.gameObject.SetActive(show);   // hidden until learned, like the game's skills

				// With no game skills learned yet, the game hides the whole list and shows "No learned skills".
				// If Scavenging is learned, show the list (with just Scavenging) instead.
				GameObject list = Traverse.Create(panel).Field("skillPanel").GetValue<GameObject>();
				Component empty = Traverse.Create(panel).Field("emptyPanelText").GetValue<Component>();
				if (show && list != null && !list.activeSelf)
				{
					list.SetActive(true);
					if (empty != null && empty.gameObject.activeSelf) { empty.gameObject.SetActive(false); hidEmptyText = true; }
				}
				else if (!show && hidEmptyText && empty != null)
				{
					empty.gameObject.SetActive(true);   // only undo what the mod hid
					hidEmptyText = false;
				}
				if (!show) return;
				var skill = new PlayerStats.PlayerSkill { Type = PlayerStats.PlayerSkillType.None, Skill = SkillValue };
				card.UpdateSkill(skill);
				SetText(Traverse.Create(card).Field("title").GetValue<Component>(), "Scavenging");
				if (ShowDailyCount.Value && ScavengerPlugin.Stash != null)
				{
					Component xp = Traverse.Create(card).Field("xp").GetValue<Component>();
					SetText(xp, Mathf.FloorToInt(SkillValue * 1000f) + " XP · " + ScavengerPlugin.Stash.SearchedCount + " today");
				}
				card.transform.SetAsLastSibling();
			}

			internal static void ShowNewSkill()
			{
				if (!ShowPopup("New skill\nScavenging\nlearned!"))
					ScavengerPlugin.Notify("New skill learned!", "Scavenging");
			}

			private static bool ShowPopup(string message)
			{
				try
				{
					if (PlayerSkillIncreaseUI.instance == null) return false;
					Traverse.Create(PlayerSkillIncreaseUI.instance).Method("ShowNotification", new object[] { message }).GetValue();
					return true;
				}
				catch (Exception) { return false; }
			}

			internal static void ShowLevelUp(int level)
			{
				try
				{
					if (PlayerSkillIncreaseUI.instance != null)
					{
						Traverse.Create(PlayerSkillIncreaseUI.instance).Method("ShowNotification", new object[] { "Scavenging\nincreased to\nlevel " + level + "!" }).GetValue();
						return;
					}
				}
				catch (Exception) { }
				ScavengerPlugin.Notify("Skill increased!", "Scavenging increased to level " + level + "!");
			}

			private static void SetText(Component tmp, string text)
			{
				if (tmp == null) return;
				var prop = tmp.GetType().GetProperty("text");
				if (prop != null) prop.SetValue(tmp, text, null);
			}
		}

		internal static class Hooks
		{
			[HarmonyPatch(typeof(CharacterPanel), "Init")]
			[HarmonyPostfix]
			private static void CharacterPanel_Init_Postfix(CharacterPanel __instance)
			{
				try { GameUI.AddCard(__instance); }
				catch (Exception e) { ScavengerPlugin.LogWarning("Scavenging card: " + e.Message); }
			}

			[HarmonyPatch(typeof(CharacterPanel), "UpdateSkills")]
			[HarmonyPostfix]
			private static void CharacterPanel_UpdateSkills_Postfix(CharacterPanel __instance)
			{
				try { GameUI.UpdateCard(__instance); }
				catch (Exception e) { ScavengerPlugin.LogWarning("Scavenging card: " + e.Message); }
			}

			// EnterExternally runs when a container's panel actually opens (after the lock check passes).
			[HarmonyPatch(typeof(Storage), "EnterExternally")]
			[HarmonyPostfix]
			private static void Storage_EnterExternally_Postfix(Storage __instance)
			{
				try { OnContainerOpened(__instance); }
				catch (Exception e) { ScavengerPlugin.LogWarning("Scavenging skill: " + e.Message); }
			}
		}
	}
}
