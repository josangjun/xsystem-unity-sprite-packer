using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteAtlasManifest))]
public class SpriteAtlasPacker : Editor
{
    private SpriteAtlasManifest atlas;
    private SerializedProperty atlasTextureProp;
    private SerializedProperty trimTransparentPixelsProp;
    private SerializedProperty allowRotationProp;
    private SerializedProperty entriesProp;

    private Vector2 scroll;
    private int padding = 2;
    private int atlasMaxSize = 4096;
    private string entrySearch = string.Empty;
    private int spritePickerControlId = -1;
    private bool waitingForSpritePicker = false;

    private void OnEnable()
    {
        atlas = (SpriteAtlasManifest)target;
        atlasTextureProp = serializedObject.FindProperty("atlasTexture");
        trimTransparentPixelsProp = serializedObject.FindProperty("_trimTransparentPixels");
        allowRotationProp = serializedObject.FindProperty("_allowRotation");
        entriesProp = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (waitingForSpritePicker &&
            Event.current.commandName == "ObjectSelectorClosed" &&
            EditorGUIUtility.GetObjectPickerControlID() == spritePickerControlId)
        {
            var picked = EditorGUIUtility.GetObjectPickerObject() as Sprite;
            if (picked != null)
            {
                AddSpriteObjects(new Object[] { picked });
            }
            waitingForSpritePicker = false;
        }

        EditorGUILayout.PropertyField(atlasTextureProp);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Atlas Settings", EditorStyles.boldLabel);
        padding = Mathf.Max(0, EditorGUILayout.IntField("Padding", padding));
        atlasMaxSize = Mathf.Max(128, EditorGUILayout.IntPopup(
            "Max Size",
            atlasMaxSize,
            new[] { "256", "512", "1024", "2048", "4096", "8192" },
            new[] { 256, 512, 1024, 2048, 4096, 8192 }));
        EditorGUILayout.PropertyField(trimTransparentPixelsProp, new GUIContent("Trim Transparent Pixels"));
        EditorGUILayout.PropertyField(allowRotationProp, new GUIContent("Allow Rotation"));
        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(10);
        if (GUILayout.Button("Add Selected Sprites"))
        {
            AddSelectedSprites();
        }

        if (GUILayout.Button("Refresh Pivot/Border from Source Sprites"))
        {
            RefreshSpriteMeta();
        }
        
        if (GUILayout.Button("Sort Entries By Name"))
        {
            SortEntriesByName();
        }
    
        if (GUILayout.Button("Rebuild Atlas"))
        {
            RebuildAtlas();
        }

        if (GUILayout.Button("Extract All Sprites"))
        {
            ExtractAllSprites();
        }

        GUILayout.Space(20);
        EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);
        entrySearch = EditorGUILayout.TextField("Search", entrySearch);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        for (int i = 0; i < atlas.entries.Count; i++)
        {
            var e = atlas.entries[i];
            if (!string.IsNullOrEmpty(entrySearch) && e.spriteName.IndexOf(entrySearch, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(e.spriteName, EditorStyles.boldLabel);
            Sprite newSourceSprite = EditorGUILayout.ObjectField("Source Sprite", e.sourceSprite, typeof(Sprite), false) as Sprite;
            if (newSourceSprite != null && newSourceSprite != e.sourceSprite)
            {
                UpdateEntrySprite(e, newSourceSprite);
                EditorUtility.SetDirty(atlas);
            }
            int newScalePercent = EditorGUILayout.IntSlider("Source Scale (%)", e.sourceScalePercent, 1, 100);
            if (e.sourceScalePercent != newScalePercent)
            {
                e.sourceScalePercent = newScalePercent;
                EditorUtility.SetDirty(atlas);
            }
            EditorGUILayout.LabelField("Source Rect", e.sourceRect.ToString());
            EditorGUILayout.LabelField("Atlas Rect", e.atlasRect.ToString());
            if (e.HasPackedFrame)
            {
                EditorGUILayout.LabelField("Packed Frame Size", e.packedFrameSize.ToString());
                EditorGUILayout.LabelField("Packed Content Rect", e.packedContentRect.ToString());
                EditorGUILayout.LabelField("Packed Rotation", e.packedRotated ? "90° Clockwise" : "None");
            }
            EditorGUILayout.LabelField("Pivot", e.pivot.ToString());
            EditorGUILayout.LabelField("Border", e.border.ToString());

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Extract"))
            {
                ExtractSprite(e);
            }
            if (GUILayout.Button("Remove"))
            {
                atlas.entries.RemoveAt(i);
                EditorUtility.SetDirty(atlas);
                break;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
    }

    private void AddSelectedSprites()
    {
        Object[] selected = ResolveSelectedSprites();
        if (selected == null || selected.Length == 0)
        {
            spritePickerControlId = EditorGUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<Sprite>(null, false, "t:sprite", spritePickerControlId);
            waitingForSpritePicker = true;
            return;
        }

        AddSpriteObjects(selected);
    }

    private static Object[] ResolveSelectedSprites()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
        if (selectedObjects == null || selectedObjects.Length == 0)
            return new Object[0];

        List<Object> sprites = new List<Object>();
        HashSet<string> seenKeys = new HashSet<string>();

        foreach (Object selectedObject in selectedObjects)
        {
            if (selectedObject is Sprite sprite)
            {
                AddResolvedSprite(sprites, seenKeys, sprite);
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(assetPath))
                continue;

            foreach (Sprite assetSprite in LoadSpritesAtPath(assetPath))
            {
                AddResolvedSprite(sprites, seenKeys, assetSprite);
            }
        }

        return sprites.ToArray();
    }

    private static IEnumerable<Sprite> LoadSpritesAtPath(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null || importer.textureType != TextureImporterType.Sprite)
            yield break;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is Sprite sprite)
                yield return sprite;
        }
    }

    private static void AddResolvedSprite(List<Object> sprites, HashSet<string> seenKeys, Sprite sprite)
    {
        string assetPath = AssetDatabase.GetAssetPath(sprite);
        string key = AssetDatabase.AssetPathToGUID(assetPath) + ":" + sprite.name;
        if (!seenKeys.Add(key))
            return;

        sprites.Add(sprite);
    }

    private void AddSpriteObjects(Object[] selected)
    {
        foreach (Object o in selected)
        {
            var sourceSprite = o as Sprite;
            if (sourceSprite == null)
                continue;

            string path = AssetDatabase.GetAssetPath(sourceSprite);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
                continue;

            if (importer.textureType != TextureImporterType.Sprite)
                continue;

            EnsureReadable(importer);

            atlas.entries.Add(new AtlasSpriteEntry
            {
                spriteName = sourceSprite.name,
                sourceSprite = sourceSprite,
                sourceSpriteName = sourceSprite.name,
                sourceRect = sourceSprite.rect,
                pivot = sourceSprite.pivot / sourceSprite.rect.size,
                border = sourceSprite.border
            });
        }

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
    }

    private void UpdateEntrySprite(AtlasSpriteEntry entry, Sprite sourceSprite)
    {
        string path = AssetDatabase.GetAssetPath(sourceSprite);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null || importer.textureType != TextureImporterType.Sprite)
            return;

        EnsureReadable(importer);
        entry.sourceSprite = sourceSprite;
        entry.spriteName = sourceSprite.name;
        entry.sourceSpriteName = sourceSprite.name;
        entry.sourceRect = sourceSprite.rect;
        entry.pivot = sourceSprite.pivot / sourceSprite.rect.size;
        entry.border = sourceSprite.border;
    }

    private void RefreshSpriteMeta()
    {
        bool changed = false;

        for (int i = 0; i < atlas.entries.Count; i++)
        {
            var e = atlas.entries[i];

            if (e.SourceTexture == null)
                continue;

            string path = AssetDatabase.GetAssetPath(e.SourceTexture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null || importer.textureType != TextureImporterType.Sprite)
                continue;

            if (importer.spriteImportMode == SpriteImportMode.Single)
            {
                if (e.pivot != importer.spritePivot || e.border != importer.spriteBorder)
                {
                    e.pivot = importer.spritePivot;
                    e.border = importer.spriteBorder;
                    changed = true;
                }
            }
            else
            {
#pragma warning disable CS0618
                foreach (var s in importer.spritesheet)
                {
                    if (s.name == e.spriteName || s.rect == e.sourceRect)
                    {
                        if (e.pivot != s.pivot || e.border != s.border || e.sourceRect != s.rect)
                        {
                            e.pivot = s.pivot;
                            e.border = s.border;
                            e.sourceRect = s.rect;
                            changed = true;
                        }
                        break;
                    }
                }
#pragma warning restore CS0618
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            Debug.Log("Sprite metadata refreshed from source importers.");
        }
        else
        {
            Debug.Log("No sprite metadata changes were detected.");
        }
    }

    private void SortEntriesByName()
    {
        atlas.entries = atlas.entries
            .GroupBy(e => e != null ? e.spriteName : string.Empty)
            .Select(g => g.First())
            .OrderBy(e => e != null ? e.spriteName : string.Empty, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
    }

    private void RebuildAtlas()
    {
        string pngPath = GetAtlasOutputPath();
        int atlasSize = atlasMaxSize;

        Texture2D atlasTex = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
        List<Texture2D> pieces = new List<Texture2D>();
        HashSet<string> entryKeys = new HashSet<string>();
        
        for (var i = 0; i < atlas.entries.Count; ++i)
        {
            var e = atlas.entries[i];
            
            if (e.SourceTexture == null)
            {
                Debug.LogWarning($"Entry {e.spriteName} has no source texture. Skipping.");
                atlas.entries.RemoveAt(i);
                --i;
                continue;
            }
            string entryKey = $"{e.guid}:{e.sourceRect.x}:{e.sourceRect.y}:{e.sourceRect.width}:{e.sourceRect.height}";
            if (entryKeys.Contains(entryKey))
            {
                Debug.LogWarning($"Entry {e.spriteName} has duplicate source sprite. Skipping.");
                atlas.entries.RemoveAt(i);
                --i;
                continue;
            }
            entryKeys.Add(entryKey);

            Texture2D readable = GetReadableTexture(e.SourceTexture);
            Rect r = e.sourceRect;
            float scale = Mathf.Max(0.01f, e.sourceScalePercent / 100f);
            int scaledWidth = Mathf.Max(1, Mathf.RoundToInt(r.width * scale));
            int scaledHeight = Mathf.Max(1, Mathf.RoundToInt(r.height * scale));
            Texture2D piece = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            Color[] pixels = readable.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            piece.SetPixels(pixels);
            piece.Apply();
            if (piece.width != scaledWidth || piece.height != scaledHeight)
            {
                Texture2D scaledPiece = SpriteAtlasFrameUtility.ScaleTexture(piece, scaledWidth, scaledHeight);
                Object.DestroyImmediate(piece);
                piece = scaledPiece;
            }
            Texture2D packedPiece = SpriteAtlasFrameUtility.PrepareFrame(piece, e, atlas.TrimTransparentPixels);
            Object.DestroyImmediate(piece);
            Object.DestroyImmediate(readable);
            pieces.Add(packedPiece);
        }

        if (atlas.AllowRotation)
        {
            Object.DestroyImmediate(atlasTex);
            atlasTex = SpriteAtlasRotationPacker.Pack(pieces, atlas.entries, padding, atlasSize);
        }
        else
        {
            Rect[] packed = atlasTex.PackTextures(pieces.ToArray(), padding, atlasSize);
            for (int i = 0; i < packed.Length; i++)
            {
                atlas.entries[i].atlasRect = ToPixelRectClamped(packed[i], atlasTex.width, atlasTex.height);
            }
        }

        byte[] png = atlasTex.EncodeToPNG();

        // Clear cached sprite refs so rebuilt sub-assets are re-bound cleanly.
        foreach (var e in atlas.entries)
        {
            e.sprite = null;
        }

        // If atlas already exists, clear old sprite metadata first to avoid stale sub-sprite leftovers.
        if (File.Exists(pngPath))
        {
            var existingImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (existingImporter != null)
            {
                existingImporter.textureType = TextureImporterType.Sprite;
                existingImporter.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618
                existingImporter.spritesheet = new SpriteMetaData[0];
#pragma warning restore CS0618
                existingImporter.SaveAndReimport();
            }
        }

        File.WriteAllBytes(pngPath, png);
        AssetDatabase.Refresh();

        Texture2D importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.isReadable = true;
        importer.maxTextureSize = atlasMaxSize;
        TextureImporterSettings textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(textureSettings);

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        SpriteAtlasRotationMetadata rotationMetadata = new SpriteAtlasRotationMetadata();
        foreach (var e in atlas.entries)
        {
            Vector2 pivot = SpriteAtlasFrameUtility.GetAtlasPivot(e);
            Vector4 border = SpriteAtlasFrameUtility.GetAtlasBorder(e);
            if (e.packedRotated)
            {
                rotationMetadata.frames.Add(new SpriteAtlasRotationFrame
                {
                    spriteName = e.spriteName,
                    atlasRect = e.atlasRect,
                    physicalRect = true,
                    pivot = pivot,
                    border = border
                });
            }
            metas.Add(new SpriteMetaData
            {
                name = e.spriteName,
                rect = e.atlasRect,
                pivot = e.packedRotated ? new Vector2(pivot.y, 1 - pivot.x) : pivot,
                border = e.packedRotated ? new Vector4(border.y, border.z, border.w, border.x) : border,
                alignment = (int)SpriteAlignment.Custom
            });
        }

#pragma warning disable CS0618
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618
        importer.userData = rotationMetadata.Write(importer.userData);

        EditorUtility.SetDirty(importer);
        importer.isReadable = false;
        importer.SaveAndReimport();

        atlas.atlasTexture = importedAtlas;
        Sprite[] importedSprites = AssetDatabase.LoadAllAssetsAtPath(pngPath).OfType<Sprite>().ToArray();
        foreach (AtlasSpriteEntry entry in atlas.entries)
        {
            entry.sprite = importedSprites.FirstOrDefault(sprite => sprite.name == entry.spriteName);
        }
        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();

        foreach (Texture2D piece in pieces)
        {
            Object.DestroyImmediate(piece);
        }
        Object.DestroyImmediate(atlasTex);

        Debug.Log("Atlas rebuilt");
    }

    private string GetAtlasOutputPath()
    {
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(atlas)).Replace('\\', '/');
        HashSet<string> sourcePaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (AtlasSpriteEntry entry in atlas.entries)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.guid))
            {
                sourcePaths.Add(AssetDatabase.GUIDToAssetPath(entry.guid).Replace('\\', '/'));
            }
        }

        string outputPath = folder + "/" + atlas.name + ".png";
        int suffix = 0;
        while (sourcePaths.Contains(outputPath))
        {
            suffix++;
            string number = suffix == 1 ? string.Empty : suffix.ToString();
            outputPath = folder + "/" + atlas.name + "_Atlas" + number + ".png";
        }

        return outputPath;
    }

    private void ExtractSprite(AtlasSpriteEntry entry)
    {
        if (atlas.atlasTexture == null)
            return;

        Texture2D readable = GetReadableTexture(atlas.atlasTexture);
        byte[] png = EncodeSpriteToPng(readable, entry);
        Object.DestroyImmediate(readable);
        string atlasAssetPath = AssetDatabase.GetAssetPath(atlas);
        string atlasFolder = Path.GetDirectoryName(atlasAssetPath);
        string initialDirectory = Path.IsPathRooted(atlasFolder)
            ? atlasFolder
            : Path.Combine(Directory.GetCurrentDirectory(), atlasFolder);
        string path = EditorUtility.SaveFilePanel("Extract Sprite", initialDirectory, entry.spriteName + ".png", "png");

        if (string.IsNullOrEmpty(path))
            return;

        if (WriteExtractedSprite(path, entry, png))
        {
            Debug.Log("Extracted: " + path);
        }
    }

    private void ExtractAllSprites()
    {
        if (atlas.atlasTexture == null)
            return;

        string atlasAssetPath = AssetDatabase.GetAssetPath(atlas);
        string atlasFolder = Path.GetDirectoryName(atlasAssetPath);
        string initialDirectory = Path.IsPathRooted(atlasFolder)
            ? atlasFolder
            : Path.Combine(Directory.GetCurrentDirectory(), atlasFolder);
        string targetDirectory = EditorUtility.SaveFolderPanel("Extract All Sprites", initialDirectory, atlas.name + "_Extracted");

        if (string.IsNullOrEmpty(targetDirectory))
            return;

        Texture2D readable = GetReadableTexture(atlas.atlasTexture);
        int extractedCount = 0;

        foreach (var entry in atlas.entries)
        {
            if (entry == null)
                continue;

            string fileName = MakeSafeFileName(entry.spriteName) + ".png";
            string outputPath = Path.Combine(targetDirectory, fileName);
            byte[] png = EncodeSpriteToPng(readable, entry);
            if (WriteExtractedSprite(outputPath, entry, png))
            {
                extractedCount++;
            }
        }

        Object.DestroyImmediate(readable);

        AssetDatabase.Refresh();
        Debug.Log($"Extracted {extractedCount}/{atlas.entries.Count} sprites to {targetDirectory}");
    }

    private static byte[] EncodeSpriteToPng(Texture2D sourceTexture, AtlasSpriteEntry entry)
    {
        Texture2D frame = SpriteAtlasFrameUtility.RestoreFrame(sourceTexture, entry);
        try
        {
            return frame.EncodeToPNG();
        }
        finally
        {
            Object.DestroyImmediate(frame);
        }
    }

    private static bool WriteExtractedSprite(string path, AtlasSpriteEntry entry, byte[] png)
    {
        File.WriteAllBytes(path, png);

        string relativePath = FileUtil.GetProjectRelativePath(path.Replace('\\', '/'));
        if (!string.IsNullOrEmpty(relativePath) && relativePath.StartsWith("Assets/"))
        {
            AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                importer.SetTextureSettings(settings);
                importer.spritePivot = entry.HasPackedFrame ? entry.packedFramePivot : entry.pivot;
                importer.spriteBorder = entry.HasPackedFrame ? entry.packedFrameBorder : entry.border;
                importer.SaveAndReimport();
            }

            return true;
        }

        Debug.LogWarning("Extracted file is outside the Unity project. Sprite import settings were not applied.");
        return true;
    }

    private static string MakeSafeFileName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
            return "sprite";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] chars = rawName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (invalidChars.Contains(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static void EnsureReadable(TextureImporter importer)
    {
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D GetReadableTexture(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(source.width, source.height);
        tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }

    private static Rect ToPixelRectClamped(Rect uv, int atlasWidth, int atlasHeight)
    {
        int x = Mathf.RoundToInt(uv.x * atlasWidth);
        int y = Mathf.RoundToInt(uv.y * atlasHeight);
        int width = Mathf.Max(1, Mathf.RoundToInt(uv.xMax * atlasWidth) - x);
        int height = Mathf.Max(1, Mathf.RoundToInt(uv.yMax * atlasHeight) - y);

        x = Mathf.Clamp(x, 0, Mathf.Max(0, atlasWidth - 1));
        y = Mathf.Clamp(y, 0, Mathf.Max(0, atlasHeight - 1));
        width = Mathf.Clamp(width, 1, atlasWidth - x);
        height = Mathf.Clamp(height, 1, atlasHeight - y);

        return new Rect(x, y, width, height);
    }
}
