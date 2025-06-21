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

		private static List<string> sourceDirectories = [];
		private static List<string> includeFiles = [];
		private static List<string> includeFileLists = [];
		private static List<string> includeDirs = [];
		private static List<string> includeSourceDirectories = [];
		private static List<string> excludeFiles = [];
		private static List<string> excludeDirs = [];
		private static List<string> excludeVpkFiles = [];

		private static string outputFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BSPZipFiles/files.txt");

		private static bool verbose;
		private static bool dryrun;
		private static bool renamenav;
		private static bool noswvtx;
		private static bool particlemanifest;
		private static bool compress;
		private static bool modify;
		private static bool unpack;
		private static bool search;

		static void Main(string[] args)
		{
			verbose = args.Contains("-V") || args.Contains("--verbose");
			dryrun = args.Contains("-D") || args.Contains("--dryrun");
			renamenav = args.Contains("-R") || args.Contains("--renamenav");
			noswvtx = args.Contains("-N") || args.Contains("--noswvtx");
			particlemanifest = args.Contains("-P") || args.Contains("--particlemanifest");
			compress = args.Contains("-C") || args.Contains("--compress");
			unpack = args.Contains("-U") || args.Contains("--unpack");
			modify = args.Contains("-M") || args.Contains("--modify");
			search = args.Contains("-S") || args.Contains("--search");

			Config.LoadConfig(Path.Combine(Config.ExeDirectory, "config.ini"));

			if (args.Length == 0)
			{
				string helpMessage = @"
Please provide path to BSP.
## Flags
-V | --verbose            Outputs a complete listing of added assets
-D | --dryrun             Creates a txt file for bspzip usage but does not pack
-R | --renamenav          Renames the nav file to embed.nav
-N | --noswvtx            Skips packing unused .sw.vtx files to save filesize
-P | --particlemanifest   Generates a particle manifest based on particles used
-C | --compress           Compresses the BSP after packing
-M | --modify             Modifies PakFile based on ResourceConfig.ini
-U | --unpack             Unpacks the BSP to <filename>_unpacked
-S | --search             Searches /maps folder of the game directory for the BSP file
";
				Console.WriteLine(helpMessage);
				return;
			}

			if (search)
				Config.BSPFile = Path.Combine(Config.GameFolder, "maps", args[^1]);
			else
				Config.BSPFile = args[^1];

			if (!File.Exists(Config.BSPFile))
			{
				if (modify)
				{
					Config.CreateDefaultResourceConfigFile(Path.Combine(Config.ExeDirectory, "ResourceConfig.ini"));
					return;
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("File not found: " + Config.BSPFile);
					Console.ResetColor();
					return;
				}
			}

			if (modify)
				LoadPathsFromFile(Path.Combine(Config.ExeDirectory, "ResourceConfig.ini"));

			Console.WriteLine("Reading BSP...");
			FileInfo fileInfo = new FileInfo(Config.BSPFile);
			BSP bsp = LoadBSP(fileInfo);

			string unpackDir;

			if (unpack)
			{
				unpackDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileNameWithoutExtension(bsp.file.FullName) + "_unpacked");
				AssetUtils.UnpackBSP(unpackDir);
				Console.WriteLine($"BSP unpacked to: {unpackDir}");
				return;
			}

			unpackDir = Path.Combine(Config.TempFolder, Guid.NewGuid().ToString());
			AssetUtils.UnpackBSP(unpackDir);
			AssetUtils.FindBspPakDependencies(bsp, unpackDir);

			Console.WriteLine("\nLooking for search paths...");
			sourceDirectories = AssetUtils.GetSourceDirectories(Config.GameFolder);

			if (includeSourceDirectories.Count != 0)
			{
				foreach (string dir in includeSourceDirectories)
				{
					string root = Directory.GetDirectoryRoot(dir);
					var globOptions = Environment.OSVersion.Platform == PlatformID.Win32NT
						? GlobOptions.CaseInsensitive
						: GlobOptions.None;

					var globResults = Glob.Directories(root, dir.Substring(root.Length), globOptions);
					if (!globResults.Any())
					{
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine($"WARNING: Found no matching folders for: {dir}\n");
						Console.ResetColor();
						continue;
					}

					foreach (string path in globResults)
					{
						sourceDirectories.Add(root + path);
					}

				}
			}

			AssetUtils.FindBspUtilityFiles(bsp, sourceDirectories, renamenav, false);

			if (dryrun)
				outputFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BSPZipFiles", $"{Path.GetFileNameWithoutExtension(bsp.file.FullName)}_files.txt");

			if (includeDirs.Count != 0)
				GetFilesFromIncludedDirs();

			Console.WriteLine("\nInitializing pak file...");
			PakFile pakfile = new PakFile(bsp, sourceDirectories, includeFiles, excludeFiles, excludeDirs, excludeVpkFiles, outputFile, noswvtx);

			if (includeFileLists.Count != 0)
			{
				foreach (string file in includeFileLists)
				{
					Console.WriteLine($"Adding files from list: {file}");
					var fileList = File.ReadAllLines(file);

					// file list format is internal path, newline, external path
					for (int i = 0; i < fileList.Length - 1; i += 2)
					{
						var internalPath = fileList[i];
						var externalPath = fileList[i + 1];
						if (!pakfile.AddInternalFile(internalPath, externalPath))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"Failed to pack {externalPath}. This may indicate a duplicate file");
							Console.ResetColor();
						}
					}
					Console.WriteLine($"Added {fileList.Length / 2} files from {file}");
				}
			}

			Console.WriteLine("Writing file list...");
			pakfile.OutputToFile();

			if (dryrun)
			{
				Directory.Delete(Config.TempFolder, true);
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"Dry run finished! File saved to {outputFile}");
				Console.ResetColor();
				return;
			}

			if (particlemanifest)
			{
				ParticleManifest manifest = new ParticleManifest(sourceDirectories, excludeDirs, excludeFiles, bsp, Config.BSPFile, Config.GameFolder);
				bsp.particleManifest = manifest.particleManifest;
			}

			Console.WriteLine("Running bspzip...");
			PackBSP(outputFile);

			if (compress)
			{
				Console.WriteLine("Compressing BSP...");
				CompressBSP();
			}

			Directory.Delete(Config.TempFolder, true);
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Finished!");
			Console.ResetColor();
		}

		static BSP LoadBSP(FileInfo fileInfo)
		{
			bool attempt = false;

			while (true)
			{
				try
				{
					BSP bsp = new BSP(fileInfo);
					return bsp;
				}
				catch (Exception ex)
				{
					if (attempt)
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("ERROR: Decompression failed, exiting.");
						Console.ResetColor();
						return null;
					}
					Console.WriteLine($"{ex.Message}");
					CompressBSP(true);
					attempt = true;
				}
			}
		}

		static void LoadPathsFromFile(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"{filePath} not found, run 'bspPack --modify' to create it");
				Console.ResetColor();
				System.Environment.Exit(1);
			}

			string currentSection = "";
			foreach (var line in File.ReadLines(filePath))
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
						if (!File.Exists(trimmedLine))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"WARNING: Could not find file {trimmedLine}");
							Console.ResetColor();
							break;
						}
						includeFiles.Add(trimmedLine);
						break;
					case "IncludeFileLists":
						if (!File.Exists(trimmedLine))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"WARNING: Could not find file {trimmedLine}");
							Console.ResetColor();
							break;
						}
						includeFileLists.Add(trimmedLine);
						break;
					case "IncludeDirs":
						if (!Directory.Exists(trimmedLine))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"WARNING: Could not find directory {trimmedLine}");
							Console.ResetColor();
							break;
						}
						includeDirs.Add(trimmedLine);
						break;
					case "IncludeSourceDirectories":
						if (!Directory.Exists(trimmedLine))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"WARNING: Could not find directory {trimmedLine}");
							Console.ResetColor();
							break;
						}
						includeSourceDirectories.Add(trimmedLine);
						break;
					case "ExcludeFiles":
						if (!File.Exists(trimmedLine))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"WARNING: Could not find file {trimmedLine}");
							Console.ResetColor();
							break;
						}
						Console.WriteLine($"EXCLUDED FILE: {trimmedLine.Replace('\\', '/')}");
						excludeFiles.Add(trimmedLine.Replace("\\", "/"));
						break;
					case "ExcludeDirs":
						if (!Directory.Exists(trimmedLine))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"WARNING: Could not find directory {trimmedLine}");
							Console.ResetColor();
							break;
						}
						excludeDirs.Add(trimmedLine.Replace("\\", "/"));
						break;
					case "ExcludeVpkFiles":
						if (!File.Exists(trimmedLine))
						{
							Console.ForegroundColor = ConsoleColor.Yellow;
							Console.WriteLine($"WARNING: Could not find file {trimmedLine}");
							Console.ResetColor();
							break;
						}
						excludeVpkFiles.Add(trimmedLine);
						break;
				}
			}
		}

		static void GetFilesFromIncludedDirs()
		{
			foreach (string dir in includeDirs)
			{
				var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
				foreach (var file in files)
				{
					includeFiles.Add(file);
				}
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
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e.ToString());
				Console.WriteLine($"Failed to run executable: {p.StartInfo.FileName}\n");
				Console.ResetColor();
				return;
			}

			string output = p.StandardOutput.ReadToEnd();
			if (verbose)
				Console.WriteLine(output);

			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				// this indicates an access violation. BSPZIP may have crashed because of too many files being packed
				if (p.ExitCode == -1073741819)
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}, this might indicate that too many files are being packed\n");
				else
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}\n");

				Console.ResetColor();
			}

		}

		static void CompressBSP(bool decompress = false)
		{
			string arguments;

			if (decompress)
				arguments = "-repack \"$bspnew\"";
			else
				arguments = "-repack -compress \"$bspnew\"";

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
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(e.ToString());
				Console.WriteLine($"Failed to run executable: {p.StartInfo.FileName}\n");
				Console.ResetColor();
				return;
			}

			string output = p.StandardOutput.ReadToEnd();
			if (verbose)
				Console.WriteLine(output);

			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				// this indicates an access violation. BSPZIP may have crashed because of too many files being packed
				if (p.ExitCode == -1073741819)
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}, this might indicate that too many files are being packed\n");
				else
					Console.WriteLine($"BSPZIP exited with code: {p.ExitCode}\n");

				Console.ResetColor();
			}
		}
	}
}
