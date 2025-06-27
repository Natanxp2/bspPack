using System.Text.RegularExpressions;
using BSPPackStandalone.UtilityProcesses;

namespace BSPPackStandalone
{
    class PakFile
    {
        // This class is the class responsible for building the list of files to include
        // The list can be saved to a text file for use with bspzip

        // the dictionary is formated as <internalPath, externalPath>
        // matching the bspzip specification https://developer.valvesoftware.com/wiki/BSPZIP
        private IDictionary<string, string> Files;

        private bool AddFile(string internalPath, string externalPath)
        {
            if (externalPath.Length > 256)
                Message.Error($"File length over 256 characters, file may not pack properly:\n{externalPath}");

            return AddFile(new KeyValuePair<string, string>(internalPath, externalPath));
        }
        // onFailure is for utility files such as nav, radar, etc which get excluded. if they are excluded, the Delegate is run. This is used for removing the files from the BSP class, so they dont appear in the summary at the end
        private bool AddFile(KeyValuePair<string, string> paths, Action<BSP>? onExcluded = null, BSP? bsp = null)
        {
            var externalPath = paths.Value.Replace('\\', '/');
            // exclude files that are excluded
            if (externalPath != "" && File.Exists(externalPath)
                                   && !excludedFiles.Any(file => file.Equals(externalPath, StringComparison.OrdinalIgnoreCase))
                                   && !excludedDirs.Any(dir => externalPath.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                                   && !excludedVpkFiles.Any(vpkFile => vpkFile.Equals(paths.Key, StringComparison.OrdinalIgnoreCase)))
            {
                Files.Add(paths);
                return true;
            }

            if (onExcluded != null && bsp != null)
            {
                onExcluded(bsp);
            }

            return false;
        }

        /// <summary>
        /// Adds a generic file dependency and tries to determine file type by extension
        /// </summary>
        /// <param name="internalPath"></param>
        /// <param name="externalPath"></param>
        private void AddGenericFile(string internalPath, string externalPath)
        {
            FileInfo fileInfo = new FileInfo(externalPath);

            // try to determine file type by extension
            switch (fileInfo.Extension)
            {
                case ".vmt":
                    AddTexture(internalPath);
                    break;
                case ".pcf":
                    AddParticle(internalPath);
                    break;
                case ".mdl":
                    AddModel(internalPath);
                    break;
                case ".wav":
                case ".mp3":
                    AddSound(internalPath);
                    break;
                case ".res":
                    AddInternalFile(internalPath, externalPath);
                    foreach (string material in AssetUtils.FindResMaterials(externalPath))
                        AddTexture(material);
                    break;
                case ".nut":
                    AddVScript(internalPath);
                    break;
                default:
                    AddInternalFile(internalPath, externalPath);
                    break;
            }
        }
        private bool AddFile(string externalPath)
        {
            if (!File.Exists(externalPath))
                return false;

            // try to get the source directory the file is located in
            FileInfo fileInfo = new FileInfo(externalPath);

            // default base directory is the game folder
            string baseDir = Config.GameFolder;

            var potentialSubDir = new List<string>(sourceDirs); // clone to prevent accidental modification
            potentialSubDir.Remove(baseDir);
            foreach (var folder in potentialSubDir)
            {
                if (fileInfo.Directory != null
                    && fileInfo.Directory.FullName.Contains(folder, StringComparison.OrdinalIgnoreCase))
                {
                    baseDir = folder;
                    break;
                }
            }

            // check needed for when file does not exist in any sub directory or the base directory
            if (fileInfo.Directory != null && !fileInfo.Directory.FullName.ToLower().Contains(baseDir.ToLower()))
            {
                return false;
            }

            string internalPath = Regex.Replace(externalPath, Regex.Escape(baseDir + Path.DirectorySeparatorChar), "", RegexOptions.IgnoreCase);

            AddGenericFile(internalPath, externalPath);
            return true;
        }

        private List<string> excludedFiles;
        private List<string> excludedDirs;
        private List<string> excludedVpkFiles;

        private List<string> sourceDirs;
        private string fileName;

        public int mdlcount { get; private set; }
        public int vmtcount { get; private set; }
        public int pcfcount { get; private set; }
        public int soundcount { get; private set; }
        public int vehiclescriptcount { get; private set; }
        public int effectscriptcount { get; private set; }
        public int vscriptcount { get; private set; }
        public int PanoramaMapBackgroundCount { get; private set; }

        private bool noSwvtx;

        public PakFile(BSP bsp, List<string> sourceDirectories, List<string> includeFiles, List<string> excludedFiles, List<string> excludedDirs, List<string> excludedVpkFiles, string outputFile, bool noswvtx)
        {
            mdlcount = vmtcount = pcfcount = soundcount = vehiclescriptcount = effectscriptcount = PanoramaMapBackgroundCount = 0;
            sourceDirs = sourceDirectories;
            fileName = outputFile;
            noSwvtx = noswvtx;

            this.excludedFiles = excludedFiles;
            this.excludedDirs = excludedDirs;
            this.excludedVpkFiles = excludedVpkFiles;
            Files = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (bsp.Nav.Key != default(string))
                AddFile(bsp.Nav, (b => b.Nav = default), bsp);

            if (bsp.Detail.Key != default(string))
                AddFile(bsp.Detail, (b => b.Detail = default), bsp);

            if (bsp.Kv.Key != default(string))
                AddFile(bsp.Kv, (b => b.Kv = default), bsp);

            if (bsp.Txt.Key != default(string))
                AddFile(bsp.Txt, (b => b.Txt = default), bsp);

            if (bsp.Jpg.Key != default(string))
                AddFile(bsp.Jpg, (b => b.Jpg = default), bsp);

            if (bsp.Radartxt.Key != default(string))
                AddFile(bsp.Radartxt, (b => b.Radartxt = default), bsp);

            if (bsp.RadarTablet.Key != default(string))
                AddFile(bsp.RadarTablet, (b => b.RadarTablet = default), bsp);

            if (bsp.PanoramaMapIcon.Key != default(string))
            {
                AddFile(bsp.PanoramaMapIcon, (b => b.PanoramaMapIcon = default), bsp);
            }

            if (bsp.ParticleManifest.Key != default(string))
            {
                if (AddFile(bsp.ParticleManifest, (b => b.ParticleManifest = default), bsp))
                {
                    foreach (string particle in AssetUtils.FindManifestPcfs(bsp.ParticleManifest.Value))
                        AddParticle(particle);
                }
            }

            if (bsp.Soundscape.Key != default(string))
            {
                if (AddFile(bsp.Soundscape, (b => b.Soundscape = default), bsp))
                {
                    foreach (string sound in AssetUtils.FindSoundscapeSounds(bsp.Soundscape.Value))
                        AddSound(sound);
                }
            }

            if (bsp.Soundscript.Key != default(string))
            {
                if (AddFile(bsp.Soundscript, (b => b.Soundscript = default), bsp))
                {
                    foreach (string sound in AssetUtils.FindSoundscapeSounds(bsp.Soundscript.Value))
                        AddSound(sound);
                }
            }

            foreach (KeyValuePair<string, string> vehicleScript in bsp.VehicleScriptList)
                if (AddInternalFile(vehicleScript.Key, vehicleScript.Value))
                    vehiclescriptcount++;
            foreach (KeyValuePair<string, string> effectScript in bsp.EffectScriptList)
                if (AddInternalFile(effectScript.Key, effectScript.Value))
                    effectscriptcount++;
            foreach (KeyValuePair<string, string> dds in bsp.Radardds)
                AddInternalFile(dds.Key, dds.Value);
            foreach (KeyValuePair<string, string> lang in bsp.Languages)
                AddInternalFile(lang.Key, lang.Value);
            foreach (string model in bsp.EntModelList)
                AddModel(model);
            for (int i = 0; i < bsp.ModelList.Count; i++)
                AddModel(bsp.ModelList[i], bsp.ModelSkinList[i]);
            foreach (string vmt in bsp.TextureList)
                AddTexture(vmt);
            foreach (string vmt in bsp.EntTextureList)
                AddTexture(vmt);
            foreach (string misc in bsp.MiscList)
                AddInternalFile(misc, FindExternalFile(misc));
            foreach (string sound in bsp.EntSoundList)
                AddSound(sound);
            foreach (string vscript in bsp.VscriptList)
                AddVScript(vscript);
            foreach (KeyValuePair<string, string> teamSelectionBackground in bsp.PanoramaMapBackgrounds)
                if (AddInternalFile(teamSelectionBackground.Key, teamSelectionBackground.Value))
                    PanoramaMapBackgroundCount++;
            foreach (var res in bsp.Res)
            {
                if (AddFile(res, null, bsp))
                {
                    foreach (string material in AssetUtils.FindResMaterials(res.Value))
                        AddTexture(material);
                }

            }

            // add all manually included files
            foreach (var file in includeFiles)
            {
                if (!AddFile(file))
                    Message.Warning($"WARNING: Failed to resolve internal path for {file}, skipping\n");
            }
        }

        public void OutputToFile()
        {
            var outputLines = new List<string>();

            foreach (KeyValuePair<string, string> entry in Files)
            {
                outputLines.Add(entry.Key);
                outputLines.Add(entry.Value);
            }

            if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BSPZipFiles")))
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BSPZipFiles"));

            if (File.Exists(fileName))
                File.Delete(fileName);
            File.WriteAllLines(fileName, outputLines);
        }

        public Dictionary<string, string> GetResponseFile()
        {
            var output = new Dictionary<string, string>();

            foreach (var entry in Files)
            {
                output.Add(entry.Key, entry.Value.Replace(entry.Key, ""));
            }

            return output;
        }

        public bool AddInternalFile(string internalPath, string externalPath)
        {
            internalPath = internalPath.Replace("\\", "/");
            // sometimes internal paths can be relative, ex. "materials/vgui/../hud/logos/spray.vmt" should be stored as "materials/hud/logos/spray.vmt".
            internalPath = Regex.Replace(internalPath, @"\/.*\/\.\.", "");
            if (!Files.ContainsKey(internalPath))
            {
                return AddFile(internalPath, externalPath);
            }

            return false;
        }

        public void AddModel(string internalPath, List<int>? skins = null)
        {
            // adds mdl files and finds its dependencies
            string externalPath = FindExternalFile(internalPath);
            if (AddInternalFile(internalPath, externalPath))
            {
                mdlcount++;
                List<string> vtxMaterialNames = [];
                foreach (string reference in AssetUtils.FindMdlRefs(internalPath))
                {
                    string ext_path = FindExternalFile(reference);

                    //don't pack .sw.vtx files if param is set
                    if (reference.EndsWith(".sw.vtx") && this.noSwvtx)
                        continue;

                    AddInternalFile(reference, ext_path);

                    if (reference.EndsWith(".phy"))
                        foreach (string gib in AssetUtils.FindPhyGibs(ext_path))
                            AddModel(gib);

                    if (reference.EndsWith(".vtx"))
                    {
                        try
                        {
                            vtxMaterialNames.AddRange(AssetUtils.FindVtxMaterials(ext_path));
                        }
                        catch (Exception)
                        {
                            Message.Warning($"WARNING: Failed to find vtx materials for file {ext_path}");
                        }

                    }
                }

                Tuple<List<string>, List<string>> mdlMatsAndModels;
                try
                {
                    mdlMatsAndModels = AssetUtils.FindMdlMaterialsAndModels(externalPath, skins, vtxMaterialNames);
                }
                catch (Exception)
                {
                    Message.Warning($"WARNING: Failed to read file {externalPath}");
                    return;
                }

                foreach (string mat in mdlMatsAndModels.Item1)
                    AddTexture(mat);

                foreach (var model in mdlMatsAndModels.Item2)
                    AddModel(model, null);

            }
        }

        public void AddTexture(string internalPath)
        {
            // adds vmt files and finds its dependencies
            string externalPath = FindExternalFile(internalPath);
            if (AddInternalFile(internalPath, externalPath))
            {
                vmtcount++;
                foreach (string vtf in AssetUtils.FindVmtTextures(externalPath))
                    AddInternalFile(vtf, FindExternalFile(vtf));
                foreach (string vmt in AssetUtils.FindVmtMaterials(externalPath))
                    AddTexture(vmt);
            }
        }

        public void AddParticle(string internalPath)
        {
            // adds pcf files and finds its dependencies
            string externalPath = FindExternalFile(internalPath);
            if (externalPath == String.Empty)
            {
                Message.Warning($"WARNING: Failed to find particle manifest file {externalPath}");
                return;
            }

            PCF? pcf = ParticleUtils.ReadParticle(externalPath);
            if (AddInternalFile(internalPath, externalPath) && pcf != null)
            {
                pcfcount++;
                foreach (string mat in pcf.MaterialNames)
                    AddTexture(mat);

                foreach (string model in pcf.ModelNames)
                    AddModel(model);
            }
            else
            {
                Message.Warning($"WARNING: Failed to find particle manifest file {externalPath}");
                return;
            }
        }

        public void AddSound(string internalPath)
        {
            // adds vmt files and finds its dependencies
            string externalPath = FindExternalFile(internalPath);
            if (AddInternalFile(internalPath, externalPath))
            {
                soundcount++;
            }
        }

        /// <summary>
        /// Adds VScript file and finds it's dependencies
        /// </summary>
        /// <param name="internalPath"></param>
        public void AddVScript(string internalPath)
        {
            string externalPath = FindExternalFile(internalPath);

            // referenced scripts don't always have extension, try adding .nut
            if (externalPath == string.Empty)
            {
                var newInternalPath = $"{internalPath}.nut";
                externalPath = FindExternalFile(newInternalPath);

                // if we find the file with the .nut extension, update the internal path to include it
                if (externalPath != string.Empty)
                    internalPath = newInternalPath;
            }

            if (!AddInternalFile(internalPath, externalPath))
            {
                Message.Warning($"WARNING: Failed to find VScript file {internalPath}\n");
                return;
            }
            vscriptcount++;

            var (vscripts, models, sounds, includedFiles, includedDirectories) = AssetUtils.FindVScriptDependencies(externalPath);
            foreach (string vscript in vscripts)
                AddVScript(vscript);
            foreach (string model in models)
                AddModel(model);
            foreach (string sound in sounds)
                AddSound(sound);
            foreach (string internalDirectoryPath in includedDirectories)
            {
                var externalDirectoriesPaths = FindExternalDirectories(internalDirectoryPath);
                if (externalDirectoriesPaths.Count == 0)
                {
                    Message.Warning($"WARNING: Failed to resolve external path for VScript hint {internalDirectoryPath}, skipping\n");
                    continue;
                }

                foreach (var externalDirectoryPath in externalDirectoriesPaths)
                {
                    var files = Directory.GetFiles(externalDirectoryPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        AddFile(file);
                    }
                }
            }
            foreach (string internalFilePath in includedFiles)
            {
                var externalFilePath = FindExternalFile(internalFilePath);
                if (!File.Exists(externalFilePath))
                {
                    Message.Warning($"WARNING: Failed to resolve external path for VScript hint {internalFilePath}, skipping\n");
                    continue;
                }

                AddGenericFile(internalFilePath, externalPath);
            }
        }

        private string FindExternalFile(string internalPath)
        {
            // Attempts to find the file from the internalPath
            // returns the externalPath or an empty string

            var sanitizedPath = SanitizePath(internalPath);

            foreach (string source in sourceDirs)
                if (File.Exists(Path.Combine(source, sanitizedPath)))
                    return Path.Combine(source, sanitizedPath.Replace("\\", "/"));
            return "";
        }

        private List<string> FindExternalDirectories(string internalPath)
        {
            // Attempts to find the directory from the internalPath
            // returns the externalPath or null

            // Note: unlike with files, the user can have several folders
            // with matching internal names but with different contents
            // spread across several custom folders 
            // If the user desires a folder override rather than a merge
            // they can use -excludedir to exclude the unwanted folder

            var sanitizedPath = SanitizePath(internalPath);
            var externalDirs = new List<string>();

            foreach (string source in sourceDirs)
                if (Directory.Exists(Path.Combine(source, sanitizedPath)))
                    externalDirs.Add(Path.Combine(source, sanitizedPath.Replace("\\", "/")));
            return externalDirs;
        }


        private static readonly string invalidChars = Regex.Escape(new string(Path.GetInvalidPathChars()));
        private static readonly string invalidRegString = $@"([{invalidChars}]*\.+$)|([{invalidChars}]+)";
        private string SanitizePath(string path)
        {
            return Regex.Replace(path, invalidRegString, "");
        }
    }
}
