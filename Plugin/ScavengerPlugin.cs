using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScavengeTimerMod
{
	[BepInPlugin("JuanBerta.Obenseuer.Scavenger", "Scavenger", "0.9.1")]
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

		// The game's current character (Saves/<Character>/<Save>), or null outside a game.
		internal static string CurrentCharacter()
		{
			try { return Traverse.Create(typeof(SaveController)).Field("CharacterName").GetValue<string>(); }
			catch (Exception) { return null; }
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
			if (save == null || PlayerStats.instance == null || GameIsLoading()) return;
			string current = CurrentCharacter();
			if (string.IsNullOrEmpty(current) || save.ownerCharacter == current) return;
			if (string.IsNullOrEmpty(save.ownerCharacter) && save.loadedFromSave)
			{
				save.ownerCharacter = current;   // data saved before this check existed, read from this character's save
				return;
			}
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
