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
        private const string PlayerSpritePath = "Assets/_Project/Resources/Art/PlayerRunner.png";

        [MenuItem("Block Escape/Build Player Prefab")]
        public static void BuildPlayerPrefab()
        {
            EnsureFolders();
            EnsureLayer("Player");
            ConfigurePlayerSprite();

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
            body.gravityScale = config.gravityScale;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = config.standingColliderSize;
            collider.offset = config.standingColliderOffset;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            var playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            visual.transform.localScale = playerSprite != null
                ? Vector3.one
                : new Vector3(0.72f, 1.45f, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = playerSprite != null ? playerSprite : CreateOrLoadSquareSprite();
            renderer.color = playerSprite != null ? Color.white : new Color(0.95f, 0.86f, 0.30f);
            renderer.sortingOrder = 25;
            var groundCheck = new GameObject("Ground Check");
            groundCheck.transform.SetParent(player.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, config.groundCheckOffsetY, 0f);

            var controller = player.AddComponent<PlayerController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("_config").objectReferenceValue = config;
            controllerData.FindProperty("_groundCheck").objectReferenceValue = groundCheck.transform;
            controllerData.FindProperty("_groundMask").intValue = LayerMask.GetMask("World");
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            var health = player.AddComponent<PlayerHealth>();
            player.AddComponent<PlayerPresentation>();
            var healthData = new SerializedObject(health);
            healthData.FindProperty("_spriteRenderer").objectReferenceValue = renderer;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        private static void ConfigurePlayerSprite()
        {
            AssetDatabase.Refresh();
            if (AssetImporter.GetAtPath(PlayerSpritePath) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
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
