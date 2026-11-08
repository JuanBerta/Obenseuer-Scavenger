using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScavengeTimerMod
{
	[BepInPlugin("JuanBerta.Obenseuer.Scavenger", "Scavenger", "1.0.0")]
	public class ScavengerPlugin : BaseUnityPlugin
	{
		private static ScavengerPlugin instance;
		private static ScavengeStorage save;
		internal static ScavengeStorage Stash { get { return save; } }

		private void Awake()
		{
			instance = this;
			ScavengingSkill.Bind(Config);

			SceneManager.sceneLoaded += OnSceneLoaded;
			EnsureSaveObject();

			Harmony.CreateAndPatchAll(typeof(SaveHooks));
			Harmony.CreateAndPatchAll(typeof(ScavengingSkill.Hooks));
			TimeOfDayAzure.DayChanged += OnDayChanged;
			SaveController.LoadingDone += OnGameLoaded;
			Logger.LogInfo("Scavenger loaded.");
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			TimeOfDayAzure.DayChanged -= OnDayChanged;
			SaveController.LoadingDone -= OnGameLoaded;
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			EnsureSaveObject();
		}

		private float nextOwnerCheck;

		private void Update()
		{
			EnsureSaveObject();
			if (Time.unscaledTime >= nextOwnerCheck)
			{
				nextOwnerCheck = Time.unscaledTime + 1f;
				CheckOwner();
			}
		}

		// The game's current character, or null outside a game. The game keeps it either as a plain name
		// ("Mike_Gray") or as a full path (".../Saves/Mike_Gray"); only the last folder name is used.
		internal static string CurrentCharacter()
		{
			try { return NormalizeCharacter(Traverse.Create(typeof(SaveController)).Field("CharacterName").GetValue<string>()); }
			catch (Exception) { return null; }
		}

		internal static string NormalizeCharacter(string name)
		{
			if (string.IsNullOrEmpty(name)) return name;
			name = name.Replace('\\', '/').TrimEnd('/');
			int slash = name.LastIndexOf('/');
			return slash >= 0 ? name.Substring(slash + 1) : name;
		}

		private static bool GameIsLoading()
		{
			try { return SaveController.instance != null && Traverse.Create(SaveController.instance).Property("Loading").GetValue<bool>(); }
			catch (Exception) { return false; }
		}

		// A new game doesn't load anything, so the skill data from the previous game would stay in memory.
		// When the character in play isn't the one this data belongs to, start the skill from 0.
		private static void CheckOwner()
		{
			if (save == null) return;
			if (PlayerStats.instance == null || SceneManager.GetActiveScene().name == "Main Menu")
			{
				save.loadedFromSave = false;   // in the main menu: whatever is in memory belongs to the last game
				return;
			}
			if (GameIsLoading()) return;
			string current = CurrentCharacter();
			if (string.IsNullOrEmpty(current) || NormalizeCharacter(save.ownerCharacter) == current) return;
			if (save.loadedFromSave)
			{
				save.ownerCharacter = current;   // data read from this save belongs to this save
				return;
			}
			// Playing without having loaded anything (a new game) while the previous game's data is in memory.
			save.ResetSkill(current);
			save.dayKey = CurrentDayKey();
			if (instance != null) instance.Logger.LogInfo("New character '" + current + "': Scavenging starts at 0.");
		}

		// ---- Day and save ------------------------------------------------------------------

		internal static string CurrentDayKey()
		{
			if (TimeOfDayAzure.instance == null)
			{
				return "";
			}
			TimeAndDay now = TimeOfDayAzure.instance.currentTimeAndDay;
			return now.year + "-" + now.month + "-" + now.day;
		}

		// A new in-game day: every trash container gives XP again.
		private static void OnDayChanged(int day)
		{
			if (save == null)
			{
				return;
			}
			save.ClearSearched();
			save.dayKey = CurrentDayKey();
		}

		// After a save is loaded: keep the "searched today" list only if it belongs to the current day.
		private static void OnGameLoaded()
		{
			EnsureSaveObject();
			if (save.loadedFromSave && string.IsNullOrEmpty(save.ownerCharacter)) save.ownerCharacter = CurrentCharacter() ?? "";
			if (save.dayKey != CurrentDayKey())
			{
				save.ClearSearched();
				save.dayKey = CurrentDayKey();
			}
		}

		// The save object is a DontDestroyOnLoad object registered with the save system, so it is saved in Globals.tnmt.
		private static void EnsureSaveObject()
		{
			if (save == null)
			{
				GameObject go = new GameObject("Scavenger_Save");
				go.SetActive(false); // stays inactive until the world exists
				save = go.AddComponent<ScavengeStorage>();
				save.Setup();
				UnityEngine.Object.DontDestroyOnLoad(go);
				SaveController.DontDestroyOnLoadObjects.Add(go);
			}

			if (!save.gameObject.activeSelf && Inventory.instance != null)
			{
				save.gameObject.SetActive(true);
			}
		}

		// ---- Shared -----------------------------------------------------------------------

		internal static void LogInfo(string text)
		{
			if (instance != null) instance.Logger.LogInfo(text);
		}

		internal static void LogWarning(string text)
		{
			if (instance != null) instance.Logger.LogWarning(text);
		}

		internal static void Notify(string title, string text)
		{
			if (Notifications.instance != null)
			{
				Notifications.instance.CreateNotification(title, text);
			}
			else if (instance != null)
			{
				instance.Logger.LogInfo(text);
			}
		}

		internal static class SaveHooks
		{
			// Save hooks only run on active objects. On a cold load (Continue from the menu) the save object is
			// still inactive, so activate it first.
			[HarmonyPatch(typeof(SaveController), "ExecuteDontDestroyOnLoadSaveLoadFunctions")]
			[HarmonyPrefix]
			private static void DontDestroyOnLoadHooks_Prefix()
			{
				if (save != null && !save.gameObject.activeSelf)
				{
					save.gameObject.SetActive(true);
				}
			}
		}
	}
}
