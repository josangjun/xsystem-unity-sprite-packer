using UnityEngine;

public static class SpriteAtlasRotationUtility
{
    public static Vector2 MapUV(Vector2 uv, Rect logicalRect, Rect packedRect, Vector2 textureSize)
    {
        float x = (uv.x * textureSize.x - logicalRect.x) / logicalRect.width;
        float y = (uv.y * textureSize.y - logicalRect.y) / logicalRect.height;
        return new Vector2(
            (packedRect.x + y * packedRect.width) / textureSize.x,
            (packedRect.y + (1 - x) * packedRect.height) / textureSize.y);
    }
}
