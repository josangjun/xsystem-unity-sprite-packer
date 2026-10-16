using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class SpriteAtlasPackingTests
{
    [TestCase(true, 100, false)]
    [TestCase(true, 50, false)]
    [TestCase(false, 100, false)]
    [TestCase(false, 50, false)]
    [TestCase(true, 100, true)]
    [TestCase(true, 50, true)]
    [TestCase(false, 100, true)]
    [TestCase(false, 50, true)]
    public void RebuildAndExtractPreserveFrameAndImportedPivot(
        bool trim, int sourceScalePercent, bool collidingSourceName)
    {
        string folder = "Assets/SpriteAtlasPackingTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        Texture2D sourceTexture = null;
        Texture2D readableAtlas = null;
        Texture2D extracted = null;
        Editor editor = null;
        try
        {
            sourceTexture = new Texture2D(32, 24, TextureFormat.RGBA32, false);
            sourceTexture.SetPixels32(new Color32[32 * 24]);
            for (int y = 11; y < 14; y++)
            {
                for (int x = 12; x < 14; x++)
                {
                    sourceTexture.SetPixel(x, y, Color.red);
                }
            }
            sourceTexture.Apply();
            string sourcePath = folder + (collidingSourceName ? "/Atlas.png" : "/Source.png");
            byte[] sourcePng = sourceTexture.EncodeToPNG();
            File.WriteAllBytes(sourcePath, sourcePng);
            AssetDatabase.ImportAsset(sourcePath);
            TextureImporter sourceImporter = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            sourceImporter.textureType = TextureImporterType.Sprite;
            sourceImporter.spriteImportMode = SpriteImportMode.Multiple;
            sourceImporter.textureCompression = TextureImporterCompression.Uncompressed;
            sourceImporter.filterMode = FilterMode.Point;
#pragma warning disable CS0618
            sourceImporter.spritesheet = new[]
            {
                new SpriteMetaData
                {
                    name = "TestFrame",
                    rect = new Rect(4, 5, 12, 10),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.1f, 0.2f)
                }
            };
#pragma warning restore CS0618
            sourceImporter.SaveAndReimport();
            Sprite sourceSprite = Array.Find(AssetDatabase.LoadAllAssetsAtPath(sourcePath),
                asset => asset is Sprite) as Sprite;
            Assert.That(sourceSprite, Is.Not.Null);

            SpriteAtlasManifest manifest = ScriptableObject.CreateInstance<SpriteAtlasManifest>();
            AssetDatabase.CreateAsset(manifest, folder + "/Atlas.asset");
            SerializedObject serialized = new SerializedObject(manifest);
            serialized.FindProperty("_trimTransparentPixels").boolValue = trim;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AtlasSpriteEntry entry = new AtlasSpriteEntry
            {
                spriteName = "TestFrame",
                sourceSprite = sourceSprite,
                sourceRect = sourceSprite.rect,
                pivot = sourceSprite.pivot / sourceSprite.rect.size,
                sourceScalePercent = sourceScalePercent
            };
            manifest.entries.Add(entry);
            editor = Editor.CreateEditor(manifest);
            typeof(SpriteAtlasPacker).GetMethod("RebuildAtlas", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(editor, null);

            CollectionAssert.AreEqual(sourcePng, File.ReadAllBytes(sourcePath), "Rebuild must not overwrite its source.");

            Assert.That(entry.packedFrameSize, Is.EqualTo(new Vector2Int(
                12 * sourceScalePercent / 100, 10 * sourceScalePercent / 100)));
            if (trim)
            {
                Assert.That(entry.atlasRect.width, Is.LessThan(entry.packedFrameSize.x));
                Assert.That(entry.atlasRect.height, Is.LessThan(entry.packedFrameSize.y));
            }

            Sprite packedSprite = manifest.GetSprite("TestFrame");
            Assert.That(packedSprite, Is.Not.Null);
            Vector2 expectedPivot = SpriteAtlasFrameUtility.GetAtlasPivot(entry);
            Vector2 importedPivot = packedSprite.pivot / packedSprite.rect.size;
            Assert.That(importedPivot.x, Is.EqualTo(expectedPivot.x).Within(0.0001f));
            Assert.That(importedPivot.y, Is.EqualTo(expectedPivot.y).Within(0.0001f));
            Vector2 contentPosition = -packedSprite.pivot;
            Vector2 originalPosition = (Vector2)entry.packedContentRect.position
                - Vector2.Scale(entry.packedFramePivot, entry.packedFrameSize);
            Assert.That(contentPosition.x, Is.EqualTo(originalPosition.x).Within(0.0001f));
            Assert.That(contentPosition.y, Is.EqualTo(originalPosition.y).Within(0.0001f));

            readableAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            readableAtlas.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(manifest.atlasTexture)));
            Texture2D baselineFrame = SpriteAtlasFrameUtility.RestoreFrame(readableAtlas, entry);
            Color32[] expectedPixels = GetVisiblePixels(baselineFrame);
            Object.DestroyImmediate(baselineFrame);
            for (int rebuildIndex = 0; rebuildIndex < 2; rebuildIndex++)
            {
                serialized.Update();
                serialized.FindProperty("_trimTransparentPixels").boolValue = rebuildIndex == 0 ? !trim : trim;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                typeof(SpriteAtlasPacker).GetMethod("RebuildAtlas", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editor, null);
                readableAtlas.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(manifest.atlasTexture)));
                Texture2D rebuiltFrame = SpriteAtlasFrameUtility.RestoreFrame(readableAtlas, entry);
                try
                {
                    CollectionAssert.AreEqual(expectedPixels, GetVisiblePixels(rebuiltFrame),
                        "Changing trim and rebuilding must preserve alpha and every visible frame pixel.");
                    CollectionAssert.AreEqual(sourcePng, File.ReadAllBytes(sourcePath));
                }
                finally
                {
                    Object.DestroyImmediate(rebuiltFrame);
                }
            }

            // Extraction must work without a source and with changed pending settings.
            entry.guid = null;
            entry.pivot = Vector2.one;
            entry.sourceScalePercent = 1;
            readableAtlas.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(manifest.atlasTexture)));
            byte[] png = (byte[])typeof(SpriteAtlasPacker)
                .GetMethod("EncodeSpriteToPng", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { readableAtlas, entry });
            extracted = new Texture2D(2, 2);
            extracted.LoadImage(png);
            Assert.That(extracted.width, Is.EqualTo(entry.packedFrameSize.x));
            Assert.That(extracted.height, Is.EqualTo(entry.packedFrameSize.y));
            Assert.That(extracted.GetPixel(0, 0).a, Is.Zero);

            string extractedPath = folder + "/Extracted.png";
            typeof(SpriteAtlasPacker).GetMethod("WriteExtractedSprite", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Path.GetFullPath(extractedPath), entry, png });
            Sprite extractedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(extractedPath);
            Assert.That(extractedSprite, Is.Not.Null, "Extracted PNG must be imported as a Sprite inside Assets.");
            Vector2 extractedPivot = extractedSprite.pivot / extractedSprite.rect.size;
            Assert.That(extractedPivot.x, Is.EqualTo(entry.packedFramePivot.x).Within(0.0001f));
            Assert.That(extractedPivot.y, Is.EqualTo(entry.packedFramePivot.y).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(editor);
            Object.DestroyImmediate(sourceTexture);
            Object.DestroyImmediate(readableAtlas);
            Object.DestroyImmediate(extracted);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    private static Color32[] GetVisiblePixels(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0)
            {
                // Trim intentionally discards RGB hidden behind fully transparent margins.
                pixels[i] = new Color32();
            }
        }

        return pixels;
    }
}
