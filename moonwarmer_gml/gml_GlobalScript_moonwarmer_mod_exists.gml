function moonwarmer_mod_exists(mod_id)
{
    return moonwarmer_get_mod_json(mod_id) != noone;
}