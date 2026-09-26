using System.Collections.Generic;
using UnityEngine;

namespace Worms.Game.UI
{
    /// <summary>
    /// Flat, rounded look for the IMGUI controls (Unity's default skin is small grey
    /// bevels, and its slider is a hairline that is hard to grab on a phone). The
    /// rounded rectangles are generated once and 9-sliced, so no texture assets are needed.
    /// </summary>
    public static class UiSkin
    {
        public static readonly Color Accent = new Color(1f, 0.8f, 0.4f);
        static readonly Color ButtonFill = new Color(0.16f, 0.2f, 0.28f, 0.94f);
        static readonly Color ButtonHover = new Color(0.22f, 0.28f, 0.38f, 0.96f);
        static readonly Color ButtonDown = new Color(0.12f, 0.15f, 0.21f, 0.98f);
        static readonly Color Outline = new Color(1f, 1f, 1f, 0.14f);
        static readonly Color PanelFill = new Color(0.07f, 0.09f, 0.13f, 0.86f);
        static readonly Color FieldFill = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        static readonly Color TrackFill = new Color(1f, 1f, 1f, 0.18f);

        const int Radius = 12;
        static readonly Dictionary<(Color, Color), Texture2D> Cache = new Dictionary<(Color, Color), Texture2D>();

        /// <summary>A (2r+2)-pixel rounded rectangle with a 1.5 px outline; slice it with <see cref="Border"/>.</summary>
        static Texture2D Rounded(Color fill, Color outline)
        {
            if (Cache.TryGetValue((fill, outline), out var cached) && cached != null) return cached;
            int size = Radius * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UiSkin", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            };
            var px = new Color[size * size];
            float half = size / 2f, inner = half - Radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded rectangle's edge (negative inside).
                    float qx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0);
                    float qy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0);
                    float d = Mathf.Sqrt(qx * qx + qy * qy) - Radius;
                    float coverage = Mathf.Clamp01(0.5f - d);
                    float rim = Mathf.Clamp01(d + 2f); // 1 in the outer 1.5 px
                    var c = Color.Lerp(fill, new Color(
                        Mathf.Lerp(fill.r, outline.r, outline.a), Mathf.Lerp(fill.g, outline.g, outline.a),
                        Mathf.Lerp(fill.b, outline.b, outline.a), Mathf.Max(fill.a, outline.a)), rim);
                    c.a *= coverage;
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            Cache[(fill, outline)] = tex;
            return tex;
        }

        static RectOffset Border => new RectOffset(Radius, Radius, Radius, Radius);

        static void Fill(GUIStyleState state, Texture2D background, Color text)
        {
            state.background = background;
            state.textColor = text;
        }

        public static void Button(GUIStyle s)
        {
            s.border = Border;
            s.padding = new RectOffset(14, 14, 6, 6);
            s.alignment = TextAnchor.MiddleCenter;
            Fill(s.normal, Rounded(ButtonFill, Outline), Color.white);
            Fill(s.hover, Rounded(ButtonHover, new Color(Accent.r, Accent.g, Accent.b, 0.55f)), Color.white);
            Fill(s.active, Rounded(ButtonDown, Accent), Accent);
            Fill(s.focused, Rounded(ButtonFill, Outline), Color.white);
            Fill(s.onNormal, Rounded(ButtonHover, Accent), Accent);
            Fill(s.onHover, Rounded(ButtonHover, Accent), Accent);
            Fill(s.onActive, Rounded(ButtonDown, Accent), Accent);
        }

        public static void Panel(GUIStyle s)
        {
            s.border = Border;
            s.padding = new RectOffset(16, 16, 12, 12);
            Fill(s.normal, Rounded(PanelFill, Outline), Color.white);
        }

        public static void Field(GUIStyle s)
        {
            s.border = Border;
            s.padding = new RectOffset(12, 12, 6, 6);
            var bg = Rounded(FieldFill, Outline);
            Fill(s.normal, bg, Color.white);
            Fill(s.hover, bg, Color.white);
            Fill(s.focused, Rounded(FieldFill, Accent), Color.white);
            Fill(s.active, Rounded(FieldFill, Accent), Color.white);
        }

        /// <summary>
        /// Track and thumb styles for <c>GUI.HorizontalSlider</c> at UI unit <paramref name="u"/>.
        /// Give the slider a rect <paramref name="height"/> tall: the thumb fills it (a finger-sized
        /// knob) and the track is drawn as a thin bar through its middle.
        /// </summary>
        public static (GUIStyle track, GUIStyle thumb, float height) Slider(float u)
        {
            float knob = Mathf.Round(Mathf.Max(18f, u * 1.05f));
            int inset = Mathf.RoundToInt((knob - Mathf.Max(6f, u * 0.3f)) / 2f);
            var track = new GUIStyle { border = Border, overflow = new RectOffset(0, 0, -inset, -inset) };
            Fill(track.normal, Rounded(TrackFill, Color.clear), Color.white);
            var thumb = new GUIStyle { border = Border, fixedWidth = knob, fixedHeight = knob };
            Fill(thumb.normal, Rounded(Accent, new Color(1f, 1f, 1f, 0.6f)), Color.white);
            var lit = Rounded(Color.Lerp(Accent, Color.white, 0.25f), Color.white);
            Fill(thumb.hover, lit, Color.white);
            Fill(thumb.active, lit, Color.white);
            return (track, thumb, knob);
        }
    }
}
