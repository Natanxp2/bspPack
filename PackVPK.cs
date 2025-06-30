using System.ComponentModel.Design;
using System.Diagnostics;

namespace bspPack;

partial class BSPPack
{
    static void PackVPK(PakFile pakfile)
    {
        Dictionary<string, string> responseFile = pakfile.GetResponseFile();
        string vpkPath;
        string finalLocation;

        if (File.Exists(Config.BSPFile))
        {
            vpkPath = Path.Combine(Config.ExeDirectory, Path.GetFileNameWithoutExtension(Config.BSPFile) + ".vpk");
            finalLocation = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileNameWithoutExtension(Config.BSPFile) + ".vpk");
        }
        else
        {
            vpkPath = Path.Combine(Config.ExeDirectory, "packed_files.vpk");
            finalLocation = vpkPath;
        }

        if (File.Exists(vpkPath))
            File.Delete(vpkPath);

        string addonPath = string.Empty;
        if (modify)
        {
            addonPath = GetAddonPath();
            if (File.Exists(addonPath))
            {
                File.Copy(addonPath, Path.Combine(Config.GameFolder, "addoninfo.txt"), true);
                responseFile.Add("addoninfo.txt", Config.GameFolder);
            }
        }

        //I have absolutely no idea why this is useful or what compilePal is actually doing.

        //It seems to assume the bsp is already in gameFolder and if it's not it just won't be able to pack it.

        //It also packs it by default if it's found but I don't understand why that would be desired in all cases.

        //I believe what's meant to happen is add the bsp from /maps folder so that it has a proper structure in the vpk however another possibilty is
        //using the same approach that's used for addon info files, copy the bsp to GameFolder and adding from there

        //I need for research this more but for now I'll just add it from /maps as that seems more likely to be correct

        //It's very possible I'm just doing something dumb here, if you are reading this and understand it, please let me know what's happening!
        //Relevant code in CompilePal is in Pack.cs -> if(packvpk) block
        //https://github.com/ruarai/CompilePal/blob/c52131c36f28d24d8ae969d8bad1eec19483f31c/CompilePalX/Compilers/BSPPack/Pack.cs#L319

        // string bspCopyPath = string.Empty;
        if (File.Exists(Config.BSPFile) && PackBSPToVPK())
        {
            //Copy implementation if that turns out to be the proper approach

            string bspName = Path.GetFileName(Config.BSPFile);
            // bspCopyPath = Path.Combine(Config.GameFolder, bspName);
            // File.Copy(Config.BSPFile, bspCopyPath, true);

            string mapsBspPath = Path.Combine(Config.GameFolder, "maps", bspName);
            if (File.Exists(mapsBspPath))
            {
                Message.Warning("BSP will be packed from /maps folder of the game directory. Please make sure it's the same file you are using to pack the vpk!");
                responseFile.Add(Path.Combine("maps", bspName), Config.GameFolder);
            }
            else
            {
                Message.Error("BSP file was not found in /maps folder of the game directory. If it's not there it can't packed correctly");
            }
            // responseFile.Add(bspName, Config.GameFolder);
        }

        Console.WriteLine("Running VPK...");
        foreach (string path in sourceDirectories)
        {
            string testedFiles = string.Empty;
            foreach (var entry in responseFile)
            {
                if (entry.Value.Contains(path) || path.Contains(entry.Value))
                    testedFiles += entry.Key + "\n";
            }

            string combinedPath = Path.Combine(path, "_tempResponseFile.txt");
            File.WriteAllText(combinedPath, testedFiles);

            PackVPK(vpkPath, combinedPath, path);

            File.Delete(combinedPath);
        }

        //Cleanup temp files
        if (File.Exists(finalLocation) && vpkPath != finalLocation)
            File.Delete(finalLocation);

        if (File.Exists(addonPath))
            File.Delete(addonPath);

        // if (File.Exists(bspCopyPath))
        //     File.Delete(bspCopyPath);

        if (new FileInfo(vpkPath).Length == 77)
        {
            Message.Error("No files were packed into the vpk");
            File.Delete(vpkPath);
            return;
        }

        if (vpkPath != finalLocation)
            File.Move(vpkPath, finalLocation);

        Message.Success($"Packed VPK and saved to {finalLocation}");
    }

    static void PackVPK(string targetVPK, string responseFile, string searchPath)
    {
        string arguments = $"a \"{targetVPK}\" \"@{responseFile}\"";

        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = searchPath,
                FileName = Config.VPK,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        try
        {
            p.Start();
        }
        catch (Exception e)
        {
            Message.Error(e.ToString());
            Message.Error("Failed to run vpk packing tool");
            Environment.Exit(1);
        }

        string output = p.StandardOutput.ReadToEnd();
        string errOutput = p.StandardError.ReadToEnd();

        if (verbose)
        {
            Console.WriteLine(output);
            Console.WriteLine(errOutput);
        }

        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            // this indicates an access violation. BSPZIP may have crashed because of too many files being packed
            // I'm gonna assume this is the same for vpk, no exit code check exists in compilePal for vpk;
            if (p.ExitCode == -1073741819)
                Message.Error($"VPK tool exited with code: {p.ExitCode}, this might indicate that too many files are being packed");

            //This means relative path to files was not found, continue looking
            else if (p.ExitCode == 1)
                return;

            else
                Message.Error($"VPK tool exited with code: {p.ExitCode}");

            DeleteTempFiles();
            Environment.Exit(p.ExitCode);
        }
    }

    static string GetAddonPath()
    {
        if (addonInfo.Count == 0)
            return string.Empty;

        addonInfo = [.. addonInfo.Where(File.Exists)];

        if (addonInfo.Count == 0)
        {
            Message.Warning("Addon Info paths exist in ResourceConfig.ini but were all invalid");
            return string.Empty;
        }
        if (addonInfo.Count == 1)
            return addonInfo[0];

        Message.Warning("Multiple addoninfo paths found but only one can be packed:");
        for (int i = 0; i < addonInfo.Count; i++)
        {
            Message.Write($"[{i + 1}]: ");
            Message.Warning(addonInfo[i]);
        }
        Message.Warning("Choose which one to pack: ");

        int selected = -1;
        while (selected < 1 || selected > addonInfo.Count)
        {
            string? input = Console.ReadLine();
            if (!int.TryParse(input, out selected) || selected < 1 || selected > addonInfo.Count)
            {
                Message.Write("Invalid selection. Please enter a valid number: ", ConsoleColor.Yellow);
            }
        }

        return addonInfo[selected - 1];
    }

    static bool PackBSPToVPK()
    {
        string? input = Message.Prompt("Do you want to pack the bsp file into the vpk? ( bsp needs to be in /maps folder ) [y/N]: ", ConsoleColor.Blue);

        if (string.IsNullOrWhiteSpace(input) || !input.Trim().StartsWith("y", StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }
        return true;
    }

    static void UnpackVPK()
    {
        string arguments = Config.BSPFile;

        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Config.VPK,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        try
        {
            p.Start();
        }
        catch (Exception e)
        {
            Message.Error(e.ToString());
            Message.Error("Failed to run vpk packing tool");
            Environment.Exit(1);
        }

        string output = p.StandardOutput.ReadToEnd();
        string errOutput = p.StandardError.ReadToEnd();

        if (verbose)
        {
            Console.WriteLine(output);
            Console.WriteLine(errOutput);
        }

        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            // this indicates an access violation. BSPZIP may have crashed because of too many files being packed
            // I'm gonna assume this is the same for vpk, no exit code check exists compilePal for vpk;
            if (p.ExitCode == -1073741819)
                Message.Error($"VPK tool exited with code: {p.ExitCode}, this might indicate that too many files are being packed");
            else
                Message.Error($"VPK tool exited with code: {p.ExitCode}");

            DeleteTempFiles();
            Environment.Exit(p.ExitCode);
        }
    }

    static string[] GetVPKFileList(string VPKPath)
    {
        string arguments = $"l \"{VPKPath}\"";

        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Config.VPK,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        p.Start();

        string output = p.StandardOutput.ReadToEnd();
        string errOutput = p.StandardError.ReadToEnd();
        if (verbose)
        {
            Console.WriteLine(output);
            Console.WriteLine(errOutput);
        }

        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            // this indicates an access violation. BSPZIP may have crashed because of too many files being packed
            // I'm gonna assume this is the same for vpk, no exit code check exists compilePal for vpk;
            if (p.ExitCode == -1073741819)
                Message.Error($"VPK tool exited with code: {p.ExitCode}, this might indicate that too many files are being packed");
            else
                Message.Error($"VPK tool exited with code: {p.ExitCode}");

            DeleteTempFiles();
            Environment.Exit(p.ExitCode);
        }

        char[] delims = ['\r', '\n'];
        return output.Split(delims, StringSplitOptions.RemoveEmptyEntries);
    }
}