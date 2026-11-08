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

		private static HashSet<string> trashSet;

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
		}

		// ---- XP and levels --------------------------------------------------------------------

		internal static float Xp
		{
			get { return ScavengerPlugin.Stash != null ? ScavengerPlugin.Stash.skillXp : 0f; }
			set { if (ScavengerPlugin.Stash != null) ScavengerPlugin.Stash.skillXp = Mathf.Max(0f, value); }
		}

		internal const int MaxLevel = 10;          // same as the game's skills

		// The skill on the game's scale: the whole part is the level, 1000 XP per level (1.562 = LVL 1, 1562 XP).
		internal static float SkillValue { get { return Mathf.Min(MaxLevel, 1f + Xp / 1000f); } }

		internal static int Level
		{
			get { return Enabled.Value ? Mathf.FloorToInt(SkillValue) : 1; }
		}

		internal static void AddXp(float amount)
		{
			if (!Enabled.Value || amount <= 0f || ScavengerPlugin.Stash == null || SkillValue >= MaxLevel) return;
			int before = Level;
			Xp += amount * XpMultiplier.Value;
			int after = Level;
			if (after > before) GameUI.ShowLevelUp(after);
			else if (ShowXpNotifications.Value) ScavengerPlugin.Notify("Skill increased!", "Scavenging");
		}

		// ---- Level bonuses (Scavenger instinct) ----------------------------------------------------

		private static int Above1 { get { return Mathf.Max(0, Level - 1); } }
		internal static float ProcChance { get { return Mathf.Clamp(ProcBase.Value + ProcPerLevel.Value * Above1, 0f, 0.6f); } }
		internal static float RareChance { get { return Mathf.Clamp(RareBase.Value + RarePerLevel.Value * Above1, 0f, 0.75f); } }
		internal static float MultiChance { get { return Mathf.Clamp(MultiBase.Value + MultiPerLevel.Value * Above1, 0f, 0.8f); } }
		internal static int MaxFinds { get { return 1 + Level / 4; } }              // up to 1 extra find, 2 at level 4, 3 at level 8
		internal static int MaxAmountPerFind { get { return 3 + Level / 5; } }      // stacks of up to 3, 4 at level 5, 5 at level 10

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
			if (Storage.currentStorage != storage || !IsTrash(storage.itemSpawnCategory)) return;
			string key = string.IsNullOrEmpty(storage.GUID) ? storage.gameObject.scene.name + "@" + storage.transform.position.ToString("F1") : storage.GUID;
			if (!ScavengerPlugin.Stash.MarkSearched(key)) return;   // already searched today

			if (UnityEngine.Random.value < ProcChance) Instinct(storage);
			AddXp(XpPerTrashContainer.Value);
		}

		private static void Instinct(Storage storage)
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
			ScavengerPlugin.Notify("Scavenger instinct!", "You dig deeper and find: " + string.Join(", ", names.ToArray()));
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
				card.gameObject.SetActive(Enabled.Value);
				if (!Enabled.Value) return;
				var skill = new PlayerStats.PlayerSkill { Type = PlayerStats.PlayerSkillType.None, Skill = SkillValue };
				card.UpdateSkill(skill);
				SetText(Traverse.Create(card).Field("title").GetValue<Component>(), "Scavenging");
				card.transform.SetAsLastSibling();
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

			[HarmonyPatch(typeof(Storage), nameof(Storage.Interact))]
			[HarmonyPostfix]
			private static void Storage_Interact_Postfix(Storage __instance)
			{
				try { OnContainerOpened(__instance); }
				catch (Exception e) { ScavengerPlugin.LogWarning("Scavenging skill: " + e.Message); }
			}
		}
	}
}
