using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "SpriteAtlasManifest", menuName = "XSystem/Sprite Atlas Manifest")]
public class SpriteAtlasManifest : ScriptableObject
{
    public Texture2D atlasTexture;

    [SerializeField]
    [Tooltip("Remove fully transparent margins when rebuilding the atlas. Extracted PNGs restore the full frame.")]
    private bool _trimTransparentPixels;

    [SerializeField]
    private bool _allowRotation;

    public List<AtlasSpriteEntry> entries =
        new List<AtlasSpriteEntry>();

    public bool TrimTransparentPixels => _trimTransparentPixels;

    public bool AllowRotation => _allowRotation;

    public AtlasSpriteEntry GetEntry(string name)
    {
        return entries.Find(entry => entry.spriteName == name);
    }

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
                if (loadedSprite != null)
                {
                    entry.sprite = loadedSprite; // 로드된 Sprite 참조를 캐시합니다.
                }
            }
        }
#endif
        // 런타임에서는 캐시된 Sprite를 반환하고, 에디터에서는 새로 로드/캐시된 Sprite를 반환합니다.
        return entry.sprite;
    }
}
