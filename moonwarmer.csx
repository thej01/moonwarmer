// Based of ImportGraphics.csx, ImportGML.csx, ImportShaders.gml, ImportSingleSound.gml
// MOONWARMER -- a deltarune mass-import script.

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
using static UndertaleModLib.Models.UndertaleSound;
using static UndertaleModLib.UndertaleData;
using ImageMagick;

EnsureDataLoaded();

// -- configuration -- 

bool packaged_moonwarmer = false;

string moonwarmer_version = "v0";

// -- loading project --

string? project_folder = Path.GetDirectoryName(ScriptPath) + "/";
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
    throw new ScriptException("_moonwarmer.json is invalid. Please make sure that your json follows proper syntax and has all required fields.");

if (loaded_json.supportedPackageTypes is null)
    loaded_json.supportedPackageTypes = new String[0];

// if enabled, it just auto detects the chapter without confirmation (which should work fine, but just in case you can disable this)
// if disabled, it will prompt the user to input the chapter number (with the autodetect value being the default)
// also setting it like this kinda dumb but whatever
bool autodetect_chapter = loaded_json.supportedPackageTypes.Length > 0;

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
    if (txt == null)
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
string scriptDir = Path.GetDirectoryName(ScriptPath);
string srcDirectory = project_folder;
string[] projectDirectories = new String[2];
projectDirectories[0] = srcDirectory + "_everychapter";
projectDirectories[1] = srcDirectory + "chapter" + chapter.ToString();

if (!Directory.Exists(srcDirectory))
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

int totalSpriteImages = 0;
foreach (string dir in spriteDirectories)
    totalSpriteImages += Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories).Length;
int totalShaderDirs = 0;
foreach (string dir in shaderDirectories)
    totalShaderDirs += Directory.GetDirectories(dir).Length;

SetProgressBar("Importing project files", "Sprite Images", 0, totalSpriteImages + codeFiles.Count + totalShaderDirs + audioFiles.Count);

// Import Graphics
StartProgressBarUpdater();
if (totalSpriteImages > 0)
{
    SyncBinding("Sprites, Backgrounds, Fonts, EmbeddedTextures, TexturePageItems, Strings", true);
    await Task.Run(() =>
    {
        foreach (string dir in spriteDirectories)
            ImportGraphics(dir, Data);
    });
    DisableAllSyncBindings();
}

// Import Audio
if (audioFiles.Count > 0)
{
    UpdateProgressStatus("Audio");
    SyncBinding("Strings, Sounds, EmbeddedAudio, AudioGroups", true);
    await Task.Run(() =>
    {
        foreach (string file in audioFiles)
            ImportSingleSound(file);
    });
    DisableAllSyncBindings();
}

// Import Shaders
if (totalShaderDirs > 0)
{
    UpdateProgressStatus("Shaders");
    SyncBinding("Strings, Shaders", true);
    await Task.Run(() =>
    {
        foreach (string dir in shaderDirectories)
            ImportShaders(dir);
    });
    DisableAllSyncBindings();
}

// Import GML
if (codeFiles.Count > 0)
{
    UpdateProgressStatus("Code");

    SyncBinding("Strings, Code, CodeLocals, Scripts, GlobalInitScripts, GameObjects, Functions, Variables", true);
    await Task.Run(() =>
    {
        UndertaleModLib.Compiler.CodeImportGroup importGroup = new(Data)
        {
            AutoCreateAssets = true
        };
        foreach (string file in codeFiles)
        {
            IncrementProgress();

            string code = File.ReadAllText(file);
            string codeName = Path.GetFileNameWithoutExtension(file);
            importGroup.QueueReplace(codeName, code);
        }
        UpdateProgressStatus("Finishing import...");
        importGroup.Import();
    });
    DisableAllSyncBindings();
}

await StopProgressBarUpdater();
HideProgressBar();
ScriptMessage("Project " + subProjName + " successfully imported.");

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