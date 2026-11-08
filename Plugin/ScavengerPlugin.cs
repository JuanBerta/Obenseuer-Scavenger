using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScavengeTimerMod
{
	[BepInPlugin("JuanBerta.Obenseuer.Scavenger", "Scavenger", "0.8.2")]
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

		private void Update()
		{
			EnsureSaveObject();
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
