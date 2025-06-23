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
using System.Runtime.CompilerServices;

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
		private static bool lowercase;

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
			lowercase = args.Contains("-L") || args.Contains("--lowercase");

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
-L | --lowercase		  Lowercases directories, files, and content of .vmt files inside of /materials folder
";
				Console.WriteLine(helpMessage);
				return;
			}
			if (lowercase)
				LowercaseMaterials();

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

				if (lowercase)
					return;

				Message.Error("File not found: " + Config.BSPFile);
				return;
			}

			if (modify)
				LoadPathsFromFile(Path.Combine(Config.ExeDirectory, "ResourceConfig.ini"));

			Console.WriteLine("Reading BSP...");
			FileInfo fileInfo = new(Config.BSPFile);
			BSP bsp = LoadBSP(fileInfo);

			string unpackDir;

			if (unpack)
			{
				unpackDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileNameWithoutExtension(bsp.File.FullName) + "_unpacked");
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
						Message.Warning($"WARNING: Found no matching folders for: {dir}\n");
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
				outputFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BSPZipFiles", $"{Path.GetFileNameWithoutExtension(bsp.File.FullName)}_files.txt");

			if (includeDirs.Count != 0)
				GetFilesFromIncludedDirs();

			Console.WriteLine("\nInitializing pak file...");
			PakFile pakfile = new(bsp, sourceDirectories, includeFiles, excludeFiles, excludeDirs, excludeVpkFiles, outputFile, noswvtx);

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
							Message.Warning($"WARNING: Failed to pack {externalPath}. This may indicate a duplicate file");
					}
					Console.WriteLine($"Added {fileList.Length / 2} files from {file}");
				}
			}

			Console.WriteLine("Writing file list...");
			pakfile.OutputToFile();

			if (dryrun)
			{
				Directory.Delete(Config.TempFolder, true);
				Message.Success($"Dry run finished! File saved to {outputFile}");
				return;
			}

			if (particlemanifest)
			{
				ParticleManifest manifest = new(sourceDirectories, excludeDirs, excludeFiles, bsp, Config.BSPFile, Config.GameFolder);
				bsp.ParticleManifest = manifest.particleManifest;
			}

			Console.WriteLine("Running bspzip...");
			PackBSP(outputFile);

			if (compress)
			{
				Console.WriteLine("Compressing BSP...");
				CompressBSP();
			}

			Directory.Delete(Config.TempFolder, true);
			Message.Success("Finished!");
		}

		static BSP LoadBSP(FileInfo fileInfo)
		{
			bool attempt = false;

			while (true)
			{
				try
				{
					BSP bsp = new(fileInfo);
					return bsp;
				}
				catch (Exception ex)
				{
					if (attempt)
					{
						Message.Error("ERROR: Decompression failed, exiting.");
						Environment.Exit(1);
					}
					Console.WriteLine($"{ex.Message}");
					CompressBSP(decompress: true);
					attempt = true;
				}
			}
		}

		static void LoadPathsFromFile(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Message.Error($"{filePath} not found, run 'bspPack --modify' to create it");
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
							Message.Warning($"WARNING: Could not find file {trimmedLine}");
							break;
						}
						includeFiles.Add(trimmedLine);
						break;
					case "IncludeFileLists":
						if (!File.Exists(trimmedLine))
						{
							Message.Warning($"WARNING: Could not find file {trimmedLine}");
							break;
						}
						includeFileLists.Add(trimmedLine);
						break;
					case "IncludeDirs":
						if (!Directory.Exists(trimmedLine))
						{
							Message.Warning($"WARNING: Could not find directory {trimmedLine}");
							break;
						}
						includeDirs.Add(trimmedLine);
						break;
					case "IncludeSourceDirectories":
						if (!Directory.Exists(trimmedLine))
						{
							Message.Warning($"WARNING: Could not find directory {trimmedLine}");
							break;
						}
						includeSourceDirectories.Add(trimmedLine);
						break;
					case "ExcludeFiles":
						if (!File.Exists(trimmedLine))
						{
							Message.Warning($"WARNING: Could not find file {trimmedLine}");
							break;
						}
						Console.WriteLine($"EXCLUDED FILE: {trimmedLine.Replace('\\', '/')}");
						excludeFiles.Add(trimmedLine.Replace("\\", "/"));
						break;
					case "ExcludeDirs":
						if (!Directory.Exists(trimmedLine))
						{
							Message.Warning($"WARNING: Could not find directory {trimmedLine}");
							break;
						}
						excludeDirs.Add(trimmedLine.Replace("\\", "/"));
						break;
					case "ExcludeVpkFiles":
						if (!File.Exists(trimmedLine))
						{
							Message.Warning($"WARNING: Could not find file {trimmedLine}");
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
				Message.Error(e.ToString());
				Message.Error($"Failed to run executable: {p.StartInfo.FileName}\n");
				return;
			}

			string output = p.StandardOutput.ReadToEnd();
			if (verbose)
				Console.WriteLine(output);

			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				// this indicates an access violation. BSPZIP may have crashed because of too many files being packed
				if (p.ExitCode == -1073741819)
					Message.Error($"BSPZIP exited with code: {p.ExitCode}, this might indicate that too many files are being packed\n");
				else
					Message.Error($"BSPZIP exited with code: {p.ExitCode}\n");

				Environment.Exit(p.ExitCode);
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
				Message.Error(e.ToString());
				Message.Error($"Failed to run executable: {p.StartInfo.FileName}");
				return;
			}

			string output = p.StandardOutput.ReadToEnd();
			if (verbose)
				Console.WriteLine(output);

			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				// this indicates an access violation. BSPZIP may have crashed because of too many files being packed
				if (p.ExitCode == -1073741819)
					Message.Error($"BSPZIP exited with code: {p.ExitCode}, this might indicate that too many files are being packed");
				else
					Message.Error($"BSPZIP exited with code: {p.ExitCode}");

				Environment.Exit(p.ExitCode);
			}
		}

		static void LowercaseMaterials()
		{
			string materialsPath = Path.Join(Config.GameFolder, "materials");

			if (!Directory.Exists(materialsPath))
				Message.Error($"Directory doesn't exist: {materialsPath}, lowercasing cancelled");

			Message.Warning($"You are trying to lowercase all directories, files, and content of .vmt files in:\n{materialsPath}");
			Message.Warning("Please make sure you have a backup in case something goes wrong!");
			string? input = Message.Prompt("Do you want to continue? [y/N]: ", ConsoleColor.Yellow);

			if (string.IsNullOrWhiteSpace(input) || !input.Trim().StartsWith("y", StringComparison.CurrentCultureIgnoreCase))
			{
				Message.Error("Lowercasing Cancelled, exiting");
				Environment.Exit(0);
			}

			int skippedCount = 0;
			try
			{
				ProcessDirectories(materialsPath, ref skippedCount);
				if (skippedCount == 0)
					Message.Success("Files and Directories successfully lowercased!");
				else
					Message.Warning("\nFiles and/or directories were skipped during lowercasing. Some files might not pack correctly");
			}
			catch (Exception e)
			{
				Message.Error($"An error occurred during lowercasing: {e.Message}");
				Message.Error("Operation aborted. Please check your files and try again.");
			}

			static void ProcessDirectories(string path, ref int skippedCount)
			{
				foreach (var dir in Directory.GetDirectories(path))
				{
					ProcessDirectories(dir, ref skippedCount);
				}

				foreach (var filePath in Directory.GetFiles(path))
				{
					string fileName = Path.GetFileName(filePath);
					string lowerFileName = fileName.ToLowerInvariant();

					if (filePath.EndsWith(".vmt", StringComparison.OrdinalIgnoreCase))
					{
						string[] lines = File.ReadAllLines(filePath);
						if (lines.Length > 0)
						{
							string firstLine = lines[0];
							List<string> lowerLines = [.. lines.Skip(1).Select(line => line.ToLowerInvariant())];
							lowerLines.Insert(0, firstLine);

							File.WriteAllLines(filePath, lowerLines);
						}
					}

					if (fileName != lowerFileName)
					{
						string newFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, lowerFileName);
						if (!File.Exists(newFilePath))
							File.Move(filePath, newFilePath);
						else
						{
							Message.Warning($"WARNING: Skipped renaming:\n{filePath}\nLowercased file already exists");
							skippedCount++;
						}
					}
				}

				string dirName = Path.GetFileName(path);
				string parentDir = Path.GetDirectoryName(path)!;
				string lowerDirName = dirName.ToLowerInvariant();

				if (dirName != lowerDirName)
				{
					string newDirPath = Path.Combine(parentDir, lowerDirName);
					if (!Directory.Exists(newDirPath))
						Directory.Move(path, newDirPath);
					else
					{
						Message.Warning($"WARNING: Skipped renaming directory {path}\nLowercased directory already exists");
						skippedCount++;
					}
				}

			}
		}
	}
}
