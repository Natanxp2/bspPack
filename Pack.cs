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
using ValveKeyValue;

namespace BSPPackStandalone
{
	class BSPPack
	{
		private static KVSerializer KVSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
		
		private static List<string> sourceDirectories = new List<string>();
		private static List<string> includeFiles = new List<string>();
        private static List<string> excludeFiles = new List<string>();
        private static List<string> excludeDirs = new List<string>();
        private static List<string> excludedVpkFiles = new List<string>();
		private static string outputFile = "BSPZipFiles/files.txt";
	
		static void Main(string[] args)
		{
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
			
			FileInfo fileInfo = new FileInfo(Config.BSPFile);
			BSP bsp = new BSP(fileInfo);
			
			string unpackDir = Path.Combine(Config.TempFolder, Guid.NewGuid().ToString());
			AssetUtils.UnpackBSP(unpackDir);
			AssetUtils.findBspPakDependencies(bsp, unpackDir);
			
			sourceDirectories = AssetUtils.GetSourceDirectories(Config.GameFolder);
			AssetUtils.findBspUtilityFiles(bsp, sourceDirectories, false, false);
			
			PakFile pakfile = new PakFile(bsp, sourceDirectories, includeFiles, excludeFiles, excludeDirs, excludedVpkFiles, outputFile, false);
			pakfile.OutputToFile();
			
			PackBSP(outputFile);
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
            if (true) 
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