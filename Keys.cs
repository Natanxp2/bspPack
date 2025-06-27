namespace bspPack;

static class Keys
{
	public static List<string> vmfSoundKeys = File.ReadAllLines(Path.Combine(Config.KeysFolder, "vmfsoundkeys.txt")).ToList();
	public static List<string> vmfModelKeys = File.ReadAllLines(Path.Combine(Config.KeysFolder, "vmfmodelkeys.txt")).ToList();
	public static List<string> vmfMaterialKeys = File.ReadAllLines(Path.Combine(Config.KeysFolder, "vmfmaterialkeys.txt")).ToList();
	public static List<string> vmtTextureKeyWords = File.ReadAllLines(Path.Combine(Config.KeysFolder, "texturekeys.txt")).ToList();
	public static List<string> vmtMaterialKeyWords = File.ReadAllLines(Path.Combine(Config.KeysFolder, "materialkeys.txt")).ToList();
}