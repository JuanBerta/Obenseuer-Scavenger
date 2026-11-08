using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using OS.Items;
using UnityEngine;

namespace ScavengeTimerMod
{
	/// <summary>
	/// The Scavenging skill. XP comes from timed scavenges and from digging through real trash
	/// (opening trash containers, picking up trash piles). Higher levels find more items, more rare
	/// items and bigger stacks, and get a chance to "proc" extra finds while searching trash in the world.
	/// XP is saved with the stash in the game's save.
	/// </summary>
	internal static class ScavengingSkill
	{
		internal static ConfigEntry<bool> Enabled;
		internal static ConfigEntry<int> MaxLevel;
		internal static ConfigEntry<float> XpMultiplier;
		internal static ConfigEntry<float> XpPerHour;
		internal static ConfigEntry<float> XpPerItem;
		internal static ConfigEntry<float> XpPerTrashContainer;
		internal static ConfigEntry<float> XpPerTrashPickup;
		internal static ConfigEntry<float> ItemsPerLevel;
		internal static ConfigEntry<float> RarePerLevel;
		internal static ConfigEntry<float> MultiPerLevel;
		internal static ConfigEntry<float> ProcBase;
		internal static ConfigEntry<float> ProcPerLevel;
		internal static ConfigEntry<string> TrashCategories;

		private static HashSet<string> trashSet;

		internal static void Bind(ConfigFile cfg)
		{
			Enabled = cfg.Bind("Skill", "Enabled", true, "Enable the Scavenging skill (XP, levels and their bonuses).");
			MaxLevel = cfg.Bind("Skill", "MaxLevel", 20, new ConfigDescription("Highest Scavenging level.", new AcceptableValueRange<int>(2, 50)));
			XpMultiplier = cfg.Bind("Skill", "XpMultiplier", 1f, new ConfigDescription("Multiplies all Scavenging XP.", new AcceptableValueRange<float>(0.1f, 10f)));
			XpPerHour = cfg.Bind("Skill", "XpPerHour", 8f, "XP per hour of timed scavenging.");
			XpPerItem = cfg.Bind("Skill", "XpPerItem", 2f, "XP per item found while timed scavenging or by a trash proc.");
			XpPerTrashContainer = cfg.Bind("Skill", "XpPerTrashContainer", 5f, "XP the first time each day you open a trash container (dumpster, trash bin...).");
			XpPerTrashPickup = cfg.Bind("Skill", "XpPerTrashPickup", 2f, "XP for picking up an item from a trash pile.");
			ItemsPerLevel = cfg.Bind("Skill", "ItemsPerHourBonusPerLevel", 0.05f, "Extra items per hour per level above 1 (0.05 = +5%).");
			RarePerLevel = cfg.Bind("Skill", "RareChanceBonusPerLevel", 0.015f, "Added to RareChance per level above 1.");
			MultiPerLevel = cfg.Bind("Skill", "MultiAmountBonusPerLevel", 0.01f, "Added to MultiAmountChance per level above 1.");
			ProcBase = cfg.Bind("Skill", "TrashProcChance", 0.05f, "Chance at level 1 to find something extra when searching trash in the world.");
			ProcPerLevel = cfg.Bind("Skill", "TrashProcChancePerLevel", 0.02f, "Added to the trash proc chance per level above 1.");
			TrashCategories = cfg.Bind("Skill", "TrashCategories",
				"Dumpster,TrashBin,Trashcan,Trash,Trash Machineshop,Trash Organic,TrashBale,CrateSack,CrateSackMines",
				"Container / pickup spawn categories that count as trash for the skill.");
			TrashCategories.SettingChanged += (s, e) => trashSet = null;
		}

		// ---- XP and levels --------------------------------------------------------------------

		internal static float Xp
		{
			get { return ScavengerPlugin.Stash != null ? ScavengerPlugin.Stash.skillXp : 0f; }
			set { if (ScavengerPlugin.Stash != null) ScavengerPlugin.Stash.skillXp = Mathf.Max(0f, value); }
		}

		// XP needed to go from level L to L+1.
		internal static float XpToNext(int level) { return Mathf.Round(40f * Mathf.Pow(level, 1.6f)); }

		internal static int Level
		{
			get
			{
				if (!Enabled.Value) return 1;
				float xp = Xp; int level = 1;
				while (level < MaxLevel.Value && xp >= XpToNext(level)) { xp -= XpToNext(level); level++; }
				return level;
			}
		}

		// XP gathered inside the current level, and the XP that level needs.
		internal static void Progress(out float inLevel, out float needed)
		{
			float xp = Xp; int level = 1;
			while (level < MaxLevel.Value && xp >= XpToNext(level)) { xp -= XpToNext(level); level++; }
			inLevel = xp; needed = level >= MaxLevel.Value ? 0f : XpToNext(level);
		}

		internal static string ShortStatus()
		{
			if (!Enabled.Value) return "";
			float inLevel, needed; Progress(out inLevel, out needed);
			return needed <= 0f ? "Scavenging Lv " + Level + " (max)"
				: "Scavenging Lv " + Level + " (" + Mathf.FloorToInt(inLevel) + "/" + Mathf.RoundToInt(needed) + " XP)";
		}

		internal static void AddXp(float amount, string reason)
		{
			if (!Enabled.Value || amount <= 0f || ScavengerPlugin.Stash == null) return;
			int before = Level;
			Xp += amount * XpMultiplier.Value;
			int after = Level;
			if (after > before)
				ScavengerPlugin.Notify("Scavenging skill reached level " + after + "! You find more, and better, stuff.");
		}

		// ---- Level bonuses ----------------------------------------------------------------------

		private static int Above1 { get { return Mathf.Max(0, Level - 1); } }
		internal static float ItemsPerHourMultiplier { get { return 1f + ItemsPerLevel.Value * Above1; } }
		internal static float RareChance(float baseChance) { return Mathf.Clamp01(Mathf.Min(0.75f, baseChance + RarePerLevel.Value * Above1)); }
		internal static float MultiChance(float baseChance) { return Mathf.Clamp01(Mathf.Min(0.8f, baseChance + MultiPerLevel.Value * Above1)); }
		internal static int ExtraMaxAmount { get { return Level / 10; } }
		internal static float ProcChance { get { return Mathf.Clamp01(Mathf.Min(0.6f, ProcBase.Value + ProcPerLevel.Value * Above1)); } }

		// ---- Scavenging in the world ----------------------------------------------------------------

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

		private static Item RollFrom(string category)
		{
			if (StorageSpawnCategoryDatabase.instance == null) return null;
			StorageSpawnCategory cat = StorageSpawnCategoryDatabase.instance.FetchStorageSpawnCategory(category);
			if (cat == null || (cat.ItemCommon.Count == 0 && cat.ItemRare.Count == 0)) return null;
			return cat.GetRandomItem(RareChance(ScavengerPlugin.RareChance.Value));
		}

		private static int RollAmount(Item item)
		{
			int amount = 1;
			if (UnityEngine.Random.value < MultiChance(ScavengerPlugin.MultiAmountChance.Value))
				amount = UnityEngine.Random.Range(2, ScavengerPlugin.MaxAmountPerFind.Value + ExtraMaxAmount + 1);
			return Mathf.Min(amount, Mathf.Max(1, item.Stackable));
		}

		// A trash container was opened: XP the first time today, and maybe a proc that adds extra items to it.
		internal static void OnContainerOpened(Storage storage)
		{
			if (!Enabled.Value || storage == null || ScavengerPlugin.Stash == null) return;
			if (Storage.currentStorage != storage || !IsTrash(storage.itemSpawnCategory)) return;
			string key = (string.IsNullOrEmpty(storage.GUID) ? storage.transform.position.ToString("F1") : storage.GUID);
			if (!ScavengerPlugin.Stash.MarkSearched(key)) return;   // already searched today

			AddXp(XpPerTrashContainer.Value, "trash container");
			if (UnityEngine.Random.value >= ProcChance) return;

			int finds = 1 + (UnityEngine.Random.value < Level / 40f ? 1 : 0);
			var names = new List<string>();
			for (int i = 0; i < finds; i++)
			{
				Item item = RollFrom(storage.itemSpawnCategory);
				if (item == null) continue;
				int amount = RollAmount(item);
				int left = storage.AddItemAndReturnRemaining(item, amount, -1, null);
				int added = amount - left;
				if (added <= 0) continue;
				names.Add(added > 1 ? item.Title + " x" + added : item.Title);
				AddXp(XpPerItem.Value * added, "trash proc");
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

		// An item was picked up from a trash pile: XP, and maybe a proc that gives an extra item.
		internal static void OnTrashPickup(string category)
		{
			if (!Enabled.Value || ScavengerPlugin.Stash == null || !IsTrash(category)) return;
			AddXp(XpPerTrashPickup.Value, "trash pickup");
			if (UnityEngine.Random.value >= ProcChance * 0.5f || Inventory.instance == null) return;
			Item item = RollFrom(category);
			if (item == null) return;
			int amount = RollAmount(item);
			Inventory.instance.AddItemDialogueAmountDrop(item.ID, amount);
			AddXp(XpPerItem.Value * amount, "trash proc");
			ScavengerPlugin.Notify("Scavenger instinct! Under the trash you also find: " + (amount > 1 ? item.Title + " x" + amount : item.Title));
		}

		// ---- Harmony hooks ------------------------------------------------------------------------

		internal static class Hooks
		{
			[HarmonyPatch(typeof(Storage), nameof(Storage.Interact))]
			[HarmonyPostfix]
			private static void Storage_Interact_Postfix(Storage __instance)
			{
				try { OnContainerOpened(__instance); }
				catch (Exception e) { ScavengerPlugin.LogWarning("Scavenging skill (container): " + e.Message); }
			}

			[HarmonyPatch(typeof(CollectibleItem), nameof(CollectibleItem.PerformPickup))]
			[HarmonyPrefix]
			private static void PerformPickup_Prefix(CollectibleItem __instance, out string __state)
			{
				__state = __instance != null && __instance.spawner != null ? __instance.spawner.itemSpawnCategory : null;
			}

			[HarmonyPatch(typeof(CollectibleItem), nameof(CollectibleItem.PerformPickup))]
			[HarmonyPostfix]
			private static void PerformPickup_Postfix(bool __result, string __state)
			{
				if (!__result || __state == null) return;
				try { OnTrashPickup(__state); }
				catch (Exception e) { ScavengerPlugin.LogWarning("Scavenging skill (pickup): " + e.Message); }
			}
		}
	}
}
