using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class Keys
{
    public static List<string> vmfSoundKeys;
    public static List<string> vmfModelKeys;
    public static List<string> vmfMaterialKeys;
	public static List<string> vmtTextureKeyWords;
    public static List<string> vmtMaterialKeyWords;

    // Method to initialize the keys by reading from files
    public static void InitializeKeys(string keysFolder)
    {
        vmfSoundKeys = File.ReadAllLines(Path.Combine(keysFolder, "vmfsoundkeys.txt")).ToList();
        vmfModelKeys = File.ReadAllLines(Path.Combine(keysFolder, "vmfmodelkeys.txt")).ToList();
        vmfMaterialKeys = File.ReadAllLines(Path.Combine(keysFolder, "vmfmaterialkeys.txt")).ToList();
        vmtTextureKeyWords = File.ReadAllLines(Path.Combine(keysFolder, "texturekeys.txt")).ToList();
        vmtMaterialKeyWords = File.ReadAllLines(Path.Combine(keysFolder, "materialkeys.txt")).ToList();
    }
}