using UnityEngine;

namespace Worms.Game.UI
{
    /// <summary>Randomized opening card made from the player's supplied cutouts.</summary>
    public sealed class WaitingScreen : MonoBehaviour
    {
        public bool Visible = true;
        bool _neon;
        Texture2D[] _art;
        Texture2D _hat;
        Texture2D _background;

        void Awake()
        {
            _neon = Random.Range(0, 2) == 1;
            string root = _neon ? "Waiting/Neon/" : "Waiting/Coastal/";
            _background = Resources.Load<Texture2D>(root + "background");
            string[] names = _neon
                ? new[] { "sky", "platform", "worm-logo", "battle-logo", "start", "green-worm",
                    "skyscraper-left", "skyscraper-right", "vehicle", "plants" }
                : new[] { "sky", "ocean", "platform", "logo", "start", "red-worm",
                    "green-worm", "mountain", "tree" };
            _art = new Texture2D[names.Length];
            for (int i = 0; i < names.Length; i++)
                if (_background == null || (_neon ? i >= 2 && i <= 5 : i >= 2 && i <= 6))
                    _art[i] = Resources.Load<Texture2D>(root + names[i]);
            _hat = Resources.Load<Texture2D>(_neon ? "Cosmetics/neon-blue-helmet" : "Cosmetics/coastal-green-helmet");
        }

        void Update()
        {
            if (Visible && Input.anyKeyDown) Visible = false;
        }

        static void Art(Texture2D texture, float x, float y, float width, float height)
        {
            if (texture == null) return;
            GUI.DrawTexture(new Rect(x * Screen.width, y * Screen.height,
                width * Screen.width, height * Screen.height), texture, ScaleMode.ScaleToFit, true);
        }

        void OnGUI()
        {
            if (!Visible || _art == null) return;
            int oldDepth = GUI.depth;
            GUI.depth = -1000;
            bool portrait = Screen.height > Screen.width;
            var oldColor = GUI.color;
            GUI.color = _neon ? new Color(0.025f, 0.025f, 0.10f) : new Color(0.30f, 0.67f, 0.96f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            if (_background != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _background, ScaleMode.ScaleAndCrop, false);

            if (_neon)
            {
                if (_background == null)
                {
                    Art(_art[0], 0f, 0f, 1f, portrait ? 0.54f : 0.66f);
                    Art(_art[6], portrait ? -0.02f : 0.06f, 0.26f, portrait ? 0.28f : 0.14f, 0.38f);
                    Art(_art[7], portrait ? 0.77f : 0.82f, 0.20f, portrait ? 0.27f : 0.14f, 0.44f);
                    Art(_art[8], 0.73f, 0.19f, portrait ? 0.18f : 0.11f, 0.11f);
                    Art(_art[9], 0f, 0.60f, portrait ? 0.43f : 0.22f, 0.24f);
                    Art(_art[1], 0f, portrait ? 0.70f : 0.68f, 1f, portrait ? 0.26f : 0.31f);
                }
                Art(_art[5], portrait ? 0.33f : 0.65f, portrait ? 0.43f : 0.43f,
                    portrait ? 0.35f : 0.17f, portrait ? 0.31f : 0.32f);
                Art(_hat, portrait ? 0.39f : 0.685f, portrait ? 0.41f : 0.41f,
                    portrait ? 0.23f : 0.10f, portrait ? 0.12f : 0.13f);
                Art(_art[2], portrait ? 0.09f : 0.31f, 0.05f, portrait ? 0.82f : 0.38f, portrait ? 0.19f : 0.22f);
                Art(_art[3], portrait ? 0.04f : 0.30f, portrait ? 0.19f : 0.21f,
                    portrait ? 0.92f : 0.42f, portrait ? 0.21f : 0.26f);
                Art(_art[4], portrait ? 0.14f : 0.36f, portrait ? 0.81f : 0.80f,
                    portrait ? 0.72f : 0.28f, 0.12f);
            }
            else
            {
                if (_background == null)
                {
                    Art(_art[0], 0f, 0f, 1f, portrait ? 0.31f : 0.32f);
                    Art(_art[7], 0.01f, portrait ? 0.25f : 0.26f, portrait ? 0.62f : 0.38f, 0.28f);
                    Art(_art[1], 0.27f, portrait ? 0.36f : 0.32f, portrait ? 0.69f : 0.52f, 0.27f);
                    Art(_art[8], portrait ? -0.15f : 0.02f, 0.39f, portrait ? 0.40f : 0.20f, 0.39f);
                }
                Art(_art[2], 0f, portrait ? 0.70f : 0.66f, 1f, portrait ? 0.27f : 0.33f);
                Art(_art[5], portrait ? 0.17f : 0.58f, portrait ? 0.49f : 0.43f,
                    portrait ? 0.33f : 0.17f, portrait ? 0.26f : 0.32f);
                Art(_hat, portrait ? 0.22f : 0.62f, portrait ? 0.47f : 0.41f,
                    portrait ? 0.22f : 0.10f, portrait ? 0.11f : 0.13f);
                Art(_art[6], portrait ? 0.56f : 0.79f, portrait ? 0.49f : 0.43f,
                    portrait ? 0.31f : 0.15f, portrait ? 0.26f : 0.32f);
                Art(_art[3], portrait ? 0.07f : 0.32f, 0.03f, portrait ? 0.86f : 0.36f,
                    portrait ? 0.28f : 0.31f);
                Art(_art[4], portrait ? 0.16f : 0.37f, portrait ? 0.81f : 0.80f,
                    portrait ? 0.68f : 0.26f, 0.12f);
            }

            var button = new Rect(Screen.width * (portrait ? 0.12f : 0.35f), Screen.height * 0.78f,
                Screen.width * (portrait ? 0.76f : 0.30f), Screen.height * 0.17f);
            if (GUI.Button(button, GUIContent.none, GUIStyle.none)) Visible = false;
            GUI.depth = oldDepth;
        }
    }
}
