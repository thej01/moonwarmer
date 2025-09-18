# Moonwarmer
Moonwarmer is a mass-import script for utmt built for DELTARUNE.

Mods in Moonwarmer (or csx) format can be *combined together very easily, allowing for strong multi-mod support.*

However, it is *not a replacement for xdelta files due to limitations (unfortunately).*

But most mods should be able to be converted to Moonwarmer despite those limitations.
### Let's make DELTARUNE modding better, together!

# NOTE: Please use Bleeding Edge version of UMT for best code merging

# Lil recommendation (PSA)
You should use *asset_get_index()* when refering to assets when making deltarune mods from now on (not even just Moonwarmer ones), to improve **[GM3P](https://github.com/deltamodders/GM3P)** compatability until **Watercooler** (**Moonwarmer**'s bigger sibling) releases.

# When should I use Moonwarmer???
Moonwarmer should only be used for specific types of mods.

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

Unless UTMT adds room/object exporting/importing, there likely won't be any support for rooms/objects.

I would need to make custom scripts for objects and rooms to ensure best compatability. 
To be honest, I am not nearly skilled enough for that. (yet...?)

It should also be noted that when Moonwarmer imports scripts, it tries its best to merge things.

The merging works pretty well, *but if two mods change the same line, the last mod loaded will win.*

# Moonwarmer API
Moonwarmer has an API that can be used to retrieve things like other Moonwarmer mod information from within the data.win. This can allow for *cross-mod interactions!*

## [Read the documentation here!]()

# Moonwarmer Example Mods
*Now, I assume you probably want to make a mod for moonwarmer since you've read this much...*

## [Documentation for that is here! It also contains a template project and some example mods.](https://github.com/thej01/moonwarmer-example-mods)

# PSA: If Moonwarmer fails to import a project...
*First off, if Moonwarmer fails during importing, please revert to the vanilla data.win if you wish to attempt further imports.*
*This should prevent corruption and further errors.*

Second off, make a bug report on [**this page in the Issues tab**](https://github.com/thej01/moonwarmer/issues)! Please provide what mod(s) you were using, and the version & variant of DELTARUNE you're using.

# Does Moonwarmer work for other GameMaker games?
Sadly, no. The way Moonwarmer is built simply isn't viable for other games and other versions of GameMaker.
(I mean, it could *hypothetically* work for some. But I wouldn't recommend it at all.)

And no, I won't be adding more generalized support in the future myself...

However, if any of you are dedicated enough, I'd *love* to see Moonwarmer forks for other games (or even a fork that makes Moonwarmer more generalized for GameMaker games)

# Contributions 
*Got a sick feature/bug fix for Moonwarmer?* Hell yeah, make a PR and hopefully that can get merged.