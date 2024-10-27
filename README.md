# bspPack
Standalone packing tool stripped out of  [CompilePal](https://github.com/ruarai/CompilePal)


## Usage
Paths to **game folder** ( folder of gameinfo.txt ) and to **steam installation folder** are stored in **config.ini** ( created on first use ).<br>
There is no automatic detection so they need to be provided manually.<br>
Running `bspPack --modify` creates `ResourceConfig.ini` which is used instead of multiple other flags in CompilePal.<br>
If compressed BSP is provided automatic decompression is attempted by running bspzip with -repack flag.



## Flags

**--verbose** - Outputs a complete listing of added assets<br>
**--dryrun** - Creates a txt file for bspzip usage but does not pack<br>
**--renamenav** - Renames the nav file to embed.nav<br>
**--noswvtx** - Skips packing unused .sw.vtx files to save filesize<br>
**--particlemanifest** - Generates a particle manifest based on particles used<br>
**--compress** - Compresses the BSP after packing<br>
**--modify** - Replaces --include, --includeDir, --exclude, --excludeDir, and --excludeVpk flags from CompilePal<br>

## Missing functionality
Support for renaming conflicting particle names<br>
Packing VPK<br>
Including custom PakFile<br>
Adding custom source directories<br>

## Linux problem
For reaons I have not quite figured out yet some texture paths are stored in upper case in BSPs. <br>
This does not seem to be a problem with how CompilePal manages files but just the way BSPs store paths.<br>
Because linux paths are case sensitive all paths are normalized to lower case, meaning all textures that are supposed to be packed need to be in lower case.<br>
Until there is a way to deal with upper case in BSPs this will remain an issue.
