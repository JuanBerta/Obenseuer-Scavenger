namespace ScavengeTimerMod
{
	// Read by BepInEx ConfigurationManager (if installed) to order settings in its menu.
	// Standard pattern: a class with this exact name and fields, passed in ConfigDescription tags.
	internal sealed class ConfigurationManagerAttributes
	{
		public int? Order;
		public bool? IsAdvanced;
	}
}
