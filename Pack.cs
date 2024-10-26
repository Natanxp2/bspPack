using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using GlobExpressions;
using BSPPackStandalone.KV;
using BSPPackStandalone.UtilityProcesses;
using ValveKeyValue;

namespace BSPPackStandalone
{
	class BSPPack
	{
		private static KVSerializer KVSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
		
		private static List<string> sourceDirectories = new List<string>();
		private static List<string> includeFiles = new List<string>();
		private static List<string> includeDirs = new List<string>();
		private static List<string> excludeFiles = new List<string>();
		private static List<string> excludeDirs = new List<string>();
		private static List<string> excludeVpkFiles = new List<string>();
		private static string outputFile = "BSPZipFiles/files.txt";
		
		private static bool verbose;
		private static bool dryrun;
		private static bool renamenav;
		private static bool noswvtx;
		private static bool genParticleManifest;
		private static bool compress;
		private static bool reset;
		private static bool modify;
		
		static void Main(string[] args)
		{
			reset = args.Contains("--reset");
			modify = args.Contains("--modify");
		
			SetPaths(reset);
			Config.GameFolder = Environment.GetEnvironmentVariable("GamePath", EnvironmentVariableTarget.User);
			Config.SteamAppsPath = Path.Combine(Environment.GetEnvironmentVariable("Steam", EnvironmentVariableTarget.User),"steamapps");
			Config.InitializeConfig();
			
			
			if (args.Length == 0)
			{
				Console.WriteLine("Please provide a path to the BSP file.");
				return;
			}
			
			Config.BSPFile = args[^1];
			if (!File.Exists(Config.BSPFile))
			{	
				if(reset)
					return;
				else if(modify)
				{
					CreateDefaultConfigFile();
					return;
				}
				else
				{
					Console.WriteLine("File not found: " + Config.BSPFile);
					return;
				}
			}
			
			if(modify)
				LoadPathsFromFile();
			
			verbose = args.Contains("--verbose");
			dryrun = args.Contains("--dryrun");
			renamenav = args.Contains("--renamenav");
			noswvtx = args.Contains("--noswvtx");
			genParticleManifest = args.Contains("--genParticleManifest");
			compress = args.Contains("--compress");

			Console.WriteLine("Reading BSP...");
			FileInfo fileInfo = new FileInfo(Config.BSPFile);
			BSP bsp = new BSP(fileInfo);
			
			string unpackDir = Path.Combine(Config.TempFolder, Guid.NewGuid().ToString());
			AssetUtils.UnpackBSP(unpackDir);
			AssetUtils.findBspPakDependencies(bsp, unpackDir);
			
			Console.WriteLine("\nLooking for search paths...");
			sourceDirectories = AssetUtils.GetSourceDirectories(Config.GameFolder);
			AssetUtils.findBspUtilityFiles(bsp, sourceDirectories, renamenav, false);
			
			if(dryrun) 
				outputFile = $"BSPZipFiles/{Path.GetFileNameWithoutExtension(bsp.file.FullName)}_files.txt";
			
		
			
			Console.WriteLine("\nInitializing pak file...");
			PakFile pakfile = new PakFile(bsp, sourceDirectories, includeFiles, excludeFiles, excludeDirs, excludeVpkFiles, outputFile, noswvtx);
			Console.WriteLine("Writing file list...");
			pakfile.OutputToFile();
			
			if(dryrun)
			{
				Console.WriteLine($"Dry run finished! File saved to {outputFile}");
				return;
			}
			
			if(genParticleManifest)
			{
				ParticleManifest manifest = new ParticleManifest(sourceDirectories, excludeDirs, excludeFiles, bsp, Config.BSPFile, Config.GameFolder);
				bsp.particleManifest = manifest.particleManifest;
			}
			
			Console.WriteLine("Running bspzip...");
			PackBSP(outputFile);
			
			if(compress)
			{
				Console.WriteLine("Compressing BSP...");
				CompressBSP();
			}
			
			Console.WriteLine("Finished!");			
		}
		
		static void SetPaths(bool reset)
		{
			if(Environment.GetEnvironmentVariable("GamePath", EnvironmentVariableTarget.User) == null || reset)		
			{
				string path;
				do
				{
					Console.Write("Provide a path to game folder ( folder which contains gameinfo.txt ): ");
					path = Console.ReadLine();
					string gameinfo = Path.Combine(path,"gameinfo.txt");
					if(!File.Exists(gameinfo))
						Console.Write("gameinfo.txt is not present in provided location.\n");
				} 
				while(!File.Exists(Path.Combine(path,"gameinfo.txt")));
				
				Environment.SetEnvironmentVariable("GamePath", path, EnvironmentVariableTarget.User);
			}
			
			if(Environment.GetEnvironmentVariable("Steam", EnvironmentVariableTarget.User) == null || reset)		
			{
				string path;
				do
				{
					Console.Write("Provide a path to default Steam folder: ");
					path = Console.ReadLine();
					string steamapps = Path.Combine(path,"steamapps");
					if(!Directory.Exists(steamapps))
						Console.Write("steamapps folder is not present in provided location.\n");
				} 
				while(!Directory.Exists(Path.Combine(path,"steamapps")));
				
				Environment.SetEnvironmentVariable("Steam", path, EnvironmentVariableTarget.User);
			}
		}
		static void LoadPathsFromFile()
		{		
			if (!File.Exists("config.ini"))
			{
				Console.WriteLine($"config.ini not found, run 'bspPack --modify' to create it");
				System.Environment.Exit(1);
			}

			string currentSection = "";
			foreach (var line in File.ReadLines("config.ini"))
			{
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
					continue;

				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					currentSection = line.Trim('[', ']');
					continue;
				}
				var trimmedLine = line.Trim().Trim('"');
				switch (currentSection)
				{
					case "IncludeFiles":
						if(!File.Exists(trimmedLine))
						{
							Console.WriteLine($"Could not find file {trimmedLine}");
							break;
						}	
						includeFiles.Add(trimmedLine);
						break;
					case "IncludeDirs":
						if(!Directory.Exists(trimmedLine))
						{
							Console.WriteLine($"Could not find directory {trimmedLine}");
							break;
						}	
						includeDirs.Add(trimmedLine);
						break;
					case "ExcludeFiles":
						if(!File.Exists(trimmedLine))
						{
							Console.WriteLine($"Could not find file {trimmedLine}");
							break;
						}	
						excludeFiles.Add(trimmedLine);
						break;
					case "ExcludeDirs":
						if(!Directory.Exists(trimmedLine))
						{
							Console.WriteLine($"Could not find directory {trimmedLine}");
							break;
						}
						excludeDirs.Add(trimmedLine);						
						break;
					case "ExcludeVpkFiles":
						if(!File.Exists(trimmedLine))
						{
							Console.WriteLine($"Could not find file {trimmedLine}");
							break;
						}	
						excludeVpkFiles.Add(trimmedLine);
						break;
				}
			}
		}
		
		static void CreateDefaultConfigFile()
		{
			if (File.Exists("config.ini"))
			{
				Console.WriteLine("Configuration file already exists.");
				return;
			}

			var lines = new List<string>
			{
				"# One path per line",
				"[IncludeFiles]",
				"",
				"[IncludeDirs]",
				"",
				"[ExcludeFiles]",
				"",
				"[ExcludeDirs]",
				"",
				"[ExcludeVpkFiles]",
				""
			};

			try
			{
				File.WriteAllLines("config.ini", lines);
				Console.WriteLine($"Default configuration file has been created.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error creating configuration file: {ex.Message}");
			}
		}
		
		static void PackBSP(string outputFile)
		{
			string arguments = "-addlist \"$bspnew\"  \"$list\" \"$bspold\"";
			arguments = arguments.Replace("$bspnew", Config.BSPFile);
			arguments = arguments.Replace("$bspold", Config.BSPFile);
			arguments = arguments.Replace("$list", outputFile);

			var startInfo = new ProcessStartInfo(Config.BSPZip, arguments)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true,
				EnvironmentVariables =
				{
					["VPROJECT"] = Config.GameFolder
				}
			};

			var p = new Process { StartInfo = startInfo };

			try
			{
				p.Start();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
				Console.WriteLine($"Failed to run executable: {p.StartInfo.FileName}\n");
				return;
			}

			string output = p.StandardOutput.ReadToEnd();
			if (verbose) 
				Console.WriteLine(output);

			p.WaitForExit();
			if (p.ExitCode != 0) {
				// this indicates an access violation. BSPZIP may have crashed because of too many files being packed
				if (p.ExitCode == -1073741819)
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}, this might indicate that too many files are being packed\n");
				else
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}\n");
			}

		}
		
		static void CompressBSP()
		{
			string arguments = "-repack -compress \"$bspnew\"";
			arguments = arguments.Replace("$bspnew", Config.BSPFile);

			var startInfo = new ProcessStartInfo(Config.BSPZip, arguments)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				CreateNoWindow = true,
				EnvironmentVariables =
				{
					["VPROJECT"] = Config.GameFolder
				}
			};

			var p = new Process { StartInfo = startInfo };

			try
			{
				p.Start();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
				Console.WriteLine($"Failed to run executable: {p.StartInfo.FileName}\n");
				return;
			}

			string output = p.StandardOutput.ReadToEnd();
			if (verbose) 
				Console.WriteLine(output);

			p.WaitForExit();
			if (p.ExitCode != 0) {
				// this indicates an access violation. BSPZIP may have crashed because of too many files being packed
				if (p.ExitCode == -1073741819)
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}, this might indicate that too many files are being packed\n");
				else
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}\n");
			}
		}
	}
}