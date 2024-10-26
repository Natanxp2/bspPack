using System;
using System.IO;

namespace BSPPackStandalone
{
    public static class Config
    {
		public static string BSPFile;
        public static readonly string GameFolder = @"E:\__STEAM__\steamapps\common\Momentum Mod Playtest\momentum";
        public static readonly string SteamAppsPath = GameFolder.Substring(0, GameFolder.IndexOf("steamapps") + 9);
        public static readonly string BSPZip = Path.Combine(GameFolder, @"..\bin\win64", "bspzip.exe");
        public static readonly string KeysFolder = Path.Combine(Directory.GetCurrentDirectory(), "Keys");
        public static readonly string TempFolder = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
        public static readonly string CopyLocation = Path.Combine(Directory.GetCurrentDirectory()); //Wtf is this?
        public static readonly string VPK = Path.Combine(Directory.GetCurrentDirectory()); //Wtf is this?
    }
}