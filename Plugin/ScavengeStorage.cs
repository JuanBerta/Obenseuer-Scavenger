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

		// The character this XP belongs to, so a new game or another character never inherits it.
		public string ownerCharacter = "";

		// Set when this object's data was read from the current save (not saved).
		[System.NonSerialized] public bool loadedFromSave;

		public void ResetSkill(string owner)
		{
			skillXp = 0f;
			searchedToday = "";
			dayKey = "";
			ownerCharacter = owner ?? "";
		}

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
			// A save without Scavenger data (older save, or one from before the mod) starts the skill at 0.
			if (!ok) ResetSkill(ScavengerPlugin.CurrentCharacter());
			loadedFromSave = true;   // the data in memory now belongs to this save (even if it had no Scavenger data)
			return ok;
		}

		// Searched containers are stored as "scene@x,y,z" (positions are stable across sessions; some
		// containers are spawned at runtime with a new GUID each time, so GUIDs can't be used).
		private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Vector3>> parsed;
		private string parsedFrom;

		private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Vector3>> Searched()
		{
			if (parsed != null && parsedFrom == searchedToday) return parsed;
			parsed = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Vector3>>();
			parsedFrom = searchedToday;
			foreach (string token in searchedToday.Split('|'))
			{
				int at = token.LastIndexOf('@');
				if (at <= 0) continue;
				string[] c = token.Substring(at + 1).Split(',');
				float x, y, z;
				var inv = System.Globalization.CultureInfo.InvariantCulture; var f = System.Globalization.NumberStyles.Float;
				if (c.Length == 3 && float.TryParse(c[0], f, inv, out x) && float.TryParse(c[1], f, inv, out y) && float.TryParse(c[2], f, inv, out z))
					parsed.Add(new System.Collections.Generic.KeyValuePair<string, Vector3>(token.Substring(0, at), new Vector3(x, y, z)));
			}
			return parsed;
		}

		public bool WasSearched(string scene, Vector3 pos)
		{
			foreach (var kv in Searched())
				if (kv.Key == scene && (kv.Value - pos).sqrMagnitude < 0.75f * 0.75f) return true;
			return false;
		}

		// Returns false if this container was already searched today.
		public bool MarkSearched(string scene, Vector3 pos)
		{
			if (WasSearched(scene, pos)) return false;
			if (searchedToday.Length > 60000) searchedToday = "";
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			string key = scene + "@" + pos.x.ToString("F2", inv) + "," + pos.y.ToString("F2", inv) + "," + pos.z.ToString("F2", inv);
			searchedToday = (searchedToday.Length == 0 ? "|" : searchedToday) + key + "|";
			return true;
		}

		public int SearchedCount { get { return Searched().Count; } }

		public void ClearSearched() { searchedToday = ""; }


	}
}
