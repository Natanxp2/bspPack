using System.Diagnostics;
using System.Runtime.InteropServices;
using GlobExpressions;

namespace bspPack;

partial class BSPPack
{
	private static List<string> sourceDirectories = [];
	private static readonly List<string> includeFiles = [];
	private static readonly List<string> includeFileLists = [];
	private static readonly List<string> includeDirs = [];
	private static readonly List<string> includeSourceDirectories = [];
	private static readonly List<string> excludeFiles = [];
	private static readonly List<string> excludeDirs = [];
	private static readonly List<string> excludeVpks = [];
	private static readonly List<string> excludeVpkFiles = [];
	private static List<string> addonInfo = [];

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
	private static bool vpk;

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
		vpk = args.Contains("-VPK") || args.Contains("--packvpk");

		Config.LoadConfig(Path.Combine(Config.ExeDirectory, "config.ini"));

		if (args.Length == 0)
		{
			string helpMessage = @"
Provide a path to a bsp to pack it.
Provide a path to a vpk path to unpack it.
## Flags
-V   | --verbose            Outputs a complete listing of added assets
-D   | --dryrun             Creates a txt file for bspzip usage but does not pack
-R   | --renamenav          Renames the nav file to embed.nav
-N   | --noswvtx            Skips packing unused .sw.vtx files to save filesize
-P   | --particlemanifest   Generates a particle manifest based on particles used
-C   | --compress           Compresses the BSP after packing
-M   | --modify             Modifies PakFile based on ResourceConfig.ini
-U   | --unpack             Unpacks the BSP to <filename>_unpacked
-S   | --search             Searches /maps folder of the game directory for the BSP file
-L   | --lowercase          Lowercases content of /materials and /models directories
-VPK | --packvpk            Packs content of a bsp to a vpk. Can be used with -M flag without providing a bsp to pack only desired files  
";
			Console.WriteLine(helpMessage);
			return;
		}
		if (lowercase)
			LowercaseAssets();

		if (search)
			Config.BSPFile = Path.Combine(Config.GameFolder, "maps", args[^1]);
		else
			Config.BSPFile = args[^1];

		if (!File.Exists(Config.BSPFile) && !vpk)
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

		if (Config.BSPFile.EndsWith(".vpk"))
		{
			UnpackVPK();
			Message.Success($"Unpacked the vpk to {Path.Combine(Directory.GetCurrentDirectory(), Config.BSPFile[..^3])}");
			return;
		}

		if (modify)
			LoadPathsFromResourceConfig(Path.Combine(Config.ExeDirectory, "ResourceConfig.ini"));

		if (includeDirs.Count != 0)
			GetFilesFromIncludedDirs();

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

		if (vpk && !File.Exists(Config.BSPFile))
		{
			PakFile vpkPakfile = new(sourceDirectories, includeFiles, excludeFiles, excludeDirs, excludeVpkFiles, outputFile, noswvtx);
			PackVPK(vpkPakfile);
			return;
		}

		Console.WriteLine("\nReading BSP...");
		FileInfo fileInfo = new(Config.BSPFile);
		BSP bsp = LoadBSP(fileInfo);

		string unpackDir;
		if (unpack)
		{
			unpackDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileNameWithoutExtension(bsp.File.FullName) + "_unpacked");
			AssetUtils.UnpackBSP(unpackDir);
			Message.Success($"BSP unpacked to: {unpackDir}");
			return;
		}

		unpackDir = Path.Combine(Config.TempFolder, Guid.NewGuid().ToString());
		AssetUtils.UnpackBSP(unpackDir);
		AssetUtils.FindBspPakDependencies(bsp, unpackDir);
		AssetUtils.FindBspUtilityFiles(bsp, sourceDirectories, renamenav, false);

		if (dryrun)
			outputFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BSPZipFiles", $"{Path.GetFileNameWithoutExtension(bsp.File.FullName)}_files.txt");

		if (particlemanifest && !dryrun)
		{
			ParticleManifest manifest = new(sourceDirectories, excludeDirs, excludeFiles, bsp, Config.BSPFile, Config.GameFolder);
			bsp.ParticleManifest = manifest.particleManifest;
		}

		Console.WriteLine("Initializing pak file...");
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

		if (vpk)
		{
			PackVPK(pakfile);
			DeleteTempFiles();
			return;
		}

		Console.WriteLine("Writing file list...");
		pakfile.OutputToFile();

		if (dryrun)
		{
			DeleteTempFiles();
			Message.Success($"Dry run finished! File saved to {outputFile}");
			return;
		}

		Console.WriteLine("Running bspzip...");
		PackBSP(outputFile);

		if (compress)
		{
			Console.WriteLine("Compressing BSP...");
			CompressBSP();
		}

		DeleteTempFiles();
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

	static void LoadPathsFromResourceConfig(string filePath)
	{
		if (!File.Exists(filePath))
		{
			Message.Error($"{filePath} not found, run 'bspPack --modify' to create it");
			Environment.Exit(1);
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
			var trimmedLine = line.Trim().Trim('"').TrimEnd('/', '\\');
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
				case "ExcludeVpks":
					if (!File.Exists(trimmedLine))
					{
						Message.Warning($"WARNING: Could not find file {trimmedLine}");
						break;
					}
					excludeVpks.Add(trimmedLine);
					break;
				case "AddonInfo":
					if (!File.Exists(trimmedLine))
					{
						Message.Warning($"WARNING: Could not find file {trimmedLine}");
						break;
					}
					addonInfo.Add(trimmedLine);
					break;
			}
		}

		if (excludeVpks.Count != 0)
		{
			foreach (string vpk in excludeVpks)
				excludeVpkFiles.AddRange(GetVPKFileList(vpk));
		}
	}

	static void GetFilesFromIncludedDirs()
	{
		foreach (string dir in includeDirs)
		{
			var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
			foreach (var file in files)
				includeFiles.Add(file);
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

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			PreloadBrokenLibraries(ref startInfo);

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

			DeleteTempFiles();
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

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			PreloadBrokenLibraries(ref startInfo);

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

			DeleteTempFiles();
			Environment.Exit(p.ExitCode);
		}
	}

	static void LowercaseAssets()
	{
		List<string> assetsPaths = [];
		assetsPaths.Add(Path.Join(Config.GameFolder, "materials"));
		assetsPaths.Add(Path.Join(Config.GameFolder, "models"));

		foreach (string path in assetsPaths)
		{
			if (!Directory.Exists(path))
				Message.Error($"Directory doesn't exist: {path}, lowercasing cancelled");

			Message.Warning($"\nYou are trying to lowercase all files and directories in:\n{path}");
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
				ProcessDirectories(path, ref skippedCount);
				if (skippedCount == 0)
					Message.Success("Files and Directories successfully lowercased!\n");
				else
					Message.Warning("\nFiles and/or directories were skipped during lowercasing. Some files might not pack correctly");
			}
			catch (Exception e)
			{
				Message.Error($"An error occurred during lowercasing: {e.Message}");
				Message.Error("Operation aborted. Please check your files and try again.");
			}
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

	static void DeleteTempFiles()
	{
		if (File.Exists(Config.BSPFile[..^4] + "_particles.txt"))
			File.Delete(Config.BSPFile[..^4] + "_particles.txt");

		if (Directory.Exists(Config.TempFolder))
			Directory.Delete(Config.TempFolder, recursive: true);
	}

	public static void PreloadBrokenLibraries(ref ProcessStartInfo startInfo)
	{
		string[] brokenGames = ["cstrike", "csgo", "portal", "portal2", "tf"];

		if (brokenGames.Contains(Path.GetFileName(Config.GameFolder)))
		{
			startInfo.Environment["LD_LIBRARY_PATH"] = Path.GetDirectoryName(Config.BSPZip);
			startInfo.Environment["LD_PRELOAD"] = "libmimalloc.so";
		}
	}
}
