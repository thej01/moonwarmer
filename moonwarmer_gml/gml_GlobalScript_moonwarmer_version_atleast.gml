function moonwarmer_version_atleast(version)
{
    var num_current = real(string_digits(moonwarmer_version()))
    var num_expecting = real(string_digits(version))

    return num_current >= num_expecting;
}

function moonwarmer_mod_version_atleast(mod_id, version)
{
    var mod_json = moonwarmer_get_mod_json(mod_id)
    if mod_json == noone
        return false;
    
    var num_current = real(string_digits(mod_json.metadata.version))
    var num_expecting = real(string_digits(version))

    return num_current >= num_expecting;
}

function moonwarmer_mod_deltarune_version_atleast(mod_id, version)
{
    var mod_json = moonwarmer_get_mod_json(mod_id)
    if mod_json == noone
        return false;
    if mod_json.deltaruneVersion == noone
        return false;
    
    var num_current = real(string_digits(mod_json.deltaruneVersion))
    var num_expecting = real(string_digits(version))

    return num_current >= num_expecting;
}