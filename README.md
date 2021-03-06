## Minecraft Version History
I use this program to generate a git history that shows the changes to Minecraft with every release.

<img src="https://i.imgur.com/lOSNnVi.png" width=300> <img src="https://i.imgur.com/UGVEbv9.png" width=200>

It works for both Java and Bedrock editions. Currently, it is difficult for public use because you need a config file to determine a lot of functionality, and my personal one is still a work in progress. But at some point, I'll include a reference config.

**Java Features**
* The versions are read as they appear in the `.minecraft/versions` directory, with a folder containing an identically-named jar and JSON manifest.
* The entire contents of the jar is extracted to the `jar` folder. You can use the config exclude unnecessary files like `.class` files or `META-INF`, leaving only the vanilla resource/data pack.
  - Generated files that change often, such as the `shipwreck_supply` loot table, can be configured to be sorted to avoid clogging diffs.
  - Structure files have their `DataVersion` tag removed, as it is incremented every release which causes unhelpful diffs.
* All NBT files (mostly structures from the jar) are converted to indented SNBT and stored in the `nbt_translations` folder.
* For versions that support it, data generators are run and their output stored in the `reports` folder. The server jar is downloaded automatically to accomplish this. This includes the command tree, the block states registry, and the other registries, as well as biome data in more recent versions.
* The source code is decompiled and stored in the `source` folder. You can choose between [cfr](https://github.com/leibnitz27/cfr) (my personal choice) or [FernFlower](https://github.com/fesh0r/fernflower). For versions that support it, Mojang mappings are automatically downloaded and used to deobfuscate the jar.

**Bedrock Features**
* The versions are read as zips containing a single folder which contains another single folder which contains the APPX and installation metadata. This is probably not helpful for the general public, but that's how I receive Bedrock builds.
* The entire contents of the APPX is extracted to the `data` folder.
* All NBT files (mostly structures) are converted to indented SNBT and stored in the `nbt_translations` folder.
* Since Bedrock stores its vanilla resource/behavior packs in slices, you can with configuration merge them into a "final" pack. Whether files across slices should be overwritten or merge in some way is also configurable. These final packs are stored in the `latest_packs` folder.

**History Order**

The program organizes the versions by branches that correspond to a release. Currently, the program has no way to determine what release each version belongs to, so it must be configured. Once you do that, however, the program does a decent job at figuring out by itself how to construct the branches.

Within a branch, all versions are sorted by release date. You can use the config to override this order. The branches themselves are also arranged by release date, where the first version of a branch will parent to the newest version of the previous branch that's still older than the version in question. So far, this seems to accurately recreate the Minecraft version history tree, but you can also use the config to explicitly define parents.

<img src="https://i.imgur.com/VAZvxCL.png" height=250> <img src="https://i.imgur.com/b9RVT3t.png" height=250>
