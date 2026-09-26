using UnityEngine;
using Worms.Game.Core;
using Worms.Game.Play;

namespace Worms.Game.Render
{
    /// <summary>
    /// Chooses the graphics tier (docs/PLAN.md §3.14): the player's choice if
    /// set, otherwise a platform default refined by a short benchmark during
    /// the first match, remembered in PlayerPrefs.
    /// </summary>
    public static class QualitySettingsManager
    {
        const string PrefChoice = "worms.tier.choice"; // -1 auto, 0..2 fixed
        const string PrefMeasured = "worms.tier.measured"; // -1 unknown

        static TierPicker _picker;

        public static int Choice
        {
            get { return PlayerPrefs.GetInt(PrefChoice, -1); }
            set { PlayerPrefs.SetInt(PrefChoice, value); PlayerPrefs.Save(); }
        }

        public static QualityTier Current
        {
            get
            {
                int choice = Choice;
                if (choice >= 0) return (QualityTier)choice;
                int measured = PlayerPrefs.GetInt(PrefMeasured, -1);
                if (measured >= 0) return (QualityTier)measured;
                return PlatformDefault;
            }
        }

        static QualityTier PlatformDefault
        {
            get
            {
                bool web = Application.platform == RuntimePlatform.WebGLPlayer;
                if (Application.isMobilePlatform) return web ? QualityTier.Low : QualityTier.Medium;
                return QualityTier.High;
            }
        }

        public static string Label(int choice)
        {
            switch (choice)
            {
                case 0: return "Thấp";
                case 1: return "Vừa";
                case 2: return "Cao";
                default: return "Tự động (" + Label((int)Current) + ")";
            }
        }

        public static void Apply(MatchPresenter presenter)
        {
            var tier = Current;
            UrpSetup.ApplyTier(tier, presenter.Sun);
            presenter.Vfx.Density = tier == QualityTier.Low ? 0.5f : tier == QualityTier.Medium ? 1f : 1.5f;
        }

        /// <summary>Call once per frame during a match; returns true when the benchmark changed the tier.</summary>
        public static bool Sample(float dt)
        {
            if (Choice >= 0 || PlayerPrefs.GetInt(PrefMeasured, -1) >= 0) return false;
            _picker ??= new TierPicker();
            _picker.Add(dt);
            if (!_picker.Done) return false;
            int tier = TierPicker.Pick(_picker.AverageMs, Application.isMobilePlatform);
            // Only step down from the default; a fast device keeps the default.
            tier = Mathf.Min(tier, (int)PlatformDefault);
            PlayerPrefs.SetInt(PrefMeasured, tier);
            PlayerPrefs.Save();
            Debug.Log($"Quality benchmark: {_picker.AverageMs:0.0} ms/frame -> tier {tier}");
            return true;
        }
    }
}
