using UnityEngine;

namespace BiomeLords.Util
{
    /// <summary>
    /// Builds a new Sprite from a source Sprite by shifting every visible
    /// pixel's hue toward a target color (and boosting saturation), while
    /// preserving the original luminance (V) and alpha.
    ///
    /// Multiplicative tints make most vanilla icon textures look muddy and
    /// dark because the source pixels are already desaturated. A hue shift
    /// forces a vibrant recolor while keeping the original shape readable.
    /// </summary>
    public static class SpriteTinter
    {
        public static Sprite Tint(Sprite source, Color targetHueColor)
        {
            if (source == null) return null;
            try
            {
                var srcTex = source.texture;
                if (srcTex == null) return source;

                // Round-trip through a RenderTexture to make non-readable
                // textures readable.
                var rt = RenderTexture.GetTemporary(
                    srcTex.width, srcTex.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

                var prev = RenderTexture.active;
                Graphics.Blit(srcTex, rt);
                RenderTexture.active = rt;

                var copy = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);

                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);

                Color.RGBToHSV(targetHueColor, out float targetH, out _, out _);

                var pixels = copy.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    if (p.a < 0.02f) continue; // skip nearly-transparent pixels

                    Color.RGBToHSV(p, out float _, out float s, out float v);
                    // Force vibrant target hue + minimum saturation, keep value.
                    float newS = Mathf.Max(s, 0.75f);
                    var shifted = Color.HSVToRGB(targetH, newS, v);
                    shifted.a = p.a;
                    pixels[i] = shifted;
                }
                copy.SetPixels(pixels);
                copy.Apply();

                // Preserve original sprite rect so the icon places correctly
                // when the source was atlas-packed.
                return Sprite.Create(copy, source.rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] SpriteTinter failed: {ex.Message}");
                return source;
            }
        }
    }
}
