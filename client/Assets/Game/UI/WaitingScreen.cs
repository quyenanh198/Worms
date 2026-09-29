using UnityEngine;

namespace Worms.Game.UI
{
    /// <summary>Randomized opening card with a separate, clickable start button.</summary>
    public sealed class WaitingScreen : MonoBehaviour
    {
        public bool Visible = true;
        Texture2D _scene;
        Texture2D _start;

        void Awake()
        {
            string root = Random.Range(0, 2) == 1 ? "Waiting/Neon/" : "Waiting/Coastal/";
            _scene = Resources.Load<Texture2D>(root + "scene");
            _start = Resources.Load<Texture2D>(root + "start");
        }

        void Update()
        {
            if (Visible && Input.anyKeyDown) Visible = false;
        }

        void OnGUI()
        {
            if (!Visible || _scene == null) return;
            int oldDepth = GUI.depth;
            GUI.depth = -1000;
            var oldColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            float scale = Mathf.Min((float)Screen.width / _scene.width,
                (float)Screen.height / _scene.height);
            var image = new Rect((Screen.width - _scene.width * scale) * 0.5f,
                (Screen.height - _scene.height * scale) * 0.5f,
                _scene.width * scale, _scene.height * scale);
            GUI.DrawTexture(image, _scene, ScaleMode.StretchToFill, false);

            // Match the painted button's position in the supplied 16:9 composition.
            var button = new Rect(image.x + image.width * 0.325f,
                image.y + image.height * 0.815f,
                image.width * 0.35f, image.height * 0.145f);
            if (_start != null)
                GUI.DrawTexture(button, _start, ScaleMode.ScaleToFit, true);
            if (GUI.Button(button, GUIContent.none, GUIStyle.none)) Visible = false;
            GUI.depth = oldDepth;
        }
    }
}
