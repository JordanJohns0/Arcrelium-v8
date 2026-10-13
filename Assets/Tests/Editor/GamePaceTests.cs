using NUnit.Framework;
using UnityEngine;
using Game;

namespace Tests.Game
{
    [TestFixture]
    public class GamePaceTests
    {
        private float savedManual;

        [SetUp]
        public void SetUp()
        {
            savedManual = PlayerPrefs.GetFloat(GamePace.PrefKey, 1f);
            GamePace.ManualMultiplier = 1f;
            GamePace.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetFloat(GamePace.PrefKey, savedManual);
            GamePace.ResetManualCache();
            GamePace.Reset();
        }

        [Test]
        public void AutoSpeedFor_FewEnemies_StaysAtBase()
        {
            Assert.AreEqual(1f, GamePace.AutoSpeedFor(0), 0.0001f);
            Assert.AreEqual(1f, GamePace.AutoSpeedFor(2), 0.0001f);
        }

        [Test]
        public void AutoSpeedFor_MoreEnemies_RampsAndCaps()
        {
            Assert.AreEqual(2f, GamePace.AutoSpeedFor(4), 0.0001f);
            Assert.AreEqual(3f, GamePace.AutoSpeedFor(6), 0.0001f);
            Assert.AreEqual(4f, GamePace.AutoSpeedFor(20), 0.0001f, "Auto speed caps at 4x.");
        }

        [Test]
        public void Update_MultipliesAutoSpeedByManualOverride()
        {
            GamePace.ManualMultiplier = 2f;
            GamePace.Update(4); // auto 2x * manual 2x
            Assert.AreEqual(4f, GamePace.Speed, 0.0001f);
        }

        [Test]
        public void Update_ClampsToMaxSpeed()
        {
            GamePace.ManualMultiplier = 3f;
            GamePace.Update(20); // auto 4x * 3x = 12x -> hard cap 6x
            Assert.AreEqual(6f, GamePace.Speed, 0.0001f);
        }

        [Test]
        public void Scale_DividesDelayBySpeed()
        {
            GamePace.Update(4); // 2x
            Assert.AreEqual(0.5f, GamePace.Scale(1f), 0.0001f);
        }

        [Test]
        public void Reset_RestoresBaseSpeed()
        {
            GamePace.Update(10);
            Assert.Greater(GamePace.Speed, 1f);
            GamePace.Reset();
            Assert.AreEqual(1f, GamePace.Speed, 0.0001f);
        }

        [Test]
        public void ManualMultiplier_PersistsToPlayerPrefs()
        {
            GamePace.ManualMultiplier = 1.5f;
            GamePace.ResetManualCache();
            Assert.AreEqual(1.5f, GamePace.ManualMultiplier, 0.0001f);
        }
    }
}
