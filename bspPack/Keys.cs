using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

static class Keys
{
    public static List<string> vmfSoundKeys;
    public static List<string> vmfModelKeys;
    public static List<string> vmfMaterialKeys;

    // Method to initialize the keys by reading from files
    public static void InitializeKeys(string keysFolder)
    {
        vmfSoundKeys = File.ReadAllLines(Path.Combine(keysFolder, "vmfsoundkeys.txt")).ToList();
        vmfModelKeys = File.ReadAllLines(Path.Combine(keysFolder, "vmfmodelkeys.txt")).ToList();
        vmfMaterialKeys = File.ReadAllLines(Path.Combine(keysFolder, "vmfmaterialkeys.txt")).ToList();
    }
}