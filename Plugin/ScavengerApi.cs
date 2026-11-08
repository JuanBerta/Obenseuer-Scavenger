using System;
using UnityEngine;

namespace ScavengeTimerMod
{
	/// <summary>
	/// Public, stable API for other mods (for example Loot Map). Call it through reflection so there is
	/// no hard dependency: type "ScavengeTimerMod.ScavengerApi" in the "Obenseuer-Scavenger" assembly.
	/// </summary>
	public static class ScavengerApi
	{
		public const int Version = 2;

		/// <summary>True when the Scavenging skill is enabled.</summary>
		public static bool Enabled { get { return ScavengingSkill.Enabled != null && ScavengingSkill.Enabled.Value; } }

		/// <summary>True if containers of this spawn category count as trash for the skill.</summary>
		public static bool IsTrash(string category) { return ScavengingSkill.Enabled != null && ScavengingSkill.IsTrash(category); }

		/// <summary>True if the container at this position in this scene was already searched today (gave its daily XP).
		/// Positions within 0.75 m count as the same container.</summary>
		public static bool SearchedAt(string scene, float x, float y, float z)
		{
			var save = ScavengerPlugin.Stash;
			return save != null && !string.IsNullOrEmpty(scene) && save.WasSearched(scene, new Vector3(x, y, z));
		}

		/// <summary>How many trash containers were searched today.</summary>
		public static int SearchedTodayCount { get { var s = ScavengerPlugin.Stash; return s != null ? s.SearchedCount : 0; } }

		/// <summary>Current Scavenging level (0 = not learned yet).</summary>
		public static int Level { get { return ScavengingSkill.Enabled != null ? ScavengingSkill.Level : 0; } }

		/// <summary>Raised when a trash container is searched for the first time today (scene, position).</summary>
		public static event Action<string, Vector3> TrashSearched;
		internal static void RaiseTrashSearched(string scene, Vector3 pos) { var h = TrashSearched; if (h != null) h(scene, pos); }
	}
}
