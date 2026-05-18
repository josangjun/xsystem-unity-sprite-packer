using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class AtlasSpriteEntry
{
    public string spriteName;
    
    public string guid;
    
    public Texture2D sourceTexture {
        get
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#else
            return sprite.texture;
#endif
        }
        set {
#if UNITY_EDITOR
            if (value == null)
            {
                guid = null;
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(value);
                guid = AssetDatabase.AssetPathToGUID(path);
            }
#endif
        }
    }
    
    public Sprite sprite;
    
    public Rect sourceRect;

    public Rect atlasRect;

    public Vector2 pivot;

    public Vector4 border;
}

[CreateAssetMenu(fileName = "SpriteAtlasPack", menuName = "XSystem/Sprite Atlas Pack")]
public class SpriteAtlasPack : ScriptableObject
{
    public Texture2D atlasTexture;
    
    public List<AtlasSpriteEntry> entries =
        new List<AtlasSpriteEntry>();
    
    public Sprite GetSprite(string name)
    {
#if UNITY_EDITOR
        if (atlasTexture != null)
        {
            string path = AssetDatabase.GetAssetPath(atlasTexture);
            if (!string.IsNullOrEmpty(path))
            {
                var sprite = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .FirstOrDefault(s => s.name == name);
                if (sprite != null) {
                    
                    return sprite;
                }
            }
        }
#endif

        var entry = entries.Find(e => e.spriteName == name);
        if (entry == null)
        {
            Debug.LogWarning($"Sprite {name} not found in atlas.");
            return null;
        }

        return entry.sprite;
    }
}
