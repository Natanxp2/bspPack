namespace bspPack;

static class Keys
{
	public static List<string> vmfSoundKeys = [.. File.ReadAllLines(Path.Combine(Config.KeysFolder, "vmfsoundkeys.txt"))];
	public static List<string> vmfModelKeys = [.. File.ReadAllLines(Path.Combine(Config.KeysFolder, "vmfmodelkeys.txt"))];
	public static List<string> vmfMaterialKeys = [.. File.ReadAllLines(Path.Combine(Config.KeysFolder, "vmfmaterialkeys.txt"))];
	public static List<string> vmtTextureKeyWords = [.. File.ReadAllLines(Path.Combine(Config.KeysFolder, "texturekeys.txt"))];
	public static List<string> vmtMaterialKeyWords = [.. File.ReadAllLines(Path.Combine(Config.KeysFolder, "materialkeys.txt"))];
}