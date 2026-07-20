using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BlockEscape.Bootstrap;
using System.Text.RegularExpressions;
using BlockEscape.Core;
using BlockEscape.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BlockEscape.Tetris.Tests
{
    public sealed class SaveServiceTests
    {
        private string _testDirectory;
        private string _savePath;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "BlockEscapeSaveTests", Guid.NewGuid().ToString("N"));
            _savePath = Path.Combine(_testDirectory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [Test]
        public void Load_MissingFileUsesDefaults()
        {
            var data = SaveService.Load(_savePath);

            Assert.That(data.version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(data.highScore, Is.Zero);
            Assert.That(data.bestSurvivalTime, Is.Zero);
            Assert.That(data.screenWidth, Is.EqualTo(1920));
            Assert.That(data.screenHeight, Is.EqualTo(1080));
        }

        [TestCase("Bootstrap", true)]
        [TestCase("MainMenu", false)]
        [TestCase("TetrisDemo", false)]
        public void AppBootstrap_RedirectsOnlyBootstrapScene(string sceneName, bool expected)
        {
            Assert.That(AppBootstrap.ShouldLoadMainMenu(sceneName), Is.EqualTo(expected));
        }

        [Test]
        public void BgmLibrary_ContainsAllFourTracks()
        {
            var trackNames = Resources.LoadAll<AudioClip>("Audio/BGM").Select(track => track.name);

            CollectionAssert.AreEquivalent(new[] { "BGM_01", "BGM_02", "BGM_03", "BGM_04" }, trackNames);
        }

        [Test]
        public void SfxLibrary_ContainsProvidedGameplayAndUiClips()
        {
            var clipNames = Resources.LoadAll<AudioClip>("Audio/SFX").Select(clip => clip.name);

            CollectionAssert.AreEquivalent(
                new[] { "click", "collected", "extra_heart", "game_over", "laser", "meteor", "player_hurt", "rocket" },
                clipNames);
        }

        [Test]
        public void BgmRandomSelection_DoesNotImmediatelyRepeatGameplayTrack()
        {
            var host = new GameObject("BGM Selection Test");
            var clips = new[]
            {
                AudioClip.Create("BGM_01", 1, 1, 44100, false),
                AudioClip.Create("BGM_03", 1, 1, 44100, false),
                AudioClip.Create("BGM_04", 1, 1, 44100, false)
            };
            try
            {
                var bootstrap = host.AddComponent<AppBootstrap>();
                var source = host.AddComponent<AudioSource>();
                source.clip = clips[0];
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(AppBootstrap).GetField("_musicSource", fields)?.SetValue(bootstrap, source);
                typeof(AppBootstrap).GetField("_gameplayTracks", fields)?.SetValue(bootstrap, clips);
                var choose = typeof(AppBootstrap).GetMethod("ChooseRandomGameplayTrack", fields);
                Assert.That(choose, Is.Not.Null);

                for (var i = 0; i < 32; i++)
                    Assert.That(choose.Invoke(bootstrap, null), Is.Not.SameAs(clips[0]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                foreach (var clip in clips)
                    UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ApplyAudioSettings_UpdatesMasterVolumeAndPublishesSettings()
        {
            var data = SaveService.Load(_savePath);
            data.masterVolume = 0.4f;
            SaveData published = null;
            SaveService.AudioSettingsChanged += Capture;
            try
            {
                SaveService.ApplyAudioSettings();

                Assert.That(AudioListener.volume, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(published, Is.SameAs(data));
            }
            finally
            {
                SaveService.AudioSettingsChanged -= Capture;
                AudioListener.volume = 1f;
            }

            void Capture(SaveData settings) => published = settings;
        }

        [Test]
        public void OptionsAudioSliders_PreviewAndCancelWithoutSaving()
        {
            var data = SaveService.Load(_savePath);
            data.masterVolume = 0.8f;
            data.musicVolume = 0.7f;
            data.sfxVolume = 0.6f;
            SaveService.ApplyAudioSettings();
            var root = new GameObject("Options Audio Test");
            try
            {
                var master = CreateSlider("Master", root.transform);
                var music = CreateSlider("Music", root.transform);
                var sfx = CreateSlider("SFX", root.transform);
                var options = root.AddComponent<OptionsMenu>();
                options.Configure(null, master, music, sfx, null, null, null, null, null, null, null);
                options.Show();

                master.value = 0.25f;
                music.value = 0.35f;
                sfx.value = 0.45f;

                Assert.That(data.masterVolume, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(data.musicVolume, Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(data.sfxVolume, Is.EqualTo(0.45f).Within(0.001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.25f).Within(0.001f));

                options.Hide();
                Assert.That(data.masterVolume, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(data.musicVolume, Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(data.sfxVolume, Is.EqualTo(0.6f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AudioListener.volume = 1f;
            }
        }

        private static Slider CreateSlider(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<Slider>();
        }

        [Test]
        public void Save_RoundTripsAndSanitizesSettings()
        {
            var data = SaveService.Load(_savePath);
            data.highScore = 1234;
            data.bestSurvivalTime = 87.5f;
            data.masterVolume = 2f;
            data.musicVolume = -1f;
            data.screenWidth = 100;
            data.screenHeight = 100;
            data.vSyncCount = 3;
            data.inputBindingOverridesJson = "{\"bindings\":[]}";
            data.hasSeenTutorial = true;

            Assert.That(SaveService.Save(), Is.True);
            var loaded = SaveService.Load(_savePath);

            Assert.That(loaded.highScore, Is.EqualTo(1234));
            Assert.That(loaded.bestSurvivalTime, Is.EqualTo(87.5f));
            Assert.That(loaded.masterVolume, Is.EqualTo(1f));
            Assert.That(loaded.musicVolume, Is.Zero);
            Assert.That(loaded.screenWidth, Is.EqualTo(640));
            Assert.That(loaded.screenHeight, Is.EqualTo(360));
            Assert.That(loaded.vSyncCount, Is.EqualTo(1));
            Assert.That(loaded.inputBindingOverridesJson, Is.EqualTo("{\"bindings\":[]}"));
            Assert.That(loaded.hasSeenTutorial, Is.True);
        }

        [Test]
        public void Load_CorruptFilePreservesItAndUsesDefaults()
        {
            Directory.CreateDirectory(_testDirectory);
            File.WriteAllText(_savePath, "{\"version\":2}");
            LogAssert.Expect(LogType.Warning, new Regex("Save file could not be loaded.*"));

            var loaded = SaveService.Load(_savePath);

            Assert.That(loaded.highScore, Is.Zero);
            Assert.That(File.Exists(_savePath), Is.False);
            Assert.That(Directory.GetFiles(_testDirectory, "save.json.corrupt-*").Length, Is.EqualTo(1));
        }

        [Test]
        public void RecordRunOnlyPersistsBetterResults()
        {
            SaveService.Load(_savePath);

            Assert.That(SaveService.RecordRun(500, 30f), Is.True);
            Assert.That(SaveService.RecordRun(400, 20f), Is.False);
            var loaded = SaveService.Load(_savePath);

            Assert.That(loaded.highScore, Is.EqualTo(500));
            Assert.That(loaded.bestSurvivalTime, Is.EqualTo(30f));
        }
    }
}
