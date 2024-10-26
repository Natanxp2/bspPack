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
		private static List<string> includedFiles = new List<string>();
		private static List<string> excludedFiles = new List<string>();
		private static List<string> excludedDirs = new List<string>();
		private static List<string> excludedVpkFiles = new List<string>();
		private static string outputFile = "BSPZipFiles/files.txt";
		
		private static bool verbose;
		private static bool dryrun;
		private static bool renamenav;
		private static bool noswvtx;
		private static bool genParticleManifest;
		private static bool compress;
	
		static void Main(string[] args)
		{
			Console.WriteLine("Reading BSP...");
			if (args.Length == 0)
			{
				Console.WriteLine("Please provide a path to the BSP file.");
				return;
			}
			
			Config.BSPFile = args[^1];
			
			if (!File.Exists(Config.BSPFile))
			{
				Console.WriteLine("File not found: " + Config.BSPFile);
				return;
			}
			
			verbose = args.Contains("--verbose");
			dryrun = args.Contains("--dryrun");
			renamenav = args.Contains("--renamenav");
			noswvtx = args.Contains("--noswvtx");
			genParticleManifest = args.Contains("--genParticleManifest");
			compress = args.Contains("--compress");

				
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
			PakFile pakfile = new PakFile(bsp, sourceDirectories, includedFiles, excludedFiles, excludedDirs, excludedVpkFiles, outputFile, noswvtx);
			Console.WriteLine("Writing file list...");
			pakfile.OutputToFile();
			
			if(dryrun)
			{
				Console.WriteLine($"Dry run finished! File saved to {outputFile}");
				return;
			}
			
			if(genParticleManifest)
			{
				ParticleManifest manifest = new ParticleManifest(sourceDirectories, excludedDirs, excludedFiles, bsp, Config.BSPFile, Config.GameFolder);
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