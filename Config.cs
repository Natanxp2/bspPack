using System;
using System.IO;
using System.Runtime.InteropServices;

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
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				BSPZip = Path.Combine(GameFolder, @"../bin/win64", "bspzip.exe");
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				BSPZip = Path.Combine(GameFolder, @"../bin/linux64", "bspzip");
			
			KeysFolder = Path.Combine(Directory.GetCurrentDirectory(), "Keys");
			TempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
			CopyLocation = Path.Combine(Directory.GetCurrentDirectory()); //Placeholder
			VPK = Path.Combine(Directory.GetCurrentDirectory()); //Placeholder
		}
		
		public static void CreateDefaultResourceConfigFile(string filePath)
		{
			if (File.Exists(filePath))
			{
				Console.WriteLine("ResourceConfig.ini already exists.");
				return;
			}

			var lines = new List<string>
			{
				"# One path per line",
				"[IncludeFiles]",
				"",
				"[IncludeFileLists]",
				"",
				"[IncludeDirs]",
				"",
				"[ExcludeFiles]",
				"",
				"[ExcludeDirs]",
				"",
				"[ExcludeVpkFiles]",
				"",
			};

			try
			{
				File.WriteAllLines(filePath, lines);
				Console.WriteLine($"Default ResourceConfig.ini file has been created.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error creating configuration file: {ex.Message}");
			}
		}
		
		
		public static void LoadConfig(string filePath)
		{
			Console.WriteLine("Loading config.ini...");
			if (!File.Exists(filePath))
			{
				CreateDefaultConfigFile(filePath);
			}
			
			bool configLoaded = true;

			string currentSection = "";
			foreach (var line in File.ReadLines(filePath))
			{
				if (line.StartsWith("#"))
				continue;

				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					currentSection = line.Trim('[', ']');
					continue;
				}

			var trimmedLine = line.Trim().Trim('"');
				switch (currentSection)
				{
					case "GameFolder":
						string gameinfo = Path.Combine(trimmedLine, "gameinfo.txt");
						if (!File.Exists(gameinfo))
						{
							Console.WriteLine($"gameinfo.txt not found in provided game directory ( {trimmedLine} ).");
							configLoaded = false;
							break;
						}
						Config.GameFolder = trimmedLine;
						break;

					case "SteamPath":
						string steamapps = Path.Combine(trimmedLine, "steamapps");
						if (!Directory.Exists(steamapps))
						{
							Console.WriteLine($"steamapps not found in provided Steam directory ( {trimmedLine} ).");
							configLoaded = false;
							break;
						}
						Config.SteamAppsPath = steamapps;
						break;
				}
			}
			
			if(!configLoaded)
				Environment.Exit(1);
			
			InitializeConfig();
		}
		
		private static void CreateDefaultConfigFile(string filePath)
		{
			var lines = new List<string>
			{
				"[GameFolder]",
				"#Specify the path to the game folder here ( directory of gameinfo.txt )",
				"",
				"[SteamPath]",
				"#Specify the path to the Steam folder here ( steam installation directory )",
				""
			};
			File.WriteAllLines(filePath, lines);
			Console.WriteLine($"Default configuration file created at {filePath}. Please provide paths to game and steam folders.");
			Environment.Exit(1);
		}
	}
}