using System;
using System.Reflection;
using OS.Items;
using UnityEngine;

namespace ScavengeTimerMod
{
	/// <summary>
	/// The scavenge stash. It extends the game's Storage, so it is shown in the game's own
	/// storage panel and saved through the game's global save (Globals.tnmt).
	/// It starts with a base capacity and adds a page of slots whenever it is full.
	/// </summary>
	public class ScavengeStorage : Storage
	{
		public const string StashGUID = "ScavengeTimer_Stash_001";

		private static ScavengeStorage _instance;
		public static ScavengeStorage Instance { get { return _instance; } }

		// Mod-only state. Private, so it is not written into the save.
		private int _currentPage;

		public int CurrentPage
		{
			get { return _currentPage; }
			set { _currentPage = value; }
		}

		public int TotalPages
		{
			get { return Mathf.Max(1, Mathf.CeilToInt((float)Slots.Length / PageSize)); }
		}

		public int PageSize { get; set; }

		// The in-game day this stash belongs to. Saved with the stash, so a save from another day is cleared.
		public string dayKey = "";

		// Scavenging skill XP. Saved with the stash, never cleared.
		public float skillXp = 0f;

		// Trash containers already searched today ("|"-separated keys), so each gives XP once a day.
		public string searchedToday = "";

		public bool MarkSearched(string key)
		{
			string token = "|" + key + "|";
			if (searchedToday.Contains(token)) return false;
			if (searchedToday.Length > 60000) searchedToday = "";
			searchedToday = (searchedToday.Length == 0 ? "|" : searchedToday) + key + "|";
			return true;
		}

		public void ClearSearched() { searchedToday = ""; }

		public void Setup(int baseCapacity, int pageSize)
		{
			GUID = StashGUID;
			global = true;
			title = "Scavenge stash";
			PageSize = Mathf.Max(1, pageSize);
			int cap = Mathf.Max(1, baseCapacity);

			storageSlots = new StorageSlot[cap];
			for (int i = 0; i < storageSlots.Length; i++)
				storageSlots[i] = new StorageSlot();

			RebuildSlots();

			spawnSettings = new SpawnSettings();

			FieldInfo startItemsField = typeof(Storage).GetField("startItems", BindingFlags.NonPublic | BindingFlags.Instance);
			if (startItemsField != null)
				startItemsField.SetValue(this, new ItemStack[0]);

			FieldInfo startDoneField = typeof(Storage).GetField("startDone", BindingFlags.NonPublic | BindingFlags.Instance);
			if (startDoneField != null)
				startDoneField.SetValue(this, true);

			initialized = true;
			_instance = this;
		}

		public override bool OnSavingGameSpecial()
		{
			return SaveController.SerializeData(this, global);
		}

		public override bool OnLoadingGameSpecial()
		{
			bool ok = SaveController.DeSerializeData(this, global);
			if (ok)
				RebuildSlots();
			return ok;
		}

		public void RebuildSlots()
		{
			Slots = new SlotController[storageSlots.Length];
			for (int i = 0; i < Slots.Length; i++)
			{
				SlotController sc = new SlotController();
				sc.Siblings = Slots;
				sc.itemStack = storageSlots[i];
				if (sc.itemStack != null)
					sc.itemStack.SetItemReference();
				Slots[i] = sc;
			}
		}

		public void ExpandByPage(int extraSlots)
		{
			if (extraSlots <= 0)
				return;

			int oldLen = Slots.Length;

			Array.Resize(ref storageSlots, oldLen + extraSlots);
			for (int i = oldLen; i < storageSlots.Length; i++)
				storageSlots[i] = new StorageSlot();

			Array.Resize(ref Slots, oldLen + extraSlots);
			for (int i = oldLen; i < Slots.Length; i++)
			{
				SlotController sc = new SlotController();
				sc.itemStack = storageSlots[i];
				Slots[i] = sc;
			}

			for (int i = 0; i < Slots.Length; i++)
				Slots[i].Siblings = Slots;
		}

		private int FindFreeSlot()
		{
			for (int i = 0; i < storageSlots.Length; i++)
			{
				if (storageSlots[i] != null && storageSlots[i].itemId == -1)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>Put an amount of one item in the stash. Each stack holds at most stackLimit.
		/// Extra stacks go to the next free slots, and a page is added if there is no free slot.</summary>
		public void AddItems(int itemId, int amount, int stackLimit)
		{
			int limit = Mathf.Max(1, stackLimit);
			int remaining = Mathf.Max(1, amount);
			while (remaining > 0)
			{
				int free = FindFreeSlot();
				if (free == -1)
				{
					ExpandByPage(PageSize);
					free = storageSlots.Length - PageSize;
				}

				int take = Mathf.Min(remaining, limit);
				StorageSlot slot = storageSlots[free];
				slot.itemId = itemId;
				slot.itemAmount = take;
				slot.ownerId = -1;
				slot.SetItemReference();
				remaining -= take;
			}
		}

		/// <summary>Empty every slot. Used when the in-game day changes.</summary>
		public void ClearAll()
		{
			for (int i = 0; i < storageSlots.Length; i++)
				storageSlots[i] = new StorageSlot();
			RebuildSlots();
		}

		public int GetPageStart(int page)
		{
			return page * PageSize;
		}

		public int GetPageEnd(int page)
		{
			return Mathf.Min((page + 1) * PageSize, Slots.Length);
		}
	}
}
