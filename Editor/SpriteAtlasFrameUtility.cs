using UnityEngine;

public static class SpriteAtlasFrameUtility
{
    public static Texture2D PrepareFrame(Texture2D frame, AtlasSpriteEntry entry, bool trimTransparentPixels)
    {
        RectInt contentRect = trimTransparentPixels
            ? FindContentRect(frame)
            : new RectInt(0, 0, frame.width, frame.height);

        entry.packedFrameSize = new Vector2Int(frame.width, frame.height);
        entry.packedContentRect = contentRect;
        entry.packedFramePivot = entry.pivot;
        float scale = Mathf.Max(0.01f, entry.sourceScalePercent / 100f);
        entry.packedFrameBorder = entry.border * scale;
        entry.packedRotated = false;

        Texture2D content = new Texture2D(contentRect.width, contentRect.height, TextureFormat.RGBA32, false);
        content.SetPixels(frame.GetPixels(contentRect.x, contentRect.y, contentRect.width, contentRect.height));
        content.Apply();
        return content;
    }

    public static Vector2 GetAtlasPivot(AtlasSpriteEntry entry)
    {
        if (!entry.HasPackedFrame)
        {
            return entry.pivot;
        }

        Vector2 framePivot = Vector2.Scale(entry.packedFramePivot, entry.packedFrameSize);
        RectInt contentRect = entry.packedContentRect;
        return new Vector2(
            (framePivot.x - contentRect.x) / contentRect.width,
            (framePivot.y - contentRect.y) / contentRect.height);
    }

    public static Vector4 GetAtlasBorder(AtlasSpriteEntry entry)
    {
        if (!entry.HasPackedFrame)
        {
            return entry.border * Mathf.Max(0.01f, entry.sourceScalePercent / 100f);
        }

        Vector4 border = entry.packedFrameBorder;
        RectInt contentRect = entry.packedContentRect;
        float rightMargin = entry.packedFrameSize.x - contentRect.xMax;
        float topMargin = entry.packedFrameSize.y - contentRect.yMax;
        Vector4 contentBorder = new Vector4(
            Mathf.Clamp(border.x - contentRect.x, 0, contentRect.width),
            Mathf.Clamp(border.y - contentRect.y, 0, contentRect.height),
            Mathf.Clamp(border.z - rightMargin, 0, contentRect.width),
            Mathf.Clamp(border.w - topMargin, 0, contentRect.height));

        // PackTextures can shrink the atlas when Max Size is exceeded.
        float scaleX = (entry.packedRotated ? entry.atlasRect.height : entry.atlasRect.width) / contentRect.width;
        float scaleY = (entry.packedRotated ? entry.atlasRect.width : entry.atlasRect.height) / contentRect.height;
        return Vector4.Scale(contentBorder, new Vector4(scaleX, scaleY, scaleX, scaleY));
    }

    public static Texture2D RestoreFrame(Texture2D atlasTexture, AtlasSpriteEntry entry)
    {
        int width = Mathf.RoundToInt(entry.atlasRect.width);
        int height = Mathf.RoundToInt(entry.atlasRect.height);
        int x = Mathf.RoundToInt(entry.atlasRect.x);
        int y = Mathf.RoundToInt(entry.atlasRect.y);
        Texture2D content = new Texture2D(width, height, TextureFormat.RGBA32, false);
        content.SetPixels(atlasTexture.GetPixels(x, y, width, height));
        content.Apply();

        if (!entry.HasPackedFrame)
        {
            return content;
        }

        try
        {
            if (entry.packedRotated)
            {
                Texture2D upright = Rotate(content, false);
                Object.DestroyImmediate(content);
                content = upright;
            }

            RectInt contentRect = entry.packedContentRect;
            if (content.width != contentRect.width || content.height != contentRect.height)
            {
                Texture2D resized = ScaleTexture(content, contentRect.width, contentRect.height);
                Object.DestroyImmediate(content);
                content = resized;
            }

            Texture2D frame = new Texture2D(
                entry.packedFrameSize.x, entry.packedFrameSize.y, TextureFormat.RGBA32, false);
            frame.SetPixels32(new Color32[frame.width * frame.height]);
            frame.SetPixels(contentRect.x, contentRect.y, contentRect.width, contentRect.height, content.GetPixels());
            frame.Apply();
            return frame;
        }
        finally
        {
            Object.DestroyImmediate(content);
        }
    }

    public static Texture2D Rotate(Texture2D source, bool clockwise)
    {
        Texture2D result = new Texture2D(source.height, source.width, TextureFormat.RGBA32, false);
        Color32[] input = source.GetPixels32();
        Color32[] output = new Color32[input.Length];
        for (int y = 0; y < source.height; y++)
        {
            for (int x = 0; x < source.width; x++)
            {
                int targetX = clockwise ? y : source.height - 1 - y;
                int targetY = clockwise ? source.width - 1 - x : x;
                output[targetY * result.width + targetX] = input[y * source.width + x];
            }
        }

        result.SetPixels32(output);
        result.Apply();
        return result;
    }

    public static Texture2D ScaleTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static RectInt FindContentRect(Texture2D frame)
    {
        Color32[] pixels = frame.GetPixels32();
        int minX = frame.width;
        int minY = frame.height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < frame.height; y++)
        {
            for (int x = 0; x < frame.width; x++)
            {
                if (pixels[y * frame.width + x].a == 0)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        // Keep an empty animation frame packable without losing its canvas or pivot.
        return maxX < 0 ? new RectInt(0, 0, 1, 1) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
