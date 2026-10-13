using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Characters;
using UI;
using Game;

namespace Tests.UI
{
    [TestFixture]
    public class CharacterHoverHealthBarTests
    {
        private static readonly FieldInfo fillField =
            typeof(CharacterHoverHealthBar).GetField("fillRect", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo blockField =
            typeof(CharacterHoverHealthBar).GetField("blockRect", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo labelField =
            typeof(CharacterHoverHealthBar).GetField("healthLabel", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly System.Collections.Generic.List<GameObject> owned =
            new System.Collections.Generic.List<GameObject>();

        private CharacterHoverHealthBar bar;

        [SetUp]
        public void SetUp()
        {
            GameState.LocalPlayerId = 0;
            var go = new GameObject("Hover Health Bar", typeof(CharacterHoverHealthBar));
            owned.Add(go);
            bar = go.GetComponent<CharacterHoverHealthBar>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            owned.Clear();
        }

        private Character Enemy(int q = 0) => Track(TestHelper.CreateCharacter(1, q, 0, 1));

        private Character Ally(int q = 0) => Track(TestHelper.CreateCharacter(0, q, 0, 0));

        private Character Track(Character character)
        {
            owned.Add(character.gameObject);
            if (character.CurrentCell != null) owned.Add(character.CurrentCell.gameObject);
            return character;
        }

        private static RectTransform Fill(CharacterHoverHealthBar target) =>
            (RectTransform)fillField.GetValue(target);

        private static RectTransform Block(CharacterHoverHealthBar target) =>
            (RectTransform)blockField.GetValue(target);

        private static TMP_Text Label(CharacterHoverHealthBar target) =>
            (TMP_Text)labelField.GetValue(target);

        [Test]
        public void ShowFor_Enemy_ShowsBarAtFullHealth()
        {
            var enemy = Enemy();

            bar.ShowFor(enemy);

            Assert.IsTrue(bar.IsVisible, "Hovering an enemy must show the health bar.");
            Assert.AreSame(enemy, bar.Hovered, "The bar must track the hovered enemy.");
            Assert.AreEqual(1f, Fill(bar).anchorMax.x, 0.0001f,
                "A full-health enemy must render a completely filled bar.");
        }

        [Test]
        public void ShowFor_Ally_DoesNotShow()
        {
            var ally = Ally();

            bar.ShowFor(ally);

            Assert.IsFalse(bar.IsVisible,
                "Allies already have permanent HUD rows; the floating bar is enemies only.");
            Assert.IsNull(bar.Hovered);
        }

        [Test]
        public void ShowFor_Structure_DoesNotShow()
        {
            var rock = Enemy(q: 3);
            rock.MarkAsStructure();

            bar.ShowFor(rock);

            Assert.IsFalse(bar.IsVisible, "Neutral structures (rocks) must not get a floating bar.");
        }

        [Test]
        public void ShowFor_Null_Hides()
        {
            bar.ShowFor(Enemy());
            Assert.IsTrue(bar.IsVisible);

            bar.ShowFor(null);

            Assert.IsFalse(bar.IsVisible, "A null hover must hide the bar.");
        }

        [Test]
        public void Damage_UpdatesFillAmount()
        {
            var enemy = Enemy();
            enemy.Stats.MaxHealth = 10;
            enemy.Stats.Initialize();

            bar.ShowFor(enemy);
            enemy.Stats.ApplyDamage(4);

            Assert.AreEqual(0.6f, Fill(bar).anchorMax.x, 0.0001f,
                "The bar must shrink to 6/10 after 4 damage.");
        }

        [Test]
        public void Block_RendersFromTheRight()
        {
            var enemy = Enemy();
            enemy.Stats.MaxHealth = 10;
            enemy.Stats.Initialize();

            bar.ShowFor(enemy);
            enemy.GainBlock(3);

            Assert.AreEqual(0.7f, Block(bar).anchorMin.x, 0.0001f,
                "Enemy block must grow in from the right edge, matching the HUD convention.");
        }

        [Test]
        public void HealthLabel_ShowsCurrentOverMax()
        {
            var enemy = Enemy();
            enemy.Stats.MaxHealth = 15;
            enemy.Stats.Initialize();
            enemy.Stats.ApplyDamage(2);

            bar.ShowFor(enemy);

            Assert.AreEqual("13 / 15", Label(bar).text,
                "The floating bar must show the exact current/max health.");
        }

        [Test]
        public void HoveredDefeat_HidesBar()
        {
            var enemy = Enemy();
            bar.ShowFor(enemy);
            Assert.IsTrue(bar.IsVisible);

            enemy.Kill();

            Assert.IsFalse(bar.IsVisible, "The bar must disappear when the hovered enemy is defeated.");
            Assert.IsNull(bar.Hovered);
        }

        [Test]
        public void Hide_StopsTracking()
        {
            bar.ShowFor(Enemy());
            Assert.IsTrue(bar.IsVisible);

            bar.Hide();

            Assert.IsFalse(bar.IsVisible);
            Assert.IsNull(bar.Hovered);
        }

        [Test]
        public void ShowFor_SecondEnemy_Retargets()
        {
            var first = Enemy(q: 0);
            var second = Enemy(q: 1);

            bar.ShowFor(first);
            bar.ShowFor(second);

            Assert.AreSame(second, bar.Hovered, "The bar must switch to the newly hovered enemy.");

            first.Stats.ApplyDamage(5);
            Assert.AreEqual(1f, Fill(bar).anchorMax.x, 0.0001f,
                "Damage to the previously hovered enemy must not move the bar.");
        }
    }
}
