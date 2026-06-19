using UnityEngine;

namespace BlockEscape.Tetris
{
    internal static class RuntimeVisuals
    {
        private static Sprite _square;

        public static Sprite Square
        {
            get
            {
                if (_square != null)
                    return _square;

                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "Runtime Square",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                _square.name = "Runtime Square Sprite";
                _square.hideFlags = HideFlags.HideAndDontSave;
                return _square;
            }
        }

        public static GameObject CreateQuad(string name, Transform parent, Vector3 position, Vector2 size, Color color, int order)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return gameObject;
        }
    }
}
