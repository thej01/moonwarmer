// Based of ImportGraphics.csx, ImportGML.csx, ImportShaders.gml, ImportSingleSound.gml
// MOONWARMER -- a deltarune mass-import script.
// whyt are you like this why load assemblies from the DATA.WIN FOLDER IK HATE YOU YI HTEA YOU
//#r "/moonwarmer_dependencies/DiffPlex.dll"

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using UndertaleModLib;
using UndertaleModLib.Util;
using UndertaleModLib.Models;
using Underanalyzer.Compiler;
using Underanalyzer.Decompiler;
using System.Reflection;
using static UndertaleModLib.Models.UndertaleSound;
using static UndertaleModLib.UndertaleData;
using ImageMagick;

EnsureDataLoaded();

// -- configuration -- 

bool packaged_moonwarmer = false;

// Try to merge code on existing files. Seems to work well but i cant trust anything.
// This WILL merge code even if this is the first mod we're installing, and that's because
// of data.win updates. I'm scared that could cause issues
// Merge conflicts are automatically resolved in a last-mod-wins style.
bool merge_code = true;

// These arrays contains code files in the data.win where merging conflicts should result in *both modifications* being added. stupid i know.
// Examples: scr_gamestart, initializer2_create_0
// This is mainly for initialization files or info files, and it is hacky (and can cause compilier errors, but when it works, it's wonderful)

// NOTE: this one checks if the name CONTAINS any of these
string[] codemerging_always_add_contains =
[
    "gamestart",
    "initializer_Create_0",
    "initializer2_Create_0",
    "scr_load_chapter",
];

// Same as above, but for exact matches
string[] codemerging_always_add_exact =
[
    "gml_GlobalScript_scr_saveprocess",
    "gml_GlobalScript_scr_save",
    "gml_GlobalScript_scr_load",
    "gml_GlobalScript_scr_text",
    "gml_GlobalScript_scr_iteminfo",
    "gml_GlobalScript_scr_armorinfo",
    "gml_GlobalScript_scr_iteminfo",
    "gml_GlobalScript_scr_keyiteminfo",
    "gml_GlobalScript_scr_spellinfo",
    "gml_GlobalScript_scr_litemname",
    "gml_GlobalScript_scr_litemdesc",
    "gml_GlobalScript_scr_litemuseb",
    "gml_GlobalScript_scr_spelltext",
    "gml_GlobalScript_scr_spell",
    "gml_Object_obj_treasure_room_Create_0",
    "gml_Object_obj_treasure_room_Other_10",
];

Assembly? diffplex = null;

string moonwarmer_version = "v2";

string scriptDir = Path.GetDirectoryName(ScriptPath);

string moonwarmer_gml = scriptDir + "/moonwarmer_gml";

if (!Directory.Exists(moonwarmer_gml))
    throw new ScriptException("Couldn't find moonwarmer_gml folder!\nExpected at: " + moonwarmer_gml + "\nDid you move the csx script from the moonwarmer_gml folder?");

string[] moonwarmer_gml_files = Directory.GetFiles(scriptDir, "*.gml", SearchOption.AllDirectories);

if (moonwarmer_gml_files.Length <= 0)
    throw new ScriptException("Couldn't find any moonwarmer .gml files in:\n" + moonwarmer_gml);

// -- loading project --

string? project_folder = scriptDir + "/";
if (!packaged_moonwarmer)
{
    ScriptMessage("Please select a moonwarmer STANDALONE folder/zip file. It should contain a file called _moonwarmer.json");
    project_folder = PromptChooseDirectory();
}

if (project_folder is null)
    throw new ScriptException("A folder/zip file was not set.");

string moonwarmer_json = project_folder + "_moonwarmer.json";

if (!File.Exists(moonwarmer_json))
{
    if (!packaged_moonwarmer)
        throw new ScriptException("_moonwarmer.json not found! Make sure you selected the folder that contains _moonwarmer.json");
    else
        throw new ScriptException("_moonwarmer.json not found! Is moonwarmer.csx a directory too high/low? moonwarmer.csx should be right outside the src folder.");
}

// load the json file
string txt = File.ReadAllText(moonwarmer_json);
MoonwarmerJson? loaded_json = JsonSerializer.Deserialize<MoonwarmerJson>(txt);
MoonwarmerMetadata? meta = loaded_json.metadata;
// bruh
if (loaded_json is null || meta is null || meta.name is null || meta.packageID is null || meta.version is null)
    throw new ScriptException("_moonwarmer.json is invalid. Please make sure that your JSON follows proper JSON syntax rules and has all required fields.");

if (loaded_json.supportedPackageTypes is null)
    loaded_json.supportedPackageTypes = [];
if (loaded_json.deltaruneVariants is null)
    loaded_json.deltaruneVariants = [];

// if enabled, it just auto detects the chapter without confirmation (which should work fine, but just in case you can disable this)
// if disabled, it will prompt the user to input the chapter number (with the autodetect value being the default)
// also setting it like this kinda dumb but whatever
bool autodetect_chapter = loaded_json.supportedPackageTypes.Length > 0;

// Load assembly
if (merge_code)
{
    // SOOO because of an utmt limitation we have to load the dependency at runtime.
    // Stupid i know.
    string diffplex_asm_path = scriptDir + "/moonwarmer_dependencies/DiffPlex.dll";
    if (File.Exists(diffplex_asm_path))
        diffplex = Assembly.LoadFrom(diffplex_asm_path);
    if (diffplex is null)
    {
        if (!autodetect_chapter)
            ScriptWarning("Couldn't find DiffPlex.dll at " + diffplex_asm_path + "\nThis will not prevent the script from running, but code will not be merged.");
        merge_code = false;
    }
}

// -- importgrpahics stuff no one caresss --

static List<MagickImage> imagesToCleanup = new List<MagickImage>();

bool importAsSprite = false;

// TODO: see if this can be reimplemented using substring instead of regex?
// "(.+?)" - match everything; "?" = match as few characters as possible.
// "(?:_(\d+))" - an underscore followed by digits;
// "?:" = don't make a separate group for the whole part
Regex sprFrameRegex = new(@"^(.+?)(?:_(\d+))$", RegexOptions.Compiled);

bool noMasksForBasicRectangles = Data.IsVersionAtLeast(2022, 9); // TODO: figure out the exact version, but this is pretty close

// -- the good shit --

// do chapter 0 for launcher
int chapter = 1;

// get the chapter number from the digits in the display name (fucking stupid, i know)
string nums = Regex.Replace(Data.GeneralInfo.DisplayName.Content, @"[^\d]", "");

// default to Launcher ig
if (!int.TryParse(nums, out chapter))
    chapter = 0;
if (!autodetect_chapter)
{
    string txt = SimpleTextInput("Chapter Number", "Input Chapter number (0 for launcher) Autodetected as chapter " + chapter.ToString(), chapter.ToString(), false);
    if (txt is null)
        throw new ScriptException("The chapter number was not set.");
    bool worked = int.TryParse(txt, out chapter);
    if (!worked)
        throw new ScriptException("Failed to parse chapter number.");
}

string subProjName = "";
if (chapter == 0)
    subProjName = meta.name + " Launcher";
else
    subProjName = meta.name + " Chapter " + chapter.ToString();

// get important directories
string srcDirectory = project_folder;
string[] projectDirectories = new String[2];
projectDirectories[0] = srcDirectory + "_everychapter";
projectDirectories[1] = srcDirectory + "chapter" + chapter.ToString();

if (!Directory.Exists(srcDirectory) && autodetect_chapter)
    throw new ScriptException("src folder missing. Did you move the csx script, but not the src folder packaged with it? The src folder should be in the same directory as the csx script.");

List<string> codeFiles = new List<string>();
List<string> spriteDirectories = new List<string>();
List<string> shaderDirectories = new List<string>();
List<string> audioFiles = new List<string>();
foreach (string dir in projectDirectories)
{
    string[] scriptsDirs = { dir + "/scripts", dir + "/code" };
    string objectsDir = dir + "/objects";
    string spriteDir = dir + "/sprites";
    string shaderDir = dir + "/shaders";
    string[] audioDirs = { dir + "/audio", dir + "/sounds", dir + "/mus" };
    foreach (string secondaryDir in scriptsDirs)
    {
        if (Directory.Exists(secondaryDir))
            codeFiles.AddRange(Directory.GetFiles(secondaryDir, "*.gml", SearchOption.AllDirectories));
    }
    if (Directory.Exists(objectsDir))
        codeFiles.AddRange(Directory.GetFiles(objectsDir, "*.gml", SearchOption.AllDirectories));
    if (Directory.Exists(spriteDir))
        spriteDirectories.Add(spriteDir);
    if (Directory.Exists(shaderDir))
        shaderDirectories.Add(shaderDir);

    foreach (string secondaryDir in audioDirs)
    {
        if (Directory.Exists(secondaryDir))
        {
            audioFiles.AddRange(Directory.GetFiles(secondaryDir, "*.wav", SearchOption.AllDirectories));
            audioFiles.AddRange(Directory.GetFiles(secondaryDir, "*.ogg", SearchOption.AllDirectories));
        }
    }
}

// Detect 2 duplicate (only 2 im lazy) code files if merging is enabled and then error (because merging fucks up if two of the same files are loaded)
// Also just bad practice lol (makes me wonder if i should remove the if statement...)
if (merge_code)
{
    List<string> seenCode = new List<string>();
    List<string> seenCode_fullpath = new List<string>();
    foreach (string file in codeFiles)
    {
        string fname = Path.GetFileName(file);
        string pname = file;
        if (seenCode.Contains(fname))
        {
            int dupIndex = seenCode.IndexOf(fname);
            throw new ScriptException("There are two scripts with same name \"" + fname + "\" trying to be loaded at the same time!\n\nPath 1: "
                                      + pname + "\n\nPath 2: " + seenCode_fullpath[dupIndex] + "\n\nPlease remove one of them.");
        }
        seenCode.Add(fname);
        seenCode_fullpath.Add(pname);
    }
}

int totalSpriteImages = 0;
foreach (string dir in spriteDirectories)
    totalSpriteImages += Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories).Length;
int totalShaderDirs = 0;
foreach (string dir in shaderDirectories)
    totalShaderDirs += Directory.GetDirectories(dir).Length;

SetProgressBar("Importing project files", "Initalizing...", 0, totalSpriteImages + codeFiles.Count + totalShaderDirs + audioFiles.Count + moonwarmer_gml_files.Length);
StartProgressBarUpdater();

string og_code_prefix = "___moonwarmer_original___";
// setup decompile shit (this is dynamic cuz of below btw)
dynamic globalDecompileContext = new GlobalDecompileContext(Data);

// when we try to append from merge conflicts, we check if the code compiles, if it does, use it. if it doesn't, dont do append merging.
// however, the function below is only in very recent versions of umt (we need it to parse code)
bool can_recover_from_append = true;
try
{
    globalDecompileContext.PrepareForCompilation();
}
catch (Exception e)
{
    SetUMTConsoleText("Append merge resolving has been disabled, update UMT to be able to enable it!");
    can_recover_from_append = false;
}

IDecompileSettings decompilerSettings = Data.ToolInfo.DecompilerSettings;
// diffplex  stuff  yea h
// I HATE REFLECTION I HATE REFLECTION I HATE REFLECTION I HATE REFLECTIONI HATE REFLECTION I HATE REFLECTION I HATE REFLECTION I HATE REFLECTION I HATE REFLECTION I HATE REFLECTION
object[]? mergeParams = null;
object? threeDifObject = null;
MethodInfo? createDiff = null;
Type? threeDifResult = null;
Type? threeWayDiffBlock = null;
Type? threeWayChangeType = null;
dynamic? threeWayChangeType_Unchanged = null;
dynamic? threeWayChangeType_OldOnly = null;
dynamic? threeWayChangeType_NewOnly = null;
dynamic? threeWayChangeType_BothSame = null;
dynamic? threeWayChangeType_Conflict = null;
// TODO: get change types like insertion and deletion so the merging can be almost-perfect!
if (merge_code)
{
    Type threeDifType = diffplex.GetType("DiffPlex.ThreeWayDiffer");
    Type lineChunkerType = diffplex.GetType("DiffPlex.Chunkers.LineChunker");
    threeWayDiffBlock = diffplex.GetType("DiffPlex.Model.ThreeWayDiffBlock");
    threeDifResult = diffplex.GetType("DiffPlex.Model.ThreeWayDiffResult");
    threeWayChangeType = diffplex.GetType("DiffPlex.Model.ThreeWayChangeType");
    createDiff = threeDifType.GetMethod("CreateDiffs");

    mergeParams = new object[6];
    threeDifObject = Activator.CreateInstance(threeDifType);

    threeWayChangeType_Unchanged = threeWayChangeType.GetField("Unchanged").GetValue(null);
    threeWayChangeType_OldOnly = threeWayChangeType.GetField("OldOnly").GetValue(null);
    threeWayChangeType_NewOnly = threeWayChangeType.GetField("NewOnly").GetValue(null);
    threeWayChangeType_BothSame = threeWayChangeType.GetField("BothSame").GetValue(null);
    threeWayChangeType_Conflict = threeWayChangeType.GetField("Conflict").GetValue(null);

    mergeParams[0] = "";
    mergeParams[1] = "";
    mergeParams[2] = "";
    mergeParams[3] = true;
    mergeParams[4] = false;
    mergeParams[5] = Activator.CreateInstance(lineChunkerType);
}

SyncBinding("Sprites, Backgrounds, Fonts, EmbeddedTextures, TexturePageItems, Strings, Scripts, Code, CodeLocals, GlobalInitScripts, GameObjects, Functions, Sounds, EmbeddedAudio, AudioGroups, Shaders", true);

await Task.Run(() =>
{
    // Import Graphics
    if (totalSpriteImages > 0)
    {
        UpdateProgressStatus("Sprite Images");

        foreach (string dir in spriteDirectories)
            ImportGraphics(dir, Data);
    }

    // Import Audio
    if (audioFiles.Count > 0)
    {
        UpdateProgressStatus("Audio");

        foreach (string file in audioFiles)
            ImportSingleSound(file);
    }

    // Import Shaders
    if (totalShaderDirs > 0)
    {
        UpdateProgressStatus("Shaders");

        foreach (string dir in shaderDirectories)
            ImportShaders(dir);
    }

    // Moonwarmer API import
    UpdateProgressStatus("Importing Moonwarmer API");
    CodeImportGroup importGroup = new(Data) { AutoCreateAssets = true };
    MoonwarmerImportAPI(importGroup);

    // Import GML (but with merging cuz im just better tbh)
    if (codeFiles.Count > 0)
    {
        UpdateProgressStatus("Code");
        foreach (string file in codeFiles)
        {
            IncrementProgress();
            string codeName = Path.GetFileNameWithoutExtension(file);
            string code = File.ReadAllText(file);

            // we need to check if the file we're replacing exists yet
            if (merge_code && Data.Code.ByName(codeName) is not null)
            {
                // Oh god. We need to merge. Oh god. Oh no. Worries. :(
                importGroup.AutoCreateAssets = false;

                // load current code
                string current_code = MoonwarmerQuickDecompile(codeName);
                string original_code = current_code;

                // check if a moonwarmer original exists
                string ogCodeName = og_code_prefix + codeName;
                if (Data.Code.ByName(ogCodeName) is not null)
                    // if it does, we need to load it for proper merging
                    original_code = MoonwarmerQuickDecompile(ogCodeName);
                else
                    // if it doesn't lets make it after
                    importGroup.QueueReplace(ogCodeName, current_code);

                CompileScriptKind kind = GuessScriptKindFromName(codeName);

                mergeParams[0] = original_code;
                mergeParams[1] = current_code;
                mergeParams[2] = code;

                dynamic outputResult = createDiff.Invoke(threeDifObject, mergeParams);

                bool append_conflicts = false;
                if (codemerging_always_add_exact.Contains(codeName))
                    append_conflicts = true;
                else
                {
                    foreach (string name in codemerging_always_add_contains)
                    {
                        if (codeName.Contains(name))
                        {
                            append_conflicts = true;
                            break;
                        }
                    }
                }

                string output_code = MoonwarmerCustomMerge(outputResult, append_conflicts);

                // only works on very recent umt versions
                if (append_conflicts && can_recover_from_append)
                {
                    CompileContext compileContext_result = new(output_code, kind, codeName, globalDecompileContext);
                    compileContext_result.Parse();

                    if (compileContext_result.HasErrors)
                    {
                        SetUMTConsoleText("!!! Errors when trying to append merge code " + codeName + " so we're just doing normal merging.");
                        // append_conflicts resulted in a compilier error so lets just do a more basic merge
                        output_code = MoonwarmerCustomMerge(outputResult, false);
                    }
                }

                // Import our output and pray.
                importGroup.AutoCreateAssets = true;
                importGroup.QueueReplace(codeName, output_code);
            }
            else
                // just import! no worries :)
                importGroup.QueueReplace(codeName, code);

        }
    }
    
    UpdateProgressStatus("Finishing import...");
    importGroup.Import();
});

DisableAllSyncBindings();

await StopProgressBarUpdater();
ScriptMessage("Project " + subProjName + " successfully imported.");

string MoonwarmerQuickDecompile(string codeName)
{
    return new DecompileContext(globalDecompileContext, Data.Code.ByName(codeName), decompilerSettings).DecompileToString();
}

string[] StringToArray(string str)
{
    return str.Split("\n");
}

string ArrayToString(string[] stringArray)
{
    return string.Join("\n", stringArray);
}

string MoonwarmerCustomMerge(dynamic diffResult, bool append_conflicts = false)
{
    var mergedPieces = new List<string>();

    var baseIndex = 0;
    var oldIndex = 0;
    var newIndex = 0;

    foreach (dynamic block in diffResult.DiffBlocks)
    {
        // Add unchanged content before this block
        while (baseIndex < block.BaseStart)
        {
            mergedPieces.Add(diffResult.PiecesBase[baseIndex]);
            baseIndex++;
            oldIndex++;
            newIndex++;
        }

        // Can't use a switch statement because these are runtime...
        if (block.ChangeType == threeWayChangeType_Unchanged)
        {
            // Add base content (all are the same)
            for (int i = 0; i < block.BaseCount; i++)
                mergedPieces.Add(diffResult.PiecesBase[baseIndex + i]);
        }
        else if (block.ChangeType == threeWayChangeType_OldOnly)
        {
            // Take old version
            for (int i = 0; i < block.OldCount; i++)
                mergedPieces.Add(diffResult.PiecesOld[oldIndex + i]);
        }
        else if (block.ChangeType == threeWayChangeType_NewOnly)
        {
            // Take new version
            for (int i = 0; i < block.NewCount; i++)
                mergedPieces.Add(diffResult.PiecesNew[newIndex + i]);
        }
        else if (block.ChangeType == threeWayChangeType_BothSame)
        {
            // Both made the same change, take either (we'll take old)
            for (int i = 0; i < block.OldCount; i++)
                mergedPieces.Add(diffResult.PiecesOld[oldIndex + i]);
        }
        else if (block.ChangeType == threeWayChangeType_Conflict)
        {
            // Last mod wins
            for (int i = 0; i < block.NewCount; i++)
                mergedPieces.Add(diffResult.PiecesNew[newIndex + i]);

            if (append_conflicts)
            {
                for (int i = 0; i < block.OldCount; i++)
                    mergedPieces.Add(diffResult.PiecesOld[oldIndex + i]);
            }
        }

        baseIndex += block.BaseCount;
        oldIndex += block.OldCount;
        newIndex += block.NewCount;
    }

    // Add remaining unchanged content
    while (baseIndex < diffResult.PiecesBase.Length)
    {
        mergedPieces.Add(diffResult.PiecesBase[baseIndex]);
        baseIndex++;
    }

    return ArrayToString(mergedPieces.ToArray());
}

List<string> api_code_array = new List<string>();
int lineno = 0;
// imports the moonwarmer api into the data.win (or updates it if its already there)
void MoonwarmerImportAPI(CodeImportGroup importGroup)
{
    foreach (string gml in moonwarmer_gml_files)
    {
        IncrementProgress();
        string codeName = Path.GetFileNameWithoutExtension(gml);
        string code = "";
        bool was_here = false;
        // if it isnt there read from file
        if (Data.Code.ByName(codeName) is null)
            code = File.ReadAllText(gml);
        else
        {
            // read from current code
            code = MoonwarmerQuickDecompile(codeName);
            was_here = true;
        }

        api_code_array = StringToArray(code).ToList();

        bool import = MoonwarmerHandleAPIFunc(codeName, was_here);

        if (import)
            importGroup.QueueReplace(codeName, ArrayToString(api_code_array.ToArray()));
    }
}

bool MoonwarmerHandleAPIFunc(string codeName, bool was_here)
{
    switch (codeName)
    {
        case "gml_GlobalScript_moonwarmer_version":
            return MoonwarmerAPI_version(codeName, was_here);

        case "gml_GlobalScript_moonwarmer_modcount":
            return MoonwarmerAPI_modcount(codeName, was_here);

        case "gml_GlobalScript_moonwarmer_get_mod_json":
            return MoonwarmerAPI_get_mod_json(codeName, was_here);

        case "gml_GlobalScript_moonwarmer_get_all_mod_ids":
            return MoonwarmerAPI_get_all_mod_ids(codeName, was_here);

        default:
            return false;
    }
}

// these functions feel dumb. they probably are.
bool MoonwarmerAPI_version(string codeName, bool was_here)
{
    lineno = 0;
    foreach (string line in api_code_array)
    {
        if (line.Contains("return"))
        {
            api_code_array[lineno] = "return \"" + moonwarmer_version + "\";";
            return true;
        }
        lineno++;
    }

    return true;
}

bool MoonwarmerAPI_modcount(string codeName, bool was_here)
{
    lineno = 0;
    foreach (string line in api_code_array)
    {
        if (line.Contains("modno = "))
        {
            // get only numbers, and add 1
            int new_no = int.Parse(Regex.Replace(line, @"[^\d]", "")) + 1;
            api_code_array[lineno] = "var modno = " + new_no.ToString() + ";";
            return true;
        }
        lineno++;
    }

    return true;
}

void MAPIInsertCurrent(string txt)
{
    api_code_array.Insert(lineno, txt);
    lineno++;
}

string MAPIArrayToString<T>(T[] array, string gml_type = "string")
{
    string stringy = "[ ";
    foreach (T item in array)
    {
        string good_yummy = item.ToString();
        if (gml_type == "string")
            good_yummy = "\"" + good_yummy + "\"";
        stringy += good_yummy + ", ";
    }
    stringy += "]";
    return stringy;
}

bool MoonwarmerAPI_get_mod_json(string codeName, bool was_here)
{
    lineno = 0;
    bool add = false;
    foreach (string line in api_code_array)
    {
        if (add)
        {
            MAPIInsertCurrent("case \"" + meta.packageID + "\":");
            string dr_ver = "noone";
            if (loaded_json.deltaruneVersion is not null)
                dr_ver = "\"" + loaded_json.deltaruneVersion + "\"";
            else
                dr_ver = "noone";

            string meta_string = string.Format(
            """
            name: "{0}", version: "{1}", packageID: "{2}",
            """, meta.name, meta.version, meta.packageID);

            string main_string = "metadata: {" + meta_string + "}, deltaruneVersion: " + dr_ver + ",";

            string pkg_types_string = "supportedPackageTypes: " + MAPIArrayToString(loaded_json.supportedPackageTypes) + ",";
            string dr_variants_string = "deltaruneVariants: " + MAPIArrayToString(loaded_json.deltaruneVariants) + ",";

            string time_string = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff");

            string manifest_string = "_manfiest: { moonwarmerVersion: \"" + moonwarmer_version + "\", timeString: \"" + time_string + "\", },";

            MAPIInsertCurrent("return {" + main_string + pkg_types_string + dr_variants_string + manifest_string + "};");
            MAPIInsertCurrent("");
            return true;
        }
        if (line.Contains("case") || line.Contains("default"))
            add = true;
        else
            lineno++;
    }

    return true;
}

bool MoonwarmerAPI_get_all_mod_ids(string codeName, bool was_here)
{
    lineno = 0;
    bool add = false;
    foreach (string line in api_code_array)
    {
        if (add)
        {
            MAPIInsertCurrent("array_push(modlist, \"" + meta.packageID + "\");");
            return true;
        }
        if (line.Contains("modlist ="))
            add = true;
        lineno++;
    }

    return true;
}

// SPRITE IMPORT CODE FROM IMPORTGRAPHICS.csx

// Texture packer by Samuel Roy
// Uses code from https://github.com/mfascia/TexturePacker

void ImportGraphics(string sourceFolder, UndertaleData Data)
{
    // TODO: see if this can be reimplemented using substring instead of regex?
    // "(.+?)" - match everything; "?" = match as few characters as possible.
    // "(?:_(\d+))" - an underscore followed by digits;
    // "?:" = don't make a separate group for the whole part
    string importFolder = CheckValidity(sourceFolder);

    try
    {
        string packDir = Path.Combine(ExePath, "Packager");
        Directory.CreateDirectory(packDir);

        string sourcePath = importFolder;
        string searchPattern = "*.png";
        string outName = Path.Combine(packDir, "atlas.txt");
        int textureSize = 2048;
        int PaddingValue = 2;
        bool debug = false;
        Packer packer = new Packer();
        packer.Process(sourcePath, searchPattern, textureSize, PaddingValue, debug);
        packer.SaveAtlasses(outName);

        int lastTextPage = Data.EmbeddedTextures.Count - 1;
        int lastTextPageItem = Data.TexturePageItems.Count - 1;

        bool bboxMasks = Data.IsVersionAtLeast(2024, 6);
        Dictionary<UndertaleSprite, Node> maskNodes = new();

        // Import everything into UTMT
        string prefix = outName.Replace(Path.GetExtension(outName), "");
        int atlasCount = 0;
        foreach (Atlas atlas in packer.Atlasses)
        {
            string atlasName = Path.Combine(packDir, $"{prefix}{atlasCount:000}.png");
            using MagickImage atlasImage = TextureWorker.ReadBGRAImageFromFile(atlasName);
            IPixelCollection<byte> atlasPixels = atlasImage.GetPixels();

            UndertaleEmbeddedTexture texture = new();
            texture.Name = new UndertaleString($"Texture {++lastTextPage}");
            texture.TextureData.Image = GMImage.FromMagickImage(atlasImage).ConvertToPng(); // TODO: other formats?
            Data.EmbeddedTextures.Add(texture);

            foreach (Node n in atlas.Nodes)
            {
                if (n.Texture != null)
                {
                    IncrementProgress();
                    // Initalize values of this texture
                    UndertaleTexturePageItem texturePageItem = new();
                    texturePageItem.Name = new UndertaleString($"PageItem {++lastTextPageItem}");
                    texturePageItem.SourceX = (ushort)n.Bounds.X;
                    texturePageItem.SourceY = (ushort)n.Bounds.Y;
                    texturePageItem.SourceWidth = (ushort)n.Bounds.Width;
                    texturePageItem.SourceHeight = (ushort)n.Bounds.Height;
                    texturePageItem.TargetX = (ushort)n.Texture.TargetX;
                    texturePageItem.TargetY = (ushort)n.Texture.TargetY;
                    texturePageItem.TargetWidth = (ushort)n.Bounds.Width;
                    texturePageItem.TargetHeight = (ushort)n.Bounds.Height;
                    texturePageItem.BoundingWidth = (ushort)n.Texture.BoundingWidth;
                    texturePageItem.BoundingHeight = (ushort)n.Texture.BoundingHeight;
                    texturePageItem.TexturePage = texture;

                    // Add this texture to UMT
                    Data.TexturePageItems.Add(texturePageItem);

                    // String processing
                    string stripped = Path.GetFileNameWithoutExtension(n.Texture.Source);

                    SpriteType spriteType = GetSpriteType(n.Texture.Source);
                    if (importAsSprite)
                    {
                        if (spriteType == SpriteType.Unknown || spriteType == SpriteType.Font)
                        {
                            spriteType = SpriteType.Sprite;
                        }
                    }

                    if (spriteType == SpriteType.Background)
                    {
                        UndertaleBackground background = Data.Backgrounds.ByName(stripped);
                        if (background != null)
                        {
                            background.Texture = texturePageItem;
                        }
                        else
                        {
                            // No background found, let's make one
                            UndertaleString backgroundUTString = Data.Strings.MakeString(stripped);
                            UndertaleBackground newBackground = new();
                            newBackground.Name = backgroundUTString;
                            newBackground.Transparent = false;
                            newBackground.Preload = false;
                            newBackground.Texture = texturePageItem;
                            Data.Backgrounds.Add(newBackground);
                        }
                    }
                    else if (spriteType == SpriteType.Sprite)
                    {
                        // Get sprite to add this texture to
                        string spriteName;
                        int frame = 0;
                        try
                        {
                            var spriteParts = sprFrameRegex.Match(stripped);
                            spriteName = spriteParts.Groups[1].Value;
                            Int32.TryParse(spriteParts.Groups[2].Value, out frame);
                        }
                        catch (Exception e)
                        {
                            ScriptMessage($"Error: Image {stripped} has an invalid name. Skipping...");
                            continue;
                        }

                        // Create TextureEntry object
                        UndertaleSprite.TextureEntry texentry = new();
                        texentry.Texture = texturePageItem;

                        // Set values for new sprites
                        UndertaleSprite sprite = Data.Sprites.ByName(spriteName);
                        if (sprite is null)
                        {
                            UndertaleString spriteUTString = Data.Strings.MakeString(spriteName);
                            UndertaleSprite newSprite = new();
                            newSprite.Name = spriteUTString;
                            newSprite.Width = (uint)n.Texture.BoundingWidth;
                            newSprite.Height = (uint)n.Texture.BoundingHeight;
                            newSprite.MarginLeft = n.Texture.TargetX;
                            newSprite.MarginRight = n.Texture.TargetX + n.Bounds.Width - 1;
                            newSprite.MarginTop = n.Texture.TargetY;
                            newSprite.MarginBottom = n.Texture.TargetY + n.Bounds.Height - 1;
                            newSprite.OriginX = 0;
                            newSprite.OriginY = 0;
                            if (frame > 0)
                            {
                                for (int i = 0; i < frame; i++)
                                    newSprite.Textures.Add(null);
                            }

                            // Only generate collision masks for sprites that need them (in newer GameMaker versions)
                            if (!noMasksForBasicRectangles ||
                                newSprite.SepMasks is not (UndertaleSprite.SepMaskType.AxisAlignedRect or UndertaleSprite.SepMaskType.RotatedRect))
                            {
                                // Generate mask later (when the current atlas is about to be unloaded)
                                maskNodes.Add(newSprite, n);
                            }

                            newSprite.Textures.Add(texentry);
                            Data.Sprites.Add(newSprite);
                            continue;
                        }

                        if (frame > sprite.Textures.Count - 1)
                        {
                            while (frame > sprite.Textures.Count - 1)
                            {
                                sprite.Textures.Add(texentry);
                            }
                            continue;
                        }

                        sprite.Textures[frame] = texentry;

                        // Update sprite dimensions
                        uint oldWidth = sprite.Width, oldHeight = sprite.Height;
                        sprite.Width = (uint)n.Texture.BoundingWidth;
                        sprite.Height = (uint)n.Texture.BoundingHeight;
                        bool changedSpriteDimensions = (oldWidth != sprite.Width || oldHeight != sprite.Height);

                        // Grow bounding box depending on how much is trimmed
                        bool grewBoundingBox = false;
                        bool fullImageBbox = sprite.BBoxMode == 1;
                        bool manualBbox = sprite.BBoxMode == 2;
                        if (!manualBbox)
                        {
                            int marginLeft = fullImageBbox ? 0 : n.Texture.TargetX;
                            int marginRight = fullImageBbox ? ((int)sprite.Width - 1) : (n.Texture.TargetX + n.Bounds.Width - 1);
                            int marginTop = fullImageBbox ? 0 : n.Texture.TargetY;
                            int marginBottom = fullImageBbox ? ((int)sprite.Height - 1) : (n.Texture.TargetY + n.Bounds.Height - 1);
                            if (marginLeft < sprite.MarginLeft)
                            {
                                sprite.MarginLeft = marginLeft;
                                grewBoundingBox = true;
                            }
                            if (marginTop < sprite.MarginTop)
                            {
                                sprite.MarginTop = marginTop;
                                grewBoundingBox = true;
                            }
                            if (marginRight > sprite.MarginRight)
                            {
                                sprite.MarginRight = marginRight;
                                grewBoundingBox = true;
                            }
                            if (marginBottom > sprite.MarginBottom)
                            {
                                sprite.MarginBottom = marginBottom;
                                grewBoundingBox = true;
                            }
                        }

                        // Only generate collision masks for sprites that need them (in newer GameMaker versions)
                        if (!noMasksForBasicRectangles ||
                            sprite.SepMasks is not (UndertaleSprite.SepMaskType.AxisAlignedRect or UndertaleSprite.SepMaskType.RotatedRect) ||
                            sprite.CollisionMasks.Count > 0)
                        {
                            if ((bboxMasks && grewBoundingBox) ||
                                (sprite.SepMasks is UndertaleSprite.SepMaskType.Precise && sprite.CollisionMasks.Count == 0) ||
                                (!bboxMasks && changedSpriteDimensions))
                            {
                                // Use this node for the sprite's collision mask if the bounding box grew, if no collision mask exists for a precise sprite,
                                // or if the sprite's dimensions have been changed altogether when bbox masks are not active.
                                maskNodes[sprite] = n;
                            }
                        }
                    }
                }
            }

            // Update masks for when bounding box masks are enabled
            foreach ((UndertaleSprite maskSpr, Node maskNode) in maskNodes)
            {
                // Generate collision mask using either bounding box or sprite dimensions
                maskSpr.CollisionMasks.Clear();
                maskSpr.CollisionMasks.Add(maskSpr.NewMaskEntry(Data));
                (int maskWidth, int maskHeight) = maskSpr.CalculateMaskDimensions(Data);
                int maskStride = ((maskWidth + 7) / 8) * 8;

                BitArray maskingBitArray = new BitArray(maskStride * maskHeight);
                for (int y = 0; y < maskHeight && y < maskNode.Bounds.Height; y++)
                {
                    for (int x = 0; x < maskWidth && x < maskNode.Bounds.Width; x++)
                    {
                        IMagickColor<byte> pixelColor = atlasPixels.GetPixel(x + maskNode.Bounds.X, y + maskNode.Bounds.Y).ToColor();
                        if (bboxMasks)
                        {
                            maskingBitArray[(y * maskStride) + x] = (pixelColor.A > 0);
                        }
                        else
                        {
                            maskingBitArray[((y + maskNode.Texture.TargetY) * maskStride) + x + maskNode.Texture.TargetX] = (pixelColor.A > 0);
                        }
                    }
                }
                BitArray tempBitArray = new BitArray(maskingBitArray.Length);
                for (int i = 0; i < maskingBitArray.Length; i += 8)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        tempBitArray[j + i] = maskingBitArray[-(j - 7) + i];
                    }
                }

                int numBytes = maskingBitArray.Length / 8;
                byte[] bytes = new byte[numBytes];
                tempBitArray.CopyTo(bytes, 0);
                for (int i = 0; i < bytes.Length; i++)
                    maskSpr.CollisionMasks[0].Data[i] = bytes[i];
            }
            maskNodes.Clear();

            // Increment atlas
            atlasCount++;
        }
    }
    finally
    {
        foreach (MagickImage img in imagesToCleanup)
        {
            img.Dispose();
        }
    }
}

public class TextureInfo
{
    public string Source;
    public int Width;
    public int Height;
    public int TargetX;
    public int TargetY;
    public int BoundingWidth;
    public int BoundingHeight;
    public MagickImage Image;
}

public enum SpriteType
{
    Sprite,
    Background,
    Font,
    Unknown
}


public enum SplitType
{
    Horizontal,
    Vertical,
}

public enum BestFitHeuristic
{
    Area,
    MaxOneAxis,
}

public struct Rect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class Node
{
    public Rect Bounds;
    public TextureInfo Texture;
    public SplitType SplitType;
}

public class Atlas
{
    public int Width;
    public int Height;
    public List<Node> Nodes;
}

public class Packer
{
    public List<TextureInfo> SourceTextures;
    public StringWriter Log;
    public StringWriter Error;
    public int Padding;
    public int AtlasSize;
    public bool DebugMode;
    public BestFitHeuristic FitHeuristic;
    public List<Atlas> Atlasses;

    public Packer()
    {
        SourceTextures = new List<TextureInfo>();
        Log = new StringWriter();
        Error = new StringWriter();
    }

    public void Process(string _SourceDir, string _Pattern, int _AtlasSize, int _Padding, bool _DebugMode)
    {
        Padding = _Padding;
        AtlasSize = _AtlasSize;
        DebugMode = _DebugMode;
        //1: scan for all the textures we need to pack
        ScanForTextures(_SourceDir, _Pattern);
        List<TextureInfo> textures = new List<TextureInfo>();
        textures = SourceTextures.ToList();
        //2: generate as many atlasses as needed (with the latest one as small as possible)
        Atlasses = new List<Atlas>();
        while (textures.Count > 0)
        {
            Atlas atlas = new Atlas();
            atlas.Width = _AtlasSize;
            atlas.Height = _AtlasSize;
            List<TextureInfo> leftovers = LayoutAtlas(textures, atlas);
            if (leftovers.Count == 0)
            {
                // we reached the last atlas. Check if this last atlas could have been twice smaller
                while (leftovers.Count == 0)
                {
                    atlas.Width /= 2;
                    atlas.Height /= 2;
                    leftovers = LayoutAtlas(textures, atlas);
                }
                // we need to go 1 step larger as we found the first size that is too small
                // if the atlas is 0x0 then it should be 1x1 instead
                if (atlas.Width == 0)
                {
                    atlas.Width = 1;
                }
                else
                {
                    atlas.Width *= 2;
                }
                if (atlas.Height == 0)
                {
                    atlas.Height = 1;
                }
                else
                {
                    atlas.Height *= 2;
                }
                leftovers = LayoutAtlas(textures, atlas);
            }
            Atlasses.Add(atlas);
            textures = leftovers;
        }
    }

    public void SaveAtlasses(string _Destination)
    {
        int atlasCount = 0;
        string prefix = _Destination.Replace(Path.GetExtension(_Destination), "");
        string descFile = _Destination;

        StreamWriter tw = new StreamWriter(_Destination);
        tw.WriteLine("source_tex, atlas_tex, x, y, width, height");
        foreach (Atlas atlas in Atlasses)
        {
            string atlasName = $"{prefix}{atlasCount:000}.png";

            // 1: Save images
            using (MagickImage img = CreateAtlasImage(atlas))
                TextureWorker.SaveImageToFile(img, atlasName);

            // 2: save description in file
            foreach (Node n in atlas.Nodes)
            {
                if (n.Texture != null)
                {
                    tw.Write(n.Texture.Source + ", ");
                    tw.Write(atlasName + ", ");
                    tw.Write((n.Bounds.X).ToString() + ", ");
                    tw.Write((n.Bounds.Y).ToString() + ", ");
                    tw.Write((n.Bounds.Width).ToString() + ", ");
                    tw.WriteLine((n.Bounds.Height).ToString());
                }
            }
            ++atlasCount;
        }
        tw.Close();
        tw = new StreamWriter(prefix + ".log");
        tw.WriteLine("--- LOG -------------------------------------------");
        tw.WriteLine(Log.ToString());
        tw.WriteLine("--- ERROR -----------------------------------------");
        tw.WriteLine(Error.ToString());
        tw.Close();
    }

    private void ScanForTextures(string _Path, string _Wildcard)
    {
        DirectoryInfo di = new(_Path);
        FileInfo[] files = di.GetFiles(_Wildcard, SearchOption.AllDirectories);
        foreach (FileInfo fi in files)
        {
            (int width, int height) = TextureWorker.GetImageSizeFromFile(fi.FullName);
            if (width == -1 || height == -1)
                continue;

            if (width <= AtlasSize && height <= AtlasSize)
            {
                TextureInfo ti = new();

                MagickReadSettings settings = new()
                {
                    ColorSpace = ColorSpace.sRGB,
                };
                MagickImage img = new(fi.FullName);
                imagesToCleanup.Add(img);

                ti.Source = fi.FullName;
                ti.BoundingWidth = (int)img.Width;
                ti.BoundingHeight = (int)img.Height;

                // GameMaker doesn't trim tilesets. I assume it didn't trim backgrounds too
                ti.TargetX = 0;
                ti.TargetY = 0;
                if (GetSpriteType(ti.Source) != SpriteType.Background)
                {
                    img.BorderColor = MagickColors.Transparent;
                    img.BackgroundColor = MagickColors.Transparent;
                    img.Border(1);
                    IMagickGeometry? bbox = img.BoundingBox;
                    if (bbox is not null)
                    {
                        ti.TargetX = bbox.X - 1;
                        ti.TargetY = bbox.Y - 1;
                        // yes, .Trim() mutates the image...
                        // it doesn't really matter though since it isn't written back or anything
                        img.Trim();
                    }
                    else
                    {
                        // Empty sprites should be 1x1
                        ti.TargetX = 0;
                        ti.TargetY = 0;
                        img.Crop(1, 1);
                    }
                    img.ResetPage();
                }
                ti.Width = (int)img.Width;
                ti.Height = (int)img.Height;
                ti.Image = img;

                SourceTextures.Add(ti);

                Log.WriteLine($"Added {fi.FullName}");
            }
            else
            {
                Error.WriteLine($"{fi.FullName} is too large to fix in the atlas. Skipping!");
            }
        }
    }

    private void HorizontalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
    {
        Node n1 = new Node();
        n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
        n1.Bounds.Y = _ToSplit.Bounds.Y;
        n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
        n1.Bounds.Height = _Height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _ToSplit.Bounds.X;
        n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
        n2.Bounds.Width = _ToSplit.Bounds.Width;
        n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
            _List.Add(n1);
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
            _List.Add(n2);
    }

    private void VerticalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
    {
        Node n1 = new Node();
        n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
        n1.Bounds.Y = _ToSplit.Bounds.Y;
        n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
        n1.Bounds.Height = _ToSplit.Bounds.Height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _ToSplit.Bounds.X;
        n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
        n2.Bounds.Width = _Width;
        n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
            _List.Add(n1);
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
            _List.Add(n2);
    }

    private TextureInfo FindBestFitForNode(Node _Node, List<TextureInfo> _Textures)
    {
        TextureInfo bestFit = null;
        float nodeArea = _Node.Bounds.Width * _Node.Bounds.Height;
        float maxCriteria = 0.0f;
        foreach (TextureInfo ti in _Textures)
        {
            switch (FitHeuristic)
            {
                // Max of Width and Height ratios
                case BestFitHeuristic.MaxOneAxis:
                    if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height)
                    {
                        float wRatio = (float)ti.Width / (float)_Node.Bounds.Width;
                        float hRatio = (float)ti.Height / (float)_Node.Bounds.Height;
                        float ratio = wRatio > hRatio ? wRatio : hRatio;
                        if (ratio > maxCriteria)
                        {
                            maxCriteria = ratio;
                            bestFit = ti;
                        }
                    }
                    break;
                // Maximize Area coverage
                case BestFitHeuristic.Area:
                    if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height)
                    {
                        float textureArea = ti.Width * ti.Height;
                        float coverage = textureArea / nodeArea;
                        if (coverage > maxCriteria)
                        {
                            maxCriteria = coverage;
                            bestFit = ti;
                        }
                    }
                    break;
            }
        }
        return bestFit;
    }

    private List<TextureInfo> LayoutAtlas(List<TextureInfo> _Textures, Atlas _Atlas)
    {
        List<Node> freeList = new List<Node>();
        List<TextureInfo> textures = new List<TextureInfo>();
        _Atlas.Nodes = new List<Node>();
        textures = _Textures.ToList();
        Node root = new Node();
        root.Bounds.Width = _Atlas.Width;
        root.Bounds.Height = _Atlas.Height;
        root.SplitType = SplitType.Horizontal;
        freeList.Add(root);
        while (freeList.Count > 0 && textures.Count > 0)
        {
            Node node = freeList[0];
            freeList.RemoveAt(0);
            TextureInfo bestFit = FindBestFitForNode(node, textures);
            if (bestFit != null)
            {
                if (node.SplitType == SplitType.Horizontal)
                {
                    HorizontalSplit(node, bestFit.Width, bestFit.Height, freeList);
                }
                else
                {
                    VerticalSplit(node, bestFit.Width, bestFit.Height, freeList);
                }
                node.Texture = bestFit;
                node.Bounds.Width = bestFit.Width;
                node.Bounds.Height = bestFit.Height;
                textures.Remove(bestFit);
            }
            _Atlas.Nodes.Add(node);
        }
        return textures;
    }

    private MagickImage CreateAtlasImage(Atlas _Atlas)
    {
        MagickImage img = new(MagickColors.Transparent, (uint)_Atlas.Width, (uint)_Atlas.Height);

        foreach (Node n in _Atlas.Nodes)
        {
            if (n.Texture is not null)
            {
                MagickImage sourceImg = n.Texture.Image;
                using IMagickImage<byte> resizedSourceImg = TextureWorker.ResizeImage(sourceImg, n.Bounds.Width, n.Bounds.Height);
                img.Composite(resizedSourceImg, n.Bounds.X, n.Bounds.Y, CompositeOperator.Copy);
            }
        }

        return img;
    }
}

public class MoonwarmerMetadata
{
    public string name { get; set; }
    public string version { get; set; }
    public string packageID { get; set; }
}

public class MoonwarmerJson
{
    public MoonwarmerMetadata metadata { get; set; }

    public string[]? supportedPackageTypes { get; set; }

    public string? deltaruneVersion { get; set; }

    public string[]? deltaruneVariants { get; set; }
}


public static SpriteType GetSpriteType(string path)
{
    string folderPath = Path.GetDirectoryName(path);
    string folderName = new DirectoryInfo(folderPath).Name;
    string lowerName = folderName.ToLower();

    if (lowerName == "backgrounds" || lowerName == "background")
    {
        return SpriteType.Background;
    }
    else if (lowerName == "fonts" || lowerName == "font")
    {
        return SpriteType.Font;
    }
    else if (lowerName == "sprites" || lowerName == "sprite")
    {
        return SpriteType.Sprite;
    }
    return SpriteType.Unknown;
}

string CheckValidity(string importFolder)
{
    importAsSprite = false;
    if (importFolder == null)
        throw new ScriptException("The import folder was not set.");

    //Stop the script if there's missing sprite entries or w/e.
    bool hadMessage = false;
    string currSpriteName = null;
    string[] dirFiles = Directory.GetFiles(importFolder, "*.png", SearchOption.AllDirectories);
    foreach (string file in dirFiles)
    {
        string FileNameWithExtension = Path.GetFileName(file);
        string stripped = Path.GetFileNameWithoutExtension(file);
        string spriteName = "";

        SpriteType spriteType = GetSpriteType(file);

        if ((spriteType != SpriteType.Sprite) && (spriteType != SpriteType.Background))
        {
            if (!hadMessage)
            {
                hadMessage = true;
                importAsSprite = true;
            }

            if (!importAsSprite)
            {
                continue;
            }
            else
            {
                spriteType = SpriteType.Sprite;
            }
        }

        // Check for duplicate filenames
        string[] dupFiles = Directory.GetFiles(importFolder, FileNameWithExtension, SearchOption.AllDirectories);
        if (dupFiles.Length > 1)
            throw new ScriptException("Duplicate file detected. There are " + dupFiles.Length + " files named: " + FileNameWithExtension);

        // Sprites can have multiple frames! Do some sprite-specific checking.
        if (spriteType == SpriteType.Sprite)
        {
            var spriteParts = sprFrameRegex.Match(stripped);
            // Allow sprites without underscores
            if (!spriteParts.Groups[2].Success)
                continue;

            spriteName = spriteParts.Groups[1].Value;

            if (!Int32.TryParse(spriteParts.Groups[2].Value, out int frame))
                throw new ScriptException($"{spriteName} has an invalid frame index.");
            if (frame < 0)
                throw new ScriptException($"{spriteName} is using an invalid numbering scheme. The script has stopped for your own protection.");

            // If it's not a first frame of the sprite
            if (spriteName == currSpriteName)
                continue;

            string[][] spriteFrames = Directory.GetFiles(importFolder, $"{spriteName}_*.png", SearchOption.AllDirectories)
                                               .Select(x =>
                                               {
                                                   var match = sprFrameRegex.Match(Path.GetFileNameWithoutExtension(x));
                                                   if (match.Groups[2].Success)
                                                       return new string[] { match.Groups[1].Value, match.Groups[2].Value };
                                                   else
                                                       return null;
                                               })
                                               .OfType<string[]>().ToArray();
            if (spriteFrames.Length == 1)
            {
                currSpriteName = null;
                continue;
            }

            int[] frameIndexes = spriteFrames.Select(x =>
            {
                if (Int32.TryParse(x[1], out int frame))
                    return (int?)frame;
                else
                    return null;
            }).OfType<int?>().Cast<int>().OrderBy(x => x).ToArray();
            if (frameIndexes.Length == 1)
            {
                currSpriteName = null;
                continue;
            }

            for (int i = 0; i < frameIndexes.Length - 1; i++)
            {
                int num = frameIndexes[i];
                int nextNum = frameIndexes[i + 1];

                if (nextNum - num > 1)
                    throw new ScriptException(spriteName + " is missing one or more indexes.\nThe detected missing index is: " + (num + 1));
            }

            currSpriteName = spriteName;
        }
    }
    return importFolder;
}

// -- ImportShaders.csx --

void ImportShaders(string importFolder)
{
    var shadersToModify = Directory.GetDirectories(importFolder).Select(x => Path.GetFileName(x));
    List<string> shadersExisting = new List<string>();
    List<string> shadersNonExist = new List<string>();
    List<string> currentList = new List<string>();
    string res = "";

    foreach (string shaderName in shadersToModify)
    {
        currentList.Clear();
        for (int j = 0; j < Data.Shaders.Count; j++)
        {
            string x = Data.Shaders[j].Name.Content;
            res += x + "\n";
            currentList.Add(x);
        }

        IncrementProgress();
        if (Data.Shaders.ByName(shaderName) != null)
        {
            Data.Shaders.Remove(Data.Shaders.ByName(shaderName));
            AddShader(shaderName, importFolder);
            Reorganize<UndertaleShader>(Data.Shaders, currentList);
        }
        else
            AddShader(shaderName, importFolder);
    }
}

void AddShader(string shader_name, string importFolder)
{
    UndertaleShader new_shader = new UndertaleShader();
    new_shader.Name = Data.Strings.MakeString(shader_name);
    string localImportDir = importFolder + "/" + shader_name + "/";
    if (File.Exists(localImportDir + "Type.txt"))
    {
        string shader_type = File.ReadAllText(localImportDir + "Type.txt");
        if (shader_type.Contains("GLSL_ES"))
            new_shader.Type = UndertaleShader.ShaderType.GLSL_ES;
        else if (shader_type.Contains("GLSL"))
            new_shader.Type = UndertaleShader.ShaderType.GLSL;
        else if (shader_type.Contains("HLSL9"))
            new_shader.Type = UndertaleShader.ShaderType.HLSL9;
        else if (shader_type.Contains("HLSL11"))
            new_shader.Type = UndertaleShader.ShaderType.HLSL11;
        else if (shader_type.Contains("PSSL"))
            new_shader.Type = UndertaleShader.ShaderType.PSSL;
        else if (shader_type.Contains("Cg_PSVita"))
            new_shader.Type = UndertaleShader.ShaderType.Cg_PSVita;
        else if (shader_type.Contains("Cg_PS3"))
            new_shader.Type = UndertaleShader.ShaderType.Cg_PS3;
        else
            new_shader.Type = UndertaleShader.ShaderType.GLSL_ES;
    }
    else
        new_shader.Type = UndertaleShader.ShaderType.GLSL_ES;
    if (File.Exists(localImportDir + "GLSL_ES_Fragment.txt"))
        new_shader.GLSL_ES_Fragment = Data.Strings.MakeString(File.ReadAllText(localImportDir + "GLSL_ES_Fragment.txt"));
    else
        new_shader.GLSL_ES_Fragment = Data.Strings.MakeString("");
    if (File.Exists(localImportDir + "GLSL_ES_Vertex.txt"))
        new_shader.GLSL_ES_Vertex = Data.Strings.MakeString(File.ReadAllText(localImportDir + "GLSL_ES_Vertex.txt"));
    else
        new_shader.GLSL_ES_Vertex = Data.Strings.MakeString("");
    if (File.Exists(localImportDir + "GLSL_Fragment.txt"))
        new_shader.GLSL_Fragment = Data.Strings.MakeString(File.ReadAllText(localImportDir + "GLSL_Fragment.txt"));
    else
        new_shader.GLSL_Fragment = Data.Strings.MakeString("");
    if (File.Exists(localImportDir + "GLSL_Vertex.txt"))
        new_shader.GLSL_Vertex = Data.Strings.MakeString(File.ReadAllText(localImportDir + "GLSL_Vertex.txt"));
    else
        new_shader.GLSL_Vertex = Data.Strings.MakeString("");
    if (File.Exists(localImportDir + "HLSL9_Fragment.txt"))
        new_shader.HLSL9_Fragment = Data.Strings.MakeString(File.ReadAllText(localImportDir + "HLSL9_Fragment.txt"));
    else
        new_shader.HLSL9_Fragment = Data.Strings.MakeString("");
    if (File.Exists(localImportDir + "HLSL9_Vertex.txt"))
        new_shader.HLSL9_Vertex = Data.Strings.MakeString(File.ReadAllText(localImportDir + "HLSL9_Vertex.txt"));
    else
        new_shader.HLSL9_Vertex = Data.Strings.MakeString("");
    if (File.Exists(localImportDir + "HLSL11_VertexData.bin"))
    {
        new_shader.HLSL11_VertexData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.HLSL11_VertexData.Data = File.ReadAllBytes(localImportDir + "HLSL11_VertexData.bin");
        new_shader.HLSL11_VertexData.IsNull = false;
    }
    if (File.Exists(localImportDir + "HLSL11_PixelData.bin"))
    {
        new_shader.HLSL11_PixelData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.HLSL11_PixelData.IsNull = false;
        new_shader.HLSL11_PixelData.Data = File.ReadAllBytes(localImportDir + "HLSL11_PixelData.bin");
    }
    if (File.Exists(localImportDir + "PSSL_VertexData.bin"))
    {
        new_shader.PSSL_VertexData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.PSSL_VertexData.IsNull = false;
        new_shader.PSSL_VertexData.Data = File.ReadAllBytes(localImportDir + "PSSL_VertexData.bin");
    }
    if (File.Exists(localImportDir + "PSSL_PixelData.bin"))
    {
        new_shader.PSSL_PixelData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.PSSL_PixelData.IsNull = false;
        new_shader.PSSL_PixelData.Data = File.ReadAllBytes(localImportDir + "PSSL_PixelData.bin");
    }
    if (File.Exists(localImportDir + "Cg_PSVita_VertexData.bin"))
    {
        new_shader.Cg_PSVita_VertexData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.Cg_PSVita_VertexData.IsNull = false;
        new_shader.Cg_PSVita_VertexData.Data = File.ReadAllBytes(localImportDir + "Cg_PSVita_VertexData.bin");
    }
    if (File.Exists(localImportDir + "Cg_PSVita_PixelData.bin"))
    {
        new_shader.Cg_PSVita_PixelData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.Cg_PSVita_PixelData.IsNull = false;
        new_shader.Cg_PSVita_PixelData.Data = File.ReadAllBytes(localImportDir + "Cg_PSVita_PixelData.bin");
    }
    if (File.Exists(localImportDir + "Cg_PS3_VertexData.bin"))
    {
        new_shader.Cg_PS3_VertexData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.Cg_PS3_VertexData.IsNull = false;
        new_shader.Cg_PS3_VertexData.Data = File.ReadAllBytes(localImportDir + "Cg_PS3_VertexData.bin");
    }
    if (File.Exists(localImportDir + "Cg_PS3_PixelData.bin"))
    {
        new_shader.Cg_PS3_PixelData = new UndertaleShader.UndertaleRawShaderData();
        new_shader.Cg_PS3_PixelData.IsNull = false;
        new_shader.Cg_PS3_PixelData.Data = File.ReadAllBytes(localImportDir + "Cg_PS3_PixelData.bin");
    }
    if (File.Exists(localImportDir + "VertexShaderAttributes.txt"))
    {
        string line;
        // Read the file and display it line by line.
        StreamReader file = new StreamReader(localImportDir + "VertexShaderAttributes.txt");
        while((line = file.ReadLine()) != null)
        {
            line = line.Trim();
            if (line != "")
            {
                UndertaleShader.VertexShaderAttribute vertex_x = new UndertaleShader.VertexShaderAttribute();
                vertex_x.Name = Data.Strings.MakeString(line);
                new_shader.VertexShaderAttributes.Add(vertex_x);
            }
        }
        file.Close();
    }
    Data.Shaders.Add(new_shader);
}

void Reorganize<T>(IList<T> list, List<string> order) where T : UndertaleNamedResource, new()
{
    Dictionary<string, T> temp = new Dictionary<string, T>();
    for (int i = 0; i < list.Count; i++)
    {
        T asset = list[i];
        string assetName = asset.Name?.Content;
        if (order.Contains(assetName))
        {
            temp[assetName] = asset;
        }
    }

    List<T> addOrder = new List<T>();
    for (int i = order.Count - 1; i >= 0; i--)
    {
        T asset;
        try
        {
            asset = temp[order[i]];
        }
        catch (Exception e)
        {
            throw new ScriptException("Missing asset with name \"" + order[i] + "\"");
        }
        addOrder.Add(asset);
    }

    foreach (T asset in addOrder)
        list.Remove(asset);
    foreach (T asset in addOrder)
        list.Insert(0, asset);
}

// -- ImportSingleSound.csx -- 
void ImportSingleSound(string filePath)
{

    UndertaleEmbeddedAudio audioFile = null;
    int audioID = -1;
    int audioGroupID = -1;
    int embAudioID = -1;
    bool usesAGRP = Data.AudioGroups.Count > 0;

    string soundPath = filePath;
    if (string.IsNullOrEmpty(soundPath))
        return;

    // Determine basic sound name properties.
    string filename = Path.GetFileName(soundPath);
    string soundName = Path.GetFileNameWithoutExtension(soundPath);
    bool isOGG = Path.GetExtension(soundPath).ToLower() == ".ogg";
    bool embedSound = false;
    bool decodeLoad = false;
    if (isOGG)
    {
        embedSound = false;
        decodeLoad = false;
        if (embedSound)
            // yes i think??
            decodeLoad = true; //ScriptQuestion("Do you want to Uncompress this sound on load? (Higher Memory, low CPU)");
    }
    else
    {
        // How can a .wav be external?
        embedSound = true;
        decodeLoad = false;
    }
    string audioGroupName = "";
    string folderName = Path.GetFileName(Path.GetDirectoryName(soundPath));
    bool needAGRP = false;

    // Search for an existing sound with the given name.
    UndertaleSound existingSound = null;
    bool replaceSoundPropertiesCheck = false;
    for (int i = 0; i < Data.Sounds.Count; i++)
    {
        if (Data.Sounds[i]?.Name?.Content == soundName)
        {
            existingSound = Data.Sounds[i];
            replaceSoundPropertiesCheck = false; //ScriptQuestion($"Sound \"{existingSound.Name.Content}\" already exists in the game; it will be replaced instead of added. Would you like to replace the sound properties as well?");
            break;
        }
    }

    // Try to find an audiogroup, when not updating an existing sound.
    if (embedSound && usesAGRP && existingSound is null)
        needAGRP = false; // ScriptQuestion($"Your last folder name is \"{folderName}\".\nDo you want to treat it as the name of the sound's audiogroup?\n(Answer No to use \"audiogroup_default\" instead)");

    if (needAGRP && usesAGRP && embedSound)
    {
        audioGroupName = folderName;

        // Find the audio group we need.
        for (int i = 0; i < Data.AudioGroups.Count; i++)
        {
            if (Data.AudioGroups[i]?.Name?.Content == audioGroupName)
            {
                audioGroupID = i;
                break;
            }
        }
        if (audioGroupID == -1)
        {
            // Still -1? Create a new one...
            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(FilePath), $"audiogroup{Data.AudioGroups.Count}.dat"), Convert.FromBase64String("Rk9STQwAAABBVURPBAAAAAAAAAA="));
            UndertaleAudioGroup newAudioGroup = new()
            {
                Name = Data.Strings.MakeString(audioGroupName),
            };
            Data.AudioGroups.Add(newAudioGroup);
        }
    }

    // If this is an existing sound, use its audio group ID.
    if (existingSound is not null)
        audioGroupID = existingSound.GroupID;

    // If the audiogroup ID is for the builtin audiogroup ID, it's embedded in the main data file and doesn't need to be loaded.
    if (audioGroupID == Data.GetBuiltinSoundGroupID())
        needAGRP = false;

    // Create embedded audio entry if required.
    UndertaleEmbeddedAudio soundData = null;
    if ((embedSound && !needAGRP) || needAGRP)
    {
        soundData = new UndertaleEmbeddedAudio() { Data = File.ReadAllBytes(soundPath) };
        Data.EmbeddedAudio.Add(soundData);
        if (existingSound is not null)
        {
            Data.EmbeddedAudio.Remove(existingSound.AudioFile);
        }
        embAudioID = Data.EmbeddedAudio.Count - 1;
    }

    // Update external audio group file if required.
    if (needAGRP)
    {
        // Load audiogroup into memory.
        UndertaleData audioGroupDat;
        string relativeAudioGroupPath;
        if (audioGroupID < Data.AudioGroups.Count && Data.AudioGroups[audioGroupID] is UndertaleAudioGroup { Path.Content: string customRelativePath })
            relativeAudioGroupPath = customRelativePath;
        else
            relativeAudioGroupPath = $"audiogroup{audioGroupID}.dat";
        string audioGroupPath = Path.Combine(Path.GetDirectoryName(FilePath), relativeAudioGroupPath);
        using (FileStream audioGroupReadStream = new(audioGroupPath, FileMode.Open, FileAccess.Read))
        {
            audioGroupDat = UndertaleIO.Read(audioGroupReadStream);
        }

        // Add the EmbeddedAudio entry to the audiogroup data.
        audioGroupDat.EmbeddedAudio.Add(soundData);
        if (existingSound is not null)
            audioGroupDat.EmbeddedAudio.Remove(existingSound.AudioFile);
        audioID = audioGroupDat.EmbeddedAudio.Count - 1;

        // Write audio group back to disk.
        using FileStream audioGroupWriteStream = new(audioGroupPath, FileMode.Create);
        UndertaleIO.Write(audioGroupWriteStream, audioGroupDat);
    }

    // Determine sound flags.
    UndertaleSound.AudioEntryFlags flags = UndertaleSound.AudioEntryFlags.Regular;
    if (isOGG && embedSound && decodeLoad)
        // OGG, embed, decode on load.
        flags = UndertaleSound.AudioEntryFlags.IsEmbedded | UndertaleSound.AudioEntryFlags.IsCompressed | UndertaleSound.AudioEntryFlags.Regular;

    if (isOGG && embedSound && !decodeLoad)
        // OGG, embed, not decode on load.
        flags = UndertaleSound.AudioEntryFlags.IsCompressed | UndertaleSound.AudioEntryFlags.Regular;

    if (!isOGG)
        // WAV, always embed.
        flags = UndertaleSound.AudioEntryFlags.IsEmbedded | UndertaleSound.AudioEntryFlags.Regular;

    if (isOGG && !embedSound)
    {
        // OGG, external.
        flags = UndertaleSound.AudioEntryFlags.Regular;
        audioID = -1;
    }

    // Determine final embedded audio reference (or null).
    UndertaleEmbeddedAudio finalAudioReference = null;
    if (!embedSound)
        finalAudioReference = null;
    if (embedSound && !needAGRP)
        finalAudioReference = Data.EmbeddedAudio[embAudioID];
    if (embedSound && needAGRP)
        finalAudioReference = null;

    // Determine final audio group reference (or null).
    UndertaleAudioGroup finalGroupReference = null;
    if (!usesAGRP)
        finalGroupReference = null;
    else
        finalGroupReference = needAGRP ? Data.AudioGroups[audioGroupID] : Data.AudioGroups[Data.GetBuiltinSoundGroupID()];

    // Update/create actual sound asset.
    if (existingSound is null)
    {
        UndertaleSound newSound = new()
        {
            Name = Data.Strings.MakeString(soundName),
            Flags = flags,
            Type = isOGG ? Data.Strings.MakeString(".ogg") : Data.Strings.MakeString(".wav"),
            File = Data.Strings.MakeString(filename),
            Effects = 0,
            Volume = 1.0f,
            Pitch = 1.0f,
            AudioID = audioID,
            AudioFile = finalAudioReference,
            AudioGroup = finalGroupReference,
            GroupID = needAGRP ? audioGroupID : Data.GetBuiltinSoundGroupID()
        };
        Data.Sounds.Add(newSound);
        //ChangeSelection(newSound);
    }
    else if (replaceSoundPropertiesCheck)
    {
        existingSound.Flags = flags;
        existingSound.Type = isOGG ? Data.Strings.MakeString(".ogg") : Data.Strings.MakeString(".wav");
        existingSound.File = Data.Strings.MakeString(filename);
        existingSound.Effects = 0;
        existingSound.Volume = 1.0f;
        existingSound.Pitch = 1.0f;
        existingSound.AudioID = audioID;
        existingSound.AudioFile = finalAudioReference;
        existingSound.AudioGroup = finalGroupReference;
        existingSound.GroupID = needAGRP ? audioGroupID : Data.GetBuiltinSoundGroupID();
        //ChangeSelection(existingSound);
    }
    else
    {
        existingSound.AudioFile = finalAudioReference;
        existingSound.AudioID = audioID;
        //ChangeSelection(existingSound);
    }
}

CompileScriptKind GuessScriptKindFromName(string? codeName)
{
    // If null, just assume script
    if (codeName is null)
        return CompileScriptKind.Script;

    // Compare prefixes against known ones
    const string globalScriptPrefix = "gml_GlobalScript_";
    if (codeName.StartsWith(globalScriptPrefix, StringComparison.Ordinal))
        // Output global script name as well
        return CompileScriptKind.GlobalScript;

    if (codeName.StartsWith("gml_Script", StringComparison.Ordinal))
        return CompileScriptKind.Script;

    if (codeName.StartsWith("gml_Object", StringComparison.Ordinal))
        return CompileScriptKind.ObjectEvent;

    if (codeName.StartsWith("gml_Room", StringComparison.Ordinal))
        return CompileScriptKind.RoomCreationCode;

    if (codeName.StartsWith("Timeline", StringComparison.Ordinal))
        return CompileScriptKind.Timeline;

    // Unknown; default to script
    return CompileScriptKind.Script;
}