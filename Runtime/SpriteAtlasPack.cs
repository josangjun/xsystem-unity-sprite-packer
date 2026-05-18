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
        var entry = entries.Find(e => e.spriteName == name);
        if (entry == null)
        {
            Debug.LogWarning($"Sprite {name} not found in atlas.");
            return null;
        }

#if UNITY_EDITOR
        // 에디터에서 entry.sprite가 null이거나 어셈블리 리로드 후 참조가 끊겼을 경우,
        // AssetDatabase에서 다시 로드하여 entry.sprite에 캐시합니다.
        if (entry.sprite == null && atlasTexture != null)
        {
            string path = AssetDatabase.GetAssetPath(atlasTexture);
            if (!string.IsNullOrEmpty(path))
            {
                var loadedSprite = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .FirstOrDefault(s => s.name == name);
                if (loadedSprite != null) {
                    entry.sprite = loadedSprite; // 로드된 Sprite 참조를 캐시합니다.
                }
            }
        }
#endif
        // 런타임에서는 캐시된 Sprite를 반환하고, 에디터에서는 새로 로드/캐시된 Sprite를 반환합니다.
        return entry.sprite;
    }
}
