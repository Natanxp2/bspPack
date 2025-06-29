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

        //Add proper addon handling when packing is working
        string addonPath = string.Empty;
        if (addonInfo.Count != 0)
        {
            addonPath = GetAddonPath();
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

        if (File.Exists(finalLocation) && vpkPath != finalLocation)
            File.Delete(finalLocation);

        if (File.Exists(addonPath))
            File.Delete(addonPath);

        if (new FileInfo(vpkPath).Length == 77)
        {
            Message.Error("No files were packed into the vpk");
            File.Delete(vpkPath);
            return;
        }

        if (vpkPath != finalLocation)
            File.Move(vpkPath, finalLocation);

        Message.Success($"Packed VPK saved to {finalLocation}");
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
            // I'm gonna assume this is the same for vpk, this check is not in compilePal;
            if (p.ExitCode == -1073741819)
                Message.Error($"VPK tool exited with code: {p.ExitCode}, this might indicate that too many files are being packed");

            //This means relative path to files was found, continue looking
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
        return string.Empty;
    }
}