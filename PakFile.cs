﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BSPPackStandalone.UtilityProcess;

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
            {
				Console.WriteLine($"File length over 256 characters, file may not pack properly:\n{externalPath}");
			}
            return AddFile(new KeyValuePair<string, string>(internalPath, externalPath));
        }
        // onFailure is for utility files such as nav, radar, etc which get excluded. if they are excluded, the Delegate is run. This is used for removing the files from the BSP class, so they dont appear in the summary at the end
        private bool AddFile(KeyValuePair<string, string> paths, Action<BSP>? onExcluded = null, BSP? bsp = null)
        {
            var externalPath = paths.Value;

            // exclude files that are excluded
            if (externalPath != "" && File.Exists(externalPath)
                                   && !excludedFiles.Contains(externalPath.ToLower().Replace('/', '\\'))
                                   && !excludedDirs.Any(externalPath.ToLower().Replace('/', '\\').StartsWith)
                                   && !excludedVpkFiles.Contains(paths.Key.ToLower().Replace('\\', '/')))
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
                    foreach (string material in AssetUtils.findResMaterials(externalPath))
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
            string baseDir = Config.gameFolderPath;

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
            if (fileInfo.Directory != null && !fileInfo.Directory.FullName.ToLower().Contains(baseDir.ToLower())) {
                return false;
            }

            string internalPath = Regex.Replace(externalPath, Regex.Escape(baseDir + "\\"), "", RegexOptions.IgnoreCase);

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

            if (bsp.nav.Key != default(string))
                AddFile(bsp.nav, (b => b.nav = default), bsp);

            if (bsp.detail.Key != default(string))
                AddFile(bsp.detail, (b => b.detail = default), bsp);

            if (bsp.kv.Key != default(string))
                AddFile(bsp.kv, (b => b.kv = default), bsp);

            if (bsp.txt.Key != default(string))
                AddFile(bsp.txt, (b => b.txt = default), bsp);

            if (bsp.jpg.Key != default(string))
                AddFile(bsp.jpg, (b => b.jpg = default), bsp);

            if (bsp.radartxt.Key != default(string))
                AddFile(bsp.radartxt, (b => b.radartxt = default), bsp);

            if (bsp.RadarTablet.Key != default(string))
                AddFile(bsp.RadarTablet, (b => b.RadarTablet = default), bsp);

            if (bsp.PanoramaMapIcon.Key != default(string))
            {
                AddFile(bsp.PanoramaMapIcon, (b => b.PanoramaMapIcon = default), bsp);
            }

            if (bsp.particleManifest.Key != default(string))
            {
                if (AddFile(bsp.particleManifest, (b => b.particleManifest = default), bsp))
                {
                    foreach (string particle in AssetUtils.findManifestPcfs(bsp.particleManifest.Value))
                        AddParticle(particle);
                }
            }
            
            if (bsp.soundscape.Key != default(string))
            {
                if (AddFile(bsp.soundscape, (b => b.soundscape = default), bsp))
                {
                    foreach (string sound in AssetUtils.findSoundscapeSounds(bsp.soundscape.Value))
                        AddSound(sound);
                }
            }
            
            if (bsp.soundscript.Key != default(string))
            {
                if (AddFile(bsp.soundscript, (b => b.soundscript = default), bsp))
                {
                    foreach (string sound in AssetUtils.findSoundscapeSounds(bsp.soundscript.Value))
                        AddSound(sound);
                }
            }

            foreach (KeyValuePair<string, string> vehicleScript in bsp.VehicleScriptList)
                if (AddInternalFile(vehicleScript.Key, vehicleScript.Value))
                    vehiclescriptcount++;
	        foreach (KeyValuePair<string, string> effectScript in bsp.EffectScriptList)
		        if (AddInternalFile(effectScript.Key, effectScript.Value))
			        effectscriptcount++;
            foreach (KeyValuePair<string, string> dds in bsp.radardds)
                AddInternalFile(dds.Key, dds.Value);
            foreach (KeyValuePair<string, string> lang in bsp.languages)
                AddInternalFile(lang.Key, lang.Value);
            foreach (string model in bsp.EntModelList)
                AddModel(model);
            for (int i = 0; i < bsp.ModelList.Count; i++)
                AddModel(bsp.ModelList[i], bsp.modelSkinList[i]);
            foreach (string vmt in bsp.TextureList)
                AddTexture(vmt);
            foreach (string vmt in bsp.EntTextureList)
                AddTexture(vmt);
            foreach (string misc in bsp.MiscList)
                AddInternalFile(misc, FindExternalFile(misc));
            foreach (string sound in bsp.EntSoundList)
                AddSound(sound);
            foreach (string vscript in bsp.vscriptList)
                AddVScript(vscript);
            foreach (KeyValuePair<string, string> teamSelectionBackground in bsp.PanoramaMapBackgrounds)
                if (AddInternalFile(teamSelectionBackground.Key, teamSelectionBackground.Value))
                    PanoramaMapBackgroundCount++;
            foreach (var res in bsp.res)
            {
                if (AddFile(res, null, bsp))
                {
                    foreach (string material in AssetUtils.findResMaterials(res.Value))
                        AddTexture(material);
                }
                
            }

			// add all manually included files
	        foreach (var file in includeFiles)
	        {
                if (!AddFile(file))
					Console.WriteLine($"Failed to resolve internal path for {file}, skipping\n");
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

            if (!Directory.Exists("BSPZipFiles"))
                Directory.CreateDirectory("BSPZipFiles");

            if (File.Exists(fileName))
                File.Delete(fileName);
            File.WriteAllLines(fileName, outputLines);
        }

        public Dictionary<string,string> GetResponseFile()
        {
            var output = new Dictionary<string,string>();

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

        public void AddModel(string internalPath, List<int> skins = null)
        {
            // adds mdl files and finds its dependencies
			Console.WriteLine($"Packing model: {internalPath}");
            string externalPath = FindExternalFile(internalPath);
			Console.WriteLine($"External path: {internalPath}");
            if (AddInternalFile(internalPath, externalPath))
            {
                mdlcount++;
                List<string> vtxMaterialNames = new List<string>();
                foreach (string reference in AssetUtils.findMdlRefs(internalPath))
                {
                    string ext_path = FindExternalFile(reference);

                    //don't pack .sw.vtx files if param is set
                    if (reference.EndsWith(".sw.vtx") && this.noSwvtx) 
                        continue;

                    AddInternalFile(reference, ext_path);

                    if (reference.EndsWith(".phy"))
                        foreach (string gib in AssetUtils.findPhyGibs(ext_path))
                            AddModel(gib);

                    if (reference.EndsWith(".vtx"))
                    {
                        try
                        {
                            vtxMaterialNames.AddRange(AssetUtils.FindVtxMaterials(ext_path));
                        } catch (Exception e)
                        {
							Console.WriteLine($"Failed to find vtx materials for file {ext_path}");
							Console.WriteLine(e);
                        }

                    }
                }

                Tuple<List<string>, List<string>> mdlMatsAndModels;
                try
                {
	                mdlMatsAndModels = AssetUtils.findMdlMaterialsAndModels(externalPath, skins, vtxMaterialNames);
                }
                catch (Exception e)
                {
					Console.WriteLine($"Failed to read file {externalPath}");
					Console.WriteLine(e);
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
                foreach (string vtf in AssetUtils.findVmtTextures(externalPath))
                    AddInternalFile(vtf, FindExternalFile(vtf));
                foreach (string vmt in AssetUtils.findVmtMaterials(externalPath))
                    AddTexture(vmt);
            }
        }

        public void AddParticle(string internalPath)
        {
            // adds pcf files and finds its dependencies
            string externalPath = FindExternalFile(internalPath);
            if (externalPath == String.Empty)
            {
				Console.WriteLine($"Failed to find particle manifest file {internalPath}");
				return;
            }

            if (AddInternalFile(internalPath, externalPath))
            {

				PCF pcf = ParticleUtils.ReadParticle(externalPath);
                pcfcount++;
                foreach (string mat in pcf.MaterialNames)
                    AddTexture(mat);

                foreach (string model in pcf.ModelNames)
                {
                    AddModel(model);
                }
            }
            else
            {
				Console.WriteLine($"Failed to find particle manifest file {internalPath}");
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
				Console.WriteLine($"Failed to find VScript file {internalPath}\n");
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
                if (externalDirectoriesPaths.Count == 0) {
                    Console.WriteLine($"Failed to resolve external path for VScript hint {internalDirectoryPath}, skipping\n");
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
                if (!File.Exists(externalFilePath)) {            
                    Console.WriteLine($"Failed to resolve external path for VScript hint {internalFilePath}, skipping\n");
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
