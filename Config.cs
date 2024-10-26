using System;
using System.IO;

namespace BSPPackStandalone
{
	public static class Config
	{
		public static string BSPFile { get; set; }
		public static string GameFolder { get; set; } 
		public static string SteamAppsPath { get; set; }
		
		public static string BSPZip { get; private set; }
		public static string KeysFolder { get; private set; }
		public static string TempFolder { get; private set; }
		public static string CopyLocation { get; private set; }
		public static string VPK { get; private set; }

		public static void InitializeConfig()
		{
			// SteamAppsPath = GameFolder.Substring(0, GameFolder.IndexOf("steamapps") + 9);
			BSPZip = Path.Combine(GameFolder, @"..\bin\win64", "bspzip.exe");
			KeysFolder = Path.Combine(Directory.GetCurrentDirectory(), "Keys");
			TempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
			CopyLocation = Path.Combine(Directory.GetCurrentDirectory()); //Placeholder
			VPK = Path.Combine(Directory.GetCurrentDirectory()); //Placeholder
		}
	}
}