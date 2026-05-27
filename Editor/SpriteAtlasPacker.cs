using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteAtlasManifest))]
public class SpriteAtlasPacker : Editor
{
    private SpriteAtlasManifest atlas;
    private SerializedProperty atlasTextureProp;
    private SerializedProperty entriesProp;

    private Vector2 scroll;
    private int padding = 2;
    private int atlasMaxSize = 4096;
    private bool allowRotation = false;
    private string entrySearch = string.Empty;
    private int spritePickerControlId = -1;
    private bool waitingForSpritePicker = false;

    private void OnEnable()
    {
        atlas = (SpriteAtlasManifest)target;
        atlasTextureProp = serializedObject.FindProperty("atlasTexture");
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
        allowRotation = EditorGUILayout.Toggle("Allow Rotation", allowRotation);

        GUILayout.Space(10);
        if (GUILayout.Button("Add Selected Sprites"))
        {
            AddSelectedSprites();
        }

        if (GUILayout.Button("Refresh Pivot/Border from Source Sprites"))
        {
            RefreshSpriteMeta();
        }

        if (GUILayout.Button("Rebuild Atlas"))
        {
            RebuildAtlas();
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
        Object[] selected = Selection.GetFiltered(typeof(Sprite), SelectionMode.Assets);
        if (selected == null || selected.Length == 0)
        {
            spritePickerControlId = EditorGUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<Sprite>(null, false, string.Empty, spritePickerControlId);
            waitingForSpritePicker = true;
            return;
        }

        AddSpriteObjects(selected);
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

            if (e.sourceTexture == null)
                continue;

            string path = AssetDatabase.GetAssetPath(e.sourceTexture);
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

    private void RebuildAtlas()
    {
        int atlasSize = atlasMaxSize;

        Texture2D atlasTex = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
        List<Texture2D> pieces = new List<Texture2D>();
        HashSet<string> entryKeys = new HashSet<string>();
        
        for (var i = 0; i < atlas.entries.Count; ++i)
        {
            var e = atlas.entries[i];
            
            if (e.sourceTexture == null)
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

            Texture2D readable = GetReadableTexture(e.sourceTexture);
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
                Texture2D scaledPiece = ScaleTexture(piece, scaledWidth, scaledHeight);
                Object.DestroyImmediate(piece);
                piece = scaledPiece;
            }
            pieces.Add(piece);
        }

        Rect[] packed;
        if (allowRotation)
        {
            var packMethod = typeof(Texture2D).GetMethod(
                "PackTextures",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Texture2D[]), typeof(int), typeof(int), typeof(bool), typeof(bool) },
                null);

            if (packMethod != null)
            {
                packed = (Rect[])packMethod.Invoke(atlasTex, new object[] { pieces.ToArray(), padding, atlasSize, false, true });
            }
            else
            {
                Debug.LogWarning("Allow Rotation is not supported in this Unity version. Packing without rotation.");
                packed = atlasTex.PackTextures(pieces.ToArray(), padding, atlasSize);
            }
        }
        else
        {
            packed = atlasTex.PackTextures(pieces.ToArray(), padding, atlasSize);
        }

        for (int i = 0; i < packed.Length; i++)
        {
            Rect uv = packed[i];
            atlas.entries[i].atlasRect = ToPixelRectClamped(uv, atlasTex.width, atlasTex.height, pieces[i].width, pieces[i].height);
        }

        byte[] png = atlasTex.EncodeToPNG();
        string atlasAssetPath = AssetDatabase.GetAssetPath(atlas);
        string folder = Path.GetDirectoryName(atlasAssetPath);
        string pngPath = folder + "/" + atlas.name + ".png";

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

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        foreach (var e in atlas.entries)
        {
            float scale = Mathf.Max(0.01f, e.sourceScalePercent / 100f);
            metas.Add(new SpriteMetaData
            {
                name = e.spriteName,
                rect = e.atlasRect,
                pivot = e.pivot,
                border = e.border * scale,
                alignment = (int)SpriteAlignment.Custom
            });
        }

#pragma warning disable CS0618
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        atlas.atlasTexture = importedAtlas;
        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();

        Debug.Log("Atlas rebuilt");
    }

    private void ExtractSprite(AtlasSpriteEntry entry)
    {
        if (atlas.atlasTexture == null)
            return;

        Texture2D readable = GetReadableTexture(atlas.atlasTexture);
        Rect r = entry.atlasRect;
        Texture2D tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
        int readY = Mathf.Clamp(Mathf.RoundToInt(r.y), 0, Mathf.Max(0, readable.height - Mathf.RoundToInt(r.height)));
        Color[] pixels = readable.GetPixels((int)r.x, readY, (int)r.width, (int)r.height);
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        string path = EditorUtility.SaveFilePanel("Extract Sprite", "", entry.spriteName + ".png", "png");

        if (string.IsNullOrEmpty(path))
            return;

        File.WriteAllBytes(path, png);
        Debug.Log("Extracted: " + path);
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

    private static Texture2D ScaleTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    private static Rect ToPixelRectClamped(Rect uv, int atlasWidth, int atlasHeight, int expectedWidth, int expectedHeight)
    {
        int x = Mathf.FloorToInt(uv.x * atlasWidth);
        int y = Mathf.FloorToInt(uv.y * atlasHeight);
        int width = Mathf.Max(1, expectedWidth);
        int height = Mathf.Max(1, expectedHeight);

        x = Mathf.Clamp(x, 0, Mathf.Max(0, atlasWidth - 1));
        y = Mathf.Clamp(y, 0, Mathf.Max(0, atlasHeight - 1));
        width = Mathf.Clamp(width, 1, atlasWidth - x);
        height = Mathf.Clamp(height, 1, atlasHeight - y);

        return new Rect(x, y, width, height);
    }
}
