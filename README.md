# Moonwarmer
Moonwarmer is a mass-import script for utmt built for DELTARUNE.

**It aims to allow people who don't know how to make csx scripts to still be able to make one.**

It is not as powerful or compatible as a tailor-made csx script, but a **simple csx script is better than no csx script.**

It also is *not a replacement for xdelta files (unfortunately).*

# Lil recommendation (PSA)
You should use *asset_get_index()* when refering to assets when making deltarune mods from now on (not even just Moonwarmer ones), to improve **[GM3P](https://github.com/deltamodders/GM3P)** compatability until **Watercooler** (**Moonwarmer**'s bigger sibling) releases.

# When should I use Moonwarmer???
Since Moonwarmer is barebones by design, Moonwarmer should only be used for specific types of mods.

Here are types of mods that Moonwarmer perfectly supports.

- Sprite Modifications *(Perfect for Moonwarmer!)*
- Code Modification *(Works perfect alone, but could conflict with other mods if they both modify the same script.)*
- Code Modifiction & New sprites that don't need custom collision or custom origins *(You can't set a collision mask, or edit origin with Moonwarmer.)*
- *Note for above: The previous note can be circumvented by using gml functions like sprite_set_xoffset & sprite_set_yoffset or sprite_collision_mask*
- Sound Modifictions/New Sounds *(not external sounds or music! embedded sounds.)*
- Shader Modifications/New Shaders

Here are the types of mods that Moonwarmer does ***NOT*** support.
- **Font modifications/new fonts** *(this could probably be added im just kinda lazy)*
- **General Info Modifications** *(this could def be added, im just lazy.)*
- **New rooms/modifications** *(this likely wont be added)*
- **New objects/modifications** *(this likely wont be added)*

***If your mod isn't possible to make with the above limitations, then continue using xdelta's or tailor-made csx scripts.***

Unless UTMT adds room/object exporting/importing, there won't be any support for rooms/objects.

I would need to make custom scripts for objects and rooms to ensure best compatability. 
To be honest, I am not nearly skilled enough for that. (yet...?)

# Other limitations

It should also be noted that when Moonwarmer imports scripts, it tries its best to merge things.

The merging works pretty well, but if two mods change the same line, the last mod loaded will win.

# Moonwarmer Project Format
Here is how a Moonwarmer project folder is structured
```

└── (root of the archive/project folder)
    ├── "_moonwarmer.json" (an important file that helps Moonwarmer recongize a project.)
    └── "chapter(num)" folder (will be loaded when the appropriate DELTARUNE chapter is loaded. chapter0 is the launcher.)
        Note, when Moonwarmer loads the folders below, subfolders will be properly loaded too!
        └── "scripts"/"code"/"objects" folder (where .gml files are held)
        └── "sprites" folder (where sprites are be held)
        └── "shaders" folder (where subfolders exported from ExportShaders.csx are held)
        └── "audio"/"sounds"/"mus" folder (where .wav files are held. .ogg files will not work.)
    └── "_everychapter" folder (will ALWAYS be loaded into the data.win)
        Same structure as a chapter(num) folder.
```

# _moonwarmer.json format
*_moonwarmer.json* is a file that is stored in your project folder that makes it a Moonwarmer project. 

Here is how it is structured:

```json
{
    "metadata": 
    {
        "name": "My Visual Mod Name",
        "version": "vVersionNumber",
        "packageID": "hostedurl.modname.authorname"
    },

    "deltaruneVersion": "1.04",
    "supportedPackageTypes": 
    [
        "DELTAMOD",
        "DELTAHUB"
    ],
    "deltaruneVariants":
    [
        "fullgame",
        "demo",
        "demo-lts"
    ]
}
```

Let me break it down for you, user.

## Metadata Section

`metadata` stores all important mod information. It's what makes your mod recognizable.
This section may be familiar to you if you've made a mod in the DELTAMOD/DELTAHUB format.
**If your mod is for the DELTAMOD/DELTAHUB format, please, make the `metadata` values the same as in _deltaModInfo.json**

The `name` field is just a visual mod name.

The `version` field is the version of your mod. Increase this everytime you update! It helps other mods recognize your mod better.
*(Note, version format does not have to be the same as in the example. It is simply what I personally use. Just keep it consistent!)*

*`packageID` is a very important field.* It is a *unique* identifier for your mod. It follows this format: `hostedurl.modname.authorname`
Here's how an example would look like: `gamebanana.bettersaves.thej01`
***Never, NEVER change this value after release.***

## supportedPackageTypes
**This is an important field!**

The `deltaruneVariant` field is the supported mod formats this project is packaged with. Leave this empty if this mod is standalone.

*Note, if you add anything to this field, the Moonwarmer.csx script will enter AUTO mode. This means there won't be any prompts, which makes the modloader's jobs easier*

### Supported Values
- "DELTAMOD"
- "DELTAHUB"

## deltaruneVariants
**This is an important field!**

The `deltaruneVariants` field are the *supported variants of DELTARUNE the project is made for*.

While it is an *array*, which means it supports several values, *it is recommended for most workflows that this stays at one entry*.

### Supported Values
- "fullgame"
- "demo"
- "demo-lts"

## Other Fields

`deltaruneVersion` is the version of DELTARUNE this mod was made for. (it doesn't do anything yet lol)

**(Please make this the same as the DELTAMOD/DELTAHUB deltaruneVersion field if you are making this mod for those.)**


