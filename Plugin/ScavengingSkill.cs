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
		internal static ConfigEntry<string> DifficultyFactors;
		private static Dictionary<string, float> difficulty;

		private static HashSet<string> trashSet;
		private static readonly HashSet<string> loggedCategories = new HashSet<string>();

		internal static void Bind(ConfigFile cfg)
		{
			Enabled = cfg.Bind("Skill", "Enabled", true, "Enable the Scavenging skill.");
			XpPerTrashContainer = cfg.Bind("Skill", "XpPerTrashContainer", 20f, "XP the first time each day you open a trash container (dumpster, trash bin...). Like the game's skills, a level is 1000 XP, up to level 10.");
			XpMultiplier = cfg.Bind("Skill", "XpMultiplier", 1f, new ConfigDescription("Multiplies all Scavenging XP.", new AcceptableValueRange<float>(0.1f, 10f)));
			ShowXpNotifications = cfg.Bind("Skill", "ShowXpNotifications", true, "Show the game's \"Skill increased!\" notification each time you gain Scavenging XP.");
			ProcBase = cfg.Bind("Instinct", "ChanceAtLevel1", 0.05f, "Chance at level 1 to find something extra when you search a trash container.");
			ProcPerLevel = cfg.Bind("Instinct", "ChancePerLevel", 0.05f, "Added to the chance per level above 1 (capped at 60%).");
			RareBase = cfg.Bind("Instinct", "RareChanceAtLevel1", 0.2f, "Chance at level 1 that an extra find comes from the container's rare pool.");
			RarePerLevel = cfg.Bind("Instinct", "RareChancePerLevel", 0.04f, "Added to the rare chance per level above 1 (capped at 75%).");
			MultiBase = cfg.Bind("Instinct", "MultiAmountChanceAtLevel1", 0.2f, "Chance at level 1 that an extra find is several of the same item.");
			MultiPerLevel = cfg.Bind("Instinct", "MultiAmountChancePerLevel", 0.03f, "Added to the multi-amount chance per level above 1 (capped at 80%).");
			TrashCategories = cfg.Bind("Skill", "TrashCategories",
				"Dumpster,TrashBin,Trashcan,Trash,Trash Machineshop,Trash Organic,TrashBale,CrateSack,CrateSackMines",
				"Container spawn categories that count as trash.");
			TrashCategories.SettingChanged += (s, e) => trashSet = null;

			StateAffectsSkill = cfg.Bind("State", "StateAffectsSkill", true,
				"Your state affects Scavenging the way it affects the game's own skills: alcohol and mushrooms. Hunger, thirst, sleep and health don't, as in the game.");
			MaxDrunkXpBonus = cfg.Bind("State", "MaxDrunkXpBonus", 1f, new ConfigDescription(
				"Extra XP when drunk, scaled by alcohol in blood (1 = up to +100% when completely drunk). In the game, alcohol raises the fail chance, and a higher fail chance gives more skill XP.",
				new AcceptableValueRange<float>(0f, 3f)));
			DrunkLowersInstinct = cfg.Bind("State", "DrunkLowersInstinct", true,
				"Being drunk lowers the Scavenger instinct chance (you miss things), like alcohol lowers the game's success chances.");
			DrinkingSkillHelps = cfg.Bind("State", "DrinkingSkillHelps", true,
				"Your Drinking skill makes the drunk instinct penalty smaller: -8% per Drinking level, down to 20% of the penalty (same numbers the game uses for how much a drink affects you).");
			MushroomsAffectLevel = cfg.Bind("State", "MushroomsAffectLevel", true,
				"Mushroom effects change your effective Scavenging level for instinct, using the game's own formula for skills.");
			ContainerDifficulty = cfg.Bind("Difficulty", "ContainerDifficulty", true,
				"Bigger or messier trash gives more XP, smaller trash less (see DifficultyFactors).");
			DifficultyFactors = cfg.Bind("Difficulty", "DifficultyFactors",
				"Dumpster:1.25,TrashBale:1.25,Trash Machineshop:1.25,CrateSackMines:1.25,CrateSack:1,TrashBin:1,Trash:1,Trashcan:0.75,Trash Organic:0.75",
				"XP multiplier per container category, as Category:factor pairs. Categories not listed use 1.");
			DifficultyFactors.SettingChanged += (s, e) => difficulty = null;
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
		internal static float ProcChance
		{
			get
			{
				float chance = Mathf.Clamp(ProcBase.Value + ProcPerLevel.Value * Above1, 0f, 0.6f);
				return DrunkLowersInstinct.Value ? chance * InstinctSober : chance;
			}
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
			string key = string.IsNullOrEmpty(storage.GUID) ? storage.gameObject.scene.name + "@" + storage.transform.position.ToString("F1") : storage.GUID;
			if (!ScavengerPlugin.Stash.MarkSearched(key)) return;   // already searched today

			bool high = StateAffectsSkill.Value && MushroomsAffectLevel.Value && Mathf.Abs(EffectiveLevel - Level) >= 0.5f;
			if (UnityEngine.Random.value < ProcChance) Instinct(storage, high);

			float diff = DifficultyFactor(cat), drunk = DrunkXpMultiplier;
			var notes = new List<string>();
			if (Mathf.Abs(diff - 1f) > 0.01f) notes.Add((diff > 1f ? "+" : "") + Mathf.RoundToInt((diff - 1f) * 100f) + "% " + cat.ToLowerInvariant());
			if (drunk > 1.01f) notes.Add("drunk +" + Mathf.RoundToInt((drunk - 1f) * 100f) + "%");
			AddXp(XpPerTrashContainer.Value * diff * drunk, string.Join(", ", notes.ToArray()));
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
				card.gameObject.SetActive(Enabled.Value && Learned);   // hidden until learned, like the game's skills
				if (!Enabled.Value || !Learned) return;
				var skill = new PlayerStats.PlayerSkill { Type = PlayerStats.PlayerSkillType.None, Skill = SkillValue };
				card.UpdateSkill(skill);
				SetText(Traverse.Create(card).Field("title").GetValue<Component>(), "Scavenging");
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
