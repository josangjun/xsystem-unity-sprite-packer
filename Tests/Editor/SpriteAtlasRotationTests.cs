using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SpriteAtlasRotationTests
{
    private readonly List<Object> _objects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in _objects)
        {
            Object.DestroyImmediate(item);
        }
        _objects.Clear();
    }

    [Test]
    public void RotationPackingRestoresAsymmetricPixelsAndFrameOrigin()
    {
        List<Texture2D> pieces = new List<Texture2D>();
        List<AtlasSpriteEntry> entries = new List<AtlasSpriteEntry>();
        List<Texture2D> frames = new List<Texture2D>();
        foreach (RectInt rect in new[] { new RectInt(4, 6, 8, 4), new RectInt(5, 4, 4, 8) })
        {
            Texture2D frame = CreateFrame(16, 16, rect);
            frames.Add(frame);
            AtlasSpriteEntry entry = new AtlasSpriteEntry { pivot = new Vector2(0.1f, 0.2f) };
            entries.Add(entry);
            pieces.Add(Track(SpriteAtlasFrameUtility.PrepareFrame(frame, entry, true)));
        }

        Texture2D atlas = Track(SpriteAtlasRotationPacker.Pack(pieces, entries, 0, 32));
        Assert.That(entries.Any(entry => entry.packedRotated), Is.True, "The fixture must exercise real rotation.");
        Assert.That(atlas.width, Is.EqualTo(8), "Rotation should fit both rectangles into an 8x8 atlas.");
        Assert.That(entries[0].atlasRect.Overlaps(entries[1].atlasRect), Is.False);
        for (int i = 0; i < entries.Count; i++)
        {
            Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(atlas, entries[i]));
            CollectionAssert.AreEqual(frames[i].GetPixels32(), restored.GetPixels32());
            Vector2 pivot = SpriteAtlasFrameUtility.GetAtlasPivot(entries[i]);
            Vector2 size = entries[i].packedContentRect.size;
            Vector2 position = -Vector2.Scale(pivot, size);
            Vector2 expected = (Vector2)entries[i].packedContentRect.position - new Vector2(1.6f, 3.2f);
            Assert.That(position.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(expected.y).Within(0.0001f));
        }
    }

    [Test]
    public void RotationMetadataPreservesOtherImporterUserData()
    {
        const string otherData = "Other importer data\nsecond line";
        SpriteAtlasRotationMetadata metadata = new SpriteAtlasRotationMetadata();
        metadata.frames.Add(new SpriteAtlasRotationFrame { spriteName = "Frame", atlasRect = new Rect(2, 3, 4, 8) });
        string encoded = metadata.Write(otherData);
        Assert.That(encoded.StartsWith(otherData, StringComparison.Ordinal), Is.True);
        Assert.That(metadata.Write(encoded), Is.EqualTo(encoded));
        Assert.That(SpriteAtlasRotationMetadata.Read(encoded).frames[0].atlasRect, Is.EqualTo(new Rect(2, 3, 4, 8)));
    }

    [Test]
    public void PaddingDoesNotShrinkAFrameThatExactlyMatchesMaxSize()
    {
        Texture2D frame = CreateFrame(8, 8, new RectInt(0, 0, 8, 8));
        AtlasSpriteEntry entry = new AtlasSpriteEntry();
        Texture2D piece = Track(SpriteAtlasFrameUtility.PrepareFrame(frame, entry, true));
        Texture2D atlas = Track(SpriteAtlasRotationPacker.Pack(
            new List<Texture2D> { piece }, new List<AtlasSpriteEntry> { entry }, 2, 8));
        Assert.That(entry.atlasRect.size, Is.EqualTo(new Vector2(8, 8)));
        Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(atlas, entry));
        CollectionAssert.AreEqual(frame.GetPixels32(), restored.GetPixels32());
    }

    [TestCase(true, 100)]
    [TestCase(false, 100)]
    [TestCase(true, 50)]
    [TestCase(false, 50)]
    public void ImportedRotationPreservesSpriteGeometryUvsAndExtractionAcrossRebuilds(bool trim, int sourceScalePercent)
    {
        string folder = "Assets/SpriteAtlasRotationTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        try
        {
            Texture2D source = CreateFrame(32, 16, new RectInt(2, 5, 12, 6));
            Texture2D second = CreateFrame(16, 16, new RectInt(5, 2, 6, 12));
            source.SetPixels(16, 0, 16, 16, second.GetPixels());
            source.Apply();
            string sourcePath = folder + "/Source.png";
            byte[] originalPng = source.EncodeToPNG();
            File.WriteAllBytes(sourcePath, originalPng);
            AssetDatabase.ImportAsset(sourcePath);
            TextureImporter sourceImporter = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            sourceImporter.textureType = TextureImporterType.Sprite;
            sourceImporter.spriteImportMode = SpriteImportMode.Multiple;
            sourceImporter.textureCompression = TextureImporterCompression.Uncompressed;
            sourceImporter.filterMode = FilterMode.Point;
#pragma warning disable CS0618
            sourceImporter.spritesheet = new[]
            {
                new SpriteMetaData { name = "Wide", rect = new Rect(0, 0, 16, 16),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.1f, 0.2f),
                    border = new Vector4(3, 6, 3, 6) },
                new SpriteMetaData { name = "Tall", rect = new Rect(16, 0, 16, 16),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.1f, 0.2f),
                    border = new Vector4(6, 3, 6, 3) }
            };
#pragma warning restore CS0618
            sourceImporter.SaveAndReimport();
            SpriteAtlasManifest manifest = ScriptableObject.CreateInstance<SpriteAtlasManifest>();
            AssetDatabase.CreateAsset(manifest, folder + "/Atlas.asset");
            foreach (Sprite sourceSprite in AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Sprite>())
            {
                manifest.entries.Add(new AtlasSpriteEntry
                {
                    spriteName = sourceSprite.name,
                    sourceSprite = sourceSprite,
                    sourceRect = sourceSprite.rect,
                    pivot = sourceSprite.pivot / sourceSprite.rect.size,
                    border = sourceSprite.border,
                    sourceScalePercent = sourceScalePercent
                });
            }
            Editor editor = Track(Editor.CreateEditor(manifest));
            SerializedObject serialized = new SerializedObject(manifest);
            serialized.FindProperty("_trimTransparentPixels").boolValue = trim;
            serialized.FindProperty("_allowRotation").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Rebuild(editor);
            AssertPackedSpriteRects(manifest);
            if (trim)
            {
                Assert.That(manifest.entries.Any(entry => entry.packedRotated), Is.True);
            }

            string atlasPath = AssetDatabase.GetAssetPath(manifest.atlasTexture);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            foreach (AtlasSpriteEntry entry in manifest.entries)
            {
                Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>()
                    .Single(item => item.name == entry.spriteName);
                AssertSpriteUvs(sprite, entry);
                if (entry.packedRotated)
                {
                    AssertImageUvs(manifest, entry, sprite, false);
                    AssertImageUvs(manifest, entry, sprite, true);
                }
            }

            Texture2D readable = Track(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            readable.LoadImage(File.ReadAllBytes(atlasPath));
            Dictionary<string, Color32[]> expected = manifest.entries.ToDictionary(
                entry => entry.spriteName,
                entry => VisiblePixels(Track(SpriteAtlasFrameUtility.RestoreFrame(readable, entry))));
            foreach (bool rotation in new[] { false, true })
            {
                serialized.Update();
                serialized.FindProperty("_allowRotation").boolValue = rotation;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Rebuild(editor);
                AssertPackedSpriteRects(manifest);
                readable.LoadImage(File.ReadAllBytes(atlasPath));
                foreach (AtlasSpriteEntry entry in manifest.entries)
                {
                    Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(readable, entry));
                    Assert.That(restored.width, Is.EqualTo(16 * sourceScalePercent / 100));
                    Assert.That(restored.height, Is.EqualTo(16 * sourceScalePercent / 100));
                    CollectionAssert.AreEqual(expected[entry.spriteName], VisiblePixels(restored));
                    AssertSpriteUvs(manifest.GetSprite(entry.spriteName), entry);
                }
                CollectionAssert.AreEqual(originalPng, File.ReadAllBytes(sourcePath));
            }
        }
        finally
        {
            // Editors must be destroyed before their target assets are removed.
            foreach (Editor editor in _objects.OfType<Editor>().ToArray())
            {
                Object.DestroyImmediate(editor);
                _objects.Remove(editor);
            }
            AssetDatabase.DeleteAsset(folder);
        }
    }

    private static void Rebuild(Editor editor)
    {
        typeof(SpriteAtlasPacker).GetMethod("RebuildAtlas", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(editor, null);
    }

    private static void AssertPackedSpriteRects(SpriteAtlasManifest manifest)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(
            AssetDatabase.GetAssetPath(manifest.atlasTexture));
#pragma warning disable CS0618
        SpriteMetaData[] spriteRects = importer.spritesheet;
#pragma warning restore CS0618
        Assert.That(spriteRects.Length, Is.EqualTo(manifest.entries.Count));
        foreach (AtlasSpriteEntry entry in manifest.entries)
        {
            SpriteMetaData spriteRect = spriteRects.Single(item => item.name == entry.spriteName);
            Assert.That(spriteRect.rect, Is.EqualTo(entry.atlasRect),
                "Sprite Editor must show the actual packed region, without helper rectangles at the origin.");
        }
        for (int i = 0; i < spriteRects.Length; i++)
        {
            for (int j = i + 1; j < spriteRects.Length; j++)
            {
                Assert.That(spriteRects[i].rect.Overlaps(spriteRects[j].rect), Is.False,
                    spriteRects[i].name + " overlaps " + spriteRects[j].name);
            }
        }
    }

    private static void AssertSpriteUvs(Sprite sprite, AtlasSpriteEntry entry)
    {
        Vector2 size = entry.packedRotated ? new Vector2(entry.atlasRect.height, entry.atlasRect.width)
            : entry.atlasRect.size;
        Assert.That(sprite.rect.size, Is.EqualTo(size));
        Vector2 pivot = SpriteAtlasFrameUtility.GetAtlasPivot(entry);
        Assert.That(sprite.pivot.x, Is.EqualTo(pivot.x * size.x).Within(0.0001f));
        Assert.That(sprite.pivot.y, Is.EqualTo(pivot.y * size.y).Within(0.0001f));
        Assert.That(sprite.border, Is.EqualTo(SpriteAtlasFrameUtility.GetAtlasBorder(entry)));
        for (int i = 0; i < sprite.vertices.Length; i++)
        {
            Vector2 normalized = (sprite.vertices[i] * sprite.pixelsPerUnit + sprite.pivot) / size;
            Vector2 atlasPosition = entry.packedRotated
                ? new Vector2(normalized.y, 1 - normalized.x) : normalized;
            Vector2 expected = (entry.atlasRect.position + Vector2.Scale(atlasPosition, entry.atlasRect.size))
                / new Vector2(sprite.texture.width, sprite.texture.height);
            Assert.That(sprite.uv[i].x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(sprite.uv[i].y, Is.EqualTo(expected.y).Within(0.0001f));
        }
    }

    private void AssertImageUvs(SpriteAtlasManifest manifest, AtlasSpriteEntry entry, Sprite sprite, bool useSpriteMesh)
    {
        GameObject gameObject = Track(new GameObject("Rotation UI", typeof(RectTransform), typeof(TextureAtlasImage)));
        TextureAtlasImage image = gameObject.GetComponent<TextureAtlasImage>();
        SerializedObject serializedImage = new SerializedObject(image);
        serializedImage.FindProperty("m_Atlas").objectReferenceValue = manifest;
        serializedImage.ApplyModifiedPropertiesWithoutUndo();
        image.SpriteName = entry.spriteName;
        image.useSpriteMesh = useSpriteMesh;
        image.rectTransform.sizeDelta = sprite.rect.size;
        foreach (Image.Type type in Enum.GetValues(typeof(Image.Type)))
        {
            image.type = type;
            using (VertexHelper vertices = new VertexHelper())
            {
                typeof(TextureAtlasImage).GetMethod("OnPopulateMesh",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Invoke(image, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.GreaterThan(0));
                UIVertex vertex = new UIVertex();
                for (int i = 0; i < vertices.currentVertCount; i++)
                {
                    vertices.PopulateUIVertex(ref vertex, i);
                    float x = vertex.position.x / sprite.rect.width + image.rectTransform.pivot.x;
                    float y = vertex.position.y / sprite.rect.height + image.rectTransform.pivot.y;
                    Vector2 expected = new Vector2(
                        (entry.atlasRect.x + y * entry.atlasRect.width) / sprite.texture.width,
                        (entry.atlasRect.y + (1 - x) * entry.atlasRect.height) / sprite.texture.height);
                    Assert.That(vertex.uv0.x, Is.EqualTo(expected.x).Within(0.0001f), type.ToString());
                    Assert.That(vertex.uv0.y, Is.EqualTo(expected.y).Within(0.0001f), type.ToString());
                }
            }
        }
    }

    private Texture2D CreateFrame(int width, int height, RectInt rect)
    {
        Texture2D texture = Track(new Texture2D(width, height, TextureFormat.RGBA32, false));
        texture.SetPixels32(new Color32[width * height]);
        for (int y = rect.yMin; y < rect.yMax; y++)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                texture.SetPixel(x, y, new Color32((byte)(20 + x * 7), (byte)(30 + y * 9), (byte)(x + y), 255));
            }
        }
        texture.Apply();
        return texture;
    }

    private static Color32[] VisiblePixels(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0)
            {
                pixels[i] = new Color32();
            }
        }
        return pixels;
    }

    private T Track<T>(T item) where T : Object
    {
        _objects.Add(item);
        return item;
    }
}
