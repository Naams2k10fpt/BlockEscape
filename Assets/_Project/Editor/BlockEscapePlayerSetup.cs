using BlockEscape.Player;
using UnityEditor;
using UnityEngine;

namespace BlockEscape.Editor
{
    public static class BlockEscapePlayerSetup
    {
        private const string ConfigPath = "Assets/_Project/Resources/PlayerConfig.asset";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string SquareAssetPath = "Assets/_Project/Art/GeneratedSquare.asset";

        [MenuItem("Block Escape/Build Player Prefab")]
        public static void BuildPlayerPrefab()
        {
            EnsureFolders();
            EnsureLayer("Player");

            var config = CreateOrLoadConfig();
            var player = CreatePlayer(config);
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            Object.DestroyImmediate(player);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Player prefab built successfully: {PrefabPath}");
        }

        private static GameObject CreatePlayer(PlayerConfig config)
        {
            var player = new GameObject("Player");
            player.layer = LayerOrDefault("Player");

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.72f, 1.45f);
            collider.offset = new Vector2(0f, -0.02f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            visual.transform.localScale = new Vector3(0.72f, 1.45f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateOrLoadSquareSprite();
            renderer.color = new Color(0.95f, 0.86f, 0.30f);
            renderer.sortingOrder = 25;
            visual.AddComponent<Animator>();

            var groundCheck = new GameObject("Ground Check");
            groundCheck.transform.SetParent(player.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, config.groundCheckOffsetY, 0f);

            var controller = player.AddComponent<PlayerController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("_config").objectReferenceValue = config;
            controllerData.FindProperty("_groundCheck").objectReferenceValue = groundCheck.transform;
            controllerData.FindProperty("_groundMask").intValue = LayerMask.GetMask("World");
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        private static PlayerConfig CreateOrLoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<PlayerConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerConfig>();
                config.name = "Player Config";
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.Sanitize();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static Sprite CreateOrLoadSquareSprite()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(SquareAssetPath))
                if (asset is Sprite sprite) return sprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Generated White Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, SquareAssetPath);

            var square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            square.name = "Generated Square Sprite";
            AssetDatabase.AddObjectToAsset(square, texture);
            AssetDatabase.SaveAssets();
            return square;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder("Assets/_Project/Prefabs/Player");
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void EnsureLayer(string layerName)
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serializedObject = new SerializedObject(tagManager);
            var layers = serializedObject.FindProperty("layers");
            for (var i = 8; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                    return;
            }

            for (var i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue))
                    continue;

                layer.stringValue = layerName;
                serializedObject.ApplyModifiedProperties();
                return;
            }

            Debug.LogWarning("No free user layer slot for " + layerName);
        }

        private static int LayerOrDefault(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? layer : 0;
        }
    }
}
