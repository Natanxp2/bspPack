using System;
using System.IO;

namespace BSPPackStandalone
{
    public static class Config
    {
        public static readonly string gameFolderPath = @"E:\__STEAM__\steamapps\common\Momentum Mod Playtest\momentum";
        public static readonly string steamPath = Path.GetFullPath(Path.Combine(gameFolderPath, @"..\..\..\.."));
        public static readonly string bspZipPath = Path.Combine(gameFolderPath, @"..\bin\win64", "bspzip.exe");
        public static readonly string tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
    }
}