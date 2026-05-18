using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteAtlasPack))]
public class SpriteAtlasPacker : Editor
{
    private SpriteAtlasPack atlas;
    private SerializedProperty atlasTextureProp;
    private SerializedProperty entriesProp;

    private Vector2 scroll;
    private int padding = 2;
    private int atlasMaxSize = 4096;
    private bool allowRotation = false;
    private string entrySearch = string.Empty;
    private int texturePickerControlId = -1;
    private bool waitingForTexturePicker = false;

    private void OnEnable()
    {
        atlas = (SpriteAtlasPack)target;
        atlasTextureProp = serializedObject.FindProperty("atlasTexture");
        entriesProp = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (waitingForTexturePicker &&
            Event.current.commandName == "ObjectSelectorClosed" &&
            EditorGUIUtility.GetObjectPickerControlID() == texturePickerControlId)
        {
            var picked = EditorGUIUtility.GetObjectPickerObject() as Texture2D;
            if (picked != null)
            {
                AddTextureObjects(new Object[] { picked });
            }
            waitingForTexturePicker = false;
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
        if (GUILayout.Button("Add Selected Textures"))
        {
            AddSelectedTextures();
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
            Texture2D newSourceTexture = EditorGUILayout.ObjectField("Texture", e.sourceTexture, typeof(Texture2D), false) as Texture2D;
            if (newSourceTexture != null && newSourceTexture != e.sourceTexture)
            {
                UpdateEntryTexture(e, newSourceTexture);
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

    private void AddSelectedTextures()
    {
        Object[] selected = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);
        if (selected == null || selected.Length == 0)
        {
            texturePickerControlId = EditorGUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.ShowObjectPicker<Texture2D>(null, false, string.Empty, texturePickerControlId);
            waitingForTexturePicker = true;
            return;
        }

        AddTextureObjects(selected);
    }

    private void AddTextureObjects(Object[] selected)
    {
        foreach (Object o in selected)
        {
            string path = AssetDatabase.GetAssetPath(o);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
                continue;

            if (importer.textureType != TextureImporterType.Sprite)
                continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            EnsureReadable(importer);

            if (importer.spriteImportMode == SpriteImportMode.Single)
            {
                atlas.entries.Add(new AtlasSpriteEntry
                {
                    spriteName = tex.name,
                    sourceTexture = tex,
                    sourceRect = new Rect(0, 0, tex.width, tex.height),
                    pivot = importer.spritePivot,
                    border = importer.spriteBorder
                });
            }
            else
            {
#pragma warning disable CS0618
                foreach (var s in importer.spritesheet)
                {
                    atlas.entries.Add(new AtlasSpriteEntry
                    {
                        spriteName = s.name,
                        sourceTexture = tex,
                        sourceRect = s.rect,
                        pivot = s.pivot,
                        border = s.border
                    });
                }
#pragma warning restore CS0618
            }
        }

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
    }

    private void UpdateEntryTexture(AtlasSpriteEntry entry, Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null || importer.textureType != TextureImporterType.Sprite)
            return;

        EnsureReadable(importer);
        entry.sourceTexture = tex;

        if (importer.spriteImportMode == SpriteImportMode.Single)
        {
            entry.sourceRect = new Rect(0, 0, tex.width, tex.height);
            entry.pivot = importer.spritePivot;
            entry.border = importer.spriteBorder;
        }
        else
        {
#pragma warning disable CS0618
            foreach (var s in importer.spritesheet)
            {
                if (s.name == entry.spriteName)
                {
                    entry.sourceRect = s.rect;
                    entry.pivot = s.pivot;
                    entry.border = s.border;
                    break;
                }
            }
#pragma warning restore CS0618
        }
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
        List<string> guids = new List<string>();
        
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
            if (guids.Contains(e.guid))
            {
                Debug.LogWarning($"Entry {e.spriteName} has duplicate source texture. Skipping.");
                atlas.entries.RemoveAt(i);
                --i;
                continue;
            }
            guids.Add(e.guid);

            Texture2D readable = GetReadableTexture(e.sourceTexture);
            Rect r = e.sourceRect;
            Texture2D piece = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
            Color[] pixels = readable.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            piece.SetPixels(pixels);
            piece.Apply();
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
            atlas.entries[i].atlasRect = new Rect(uv.x * atlasTex.width, uv.y * atlasTex.height, uv.width * atlasTex.width, uv.height * atlasTex.height);
        }

        byte[] png = atlasTex.EncodeToPNG();
        string atlasAssetPath = AssetDatabase.GetAssetPath(atlas);
        string folder = Path.GetDirectoryName(atlasAssetPath);
        string pngPath = folder + "/" + atlas.name + ".png";

        File.WriteAllBytes(pngPath, png);
        AssetDatabase.Refresh();

        Texture2D importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.isReadable = true;

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        foreach (var e in atlas.entries)
        {
            metas.Add(new SpriteMetaData
            {
                name = e.spriteName,
                rect = e.atlasRect,
                pivot = e.pivot,
                border = e.border,
                alignment = (int)SpriteAlignment.Custom
            });
        }

#pragma warning disable CS0618
        importer.spritesheet = metas.ToArray();
#pragma warning restore CS0618

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
        Color[] pixels = readable.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
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
}