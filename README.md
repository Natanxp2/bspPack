# bspPack

Standalone cross-platform packing tool stripped out of [CompilePal](https://github.com/ruarai/CompilePal)

## Usage

Paths to **game folder** ( folder of gameinfo.txt ) and to **steam installation folder** are stored in **config.ini** which is created on first use.<br>
Running `bspPack --modify` creates `ResourceConfig.ini` which is used instead of multiple other flags in CompilePal.<br>
If compressed BSP is provided automatic decompression is attempted by running bspzip with -repack flag.

Provide a path to a **.bsp** to pack it.
Provide a path to a **.vpk** to unpack it.

## Known problems

As of 30/06/2025 Momentum Mod's bspzip is [broken on linux](https://github.com/momentum-mod/game/issues/2356), it fails to compress or decompress bsps.
As of 30/06/2025 VPK tool from Momentum Mod and CS:S ( and possibly other source games ) doesn't unpack vpks correctly. It creates a proper folder structure but files are not actually being unpacked

## Flags

**`-V   | --verbose`** - Outputs a complete listing of added assets<br>
**`-D   | --dryrun`** - Creates a txt file for bspzip usage but does not pack<br>
**`-R   | --renamenav`** - Renames the nav file to embed.nav<br>
**`-N   | --noswvtx`** - Skips packing unused .sw.vtx files to save filesize<br>
**`-P   | --particlemanifest`** - Generates a particle manifest based on particles used<br>
**`-C   | --compress`** - Compresses the BSP after packing<br>
**`-M   | --modify`** - Modifies PakFile based on ResourceConfig.ini[^1]<br>
**`-U   | --unpack`** - Unpacks the BSP to **\<filename\>\_unpacked**<br>
**`-S   | --search`** - Searches **\/maps** folder of the game directory for the BSP file<br>
**`-L   | --lowercase`** - Lowercases relevant files and directories[^2]<br>
**`-VPK | --packvpk`** - Packs content of a bsp to a vpk. Can be used with -M flag without providing a bsp to pack only desired files

[^1]: Replaces --include, --includefilelist, --includeDir, --includesourcedirectories, --exclude, --excludeDir, --excludeVpk, and -ainfo flags from CompilePal
[^2]: Lowercases all files and directories in **/materials** and **/models** as well as content of .vmt files

## Linux problem

BSPs store some paths in uppercase. <br>
Because linux paths are case sensitive all paths are normalized to lower case, meaning all textures and models that are supposed to be packed need to be in lower case.<br>
Until there is a way to deal with that you can use --lowercase (-L) flag to automatically lowercase all relevant files and directories<br>
Please use with caution and create backups!
