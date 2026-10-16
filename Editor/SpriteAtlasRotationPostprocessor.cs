using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

public class SpriteAtlasRotationPostprocessor : AssetPostprocessor
{
    private void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
    {
        SpriteAtlasRotationMetadata metadata = SpriteAtlasRotationMetadata.Read(assetImporter.userData);
        if (metadata == null)
        {
            return;
        }

        foreach (Sprite sprite in sprites)
        {
            SpriteAtlasRotationFrame frame = metadata.frames.Find(item => item.spriteName == sprite.name);
            if (frame == null)
            {
                continue;
            }

            if (frame.physicalRect)
            {
                RestoreLogicalGeometry(sprite, frame);
            }

            Vector2[] vertices = sprite.vertices;
            Vector2 textureSize = new Vector2(texture.width, texture.height);
            NativeArray<Vector2> uvs = new NativeArray<Vector2>(vertices.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 sourceUV = (sprite.rect.position + vertices[i] * sprite.pixelsPerUnit + sprite.pivot)
                        / textureSize;
                    uvs[i] = SpriteAtlasRotationUtility.MapUV(
                        sourceUV, sprite.rect, frame.atlasRect, textureSize);
                }
                sprite.SetVertexAttribute(VertexAttribute.TexCoord0, uvs);
            }
            finally
            {
                uvs.Dispose();
            }
        }
    }

    private static void RestoreLogicalGeometry(Sprite sprite, SpriteAtlasRotationFrame frame)
    {
        Rect logicalRect = new Rect(frame.atlasRect.x, frame.atlasRect.y,
            frame.atlasRect.height, frame.atlasRect.width);
        using (SerializedObject data = new SerializedObject(sprite))
        {
            RequireProperty(data, "m_Rect").rectValue = logicalRect;
            RequireProperty(data, "m_RD.textureRect").rectValue = logicalRect;
            RequireProperty(data, "m_Pivot").vector2Value = frame.pivot;
            RequireProperty(data, "m_Border").vector4Value = frame.border;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // Importer rects describe storage. Only this Sprite's render geometry describes the upright frame.
        sprite.OverrideGeometry(new[]
        {
            Vector2.zero,
            new Vector2(logicalRect.width, 0),
            new Vector2(0, logicalRect.height),
            logicalRect.size
        }, new ushort[] { 0, 2, 1, 2, 3, 1 });
    }

    private static SerializedProperty RequireProperty(SerializedObject data, string path)
    {
        SerializedProperty property = data.FindProperty(path);
        if (property == null)
        {
            throw new System.InvalidOperationException("Sprite rotation import requires serialized property: " + path);
        }
        return property;
    }
}
