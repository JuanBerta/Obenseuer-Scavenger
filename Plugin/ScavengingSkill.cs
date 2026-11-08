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
		internal static ConfigEntry<int> MaxLevel;
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
			MaxLevel = cfg.Bind("Skill", "MaxLevel", 20, new ConfigDescription("Highest Scavenging level.", new AcceptableValueRange<int>(2, 50)));
			XpPerTrashContainer = cfg.Bind("Skill", "XpPerTrashContainer", 10f, "XP the first time each day you open a trash container (dumpster, trash bin...).");
			XpMultiplier = cfg.Bind("Skill", "XpMultiplier", 1f, new ConfigDescription("Multiplies all Scavenging XP.", new AcceptableValueRange<float>(0.1f, 10f)));
			ShowXpNotifications = cfg.Bind("Skill", "ShowXpNotifications", true, "Show a notification each time you gain Scavenging XP.");
			ProcBase = cfg.Bind("Instinct", "ChanceAtLevel1", 0.05f, "Chance at level 1 to find something extra when you search a trash container.");
			ProcPerLevel = cfg.Bind("Instinct", "ChancePerLevel", 0.025f, "Added to the chance per level above 1 (capped at 60%).");
			RareBase = cfg.Bind("Instinct", "RareChanceAtLevel1", 0.2f, "Chance at level 1 that an extra find comes from the container's rare pool.");
			RarePerLevel = cfg.Bind("Instinct", "RareChancePerLevel", 0.02f, "Added to the rare chance per level above 1 (capped at 75%).");
			MultiBase = cfg.Bind("Instinct", "MultiAmountChanceAtLevel1", 0.2f, "Chance at level 1 that an extra find is several of the same item.");
			MultiPerLevel = cfg.Bind("Instinct", "MultiAmountChancePerLevel", 0.015f, "Added to the multi-amount chance per level above 1 (capped at 80%).");
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

		// XP needed to go from level L to L+1.
		internal static float XpToNext(int level) { return Mathf.Round(25f * Mathf.Pow(level, 1.5f)); }

		private static int LevelAndRemainder(out float inLevel)
		{
			float xp = Xp; int level = 1;
			while (level < MaxLevel.Value && xp >= XpToNext(level)) { xp -= XpToNext(level); level++; }
			inLevel = xp;
			return level;
		}

		internal static int Level
		{
			get { float r; return Enabled.Value ? LevelAndRemainder(out r) : 1; }
		}

		internal static string ShortStatus()
		{
			float inLevel; int level = LevelAndRemainder(out inLevel);
			if (level >= MaxLevel.Value) return "Lv " + level + " (max)";
			return "Lv " + level + " (" + Mathf.FloorToInt(inLevel) + "/" + Mathf.RoundToInt(XpToNext(level)) + " XP)";
		}

		internal static string LongStatus()
		{
			if (!Enabled.Value) return "The Scavenging skill is turned off in the config.";
			float inLevel; int level = LevelAndRemainder(out inLevel);
			string next = level >= MaxLevel.Value ? "Max level reached."
				: Mathf.FloorToInt(inLevel) + "/" + Mathf.RoundToInt(XpToNext(level)) + " XP to level " + (level + 1) + ".";
			return "Scavenging level " + level + ". " + next + " Instinct: " + Mathf.RoundToInt(ProcChance * 100f) + "% chance, "
				+ Mathf.RoundToInt(RareChance * 100f) + "% rare, up to " + MaxFinds + " extra find(s).";
		}

		internal static void AddXp(float amount)
		{
			if (!Enabled.Value || amount <= 0f || ScavengerPlugin.Stash == null) return;
			int before = Level;
			float gained = amount * XpMultiplier.Value;
			Xp += gained;
			int after = Level;
			if (after > before)
				ScavengerPlugin.Notify("Scavenging skill reached level " + after + "! You find more, and better, stuff in the trash.");
			else if (ShowXpNotifications.Value)
				ScavengerPlugin.Notify("+" + Mathf.RoundToInt(gained) + " Scavenging XP · " + ShortStatus());
		}

		// ---- Level bonuses (Scavenger instinct) ----------------------------------------------------

		private static int Above1 { get { return Mathf.Max(0, Level - 1); } }
		internal static float ProcChance { get { return Mathf.Clamp(ProcBase.Value + ProcPerLevel.Value * Above1, 0f, 0.6f); } }
		internal static float RareChance { get { return Mathf.Clamp(RareBase.Value + RarePerLevel.Value * Above1, 0f, 0.75f); } }
		internal static float MultiChance { get { return Mathf.Clamp(MultiBase.Value + MultiPerLevel.Value * Above1, 0f, 0.8f); } }
		internal static int MaxFinds { get { return 1 + Level / 7; } }              // 1 extra find, 2 at level 7, 3 at 14...
		internal static int MaxAmountPerFind { get { return 3 + Level / 10; } }     // stacks of up to 3, 4 at level 10...

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
			ScavengerPlugin.Notify("Scavenger instinct! You dig deeper and find: " + string.Join(", ", names.ToArray()));
		}

		internal static class Hooks
		{
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
