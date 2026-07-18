using System.Collections.Generic;
using BlockEscape.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace BlockEscape.Editor
{
    public static class BlockEscapeArenaSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/ArenaSandbox.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/Arena/Arena.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string SquareAssetPath = "Assets/_Project/Art/GeneratedSquare.asset";
        private const string GroundTilePath = "Assets/_Project/Tilemaps/Tiles/ArenaGroundTile.asset";
        private const string PlatformTilePath = "Assets/_Project/Tilemaps/Tiles/ArenaPlatformTile.asset";
        private const string ObstacleTilePath = "Assets/_Project/Tilemaps/Tiles/ArenaObstacleTile.asset";
        private const string DecorationTilePath = "Assets/_Project/Tilemaps/Tiles/ArenaDecorationTile.asset";

        [MenuItem("Block Escape/Build Arena Sandbox")]
        public static void BuildArenaSandbox()
        {
            EnsureFolders();
            EnsureLayers();

            var sprite = CreateOrLoadSquareSprite();
            var groundTile = CreateOrUpdateTile(GroundTilePath, sprite, new Color(0.30f, 0.50f, 0.42f), Tile.ColliderType.Grid);
            var platformTile = CreateOrUpdateTile(PlatformTilePath, sprite, new Color(0.28f, 0.40f, 0.62f), Tile.ColliderType.Grid);
            var obstacleTile = CreateOrUpdateTile(ObstacleTilePath, sprite, new Color(0.88f, 0.24f, 0.28f), Tile.ColliderType.Grid);
            var decorationTile = CreateOrUpdateTile(DecorationTilePath, sprite, new Color(0.16f, 0.18f, 0.26f), Tile.ColliderType.None);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ArenaSandbox";
            CreateCamera();

            var arena = CreateArena(groundTile, platformTile, obstacleTile, decorationTile);
            PrefabUtility.SaveAsPrefabAssetAndConnect(arena, PrefabPath, InteractionMode.AutomatedAction);
            CreateInputService();
            CreatePlayablePlayer(arena.transform.Find("Spawn Points/Player Spawn"));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = arena;
            Debug.Log($"Arena sandbox built successfully: {ScenePath}");
        }

        private static void CreateInputService()
        {
            var inputObject = new GameObject("Input Service (Persistent)");
            inputObject.AddComponent<InputService>();
        }

        private static void CreatePlayablePlayer(Transform spawn)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                CreatePhysicsPlayerPlaceholder(spawn);
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = spawn != null ? spawn.position + Vector3.up : new Vector3(-8f, -7f, 0f);
        }

        private static GameObject CreateArena(TileBase groundTile, TileBase platformTile, TileBase obstacleTile, TileBase decorationTile)
        {
            var arena = new GameObject("Arena");

            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(arena.transform, false);
            var grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            CreateTilemap("Ground Tilemap", gridObject.transform, groundTile, GroundCells(), "World", 0, true, false);
            CreateTilemap("Platform Tilemap", gridObject.transform, platformTile, PlatformCells(), "World", 1, true, false);
            CreateTilemap("Obstacle Tilemap", gridObject.transform, obstacleTile, ObstacleCells(), "Hazard", 2, true, true);
            CreateTilemap("Decoration Tilemap", gridObject.transform, decorationTile, DecorationCells(), "Default", -2, false, false);

            var spawns = new GameObject("Spawn Points");
            spawns.transform.SetParent(arena.transform, false);
            CreateMarker("Player Spawn", spawns.transform, new Vector3(-8f, -8f, 0f));
            CreateMarker("Drone Left", spawns.transform, new Vector3(-9f, 5f, 0f));
            CreateMarker("Drone Right", spawns.transform, new Vector3(9f, 5f, 0f));

            var killZone = new GameObject("Kill Zone");
            killZone.transform.SetParent(arena.transform, false);
            killZone.layer = LayerOrDefault("Sensor");
            killZone.transform.localPosition = new Vector3(0f, -15f, 0f);
            var collider = killZone.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(26f, 2f);

            return arena;
        }

        private static void CreateTilemap(
            string name,
            Transform parent,
            TileBase tile,
            IEnumerable<Vector3Int> cells,
            string layer,
            int sortingOrder,
            bool hasCollider,
            bool isTrigger)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.layer = LayerOrDefault(layer);

            var tilemap = gameObject.AddComponent<Tilemap>();
            foreach (var cell in cells)
                tilemap.SetTile(cell, tile);

            var renderer = gameObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (!hasCollider)
                return;

            var tilemapCollider = gameObject.AddComponent<TilemapCollider2D>();
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            var composite = gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.isTrigger = isTrigger;
#pragma warning disable 618
            tilemapCollider.usedByComposite = true;
#pragma warning restore 618
        }

        private static IEnumerable<Vector3Int> GroundCells()
        {
            for (var x = -11; x <= 11; x++)
                yield return new Vector3Int(x, -12, 0);

            for (var x = -11; x <= -8; x++)
                yield return new Vector3Int(x, -9, 0);

            for (var x = 8; x <= 11; x++)
                yield return new Vector3Int(x, -9, 0);

            for (var x = -4; x <= -2; x++)
                yield return new Vector3Int(x, -8, 0);
        }

        private static IEnumerable<Vector3Int> PlatformCells()
        {
            for (var x = -6; x <= -4; x++)
                yield return new Vector3Int(x, -3, 0);

            for (var x = 3; x <= 5; x++)
                yield return new Vector3Int(x, 0, 0);

            for (var x = -1; x <= 1; x++)
                yield return new Vector3Int(x, 4, 0);
        }

        private static IEnumerable<Vector3Int> ObstacleCells()
        {
            yield return new Vector3Int(-1, -11, 0);
            yield return new Vector3Int(0, -11, 0);
            yield return new Vector3Int(1, -11, 0);
            yield return new Vector3Int(6, -8, 0);
        }

        private static IEnumerable<Vector3Int> DecorationCells()
        {
            for (var y = -10; y <= 10; y++)
            {
                yield return new Vector3Int(-7, y, 0);
                yield return new Vector3Int(7, y, 0);
            }

            for (var x = -7; x <= 7; x++)
            {
                yield return new Vector3Int(x, -10, 0);
                yield return new Vector3Int(x, 10, 0);
            }
        }

        private static void CreateMarker(string name, Transform parent, Vector3 position)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
        }

        private static void CreatePhysicsPlayerPlaceholder(Transform spawn)
        {
            var placeholder = new GameObject("Player Placeholder (Physics Test)");
            placeholder.layer = LayerOrDefault("Player");
            placeholder.transform.position = spawn != null ? spawn.position + Vector3.up : new Vector3(-8f, -7f, 0f);
            placeholder.transform.localScale = new Vector3(0.72f, 1.45f, 1f);

            var renderer = placeholder.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateOrLoadSquareSprite();
            renderer.color = new Color(0.95f, 0.86f, 0.30f);
            renderer.sortingOrder = 10;

            var body = placeholder.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = true;

            var collider = placeholder.AddComponent<CapsuleCollider2D>();
            collider.size = Vector2.one;
            collider.direction = CapsuleDirection2D.Vertical;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, -2f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 14f;
            camera.backgroundColor = new Color(0.04f, 0.045f, 0.065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static Tile CreateOrUpdateTile(string path, Sprite sprite, Color color, Tile.ColliderType colliderType)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = sprite;
            tile.color = color;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
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
            EnsureFolder("Assets/_Project/Scenes/Sandbox");
            EnsureFolder("Assets/_Project/Prefabs/Arena");
            EnsureFolder("Assets/_Project/Tilemaps/Tiles");
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

        private static void EnsureLayers()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serializedObject = new SerializedObject(tagManager);
            var layers = serializedObject.FindProperty("layers");
            AddLayer(layers, "World");
            AddLayer(layers, "FallingBlock");
            AddLayer(layers, "Player");
            AddLayer(layers, "Enemy");
            AddLayer(layers, "Hazard");
            AddLayer(layers, "Pickup");
            AddLayer(layers, "Sensor");
            serializedObject.ApplyModifiedProperties();
        }

        private static void AddLayer(SerializedProperty layers, string layerName)
        {
            for (var i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

            for (var i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
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
