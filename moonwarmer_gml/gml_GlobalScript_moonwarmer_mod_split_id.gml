function moonwarmer_mod_split_id_array(mod_id)
{
    return string_split(mod_id, ".");
}

function moonwarmer_mod_split_id_struct(mod_id)
{
    var array = moonwarmer_mod_split_id_array(mod_id);

    return 
    {
        hosted_url: array[0],
        mod_name: array[1],
        mod_author: array[2],
    };
}