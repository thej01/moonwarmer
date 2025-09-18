function moonwarmer_timestring_split_array(timestring)
{
    return string_split(timestring, "-");
}

function moonwarmer_timestring_split_struct(timestring)
{
    var array = moonwarmer_timestring_split_array(timestring)

    return 
    {
        year: array[0],
        month: array[1],
        day: array[2],
        hour: array[3],
        minute: array[4],
        second: array[5],
        millisecond: array[6],
    };
}