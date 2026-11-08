using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using BepInEx.Unity.Mono.Configuration;
using HarmonyLib;
using OS.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScavengeTimerMod
{
	[BepInPlugin("JuanBerta.Obenseuer.Scavenger", "Scavenger", "0.6.0")]
	public class ScavengerPlugin : BaseUnityPlugin
	{
		private const float DefaultScavengeSeconds = 3600f;
		private const int StashBaseCapacity = 30;
		private const int StashPageSize = 88;
		private const string DefaultTenementItemIds =
			"10530,10540,10560,12700,12710,13450,13570,14140,14220,14230,14250,14380,14830,14860,15000,15010,15050,15780,16080,16690,16700,18180,19140,35010,35020,35040,35050,35060,35070,35080,35090,35100,35110,35120,35140,35150,35170,35180";

		internal static ConfigEntry<KeyboardShortcut> OpenKey;
		internal static ConfigEntry<KeyboardShortcut> StashKey;
		internal static ConfigEntry<KeyboardShortcut> NextPageKey;
		internal static ConfigEntry<KeyboardShortcut> PrevPageKey;
		internal static ConfigEntry<int> MaxHours;
		internal static ConfigEntry<float> ItemsPerHour;
		internal static ConfigEntry<float> RareChance;
		internal static ConfigEntry<string> Categories;
		internal static ConfigEntry<float> TenementChance;
		internal static ConfigEntry<float> MultiAmountChance;
		internal static ConfigEntry<int> MaxAmountPerFind;
		internal static ConfigEntry<string> TenementItemIds;

		private static ScavengerPlugin instance;
		internal static ScavengeStorage Stash { get { return stash; } }
		private static ScavengeStorage stash;
		private static bool pickerOpen;
		private static bool scavenging;
		private static bool stashOpen;

		private static readonly KeyboardShortcut EscapeKey = new KeyboardShortcut(KeyCode.Escape);

		private void Awake()
		{
			instance = this;
			MigrateOldConfig();
			OpenKey = Config.Bind("General", "OpenKey", new KeyboardShortcut(KeyCode.F8),
				"Opens the game's time picker in scavenge mode. Press again to close it.");
			StashKey = Config.Bind("General", "StashKey", new KeyboardShortcut(KeyCode.F9),
				"Opens the scavenge stash in the game's storage panel.");
			NextPageKey = Config.Bind("General", "NextPageKey", new KeyboardShortcut(KeyCode.RightBracket),
				"Next stash page. Shift + ] also works on non-US keyboards that type ].");
			PrevPageKey = Config.Bind("General", "PreviousPageKey", new KeyboardShortcut(KeyCode.LeftBracket),
				"Previous stash page. Shift + [ also works on non-US keyboards that type [.");
			MaxHours = Config.Bind("General", "MaxHours", 12,
				new ConfigDescription("Longest scavenge allowed, in hours.", new AcceptableValueRange<int>(1, 24)));
			ItemsPerHour = Config.Bind("Loot", "ItemsPerHour", 4f,
				new ConfigDescription("Average number of items found per hour of scavenging.", new AcceptableValueRange<float>(0.1f, 20f)));
			RareChance = Config.Bind("Loot", "RareChance", 0.2f,
				new ConfigDescription("Chance (0 to 1) that a container find comes from the category's rare pool.", new AcceptableValueRange<float>(0f, 1f)));
			Categories = Config.Bind("Loot", "Categories", "Dumpster,TrashBin,Trashcan,CrateSack,Cupboard,KitchenDrawer",
				"Container spawn categories to scavenge from, comma separated (names from StorageSpawnCategory.json).");
			TenementChance = Config.Bind("Loot", "TenementChance", 0.3f,
				new ConfigDescription("Chance (0 to 1) that a find is a tenement upgrade material instead of a container item.", new AcceptableValueRange<float>(0f, 1f)));
			MultiAmountChance = Config.Bind("Loot", "MultiAmountChance", 0.3f,
				new ConfigDescription("Chance (0 to 1) that a find gives several of the same item instead of one.", new AcceptableValueRange<float>(0f, 1f)));
			MaxAmountPerFind = Config.Bind("Loot", "MaxAmountPerFind", 3,
				new ConfigDescription("Largest amount of one item a single find can give.", new AcceptableValueRange<int>(2, 10)));
			TenementItemIds = Config.Bind("Loot", "TenementItemIds", DefaultTenementItemIds,
				"Item IDs used in tenement upgrades (from the viewer's data/tenement.json), comma separated.");

			ScavengingSkill.Bind(Config);

			SceneManager.sceneLoaded += OnSceneLoaded;
			EnsureStashExists();

			Harmony.CreateAndPatchAll(typeof(GameHooks));
			Harmony.CreateAndPatchAll(typeof(ScavengingSkill.Hooks));
			TimeOfDayAzure.DayChanged += OnDayChanged;
			SaveController.LoadingDone += OnGameLoaded;
			Logger.LogInfo("Scavenger loaded.");
		}

		// The mod used to be "Scavenge Timer". Keep the player's settings when upgrading.
		private void MigrateOldConfig()
		{
			try
			{
				string newPath = Config.ConfigFilePath;
				string oldPath = Path.Combine(Path.GetDirectoryName(newPath), "JuanBerta.Obenseuer.ScavengeTimer.cfg");
				if (!File.Exists(newPath) && File.Exists(oldPath))
				{
					File.Copy(oldPath, newPath);
					Config.Reload();
					Logger.LogInfo("Imported settings from Scavenge Timer.");
				}
			}
			catch (Exception e) { Logger.LogWarning("Could not import Scavenge Timer settings: " + e.Message); }
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			TimeOfDayAzure.DayChanged -= OnDayChanged;
			SaveController.LoadingDone -= OnGameLoaded;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			EnsureStashExists();
		}

		private void Update()
		{
			// The game can close its time picker by itself; stop picker mode then.
			if (pickerOpen && (WaitingUI.instance == null || !WaitingUI.instance.waitingUIisVisible))
			{
				pickerOpen = false;
			}

			EnsureStashExists();

			if (OpenKey.Value.IsDown())
			{
				if (pickerOpen) ClosePicker();
				else OpenPicker();
			}

			if (StashKey.Value.IsDown())
			{
				if (stashOpen) CloseStash();
				else OpenStash();
			}

			if (EscapeKey.IsDown())
			{
				if (scavenging)
				{
					// Stop early. The game's wait then calls OnScavengeFinished with the time that passed.
					WaitingController.instance.Wakeup();
				}
				else if (pickerOpen)
				{
					ClosePicker();
				}
			}

			if (stashOpen)
			{
				UpdateOpenStash();
			}
		}

		// ---- Time picker -------------------------------------------------------------------

		private static void OpenPicker()
		{
			string blocked = BlockReason();
			if (blocked != null)
			{
				Notify(blocked);
				return;
			}
			if (WaitingUI.instance == null)
			{
				Notify("The time picker is not available here.");
				return;
			}
			pickerOpen = true;
			WaitingUI.instance.ShowClock();
		}

		private static void ClosePicker()
		{
			pickerOpen = false;
			if (WaitingUI.instance != null)
			{
				WaitingUI.instance.CloseTimeSetUI();
			}
		}

		// Called when the picker's Sleep button is pressed in scavenge mode.
		internal static void StartScavengeFromPicker()
		{
			int seconds = WaitingUI.instance.CurrentSleepSetTime;
			if (seconds < 600)
			{
				Notify("Scavenging needs at least 10 minutes.");
				return;
			}
			if (seconds > MaxHours.Value * 3600)
			{
				Notify("Scavenging can be at most " + MaxHours.Value + " hours.");
				return;
			}
			string blocked = BlockReason();
			if (blocked != null)
			{
				Notify(blocked);
				return;
			}
			if (GetCategories().Count == 0 && GetTenementItemIds().Count == 0)
			{
				Notify("No valid loot. Check Categories and TenementItemIds in the config file.");
				return;
			}

			pickerOpen = false;
			scavenging = true;
			WaitingController.instance.SetTimeWaitedCallback(OnScavengeFinished);
			WaitingController.instance.Wait(seconds, WaitingController.Type.Waiting);
		}

		// Returns why scavenging can't start right now, or null if it can.
		private static string BlockReason()
		{
			if (PlayerStats.instance == null || WaitingController.instance == null || GameController.instance == null)
			{
				return "You need to be in the game to scavenge.";
			}
			if (WaitingController.instance.waitingType != WaitingController.Type.None)
			{
				return "You are already busy.";
			}
			if (WaitingController.WaitingBlocked || !WaitingController.instance.CanStartWaitingStaus)
			{
				return "You can't wait right now.";
			}
			PlayerStatsTiredness.Reason reason = PlayerStats.instance.tiredness.CanSleep(waiting: true);
			if (reason != PlayerStatsTiredness.Reason.None)
			{
				return PlayerStatsTiredness.CantSleepReason(reason);
			}
			return null;
		}

		// ---- Finding items -----------------------------------------------------------------

		private static void OnScavengeFinished(int secondsWaited)
		{
			scavenging = false;
			float hours = secondsWaited / 3600f;
			float expected = hours * ItemsPerHour.Value * ScavengingSkill.ItemsPerHourMultiplier;
			int rolls = Mathf.FloorToInt(expected);
			if (UnityEngine.Random.value < expected - rolls)
			{
				rolls++;
			}

			EnsureStashExists();
			stash.dayKey = CurrentDayKey();

			List<StorageSpawnCategory> categories = GetCategories();
			List<int> tenementIds = GetTenementItemIds();
			int found = 0;
			for (int i = 0; i < rolls; i++)
			{
				Item item = null;
				bool useTenement = tenementIds.Count > 0
					&& (categories.Count == 0 || UnityEngine.Random.value < TenementChance.Value);
				if (useTenement)
				{
					int id = tenementIds[UnityEngine.Random.Range(0, tenementIds.Count)];
					item = ItemDatabase.instance.FetchItemByID(id);
				}
				else if (categories.Count > 0)
				{
					StorageSpawnCategory category = categories[UnityEngine.Random.Range(0, categories.Count)];
					item = category.GetRandomItem(ScavengingSkill.RareChance(RareChance.Value));
				}
				if (item == null)
				{
					continue;
				}
				int amount = 1;
				if (UnityEngine.Random.value < ScavengingSkill.MultiChance(MultiAmountChance.Value))
				{
					amount = UnityEngine.Random.Range(2, MaxAmountPerFind.Value + ScavengingSkill.ExtraMaxAmount + 1);
				}
				stash.AddItems(item.ID, amount, item.Stackable);
				found += amount;
			}

			ScavengingSkill.AddXp(hours * ScavengingSkill.XpPerHour.Value + found * ScavengingSkill.XpPerItem.Value, "timed scavenge");

			string timeText = (secondsWaited / 60).ToString(CultureInfo.InvariantCulture) + " min";
			string skill = ScavengingSkill.Enabled.Value ? " " + ScavengingSkill.ShortStatus() + "." : "";
			Notify(found > 0
				? "Scavenging finished after " + timeText + ". Found " + found + " item(s). Take them from the stash (F9)." + skill
				: "Scavenging finished after " + timeText + ". Found nothing this time." + skill);

			OpenStash();
		}

		// Item IDs from the tenement upgrade list that exist in the game's item database.
		private static List<int> GetTenementItemIds()
		{
			var ids = new List<int>();
			if (TenementItemIds == null || string.IsNullOrEmpty(TenementItemIds.Value) || ItemDatabase.instance == null)
			{
				return ids;
			}
			foreach (string raw in TenementItemIds.Value.Split(','))
			{
				int id;
				if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
					&& ItemDatabase.instance.FetchItemByID(id) != null)
				{
					ids.Add(id);
				}
			}
			return ids;
		}

		private static List<StorageSpawnCategory> GetCategories()
		{
			var result = new List<StorageSpawnCategory>();
			if (StorageSpawnCategoryDatabase.instance == null || string.IsNullOrEmpty(Categories.Value))
			{
				return result;
			}
			foreach (string raw in Categories.Value.Split(','))
			{
				StorageSpawnCategory category = StorageSpawnCategoryDatabase.instance.FetchStorageSpawnCategory(raw.Trim());
				if (category != null && (category.ItemCommon.Count > 0 || category.ItemRare.Count > 0))
				{
					result.Add(category);
				}
			}
			return result;
		}

		// ---- Day and save ------------------------------------------------------------------

		private static string CurrentDayKey()
		{
			if (TimeOfDayAzure.instance == null)
			{
				return "";
			}
			TimeAndDay now = TimeOfDayAzure.instance.currentTimeAndDay;
			return now.year + "-" + now.month + "-" + now.day;
		}

		// The stash is emptied when a new in-game day starts. Items found after midnight are added later and stay.
		private static void OnDayChanged(int day)
		{
			if (stash == null)
			{
				return;
			}
			if (stashOpen)
			{
				CloseStash();
			}
			stash.ClearAll();
			stash.ClearSearched();
			stash.dayKey = CurrentDayKey();
		}

		// After a save is loaded: keep the stash only if it belongs to the current day.
		private static void OnGameLoaded()
		{
			EnsureStashExists();
			if (stash.dayKey != CurrentDayKey())
			{
				stash.ClearAll();
				stash.ClearSearched();
				stash.dayKey = CurrentDayKey();
			}
		}

		// The stash is a DontDestroyOnLoad object registered with the save system, so it is saved in Globals.tnmt.
		private static void EnsureStashExists()
		{
			if (stash == null)
			{
				GameObject go = new GameObject("ScavengeTimer_Stash");
				go.SetActive(false); // stays inactive until the world exists
				stash = go.AddComponent<ScavengeStorage>();
				stash.Setup(StashBaseCapacity, StashPageSize);
				UnityEngine.Object.DontDestroyOnLoad(go);
				SaveController.DontDestroyOnLoadObjects.Add(go);
			}

			if (!stash.gameObject.activeSelf && Inventory.instance != null)
			{
				stash.gameObject.SetActive(true);
			}
		}

		// ---- Stash UI (the game's storage panel) ------------------------------------------

		private static void OpenStash()
		{
			if (stashOpen)
			{
				return;
			}
			if (Inventory.instance == null || GameUIController.instance == null
				|| Inventory.instance.inventoryPanelUI == null || Inventory.instance.storagePanelUI == null)
			{
				Notify("The stash can only be opened in the game.");
				return;
			}
			if (GameUIController.instance.IsAnyMenuOpen())
			{
				Notify("Close the open menu first, then press F9 to open the stash.");
				return;
			}

			EnsureStashExists();
			stashOpen = true;
			stash.CurrentPage = 0;
			GameUIController.instance.ShowGameMenu(false, true, MenuPanels.Storage, MenuPageNames.Inventory);
			Inventory.instance.ForeignSlots = stash.Slots;
			BindAllSlots();
			ApplyPageVisibility();
			UpdateTitle();
		}

		private static void CloseStash()
		{
			if (!stashOpen)
			{
				return;
			}
			stashOpen = false;
			UnbindAllSlots();

			if (Inventory.instance != null && stash != null && Inventory.instance.ForeignSlots == stash.Slots)
			{
				Inventory.instance.ForeignSlots = null;
			}
			if (GameUIController.instance != null)
			{
				GameUIController.instance.HideGameMenu();
			}
		}

		private static void UpdateOpenStash()
		{
			if (Inventory.instance == null || GameUIController.instance == null || stash == null)
			{
				return;
			}

			DetectPageKeys();

			// Infinite storage: when the stash is full, add a page and show it.
			if (stash.IsStorageFull())
			{
				stash.ExpandByPage(StashPageSize);
				BindAllSlots();
				Inventory.instance.ForeignSlots = stash.Slots;
				stash.CurrentPage = stash.TotalPages - 1;
				ApplyPageVisibility();
				UpdateTitle();
			}

			// The player closed the storage panel (Esc or another game flow).
			if (Inventory.instance.storagePanelUI != null
				&& !Inventory.instance.storagePanelUI.gameObject.activeInHierarchy)
			{
				CloseStash();
			}
		}

		private static void ChangePage(int delta)
		{
			int total = stash.TotalPages;
			if (total <= 1)
			{
				return;
			}
			stash.CurrentPage = (stash.CurrentPage + delta + total) % total;
			if (Inventory.instance != null)
			{
				Inventory.instance.ForeignSlots = stash.Slots;
			}
			ApplyPageVisibility();
			UpdateTitle();
		}

		// Uses the typed character too, because '[' and ']' need AltGr on some layouts and KeyCode does not see them.
		private static void DetectPageKeys()
		{
			bool next = NextPageKey.Value.IsDown();
			bool prev = PrevPageKey.Value.IsDown();

			string input = UnityEngine.Input.inputString;
			for (int i = 0; i < input.Length; i++)
			{
				if (input[i] == ']') next = true;
				else if (input[i] == '[') prev = true;
			}

			if (next) ChangePage(1);
			if (prev) ChangePage(-1);
		}

		// Create the slot UIs once, so the game's own drag and drop works on the stash.
		private static void BindAllSlots()
		{
			if (Inventory.instance == null || Inventory.instance.storagePanelUI == null)
			{
				return;
			}

			Transform panel = Inventory.instance.storagePanelUI.storageSlotPanel.transform;
			for (int i = 0; i < stash.Slots.Length; i++)
			{
				SlotController sc = stash.Slots[i];
				if (sc.slot == null)
				{
					sc.SetAllowedCategories(stash.AllowedCategories);
					sc.SetForbiddenCategories(stash.ForbiddenCategories);
					sc.CaptureSlot(panel);
				}

				// Stash slots must not be marked as player slots, so the game treats them as storage.
				if (sc.slot != null)
				{
					sc.slot.playerSlot = false;
				}
			}
		}

		// Show only the current page. Hidden slots keep their UI so the game's own loops over all slots stay safe.
		private static void ApplyPageVisibility()
		{
			int start = stash.GetPageStart(stash.CurrentPage);
			int end = stash.GetPageEnd(stash.CurrentPage);

			for (int i = 0; i < stash.Slots.Length; i++)
			{
				SlotController sc = stash.Slots[i];
				if (sc != null && sc.slot != null && sc.slot.gameObject != null)
				{
					sc.slot.gameObject.SetActive(i >= start && i < end);
				}
			}
		}

		private static void UnbindAllSlots()
		{
			if (stash == null)
			{
				return;
			}
			for (int i = 0; i < stash.Slots.Length; i++)
			{
				SlotController sc = stash.Slots[i];
				if (sc != null)
				{
					if (sc.slot != null)
					{
						sc.RelaseSlot(destroy: true);
						sc.slot = null;
					}
					sc.ResetAllowedCategories();
					sc.ResetForbiddenCategories();
				}
			}
		}

		private static void UpdateTitle()
		{
			string skill = ScavengingSkill.Enabled.Value ? " — " + ScavengingSkill.ShortStatus() : "";
			stash.title = "Scavenge stash — Page " + (stash.CurrentPage + 1) + "/" + stash.TotalPages + skill;
			if (Inventory.instance != null && Inventory.instance.storagePanelUI != null)
			{
				Inventory.instance.storagePanelUI.UpdateStoragePanelUI(stash);
			}
		}

		// ---- Shared -----------------------------------------------------------------------

		// The picker's sleep button shows "Sleep". In scavenge mode, change its text to "Scavenge".
		private static void SetSleepButtonLabel(WaitingUI ui, string label)
		{
			if (ui == null)
			{
				return;
			}
			GameObject buttons = Traverse.Create(ui).Field("sleepButtons").GetValue<GameObject>();
			if (buttons == null)
			{
				return;
			}
			foreach (Component c in buttons.GetComponentsInChildren<Component>(true))
			{
				if (c == null || c.GetType().Name.IndexOf("TextMeshPro", StringComparison.Ordinal) < 0)
				{
					continue;
				}
				System.Reflection.PropertyInfo prop = c.GetType().GetProperty("text");
				if (prop == null)
				{
					continue;
				}
				string current = prop.GetValue(c, null) as string;
				if (current == "Sleep" || current == "Scavenge")
				{
					prop.SetValue(c, label, null);
				}
			}
		}

		internal static void LogWarning(string text)
		{
			if (instance != null) instance.Logger.LogWarning(text);
		}

		internal static void Notify(string text)
		{
			if (Notifications.instance != null)
			{
				Notifications.instance.CreateNotification("Scavenging", text);
			}
			else if (instance != null)
			{
				instance.Logger.LogInfo(text);
			}
		}

		// Harmony patches on the game's own time picker and save system.
		internal static class GameHooks
		{
			// The picker's Sleep button calls StartSleep(). In scavenge mode, start the scavenge instead.
			[HarmonyPatch(typeof(WaitingUI), nameof(WaitingUI.StartSleep))]
			[HarmonyPrefix]
			private static bool StartSleep_Prefix()
			{
				if (!pickerOpen)
				{
					return true;
				}
				StartScavengeFromPicker();
				return false;
			}

			// The game only shows the hour slider (and clears the header) without an alarm clock.
			// In scavenge mode, show the slider with a default duration and the header.
			[HarmonyPatch(typeof(WaitingUI), "SetTimeOpen")]
			[HarmonyPostfix]
			private static void SetTimeOpen_Postfix(WaitingUI __instance)
			{
				if (!pickerOpen || __instance == null)
				{
					return;
				}
				Traverse.Create(__instance).Field("setAlarm").GetValue<GameObject>().SetActive(true);
				Traverse.Create(__instance).Method("ChangeSleepTimeAndSlider", new object[] { DefaultScavengeSeconds }).GetValue();
				__instance.UpdateSleptTime(0f);
			}

			// The picker's header text shows what the player is doing. Show "Scavenging" instead of "Waiting".
			[HarmonyPatch(typeof(WaitingUI), "GetTypeString")]
			[HarmonyPostfix]
			private static void GetTypeString_Postfix(ref string __result)
			{
				if (pickerOpen || scavenging)
				{
					__result = ScavengingSkill.Enabled.Value ? "Scavenging (Lv " + ScavengingSkill.Level + ")" : "Scavenging";
				}
			}

			// If the game closes the picker itself, leave picker mode.
			[HarmonyPatch(typeof(WaitingUI), nameof(WaitingUI.CloseTimeSetUI))]
			[HarmonyPostfix]
			private static void CloseTimeSetUI_Postfix()
			{
				pickerOpen = false;
			}

			// Save hooks only run on active objects. On a cold load (Continue from the menu) the stash is
			// still inactive, so activate it first, as BankInventory does.
			[HarmonyPatch(typeof(SaveController), "ExecuteDontDestroyOnLoadSaveLoadFunctions")]
			[HarmonyPrefix]
			private static void DontDestroyOnLoadHooks_Prefix()
			{
				if (stash != null && !stash.gameObject.activeSelf)
				{
					stash.gameObject.SetActive(true);
				}
			}
		}
	}
}
