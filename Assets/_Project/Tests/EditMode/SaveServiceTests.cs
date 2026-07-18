using System;
using System.IO;
using System.Text.RegularExpressions;
using BlockEscape.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            Assert.That(SaveService.Save(), Is.True);
            var loaded = SaveService.Load(_savePath);

            Assert.That(loaded.highScore, Is.EqualTo(1234));
            Assert.That(loaded.bestSurvivalTime, Is.EqualTo(87.5f));
            Assert.That(loaded.masterVolume, Is.EqualTo(1f));
            Assert.That(loaded.musicVolume, Is.Zero);
            Assert.That(loaded.screenWidth, Is.EqualTo(640));
            Assert.That(loaded.screenHeight, Is.EqualTo(360));
            Assert.That(loaded.vSyncCount, Is.EqualTo(1));
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
