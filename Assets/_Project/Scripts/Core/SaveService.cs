using System;
using System.IO;
using UnityEngine;

namespace BlockEscape.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int highScore;
        public float bestSurvivalTime;
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        public bool fullscreen = true;
        public int screenWidth = 1920;
        public int screenHeight = 1080;
        public int vSyncCount = 1;
        public string inputBindingOverridesJson = string.Empty;
        public bool hasSeenTutorial;

        public void Sanitize()
        {
            version = CurrentVersion;
            highScore = Mathf.Max(0, highScore);
            bestSurvivalTime = Mathf.Max(0f, bestSurvivalTime);
            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            screenWidth = Mathf.Max(640, screenWidth);
            screenHeight = Mathf.Max(360, screenHeight);
            vSyncCount = vSyncCount > 0 ? 1 : 0;
            inputBindingOverridesJson ??= string.Empty;
        }
    }

    public static class SaveService
    {
        private const string FileName = "blockescape-save.json";
        private static string _savePath;

        public static event Action<SaveData> AudioSettingsChanged;
        public static SaveData Data { get; private set; } = new SaveData();
        public static string SavePath => _savePath ?? Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Load(string path = null)
        {
            _savePath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Application.persistentDataPath, FileName)
                : path;

            if (!File.Exists(_savePath))
            {
                Data = new SaveData();
                return Data;
            }

            try
            {
                var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(_savePath));
                if (loaded == null || loaded.version != SaveData.CurrentVersion)
                    throw new InvalidDataException("Unsupported or empty save data.");

                loaded.Sanitize();
                Data = loaded;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidDataException)
            {
                Debug.LogWarning($"Save file could not be loaded. Defaults will be used. {exception.Message}");
                BackupCorruptFile(_savePath);
                Data = new SaveData();
            }

            return Data;
        }

        public static bool Save(string path = null)
        {
            if (!string.IsNullOrWhiteSpace(path))
                _savePath = path;
            if (string.IsNullOrWhiteSpace(_savePath))
                _savePath = Path.Combine(Application.persistentDataPath, FileName);

            Data ??= new SaveData();
            Data.Sanitize();
            var temporaryPath = _savePath + ".tmp";

            try
            {
                var directory = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(temporaryPath, JsonUtility.ToJson(Data, true));
                if (File.Exists(_savePath))
                    File.Replace(temporaryPath, _savePath, null);
                else
                    File.Move(temporaryPath, _savePath);
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                Debug.LogWarning($"Save file could not be written. {exception.Message}");
                TryDelete(temporaryPath);
                return false;
            }
        }

        public static bool RecordRun(int score, float survivalTime)
        {
            var changed = false;
            if (score > Data.highScore)
            {
                Data.highScore = score;
                changed = true;
            }

            if (survivalTime > Data.bestSurvivalTime)
            {
                Data.bestSurvivalTime = survivalTime;
                changed = true;
            }

            return changed && Save();
        }

        public static void ApplyDisplaySettings()
        {
            Data.Sanitize();
            var supported = Screen.resolutions;
            if (supported.Length > 0)
            {
                var found = false;
                foreach (var resolution in supported)
                {
                    if (resolution.width != Data.screenWidth || resolution.height != Data.screenHeight)
                        continue;
                    found = true;
                    break;
                }

                if (!found)
                {
                    Data.screenWidth = Screen.currentResolution.width;
                    Data.screenHeight = Screen.currentResolution.height;
                }
            }

            QualitySettings.vSyncCount = Data.vSyncCount;
            Screen.SetResolution(Data.screenWidth, Data.screenHeight, Data.fullscreen);
        }

        public static void ApplyAudioSettings()
        {
            Data.Sanitize();
            AudioListener.volume = Data.masterVolume;
            AudioSettingsChanged?.Invoke(Data);
        }

        private static void BackupCorruptFile(string path)
        {
            if (!File.Exists(path))
                return;

            try
            {
                var backupPath = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}";
                File.Move(path, backupPath);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                Debug.LogWarning($"Corrupt save file could not be preserved. {exception.Message}");
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                Debug.LogWarning($"Temporary save file could not be removed. {exception.Message}");
            }
        }
    }
}
