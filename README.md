# bspPack
Standalone packing tool stripped out of  [CompilePal](https://github.com/ruarai/CompilePal)


## Usage
Paths to **game folder** ( folder of gameinfo.txt ) and to **steam installation folder** are stored in **config.ini** ( created on first use ).<br>
There is no automatic detection so they need to be provided manually.<br>
Running `bspPack --modify` creates `ResourceConfig.ini` which is used instead of multiple other flags in CompilePal.<br>
If compressed BSP is provided automatic decompression is attempted by running bspzip with -repack flag.



## Flags

**`-V | --verbose`** - Outputs a complete listing of added assets<br>
**`-D | --dryrun`** - Creates a txt file for bspzip usage but does not pack<br>
**`-R | --renamenav`** - Renames the nav file to embed.nav<br>
**`-N | --noswvtx`** - Skips packing unused .sw.vtx files to save filesize<br>
**`-P | --particlemanifest`** - Generates a particle manifest based on particles used<br>
**`-C | --compress`** - Compresses the BSP after packing<br>
**`-M | --modify`** - Modifies PakFile based on ResourceConfig.ini[^1]<br>
**`-U | --unpack`** - Unpacks the BSP to **\<filename\>\_unpacked**<br>
**`-S | --search`** - Searches **\/maps** folder of the game directory for the BSP file<br>
**`-L | --lowercase`** - Lowercases all directories, files, and content of .vmt files in /materials directory<br>

[^1]: Replaces --include, --includefilelist, --includeDir, --includesourcedirectories, --exclude, --excludeDir, and --excludeVpk flags from CompilePal

## Missing functionality
Support for renaming conflicting particle names<br>
Packing VPK<br>

## Linux problem
For reasons I have not quite figured out yet some texture paths are stored in upper case in BSPs. <br>
This does not seem to be a problem with how CompilePal manages files but just the way BSPs store paths.<br>
Because linux paths are case sensitive all paths are normalized to lower case, meaning all textures that are supposed to be packed need to be in lower case.<br>
Until there is a way to deal with upper case in BSPs you can use --lowercase (-L) flag to automatically lowercase all directories, files, and content of .vmt files inside of /materials folder.<br>
Please use with caution!
