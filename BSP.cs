using System.Text;
using System.Text.RegularExpressions;

namespace bspPack;

// this is the class that stores data about the bsp.
// You can find information about the file format here
// https://developer.valvesoftware.com/wiki/Source_BSP_File_Format#BSP_file_header
class CompressedBSPException : Exception { }

class BSP
{
    private readonly KeyValuePair<int, int>[] Offsets; // offset/length
    private static readonly char[] SpecialCaracters = { '*', '#', '@', '>', '<', '^', '(', ')', '}', '$', '!', '?', ' ' };

    public List<Dictionary<string, string>> EntityList { get; private set; } = [];

    public List<List<Tuple<string, string>>> EntityListArrayForm { get; private set; } = [];

    public List<int>[] ModelSkinList { get; private set; } = [];

    public List<string> ModelList { get; private set; } = [];

    public List<string> EntModelList { get; private set; } = [];

    public List<string> ParticleList { get; private set; } = [];

    public List<string> TextureList { get; private set; } = [];
    public List<string> EntTextureList { get; private set; } = [];

    public List<string> EntSoundList { get; private set; } = [];

    public List<string> MiscList { get; private set; } = [];

    // key/values as internalPath/externalPath
    public KeyValuePair<string, string> ParticleManifest { get; set; }
    public KeyValuePair<string, string> Soundscript { get; set; }
    public KeyValuePair<string, string> Soundscape { get; set; }
    public KeyValuePair<string, string> Detail { get; set; }
    public KeyValuePair<string, string> Nav { get; set; }
    public List<KeyValuePair<string, string>> Res { get; } = [];
    public KeyValuePair<string, string> Kv { get; set; }
    public KeyValuePair<string, string> Txt { get; set; }
    public KeyValuePair<string, string> Jpg { get; set; }
    public KeyValuePair<string, string> Radartxt { get; set; }
    public List<KeyValuePair<string, string>> Radardds { get; set; } = [];
    public KeyValuePair<string, string> RadarTablet { get; set; }
    public List<KeyValuePair<string, string>> Languages { get; set; } = [];
    public List<KeyValuePair<string, string>> VehicleScriptList { get; set; } = [];
    public List<KeyValuePair<string, string>> EffectScriptList { get; set; } = [];
    public List<string> VscriptList { get; set; } = [];
    public List<KeyValuePair<string, string>> PanoramaMapBackgrounds { get; set; } = [];
    public KeyValuePair<string, string> PanoramaMapIcon { get; set; }

    public FileInfo File { get; private set; }
    private readonly bool IsL4D2 = false;
    private readonly int BspVersion;

    public BSP(FileInfo file)
    {
        File = file;
        Offsets = new KeyValuePair<int, int>[64];

        using var bsp = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using (var reader = new BinaryReader(bsp))
        {
            bsp.Seek(4, SeekOrigin.Begin); // skip header
            this.BspVersion = reader.ReadInt32();

            if (reader.ReadInt32() == 0 && this.BspVersion == 21)
                IsL4D2 = true;

            bsp.Seek(-4, SeekOrigin.Current);

            for (int i = 0; i < Offsets.GetLength(0); i++)
            {
                if (IsL4D2)
                {
                    bsp.Seek(4, SeekOrigin.Current);
                    Offsets[i] = new KeyValuePair<int, int>(reader.ReadInt32(), reader.ReadInt32());
                    bsp.Seek(4, SeekOrigin.Current);
                }
                else
                {
                    Offsets[i] = new KeyValuePair<int, int>(reader.ReadInt32(), reader.ReadInt32());
                    bsp.Seek(8, SeekOrigin.Current);
                }
            }

            bsp.Seek(Offsets[0].Key, SeekOrigin.Begin);
            if (reader.ReadChars(4).SequenceEqual("LZMA".ToCharArray()))
            {
                throw new FormatException("BSP is compressed. Trying to decompress...");
            }

            BuildEntityList(bsp, reader);
            BuildEntModelList();
            BuildModelList(bsp, reader);
            BuildParticleList();
            BuildEntTextureList();
            BuildTextureList(bsp, reader);
            BuildEntSoundList();
            BuildMiscList();
        }
    }

    public void BuildEntityList(FileStream bsp, BinaryReader reader)
    {
        EntityList = [];
        EntityListArrayForm = [];

        bsp.Seek(Offsets[0].Key, SeekOrigin.Begin);
        byte[] ent = reader.ReadBytes(Offsets[0].Value);
        List<byte> ents = [];

        const int LCURLY = 123;
        const int RCURLY = 125;
        const int NEWLINE = 10;

        for (int i = 0; i < ent.Length; i++)
        {
            if (ent[i] == LCURLY && i + 1 < ent.Length)
            {
                // if curly isnt followed by newline assume its part of filename
                if (ent[i + 1] != NEWLINE)
                    ents.Add(ent[i]);
            }
            if (ent[i] != LCURLY && ent[i] != RCURLY)
                ents.Add(ent[i]);
            else if (ent[i] == RCURLY)
            {
                // if curly isnt followed by newline assume its part of filename
                if (i + 1 < ent.Length && ent[i + 1] != NEWLINE)
                {
                    ents.Add(ent[i]);
                    continue;
                }


                string rawent = Encoding.ASCII.GetString([.. ents]);
                Dictionary<string, string> entity = new(StringComparer.InvariantCultureIgnoreCase);
                var entityArrayFormat = new List<Tuple<string, string>>();
                // split on \n, ignore \n inside of quotes
                foreach (string s in Regex.Split(rawent, "(?=(?:(?:[^\"]*\"){2})*[^\"]*$)\\n"))
                {
                    if (s.Length != 0)
                    {
                        // split on non escaped quotes
                        string[] c = Regex.Split(s, "(?<!\\\\)[\"\"]");
                        if (!entity.ContainsKey(c[1]))
                            entity.Add(c[1], c[3]);
                        entityArrayFormat.Add(Tuple.Create(c[1], c[3]));
                    }
                }
                EntityList.Add(entity);
                EntityListArrayForm.Add(entityArrayFormat);
                ents = [];
            }
        }
    }

    public void BuildTextureList(FileStream bsp, BinaryReader reader)
    {
        // builds the list of textures applied to brushes

        Regex patch_pattern = new Regex(@"^materials/maps/[^/]+/(.+)_wvt_patch.(vmt)$", RegexOptions.IgnoreCase);

        string mapname = bsp.Name.Split('\\').Last().Split('.')[0];

        TextureList = [];
        bsp.Seek(Offsets[43].Key, SeekOrigin.Begin);
        TextureList = [.. Encoding.ASCII.GetString(reader.ReadBytes(Offsets[43].Value)).Split('\0')];
        for (int i = 0; i < TextureList.Count; i++)
        {
            if (TextureList[i].StartsWith('/')) // materials in root level material directory start with /
                TextureList[i] = "materials" + TextureList[i].ToLower() + ".vmt";
            else
                TextureList[i] = "materials/" + TextureList[i].ToLower() + ".vmt";

            // pack wvt (world vertex transition) patch materials. These are generated when blend textures are used on non displacement brushes and are renamed maps/{mapname}/{material_name}_wvt_patch.vmt
            // https://github.com/ValveSoftware/source-sdk-2013/blob/a62efecf624923d3bacc67b8ee4b7f8a9855abfd/src/utils/vbsp/worldvertextransitionfixup.cpp#L47
            if (patch_pattern.Match(TextureList[i]) is Match { Success: true } match)
            {
                TextureList[i] = Path.Join("materials", $"{match.Groups[1].Value}.{match.Groups[2].Value}");
            }
        }

        // find skybox materials
        Dictionary<string, string> worldspawn = EntityList.FirstOrDefault(item => item["classname"] == "worldspawn", []);
        if (worldspawn.TryGetValue("skyname", out string? skyname))
        {
            skyname = skyname.ToLower();
            foreach (string s in new string[] { "", "bk", "dn", "ft", "lf", "rt", "up" })
            {
                TextureList.Add("materials/skybox/" + skyname + s + ".vmt");
                TextureList.Add("materials/skybox/" + skyname + "_hdr" + s + ".vmt");
            }
        }

        // find detail materials
        if (worldspawn.TryGetValue("detailmaterial", out string? detailmaterial))
        {
            detailmaterial = detailmaterial.ToLower();
            TextureList.Add("materials/" + detailmaterial + ".vmt");
        }

        // find menu photos
        TextureList.Add("materials/vgui/maps/menu_photos_" + mapname + ".vmt");
    }

    public void BuildEntTextureList()
    {
        // builds the list of textures referenced in entities

        List<string> materials = [];
        HashSet<string> skybox_swappers = [];

        foreach (Dictionary<string, string> ent in EntityList)
        {
            foreach (KeyValuePair<string, string> prop in ent)
            {
                if (Keys.vmfMaterialKeys.Contains(prop.Key.ToLower()))
                {
                    materials.Add(prop.Value);
                    if (prop.Key.StartsWith("team_icon", StringComparison.InvariantCultureIgnoreCase))
                        materials.Add(prop.Value + "_locked");
                }

            }

            if (ent["classname"].Contains("skybox_swapper") && ent.TryGetValue("SkyboxName", out string? skyboxname))
            {
                skyboxname = skyboxname.ToLower();
                if (ent.TryGetValue("targetname", out string? targetname))
                    skybox_swappers.Add(targetname.ToLower());

                foreach (string s in new string[] { "", "bk", "dn", "ft", "lf", "rt", "up" })
                {
                    materials.Add("skybox/" + skyboxname + s + ".vmt");
                    materials.Add("skybox/" + skyboxname + "_hdr" + s + ".vmt");
                }
            }

            // special condition for sprites
            if (ent["classname"].Contains("sprite") && ent.TryGetValue("model", out string? model))
            {
                // strip leading materials folder
                if (model.StartsWith("materials/", StringComparison.InvariantCultureIgnoreCase))
                    model = model[10..];

                materials.Add(model);
            }

            // special condition for item_teamflag
            if (ent["classname"].Contains("item_teamflag"))
            {
                if (ent.ContainsKey("flag_trail"))
                {
                    materials.Add("effects/" + ent["flag_trail"]);
                    materials.Add("effects/" + ent["flag_trail"] + "_red");
                    materials.Add("effects/" + ent["flag_trail"] + "_blu");
                }
                if (ent.ContainsKey("flag_icon"))
                {
                    materials.Add("vgui/" + ent["flag_icon"]);
                    materials.Add("vgui/" + ent["flag_icon"] + "_red");
                    materials.Add("vgui/" + ent["flag_icon"] + "_blu");
                }
            }

            // special condition for env_funnel. Hardcoded to use sprites/flare6.vmt
            if (ent["classname"].Contains("env_funnel"))
                materials.Add("sprites/flare6.vmt");

            // special condition for env_embers. Hardcoded to use particle/fire.vmt
            if (ent["classname"].Contains("env_embers"))
                materials.Add("particle/fire.vmt");

            //special condition for func_dustcloud and func_dustmotes.  Hardcoded to use particle/sparkles.vmt
            if (ent["classname"].StartsWith("func_dust"))
                materials.Add("particle/sparkles.vmt");

            // special condition for vgui_slideshow_display. directory paramater references all textures in a folder (does not include subfolders)
            if (ent["classname"].Contains("vgui_slideshow_display"))
            {
                if (ent.ContainsKey("directory"))
                {
                    var directory = Path.Combine(Config.GameFolder, "materials", "vgui", ent["directory"]);
                    if (Directory.Exists(directory))
                    {
                        foreach (var file in Directory.GetFiles(directory))
                        {
                            if (file.EndsWith(".vmt"))
                                materials.Add($"/vgui/{ent["directory"]}/{Path.GetFileName(file)}");
                        }
                    }
                }
            }

        }

        // pack I/O referenced materials
        // need to use array form of entity because multiple outputs with same command can't be stored in dict
        foreach (var ent in EntityListArrayForm)
        {
            foreach (var prop in ent)
            {
                var io = ParseIO(prop.Item2);
                if (io == null)
                    continue;


                var (target, command, parameter, scriptArgs) = io;

                switch (command.ToLower())
                {
                    case "setcountdownimage":
                        materials.Add($"vgui/{parameter}");
                        break;
                    case "command":
                        // format of Command is <command> <parameter>
                        if (!parameter.Contains(' '))
                        {
                            continue;
                        }
                        (command, parameter) = parameter.Split(' ') switch { var param => (param[0], param[1]) };
                        if (command == "r_screenoverlay")
                            materials.Add(parameter);
                        break;
                    case "setoverlaymaterial":
                        materials.Add(parameter);
                        break;
                    case "addoutput":
                        if (!parameter.Contains(' '))
                        {
                            continue;
                        }
                        string k, v;
                        (k, v) = parameter.Split(' ') switch { var a => (a[0], a[1]) };

                        // support packing mats when using addoutput to change skybox_swappers
                        if (skybox_swappers.Contains(target.ToLower()) && k.ToLower() == "skyboxname")
                        {
                            foreach (string s in new string[] { "", "bk", "dn", "ft", "lf", "rt", "up" })
                            {
                                materials.Add("skybox/" + v + s + ".vmt");
                                materials.Add("skybox/" + v + "_hdr" + s + ".vmt");
                            }
                        }
                        break;
                    case "runscriptcode":
                        if (scriptArgs.Length != 0)
                        {
                            for (int i = 0; i < scriptArgs.Length; i++)
                            {
                                string arg = scriptArgs.ElementAtOrDefault(i + 1)!;

                                if (arg != default)
                                {
                                    if (scriptArgs[i].ToLower() == "setskyboxtexture")
                                    {
                                        foreach (string s in new string[] { "", "bk", "dn", "ft", "lf", "rt", "up" })
                                        {
                                            materials.Add("skybox/" + arg + s + ".vmt");
                                            materials.Add("skybox/" + arg + "_hdr" + s + ".vmt");
                                        }
                                    }
                                    else if (scriptArgs[i].Contains("SetScriptOverlayMaterial", StringComparison.InvariantCultureIgnoreCase))
                                        materials.Add(arg);
                                }
                            }
                        }
                        break;
                }
            }
        }

        // format and add materials
        EntTextureList = [];
        foreach (string material in materials)
        {
            string materialpath = material;
            if (!material.EndsWith(".vmt") && !materialpath.EndsWith(".spr"))
                materialpath += ".vmt";

            EntTextureList.Add("materials/" + materialpath);
        }
    }

    public void BuildModelList(FileStream bsp, BinaryReader reader)
    {
        // builds the list of models that are from prop_static

        ModelList = [];
        // getting information on the gamelump
        int propStaticId = 0;
        bsp.Seek(Offsets[35].Key, SeekOrigin.Begin);
        KeyValuePair<int, int>[] GameLumpOffsets = new KeyValuePair<int, int>[reader.ReadInt32()]; // offset/length
        for (int i = 0; i < GameLumpOffsets.Length; i++)
        {
            if (reader.ReadInt32() == 1936749168)
                propStaticId = i;
            bsp.Seek(4, SeekOrigin.Current); //skip flags and version
            GameLumpOffsets[i] = new KeyValuePair<int, int>(reader.ReadInt32(), reader.ReadInt32());
        }

        // reading model names from game lump
        bsp.Seek(GameLumpOffsets[propStaticId].Key, SeekOrigin.Begin);
        int modelCount = reader.ReadInt32();
        for (int i = 0; i < modelCount; i++)
        {
            string model = Encoding.ASCII.GetString(reader.ReadBytes(128)).Trim('\0');
            if (model.Length != 0)
                ModelList.Add(model);
        }

        // from now on we have models, now we want to know what skins they use

        // skipping leaf lump
        int leafCount = reader.ReadInt32();

        // bsp v25 uses ints instead of shorts for leaf lump
        if (this.BspVersion == 25)
            bsp.Seek(leafCount * sizeof(int), SeekOrigin.Current);
        else
            bsp.Seek(leafCount * sizeof(short), SeekOrigin.Current);

        // reading staticprop lump

        int propCount = reader.ReadInt32();

        //dont bother if there's no props, avoid a dividebyzero exception.
        if (propCount <= 0)
            return;

        long propOffset = bsp.Position;
        int byteLength = GameLumpOffsets[propStaticId].Key + GameLumpOffsets[propStaticId].Value - (int)propOffset;
        int propLength = byteLength / propCount;

        ModelSkinList = new List<int>[modelCount]; // stores the ids of used skins

        for (int i = 0; i < modelCount; i++)
            ModelSkinList[i] = [];

        for (int i = 0; i < propCount; i++)
        {
            bsp.Seek(i * propLength + propOffset + 24, SeekOrigin.Begin); // 24 skips origin and angles
            int modelId = reader.ReadUInt16();
            bsp.Seek(6, SeekOrigin.Current);
            int skin = reader.ReadInt32();

            if (ModelSkinList[modelId].IndexOf(skin) == -1)
                ModelSkinList[modelId].Add(skin);
        }

    }

    public void BuildEntModelList()
    {
        // builds the list of models referenced in entities

        EntModelList = [];
        foreach (Dictionary<string, string> ent in EntityList)
        {
            foreach (KeyValuePair<string, string> prop in ent)
            {
                if (ent["classname"].StartsWith("func"))
                {
                    if (prop.Key == "gibmodel")
                        EntModelList.Add(prop.Value);
                }
                else if (!ent["classname"].StartsWith("trigger") &&
                    !ent["classname"].Contains("sprite"))
                {
                    if (Keys.vmfModelKeys.Contains(prop.Key))
                        EntModelList.Add(prop.Value);
                    // item_sodacan is hardcoded to models/can.mdl
                    // env_beverage spawns item_sodacans
                    else if (prop.Value == "item_sodacan" || prop.Value == "env_beverage")
                        EntModelList.Add("models/can.mdl");
                    // tf_projectile_throwable is hardcoded to models/props_gameplay/small_loaf.mdl
                    else if (prop.Value == "tf_projectile_throwable")
                        EntModelList.Add("models/props_gameplay/small_loaf.mdl");
                }

            }
        }

        // pack I/O referenced models
        // need to use array form of entity because multiple outputs with same command can't be stored in dict
        string[] modelcommands = ["setmodel", "setcustommodel", "setcustommodelwithclassanimations"];
        foreach (var ent in EntityListArrayForm)
        {
            foreach (var prop in ent)
            {
                var io = ParseIO(prop.Item2);
                if (io == null)
                    continue;

                var (target, command, parameter, scriptArgs) = io;

                if (modelcommands.Contains(command.ToLower()))
                    EntModelList.Add(parameter);

                if (scriptArgs.Length != 0)
                {
                    for (int i = 0; i < scriptArgs.Length; i++)
                    {
                        string arg = scriptArgs.ElementAtOrDefault(i + 1)!;

                        var funcOnly = "";
                        if (scriptArgs[i].Contains(".Set"))
                            funcOnly = $"S{scriptArgs[i].Split(".S")[1]}";

                        if (funcOnly != "" && (modelcommands.Contains(funcOnly.ToLower()) || funcOnly.Equals("setmodelsimple", StringComparison.InvariantCultureIgnoreCase)) && arg != default)
                            EntModelList.Add(arg);
                    }
                }
            }
        }
    }

    public void BuildEntSoundList()
    {
        // builds the list of sounds referenced in entities
        EntSoundList = [];
        foreach (Dictionary<string, string> ent in EntityList)
            foreach (KeyValuePair<string, string> prop in ent)
            {
                if (Keys.vmfSoundKeys.Contains(prop.Key.ToLower()))
                    EntSoundList.Add("sound/" + prop.Value.Trim(SpecialCaracters));
            }

        // pack I/O referenced sounds
        // need to use array form of entity because multiple outputs with same command can't be stored in dict
        foreach (var ent in EntityListArrayForm)
        {
            foreach (var prop in ent)
            {
                var io = ParseIO(prop.Item2);
                if (io == null)
                    continue;

                var (target, command, parameter, scriptArgs) = io;
                command = command.ToLower();
                if (command.StartsWith("playvo"))
                {
                    //Parameter value following PlayVO is always either a sound path or an empty string
                    if (!string.IsNullOrWhiteSpace(parameter))
                        EntSoundList.Add($"sound/{parameter}");
                }
                else if (command == "command")
                {
                    // format of Command is <command> <parameter>
                    if (!parameter.Contains(' '))
                        continue;

                    (command, parameter) = parameter.Split(' ') switch { var param => (param[0], param[1]) };

                    if (command == "play" || command == "playgamesound")
                        EntSoundList.Add($"sound/{parameter}");
                }

                if (scriptArgs.Length != 0)
                {
                    for (int i = 0; i < scriptArgs.Length; i++)
                    {
                        string arg = scriptArgs.ElementAtOrDefault(i + 1)!;

                        if (arg != default)
                        {
                            var funcOnly = "";

                            if (scriptArgs[i].Contains(".E"))
                                funcOnly = $"E{scriptArgs[i].Split(".E")[1]}";

                            if (funcOnly == "EmitSound" || scriptArgs[i] == "EmitAmbientSoundOn")
                                EntSoundList.Add($"sound/{arg}");

                            else if (scriptArgs[i] == "EmitSoundEx")
                            {
                                var table = AssetUtils.VscriptTableToDict(arg);
                                EntSoundList.Add($"sound/{table["sound_name"]}");
                            }
                        }
                    }
                }
            }
        }
    }
    // color correction, etc.
    public void BuildMiscList()
    {
        MiscList = [];

        // find color correction files
        foreach (Dictionary<string, string> cc in EntityList.Where(item => item["classname"].StartsWith("color_correction")))
            if (cc.TryGetValue("filename", out string? filename))
                MiscList.Add(filename);

        // pack I/O referenced TF2 upgrade files
        // need to use array form of entity because multiple outputs with same command can't be stored in dict
        foreach (var ent in EntityListArrayForm)
        {
            foreach (var prop in ent)
            {
                var io = ParseIO(prop.Item2);

                if (io == null) continue;

                var (target, command, parameter, _) = io;
                if (!command.Equals("setcustomupgradesfile", StringComparison.InvariantCultureIgnoreCase)) continue;

                MiscList.Add(parameter);

            }
        }
    }

    public void BuildParticleList()
    {
        ParticleList = [];
        string[] particleKeys = ["effect_name", "particle_name", "explode_particle"];
        foreach (var ent in EntityListArrayForm)
        {
            foreach (var prop in ent)
            {
                if (particleKeys.Contains(prop.Item1.ToLower()))
                    ParticleList.Add(prop.Item2.ToLower());

                var io = ParseIO(prop.Item2);

                if (io == null) continue;

                var (_, _, _, scriptArgs) = io;

                if (scriptArgs.Length != 0)
                {
                    for (int i = 0; i < scriptArgs.Length; i++)
                    {
                        string arg = scriptArgs.ElementAtOrDefault(i + 1)!;

                        if (scriptArgs[i] == "DispatchParticleEffect" && arg != default)
                        {
                            Message.Warning($"WARNING: DispatchParticleEffect will not precache custom particles!");
                            ParticleList.Add(arg);
                        }
                    }
                }
            }

        }
    }


    /// <summary>
    /// Parses an IO string and "AddOutput" inputs for the target, command, and parameter
    /// For "RunScriptCode" inputs: scriptArgs is filled out as follows:
    /// 1. Function first, comma-separated arguments as the rest
    /// 2. The parameter itself if not a function (no '(' character found)
    /// 3. Semicolon separated params get appended to this
    /// Search 'scriptArgs[i] == "IncludeScript"' in AssetUtils.cs for getting assets from args.
    /// </summary>
    /// <param name="property">Entity property</param>
    /// <returns>Tuple containing (target, command, parameter, scriptArgs)</returns>
    public Tuple<string, string, string, string[]>? ParseIO(string property, bool trimScriptBackticks = true)
    {
        if (!property.Contains('\u001b')) return null;

        var io = property.Split('\u001b');
        if (io.Length != 5)
        {
            Message.Warning($"WARNING: Failed to decode IO, ignoring: {property}");
            return null;
        }

        var targetInput = io[1];
        var parameter = io[2];

        string[] scriptArgs = [];

        string targetInputLower = targetInput.ToLower();

        // AddOutput dynamically adds I/O to other entities, parse it to get input/parameter
        if (targetInputLower == "addoutput")
        {
            // AddOutput format: <output name> <target name>:<input name>:<parameter>:<delay>:<max times to fire> or simple form <key> <value>
            // only need to convert complex form into simple form
            if (parameter.Contains(':'))
            {
                var splitIo = parameter.Split(':');
                if (splitIo.Length < 3)
                {
                    Message.Warning($"WARNING: Failed to decode AddOutput, format may be incorrect: {property}");
                    return null;
                }

                targetInput = splitIo[1];
                parameter = splitIo[2];
            }
        }

        // Parse VScript RunScriptCode parameters for assets and basic syntax errors
        // TODO: table args (EmitSoundEx, SpawnEntityFromTable) can use whitespace instead of commas for kv.  can probably be parsed into a dict.
        // TODO: Trim backticks before returning? would save adding .Trim('`') boilerplate elsewhere
        else if (targetInputLower == "runscriptcode")
        {
            // check for unfinished quotes
            int backticks = 0;

            foreach (char c in parameter)
                if (c == '`')
                    backticks++;

            if (backticks != 0 && backticks % 2 != 0)
            {
                Message.Warning($"WARNING: ({io[0]}) Invalid string in VScript IO: {parameter}\n");
                return null;
            }

            string[] splitfuncs = parameter.Split(";");

            // Complex scripts with nested parentheses/weird syntax will have issues with this, regex would probably be better
            // Wrapping this whole thing in a try/catch so it doesn't blow up compiles if it runs into a param it can't handle
            List<string> argsList = [];
            foreach (string func in splitfuncs)
            {
                try
                {
                    string[] splitparam = func.Replace(")", string.Empty).Split('(', StringSplitOptions.TrimEntries);

                    // Add func name as first element, or just the param string if it's standalone.
                    argsList.Add(splitparam[0]);

                    //table funcs are handled with AssetUtils.VScriptTableToDict
                    if (splitparam.Length != 0 && splitparam[1].StartsWith('{'))
                        argsList.Add(splitparam[1]);
                    else
                        for (int i = 1; i < splitparam.Length; i++)
                            argsList.AddRange(splitparam[i].Split(',', StringSplitOptions.TrimEntries));
                }
                catch
                {
                    Message.Warning($"WARNING: ({io[0]}) Cannot parse VScript IO: {parameter}\n");
                }
            }

            scriptArgs = [.. argsList];

            if (trimScriptBackticks)
                scriptArgs = [.. scriptArgs.Select(x => x.Trim('`'))];
        }

        return new Tuple<string, string, string, string[]>(io[0], targetInput, parameter, scriptArgs);
    }
}
