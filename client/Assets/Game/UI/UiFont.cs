using UnityEngine;

namespace Worms.Game.UI
{
    /// <summary>
    /// The font every menu and HUD label uses. Unity's built-in font has no Latin
    /// Extended Additional glyphs, so on the Web build Vietnamese letters with two marks
    /// (ắ, ầ, ờ, ủ, ...) came out blank: "Bắt đầu" read "B t đ u". Be Vietnam Pro (SIL OFL,
    /// Resources/Fonts) covers all of Vietnamese and is imported as a dynamic font with its
    /// data included, so it renders the same on every target.
    /// </summary>
    public static class UiFont
    {
        const string Path = "Fonts/BeVietnamPro-Regular";
        static Font _font;
        static bool _loaded;

        public static Font Get()
        {
            if (!_loaded)
            {
                _loaded = true;
                _font = Resources.Load<Font>(Path);
                if (_font == null) Debug.LogWarning("UiFont: " + Path + " not found, falling back to the built-in font");
            }
            return _font;
        }

        /// <summary>
        /// Makes the font the IMGUI default, so a control drawn without an explicit style
        /// (the game-over "Chơi lại"/"Về menu" buttons were) still gets Vietnamese letters.
        /// Call at the top of every OnGUI.
        /// </summary>
        public static void UseForSkin()
        {
            var font = Get();
            if (font != null && GUI.skin.font != font) GUI.skin.font = font;
        }

        /// <summary>Puts the UI font on these styles (no-op if it failed to load).</summary>
        public static void Apply(params GUIStyle[] styles)
        {
            var font = Get();
            if (font == null) return;
            foreach (var style in styles)
                if (style != null) style.font = font;
        }
    }
}
