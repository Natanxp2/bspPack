using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BSPPackStandalone
{
	public static class Config
	{
		public static readonly string ExeDirectory = AppContext.BaseDirectory;
		public static string BSPFile { get; set; } = null!;
		public static string GameFolder { get; set; } = null!;
		public static string SteamAppsPath { get; set; } = null!;

		public static string BSPZip { get; private set; } = null!;
		public static string KeysFolder { get; private set; } = null!;
		public static string TempFolder { get; private set; } = null!;
		public static string CopyLocation { get; private set; } = null!;
		public static string VPK { get; private set; } = null!;

		public static void InitializeConfig()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				BSPZip = Path.Combine(GameFolder, @"../bin/win64", "bspzip.exe");
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				BSPZip = Path.Combine(GameFolder, @"../bin/linux64", "bspzip");

			KeysFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keys");

			if (!Directory.Exists(KeysFolder))
			{
				Message.Error($"Keys folder doesn't exist in {AppDomain.CurrentDomain.BaseDirectory}!");
				Environment.Exit(1);
			}

			TempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
			CopyLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory); //Placeholder
			VPK = Path.Combine(AppDomain.CurrentDomain.BaseDirectory); //Placeholder
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
				"[IncludeSourceDirectories]",
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
							Message.Error($"gameinfo.txt not found in provided game directory ( {trimmedLine} ).");
							configLoaded = false;
							break;
						}
						GameFolder = trimmedLine;
						break;

					case "SteamPath":
						string steamapps = Path.Combine(trimmedLine, "steamapps");
						if (!Directory.Exists(steamapps))
						{
							Message.Error($"steamapps not found in provided Steam directory ( {trimmedLine} ).");
							configLoaded = false;
							break;
						}
						SteamAppsPath = steamapps;
						break;
				}
			}

			if (!configLoaded)
				Environment.Exit(1);

			InitializeConfig();
		}

		private static void CreateDefaultConfigFile(string filePath)
		{
			string steamPath = DetectSteamPath();
			var gamePaths = DetectGamePaths(steamPath);
			string chosenGame = string.Empty;

			Message.Info("config.ini not found, creating...");

			if (string.IsNullOrEmpty(steamPath))
				Message.Warning("Path to steam not found, please provide it manually in config.ini");
			Message.Write("Path to steam found at: ");
			Message.Write(steamPath + "\n", ConsoleColor.Blue);

			if (gamePaths.Count > 0)
			{
				Message.WriteLine("Following games were found:");
				for (int i = 0; i < gamePaths.Count; i++)
				{
					Message.Write($"[{i + 1}] ");
					Message.WriteLine($"{gamePaths[i].Item1} ", ConsoleColor.Blue);
				}
				Message.Write("\nChoose which one to use a the base: ");

				int selected = -1;
				while (selected < 1 || selected > gamePaths.Count)
				{
					string? input = Console.ReadLine();
					if (!int.TryParse(input, out selected) || selected < 1 || selected > gamePaths.Count)
					{
						Message.Write("Invalid selection. Please enter a valid number: ", ConsoleColor.Yellow);
					}
				}
				chosenGame = Path.Combine(steamPath, "steamapps", "common", gamePaths[selected - 1].Item1, gamePaths[selected - 1].Item2);
			}
			else
			{
				Message.Warning("No supported games found. Please provide the game folder manually in config.ini.");
			}

			var lines = new List<string>
			{
				"[GameFolder]",
				"#Specify the path to the game folder here ( directory of gameinfo.txt )",
				chosenGame,
				"[SteamPath]",
				"#Specify the path to the Steam folder here ( steam installation directory )",
				steamPath
			};

			File.WriteAllLines(filePath, lines);
			Message.Write($"Default configuration file created at: ");
			Message.Write(filePath + "\n", ConsoleColor.Blue);
		}

		private static string DetectSteamPath()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try
				{
					using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
					var regPath = key?.GetValue("SteamPath") as string;
					if (!string.IsNullOrEmpty(regPath) && Directory.Exists(regPath))
						return regPath;
				}
				catch
				{
					return string.Empty;
				}
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				var possiblePaths = new[]
				{
					Path.Combine(home, ".steam", "steam"),
					Path.Combine(home, ".local", "share", "Steam")
				};

				foreach (string path in possiblePaths)
				{
					if (Directory.Exists(path))
						return path;
				}
			}
			return string.Empty;
		}

		private static List<(string, string)> DetectGamePaths(string steamPath)
		{
			var games = new (string Name, string subDir)[]
			{
				("Team Fortress 2", "tf"),
				("Counter-Strike Source", "cstrike"),
				("Counter-Strike Global Offensive", "csgo"),
				("Portal", "portal"),
				("Portal 2", "portal2"),
				("Momentum Mod", "momentum"),
				("Momentum Mod Playtest", "momentum"),
			};

			List<(string, string)> found = [];
			string common = Path.Combine(steamPath, "steamapps", "common");

			if (!Directory.Exists(common))
				return [];

			foreach (var (name, subdir) in games)
			{
				string gameRoot = Path.Combine(common, name);
				if (!Directory.Exists(gameRoot))
					continue;

				string gameInfoPath = Path.Combine(gameRoot, subdir);
				found.Add((name, gameInfoPath));
			}

			return found;
		}
	}
}
