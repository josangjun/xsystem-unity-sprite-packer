using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SpriteAtlasRotationPacker
{
    public static Texture2D Pack(List<Texture2D> pieces, List<AtlasSpriteEntry> entries, int padding, int maxSize)
    {
        float scale = 1;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2Int[] sizes = pieces.Select(piece => new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(piece.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(piece.height * scale)))).ToArray();
            int minimum = sizes.Length == 0 ? 1 : sizes.Max(size => Mathf.Max(size.x, size.y));
            int side = Mathf.Min(maxSize, Mathf.NextPowerOfTwo(minimum));
            while (true)
            {
                if (TryPack(sizes, side, padding, out RectInt[] rects, out bool[] rotations))
                {
                    return WriteTexture(pieces, entries, sizes, rects, rotations, side);
                }
                if (side == maxSize)
                {
                    break;
                }
                side = Mathf.Min(maxSize, side * 2);
            }
            scale *= 0.5f;
        }

        throw new InvalidOperationException("The sprites cannot fit in the selected atlas Max Size.");
    }

    private static bool TryPack(
        Vector2Int[] sizes, int side, int padding, out RectInt[] placements, out bool[] rotations)
    {
        placements = new RectInt[sizes.Length];
        rotations = new bool[sizes.Length];
        // Padding belongs between pieces; it need not fit beyond the atlas's outer edges.
        List<RectInt> freeRects = new List<RectInt> { new RectInt(0, 0, side + padding, side + padding) };
        int[] order = Enumerable.Range(0, sizes.Length)
            .OrderByDescending(index => Mathf.Max(sizes[index].x, sizes[index].y))
            .ThenByDescending(index => sizes[index].x * sizes[index].y)
            .ThenBy(index => index).ToArray();
        foreach (int index in order)
        {
            int bestShortSide = int.MaxValue;
            int bestLongSide = int.MaxValue;
            RectInt bestRect = new RectInt();
            bool rotated = false;
            foreach (RectInt free in freeRects)
            {
                for (int orientation = 0; orientation < 2; orientation++)
                {
                    int width = (orientation == 0 ? sizes[index].x : sizes[index].y) + padding;
                    int height = (orientation == 0 ? sizes[index].y : sizes[index].x) + padding;
                    if (width > free.width || height > free.height)
                    {
                        continue;
                    }
                    int shortSide = Mathf.Min(free.width - width, free.height - height);
                    int longSide = Mathf.Max(free.width - width, free.height - height);
                    if (shortSide < bestShortSide || (shortSide == bestShortSide && longSide < bestLongSide))
                    {
                        bestShortSide = shortSide;
                        bestLongSide = longSide;
                        bestRect = new RectInt(free.x, free.y, width, height);
                        rotated = orientation == 1;
                    }
                }
            }
            if (bestShortSide == int.MaxValue)
            {
                return false;
            }

            placements[index] = new RectInt(bestRect.x, bestRect.y,
                bestRect.width - padding, bestRect.height - padding);
            rotations[index] = rotated;
            SplitFreeRects(freeRects, bestRect);
        }
        return true;
    }

    private static void SplitFreeRects(List<RectInt> freeRects, RectInt used)
    {
        for (int i = freeRects.Count - 1; i >= 0; i--)
        {
            RectInt free = freeRects[i];
            if (!free.Overlaps(used))
            {
                continue;
            }

            freeRects.RemoveAt(i);
            if (used.xMin > free.xMin)
            {
                freeRects.Add(new RectInt(free.x, free.y, used.xMin - free.xMin, free.height));
            }
            if (used.xMax < free.xMax)
            {
                freeRects.Add(new RectInt(used.xMax, free.y, free.xMax - used.xMax, free.height));
            }
            if (used.yMin > free.yMin)
            {
                freeRects.Add(new RectInt(free.x, free.y, free.width, used.yMin - free.yMin));
            }
            if (used.yMax < free.yMax)
            {
                freeRects.Add(new RectInt(free.x, used.yMax, free.width, free.yMax - used.yMax));
            }
        }

        for (int i = freeRects.Count - 1; i >= 0; i--)
        {
            for (int j = 0; j < freeRects.Count; j++)
            {
                if (i != j && Contains(freeRects[j], freeRects[i]))
                {
                    freeRects.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static bool Contains(RectInt outer, RectInt inner)
    {
        return inner.xMin >= outer.xMin && inner.yMin >= outer.yMin
            && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
    }

    private static Texture2D WriteTexture(List<Texture2D> pieces, List<AtlasSpriteEntry> entries,
        Vector2Int[] sizes, RectInt[] rects, bool[] rotations, int side)
    {
        Color32[] atlasPixels = new Color32[side * side];
        for (int i = 0; i < pieces.Count; i++)
        {
            Texture2D piece = pieces[i];
            bool resized = piece.width != sizes[i].x || piece.height != sizes[i].y;
            if (resized)
            {
                piece = SpriteAtlasFrameUtility.ScaleTexture(piece, sizes[i].x, sizes[i].y);
            }
            try
            {
                Color32[] pixels = piece.GetPixels32();
                for (int y = 0; y < piece.height; y++)
                {
                    for (int x = 0; x < piece.width; x++)
                    {
                        int targetX = rects[i].x + (rotations[i] ? y : x);
                        int targetY = rects[i].y + (rotations[i] ? piece.width - 1 - x : y);
                        atlasPixels[targetY * side + targetX] = pixels[y * piece.width + x];
                    }
                }
                entries[i].atlasRect = new Rect(rects[i].x, rects[i].y, rects[i].width, rects[i].height);
                entries[i].packedRotated = rotations[i];
            }
            finally
            {
                if (resized)
                {
                    Object.DestroyImmediate(piece);
                }
            }
        }

        Texture2D atlas = new Texture2D(side, side, TextureFormat.RGBA32, false);
        atlas.SetPixels32(atlasPixels);
        atlas.Apply();
        return atlas;
    }
}
