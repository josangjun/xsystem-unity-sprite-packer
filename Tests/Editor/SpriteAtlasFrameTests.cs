using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SpriteAtlasFrameTests
{
    private readonly List<Object> _createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in _createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void ManifestTrimOptionDefaultsOffAndIsSerialized()
    {
        SpriteAtlasManifest manifest = Track(ScriptableObject.CreateInstance<SpriteAtlasManifest>());
        Assert.That(manifest.TrimTransparentPixels, Is.False);
        SerializedObject serialized = new SerializedObject(manifest);
        serialized.FindProperty("_trimTransparentPixels").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SpriteAtlasManifest restored = Track(ScriptableObject.CreateInstance<SpriteAtlasManifest>());
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(manifest), restored);
        Assert.That(restored.TrimTransparentPixels, Is.True);
    }

    [Test]
    public void TrimIncludesEveryNonzeroAlphaPixelAndPngRestoresCanvas()
    {
        Texture2D source = CreateFrame(12, 10, new RectInt(8, 6, 2, 3));
        source.SetPixel(7, 4, new Color32(255, 0, 0, 1));
        source.Apply();
        AtlasSpriteEntry entry = new AtlasSpriteEntry { pivot = new Vector2(0.25f, 0.1f) };
        Texture2D content = Track(SpriteAtlasFrameUtility.PrepareFrame(source, entry, true));
        Assert.That(entry.packedContentRect, Is.EqualTo(new RectInt(7, 4, 3, 5)));
        Assert.That(content.width, Is.EqualTo(3));
        Assert.That(content.height, Is.EqualTo(5));

        Texture2D atlas = PlaceInAtlas(content, entry);
        Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(atlas, entry));
        Texture2D decoded = Track(new Texture2D(2, 2));
        Assert.That(decoded.LoadImage(restored.EncodeToPNG()), Is.True);
        Assert.That(decoded.width, Is.EqualTo(12));
        Assert.That(decoded.height, Is.EqualTo(10));
        CollectionAssert.AreEqual(source.GetPixels32(), decoded.GetPixels32());
    }

    [TestCase(0.1f, 0.2f)]
    [TestCase(0.5f, 0.5f)]
    [TestCase(1f, 1f)]
    public void TrimPreservesPixelPositionsRelativeToPivot(float pivotX, float pivotY)
    {
        Texture2D source = CreateFrame(12, 10, new RectInt(8, 6, 2, 3));
        AtlasSpriteEntry entry = new AtlasSpriteEntry { pivot = new Vector2(pivotX, pivotY) };
        Texture2D content = Track(SpriteAtlasFrameUtility.PrepareFrame(source, entry, true));
        Sprite original = Track(Sprite.Create(source, new Rect(0, 0, 12, 10), entry.pivot, 100));
        Sprite trimmed = Track(Sprite.Create(
            content, new Rect(0, 0, 2, 3), SpriteAtlasFrameUtility.GetAtlasPivot(entry), 100));

        Vector2 originalContentPosition = new Vector2(8, 6) - original.pivot;
        Vector2 trimmedContentPosition = -trimmed.pivot;
        Assert.That(trimmedContentPosition.x, Is.EqualTo(originalContentPosition.x).Within(0.0001f));
        Assert.That(trimmedContentPosition.y, Is.EqualTo(originalContentPosition.y).Within(0.0001f));
    }

    [Test]
    public void DisabledTrimPreservesFullFrameAndPivot()
    {
        Texture2D source = CreateFrame(12, 10, new RectInt(8, 6, 2, 3));
        AtlasSpriteEntry entry = new AtlasSpriteEntry { pivot = new Vector2(0.2f, 0.3f) };
        Texture2D content = Track(SpriteAtlasFrameUtility.PrepareFrame(source, entry, false));
        Assert.That(entry.packedContentRect, Is.EqualTo(new RectInt(0, 0, 12, 10)));
        Assert.That(SpriteAtlasFrameUtility.GetAtlasPivot(entry), Is.EqualTo(entry.pivot));
        CollectionAssert.AreEqual(source.GetPixels32(), content.GetPixels32());
    }

    [Test]
    public void FullyTransparentFramePacksToOnePixelAndRestores()
    {
        Texture2D source = CreateFrame(12, 10, new RectInt());
        AtlasSpriteEntry entry = new AtlasSpriteEntry { pivot = new Vector2(0.5f, 0.5f) };
        Texture2D content = Track(SpriteAtlasFrameUtility.PrepareFrame(source, entry, true));
        Assert.That(entry.packedContentRect, Is.EqualTo(new RectInt(0, 0, 1, 1)));
        Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(PlaceInAtlas(content, entry), entry));
        CollectionAssert.AreEqual(source.GetPixels32(), restored.GetPixels32());
    }

    [Test]
    public void ExtractionUsesLastBuildEvenAfterSourceSettingsChange()
    {
        Texture2D source = CreateFrame(6, 5, new RectInt(3, 2, 2, 2));
        AtlasSpriteEntry entry = new AtlasSpriteEntry
        {
            pivot = new Vector2(0.2f, 0.3f),
            border = new Vector4(2, 2, 2, 2),
            sourceScalePercent = 50
        };
        Texture2D content = Track(SpriteAtlasFrameUtility.PrepareFrame(source, entry, true));
        Texture2D atlas = PlaceInAtlas(content, entry);
        entry.sourceScalePercent = 100;
        entry.sourceRect = new Rect(0, 0, 100, 100);
        entry.pivot = Vector2.one;
        entry.border = Vector4.zero;

        Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(atlas, entry));
        Assert.That(restored.width, Is.EqualTo(6));
        Assert.That(restored.height, Is.EqualTo(5));
        Assert.That(entry.packedFramePivot, Is.EqualTo(new Vector2(0.2f, 0.3f)));
        Assert.That(entry.packedFrameBorder, Is.EqualTo(Vector4.one));
        CollectionAssert.AreEqual(source.GetPixels32(), restored.GetPixels32());
    }

    [Test]
    public void LegacyEntryWithoutTrimMetadataExtractsItsAtlasRect()
    {
        Texture2D atlas = CreateFrame(12, 10, new RectInt(8, 6, 2, 3));
        AtlasSpriteEntry entry = new AtlasSpriteEntry { atlasRect = new Rect(8, 6, 2, 3) };
        Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(atlas, entry));
        Assert.That(restored.width, Is.EqualTo(2));
        Assert.That(restored.height, Is.EqualTo(3));
        CollectionAssert.AreEqual(atlas.GetPixels(8, 6, 2, 3), restored.GetPixels());
    }

    [Test]
    public void DownscaledPackedContentRestoresFullCanvasSize()
    {
        Texture2D source = CreateFrame(12, 10, new RectInt(4, 2, 4, 6));
        AtlasSpriteEntry entry = new AtlasSpriteEntry();
        Track(SpriteAtlasFrameUtility.PrepareFrame(source, entry, true));
        Texture2D smallContent = CreateFrame(2, 3, new RectInt(0, 0, 2, 3));
        Texture2D restored = Track(SpriteAtlasFrameUtility.RestoreFrame(PlaceInAtlas(smallContent, entry), entry));
        Assert.That(restored.width, Is.EqualTo(12));
        Assert.That(restored.height, Is.EqualTo(10));
        Assert.That(restored.GetPixel(0, 0).a, Is.Zero);
        Assert.That(restored.GetPixel(5, 4).a, Is.EqualTo(1));
    }

    [Test]
    public void BordersAccountForRemovedMarginsAndPackingScale()
    {
        Texture2D source = CreateFrame(12, 10, new RectInt(3, 2, 6, 6));
        AtlasSpriteEntry entry = new AtlasSpriteEntry { border = new Vector4(4, 4, 4, 4) };
        Track(SpriteAtlasFrameUtility.PrepareFrame(source, entry, true));
        entry.atlasRect = new Rect(0, 0, 3, 3);
        Assert.That(SpriteAtlasFrameUtility.GetAtlasBorder(entry), Is.EqualTo(new Vector4(0.5f, 1, 0.5f, 1)));
    }

    private Texture2D CreateFrame(int width, int height, RectInt contentRect)
    {
        Texture2D texture = Track(new Texture2D(width, height, TextureFormat.RGBA32, false));
        texture.SetPixels32(new Color32[width * height]);
        for (int y = contentRect.yMin; y < contentRect.yMax; y++)
        {
            for (int x = contentRect.xMin; x < contentRect.xMax; x++)
            {
                texture.SetPixel(x, y, Color.red);
            }
        }
        texture.Apply();
        return texture;
    }

    private Texture2D PlaceInAtlas(Texture2D content, AtlasSpriteEntry entry)
    {
        Texture2D atlas = CreateFrame(content.width + 4, content.height + 6, new RectInt());
        atlas.SetPixels(2, 3, content.width, content.height, content.GetPixels());
        atlas.Apply();
        entry.atlasRect = new Rect(2, 3, content.width, content.height);
        return atlas;
    }

    private T Track<T>(T createdObject) where T : Object
    {
        _createdObjects.Add(createdObject);
        return createdObject;
    }
}
