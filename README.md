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
To be honest, I am not nearly skilled enough for that.

# Other limitations

It should also be noted that when Moonwarmer imports scripts, it does *not* merge anything. It completely overrides the original script.

I thought about adding merging, *but without the original data.win's files, im not sure how it could be done.*