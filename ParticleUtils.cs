using System.Text;

namespace bspPack;

public class PCF
{
    //Class to hold information about a pcf
    public string FilePath = null!;

    public int BinaryVersion;
    public int PcfVersion;

    public int NumDictStrings;
    public List<string> StringDict = [];

    public List<string> ParticleNames = [];
    public List<string> MaterialNames = [];
    public List<string> ModelNames = [];


    public List<string> GetModelNames()
    {
        List<string> modelList = [];
        //All strings including model names are stored in string dict for binary 4+
        //TODO I think only binary 4+ support models, but if not we need to implement a method to read them for lower versions
        foreach (string s in StringDict)
        {
            if (s.EndsWith(".mdl"))
            {
                modelList.Add(s);
            }
        }
        ModelNames = modelList;

        return modelList;
    }

    //All strings including materials are stored in string dict of binary v4 pcfs
    public List<string> GetMaterialNamesV4()
    {
        List<string> materialNames = [];

        foreach (string s in StringDict)
        {

            if (s.EndsWith(".vmt") || s.EndsWith(".vtf"))
            {
                // Alien Swarm does not prepend materials/ to particles, add it just in case
                if (this.BinaryVersion == 5 && this.PcfVersion == 2)
                    materialNames.Add("materials/" + s);

                materialNames.Add(s);
            }
        }
        return materialNames;
    }

}

public static class ParticleUtils
{
    //Partially reads particle to get particle name to determine if it is a target particle
    //Returns null if not target particle
    public static PCF? IsTargetParticle(string filePath, List<string> targetParticles)
    {
        FileStream fs;
        try
        {
            fs = new FileStream(filePath, FileMode.Open);
        }
        catch (FileNotFoundException)
        {
            Message.Warning($"WARNING: Could not find {filePath}\n");
            return null;
        }

        PCF pcf = new();
        BinaryReader reader = new(fs);

        pcf.FilePath = filePath;

        //Get Magic String
        string magicString = ReadNullTerminatedString(fs, reader);

        //Throw away unneccesary info
        magicString = magicString.Replace("<!-- dmx encoding binary ", "");
        magicString = magicString.Replace(" -->", "");

        //Extract info from magic string
        string[] magicSplit = magicString.Split(' ');

        //Store binary and pcf versions
        Int32.TryParse(magicSplit[0], out pcf.BinaryVersion);
        Int32.TryParse(magicSplit[3], out pcf.PcfVersion);

        //Different versions have different stringDict sizes
        if (pcf.BinaryVersion != 4 && pcf.BinaryVersion != 5)
        {
            pcf.NumDictStrings = reader.ReadInt16(); //Read as short
        }
        else
        {
            pcf.NumDictStrings = reader.ReadInt32(); //Read as int
        }

        //Add strings to string dict
        for (int i = 0; i < pcf.NumDictStrings; i++)
            pcf.StringDict.Add(ReadNullTerminatedString(fs, reader));

        //Read element dict for particle names
        int numElements = reader.ReadInt32();
        for (int i = 0; i < numElements; i++)
        {
            int typeNameIndex = reader.ReadUInt16();
            string typeName = pcf.StringDict[typeNameIndex];

            string elementName = "";

            if (pcf.BinaryVersion != 4 && pcf.BinaryVersion != 5)
            {
                elementName = ReadNullTerminatedString(fs, reader);
                //Skip data signature
                fs.Seek(16, SeekOrigin.Current);
            }
            else if (pcf.BinaryVersion == 4)
            {
                int elementNameIndex = reader.ReadUInt16();
                elementName = pcf.StringDict[elementNameIndex];
                fs.Seek(16, SeekOrigin.Current);
            }
            else if (pcf.BinaryVersion == 5)
            {
                int elementNameIndex = reader.ReadUInt16();
                elementName = pcf.StringDict[elementNameIndex];
                fs.Seek(20, SeekOrigin.Current);
            }

            //Get particle names
            if (typeName == "DmeParticleSystemDefinition")
                pcf.ParticleNames.Add(elementName);

        }

        bool containsParticle = false;
        foreach (string particleName in pcf.ParticleNames)
        {
            foreach (string targetParticle in targetParticles)
            {
                if (particleName == targetParticle)
                {
                    containsParticle = true;
                }
            }
        }

        //If target particle is not in pcf dont read it
        reader.Close();
        fs.Close();

        if (!containsParticle)
            return null;

        return pcf;
    }

    //Fully reads particle
    public static PCF? ReadParticle(string filePath)
    {
        FileStream fs;
        try
        {
            fs = new FileStream(filePath, FileMode.Open);
        }
        catch (FileNotFoundException)
        {
            Message.Warning($"WARNING: Could not find {filePath}\n");
            return null;
        }

        PCF pcf = new();
        BinaryReader reader = new(fs);

        pcf.FilePath = filePath;

        //Get Magic String
        string magicString = ReadNullTerminatedString(fs, reader);

        //Throw away unneccesary info
        magicString = magicString.Replace("<!-- dmx encoding binary ", "");
        magicString = magicString.Replace(" -->", "");

        //Extract info from magic string
        string[] magicSplit = magicString.Split(' ');

        //Store binary and pcf versions
        Int32.TryParse(magicSplit[0], out pcf.BinaryVersion);
        Int32.TryParse(magicSplit[3], out pcf.PcfVersion);

        //Different versions have different stringDict sizes
        if (pcf.BinaryVersion != 4 && pcf.BinaryVersion != 5)
        {
            pcf.NumDictStrings = reader.ReadInt16(); //Read as short
        }
        else
        {
            pcf.NumDictStrings = reader.ReadInt32(); //Read as int
        }

        //Add strings to string dict
        for (int i = 0; i < pcf.NumDictStrings; i++)
            pcf.StringDict.Add(ReadNullTerminatedString(fs, reader));

        //Read element dict for particle names
        int numElements = reader.ReadInt32();
        for (int i = 0; i < numElements; i++)
        {
            int typeNameIndex = reader.ReadUInt16();
            string typeName = pcf.StringDict[typeNameIndex];

            string elementName = "";

            if (pcf.BinaryVersion != 4 && pcf.BinaryVersion != 5)
            {
                elementName = ReadNullTerminatedString(fs, reader);
                //Skip data signature
                fs.Seek(16, SeekOrigin.Current);
            }
            else if (pcf.BinaryVersion == 4)
            {
                int elementNameIndex = reader.ReadUInt16();
                elementName = pcf.StringDict[elementNameIndex];
                fs.Seek(16, SeekOrigin.Current);
            }
            else if (pcf.BinaryVersion == 5)
            {
                int elementNameIndex = reader.ReadUInt16();
                elementName = pcf.StringDict[elementNameIndex];
                fs.Seek(20, SeekOrigin.Current);
            }

            //Get particle names
            if (typeName == "DmeParticleSystemDefinition")
                pcf.ParticleNames.Add(elementName);

        }

        if (pcf.BinaryVersion == 4 || pcf.BinaryVersion == 5)
        {
            //Can extract all neccesary data from string dict

            //Add materials and models to the master list
            List<string> materialNames = pcf.GetMaterialNamesV4();
            if (materialNames != null && materialNames.Count != 0)
                pcf.MaterialNames.AddRange(materialNames);

            List<string> modelNames = pcf.GetModelNames();
            if (modelNames != null && modelNames.Count != 0)
                pcf.ModelNames.AddRange(modelNames);

            reader.Close();
            fs.Close();
            return pcf;
        }

        //Have to read element attributes to get materials for binary version under 4

        //Read Element Attributes
        for (int a = 0; a < numElements; a++)
        {
            int numElementAttribs = reader.ReadInt32();
            for (int n = 0; n < numElementAttribs; n++)
            {
                int typeID = reader.ReadUInt16();
                int attributeType = reader.ReadByte();
                string attributeTypeName = pcf.StringDict[typeID];

                int count = (attributeType > 14) ? reader.ReadInt32() : 1;
                attributeType = (attributeType > 14) ? attributeType - 14 : attributeType;

                int[] typelength = [0, 4, 4, 4, 1, 1, 4, 4, 4, 8, 12, 16, 12, 16, 64];

                switch (attributeType)
                {
                    case 5:
                        string material = ReadNullTerminatedString(fs, reader);
                        if (attributeTypeName == "material")
                            pcf.MaterialNames.Add("materials/" + material);
                        break;

                    case 6:
                        for (int i = 0; i < count; i++)
                        {
                            uint len = reader.ReadUInt32();
                            fs.Seek(len, SeekOrigin.Current);
                        }
                        break;

                    default:
                        fs.Seek(typelength[attributeType] * count, SeekOrigin.Current);
                        break;

                }
            }
        }

        reader.Close();
        fs.Close();

        return pcf;
    }


    private static string ReadNullTerminatedString(FileStream fs, BinaryReader reader)
    {
        List<byte> verString = [];
        byte v;
        do
        {
            v = reader.ReadByte();
            verString.Add(v);
        } while (v != '\0' && fs.Position != fs.Length);

        return Encoding.ASCII.GetString(verString.ToArray()).Trim('\0');
    }

}

class ParticleManifest
{
    //Class responsible for holding information about particles
    private List<PCF> particles = [];
    private readonly string internalPath = "particles";
    private readonly string filepath = String.Empty;
    private readonly string baseDirectory;

    public KeyValuePair<string, string> particleManifest { get; private set; }

    public ParticleManifest(List<string> sourceDirectories, List<string> ignoreDirectories, List<string> excludedFiles, BSP map, string bspPath, string gameFolder)
    {
        Console.WriteLine($"\nGenerating Particle Manifest...");

        baseDirectory = gameFolder + "/";

        particles = [];

        //Add /particles to all source directories
        //Remove those that don't exist
        //Add any directories that may be withing remaining ones recursively
        List<string> particleDirectories = [.. sourceDirectories
                                                .Select(s => Path.Combine(s, internalPath))
                                                .Where(Directory.Exists)
                                                .SelectMany(path =>
                                                    new[] { path }
                                                    .Concat(Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                                                )
                                            ];

        //Search directories for pcf and find particles that match used particle names
        //TODO multithread this?
        foreach (string dir in particleDirectories)
        {
            if (ignoreDirectories.Contains(dir))
                continue;

            foreach (string file in Directory.GetFiles(dir))
            {
                if (file.EndsWith(".pcf") && !excludedFiles.Contains(file.ToLower()))
                {
                    PCF? pcf = ParticleUtils.IsTargetParticle(file, map.ParticleList);
                    if (pcf != null && !particles.Exists(p => p.FilePath == pcf.FilePath))
                        particles.Add(pcf);
                }
            }
        }

        if (particles == null || particles.Count == 0)
        {
            Message.Warning("WARNING: Could not find any PCFs that contained used particles!\n");
            return;
        }

        //Check for pcfs that contain the same particle name
        List<PCF> conflictingParticles = [];
        if (particles.Count > 1)
        {
            for (int i = 0; i < particles.Count - 1; i++)
            {
                for (int j = i + 1; j < particles.Count; j++)
                {
                    //Create a list of names that intersect between the 2 lists
                    List<string> conflictingNames = [.. particles[i].ParticleNames.Intersect(particles[j].ParticleNames)];

                    if (conflictingNames.Count != 0 && particles[i].FilePath != particles[j].FilePath)
                    {
                        conflictingParticles.Add(particles[i]);
                        conflictingParticles.Add(particles[j]);
                    }

                }
            }
        }

        //Solve conflicts
        if (conflictingParticles.Count != 0)
        {
            string pairText = conflictingParticles.Count / 2 == 1 ? "pair" : "pairs";
            Message.Warning($"\nFound {conflictingParticles.Count / 2} conflicting particle {pairText}:");
            //Remove particle if it is in a particle conflict, add back when conflict is manually resolved
            foreach (PCF conflictParticle in conflictingParticles)
            {
                particles.Remove(conflictParticle);
            }

            List<PCF> resolvedConflicts = [];

            for (int i = 0; i < conflictingParticles.Count; i += 2)
            {
                resolvedConflicts.Add(ResolveConflicts(conflictingParticles[i], conflictingParticles[i + 1], i / 2));
            }


            //Add resolved conflicts back into particle list
            particles.AddRange(resolvedConflicts);


        }

        //Remove duplicates
        particles = [.. particles.Distinct()];

        //Dont create particle manifest if there is no particles
        if (particles.Count == 0)
            return;

        //Generate manifest file
        filepath = bspPath[..^4] + "_particles.txt";

        //Write manifest
        using (StreamWriter sw = new(filepath))
        {
            sw.WriteLine("particles_manifest");
            sw.WriteLine("{");

            foreach (string source in sourceDirectories)
            {
                foreach (PCF particle in particles)
                {
                    if (particle.FilePath.StartsWith(source + "/" + internalPath))
                    {
                        // add 1 for the backslash between source dir and particle path
                        string internalParticlePath = particle.FilePath[(source.Length + 1)..];

                        sw.WriteLine($"      \"file\"    \"!{internalParticlePath}\"");
                        Console.WriteLine($"PCF added to manifest: {internalParticlePath}");
                    }
                }
            }

            sw.WriteLine("}");
        }

        string internalDirectory = filepath;
        if (filepath.StartsWith(baseDirectory, StringComparison.CurrentCultureIgnoreCase))
        {
            internalDirectory = filepath.Substring(baseDirectory.Length);
        }
        //Store internal/external dir so it can be packed
        particleManifest = new KeyValuePair<string, string>(internalDirectory, filepath);

        static PCF ResolveConflicts(PCF p1, PCF p2, int num)
        {
            if (num == 0)
                num = 1;
            Message.Warning($"\nConflict {num}:");
            Message.Write("[1]: ", ConsoleColor.Yellow);
            Message.Warning(p1.FilePath);
            Message.Write("[2]: ", ConsoleColor.Yellow);
            Message.Warning(p2.FilePath);
            Message.Write("\nChoose which particle to use: ", ConsoleColor.Yellow);
            int selected = -1;

            while (selected < 1 || selected > 2)
            {
                string? input = Console.ReadLine();
                if (!int.TryParse(input, out selected) || selected < 1 || selected > 2)
                {
                    Message.Write("Invalid selection. Please enter a valid number: ", ConsoleColor.Yellow);
                }
            }

            if (selected == 1)
                return p1;
            else
                return p2;
        }
    }
}
