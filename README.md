# bspPack
Standalone packing tool stripped out of  [CompilePal](https://github.com/ruarai/CompilePal)


## Usage
Path to **game folder** ( folder of gameinfo.txt ) and to **steam installation folder** is stored in **config.ini** ( created on first use ).<br>
There is no automatic detection so they need to be provided manually.<br>
Running `bspPack --modify` creates a `ResourceConfig.ini` which is used instead of multiple other flags in CompilePal.<br>
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
