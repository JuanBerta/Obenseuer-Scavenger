using System.Reflection;
using UnityEngine;

namespace ScavengeTimerMod
{
	/// <summary>
	/// Hidden save object for the Scavenging skill. It extends the game's Storage only so it is saved
	/// through the game's global save (Globals.tnmt) with each save game. It never holds items and has no UI.
	/// The class name and GUID are kept from Scavenge Timer, so existing saves keep their skill XP.
	/// </summary>
	public class ScavengeStorage : Storage
	{
		public const string StashGUID = "ScavengeTimer_Stash_001";

		// The in-game day the "searched today" list belongs to.
		public string dayKey = "";

		// Scavenging skill XP. Never cleared.
		public float skillXp = 0f;

		// Trash containers already searched today ("|"-separated keys), so each gives XP once a day.
		public string searchedToday = "";

		public void Setup()
		{
			GUID = StashGUID;
			global = true;
			title = "Scavenger";
			storageSlots = new StorageSlot[0];
			Slots = new SlotController[0];
			spawnSettings = new SpawnSettings();

			FieldInfo startItemsField = typeof(Storage).GetField("startItems", BindingFlags.NonPublic | BindingFlags.Instance);
			if (startItemsField != null)
				startItemsField.SetValue(this, new ItemStack[0]);
			FieldInfo startDoneField = typeof(Storage).GetField("startDone", BindingFlags.NonPublic | BindingFlags.Instance);
			if (startDoneField != null)
				startDoneField.SetValue(this, true);

			initialized = true;
		}

		public override bool OnSavingGameSpecial()
		{
			// The old stash's items are not kept: the stash no longer exists.
			storageSlots = new StorageSlot[0];
			return SaveController.SerializeData(this, global);
		}

		public override bool OnLoadingGameSpecial()
		{
			bool ok = SaveController.DeSerializeData(this, global);
			storageSlots = new StorageSlot[0];
			Slots = new SlotController[0];
			return ok;
		}

		public bool MarkSearched(string key)
		{
			string token = "|" + key + "|";
			if (searchedToday.Contains(token)) return false;
			if (searchedToday.Length > 60000) searchedToday = "";
			searchedToday = (searchedToday.Length == 0 ? "|" : searchedToday) + key + "|";
			return true;
		}

		public void ClearSearched() { searchedToday = ""; }
	}
}
