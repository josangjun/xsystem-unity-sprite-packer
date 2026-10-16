using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpriteAtlasRotationMetadata
{
    private const string Marker = "XSystemSpriteAtlasRotation:";

    public List<SpriteAtlasRotationFrame> frames = new List<SpriteAtlasRotationFrame>();

    public static SpriteAtlasRotationMetadata Read(string userData)
    {
        int start = FindMarker(userData);
        if (start < 0)
        {
            return null;
        }

        int end = userData.IndexOf('\n', start);
        string json = end < 0 ? userData.Substring(start + Marker.Length)
            : userData.Substring(start + Marker.Length, end - start - Marker.Length);
        return JsonUtility.FromJson<SpriteAtlasRotationMetadata>(json);
    }

    public string Write(string userData)
    {
        userData = userData ?? string.Empty;
        int start = FindMarker(userData);
        if (start >= 0)
        {
            int end = userData.IndexOf('\n', start);
            userData = userData.Remove(start, (end < 0 ? userData.Length : end) - start);
        }

        return userData + (userData.Length == 0 || userData.EndsWith("\n") ? "" : "\n")
            + Marker + JsonUtility.ToJson(this);
    }

    private static int FindMarker(string userData)
    {
        if (string.IsNullOrEmpty(userData))
        {
            return -1;
        }

        if (userData.StartsWith(Marker, System.StringComparison.Ordinal))
        {
            return 0;
        }

        int index = userData.IndexOf("\n" + Marker, System.StringComparison.Ordinal);
        return index >= 0 ? index + 1 : -1;
    }
}
