using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class AtlasSpriteEntry : System.IComparable<AtlasSpriteEntry>
{
    public string spriteName;

    public string guid;

    public string sourceSpriteName;

    [Range(1, 100)]
    public int sourceScalePercent = 100;

    public Sprite sourceSprite
    {
        get
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
            string lookupName = !string.IsNullOrEmpty(sourceSpriteName) ? sourceSpriteName : spriteName;
            if (!string.IsNullOrEmpty(lookupName))
            {
                var matched = sprites.FirstOrDefault(s => s.name == lookupName);
                if (matched != null)
                {
                    return matched;
                }
            }

            return sprites.FirstOrDefault();
#else
            return sprite;
#endif
        }
        set
        {
#if UNITY_EDITOR
            if (value == null)
            {
                guid = null;
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(value);
                guid = AssetDatabase.AssetPathToGUID(path);
                sourceSpriteName = value.name;
            }
#endif
        }
    }

    public Texture2D SourceTexture
    {
        get
        {
#if UNITY_EDITOR
            return sourceSprite != null ? sourceSprite.texture : null;
#else
            return sprite.texture;
#endif
        }
    }

    public Sprite sprite;

    public Rect sourceRect;

    public Rect atlasRect;

    // Snapshot of the last build, independent of pending source/settings changes.
    public Vector2Int packedFrameSize;

    public RectInt packedContentRect;

    public Vector2 packedFramePivot;

    public Vector4 packedFrameBorder;

    public bool packedRotated;

    public bool HasPackedFrame => packedFrameSize.x > 0 && packedFrameSize.y > 0;

    public Vector2 pivot;

    public Vector4 border;

    public int CompareTo(AtlasSpriteEntry other)
    {
        if (other == null)
        {
            return 1;
        }

        return string.Compare(spriteName, other.spriteName, System.StringComparison.OrdinalIgnoreCase);
    }
}
