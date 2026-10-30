using System.Collections;
using Gargote.Core;
using Gargote.Presentation;
using Gargote.Services.Save;
using Gargote.Systems.Street;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gargote.Tests
{
    /// <summary>
    /// Intégration M2 : la scène Rue reflète l'économie (source de vérité), reste stable sur une heure accélérée,
    /// ne garde aucun client pour un commerce hors écran, ne réalloue pas ses pools et ne touche jamais Time.timeScale.
    /// </summary>
    public class StreetPlayModeTests
    {
        private GameRoot _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameRoot.SaveServiceOverride = () => new InMemorySaveService();
            GameRoot.SkipOfflinePanel = true;
            SceneManager.LoadScene("Street");
            yield return null;
            yield return null;
            _root = Object.FindAnyObjectByType<GameRoot>();
            Assert.That(_root, Is.Not.Null);
            Assert.That(_root.Ready, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.SaveServiceOverride = null;
            GameRoot.SkipOfflinePanel = false;
        }

        private void AssertViewsMatchEconomy()
        {
            var businesses = _root.Session.Businesses;
            var stages = _root.streetConfig.stageLevels;
            for (int i = 0; i < businesses.Count; i++)
            {
                var state = businesses[i];
                var view = _root.Presenter.Views[i];
                Assert.That(view.IsLocked, Is.EqualTo(!state.IsUnlocked), "verrou " + i);
                if (state.IsUnlocked)
                    Assert.That(view.Stage, Is.EqualTo(VisualStages.StageFor(state.Level, stages)), "stade " + i);
                Assert.That(view.HasManagerShown, Is.EqualTo(state.HasManager), "gérant " + i);
            }
        }

        private void Give(double amount) => _root.Session.Economy.Earn(BigDouble.FromDouble(amount));

        [UnityTest]
        public IEnumerator View_Reflects_Purchase_Milestone_Unlock_Manager_And_Prestige()
        {
            var businesses = _root.Session.Businesses;
            AssertViewsMatchEconomy();

            // Achat simple.
            Give(1e6);
            Assert.That(businesses.BuyLevels(0, 1), Is.True);
            yield return null;
            AssertViewsMatchEconomy();

            // Palier 25 : stade 1 (étal).
            Give(1e9);
            Assert.That(businesses.BuyLevels(0, 25 - businesses[0].Level), Is.True);
            yield return null;
            Assert.That(_root.Presenter.Views[0].Stage, Is.GreaterThanOrEqualTo(1));
            AssertViewsMatchEconomy();

            // Déblocage + gérant.
            Give(1e12);
            Assert.That(businesses.BuyLevels(1, 1), Is.True);
            Assert.That(businesses.HireManager(0), Is.True);
            yield return new WaitForSecondsRealtime(0.8f);
            AssertViewsMatchEconomy();

            // Prestige libre : la rue se resynchronise.
            Give(1e15);
            Assert.That(_root.Session.PerformPrestige(false), Is.True);
            yield return null;
            AssertViewsMatchEconomy();
        }

        [UnityTest]
        public IEnumerator One_Accelerated_Hour_Is_Stable()
        {
            var businesses = _root.Session.Businesses;
            Give(1e30);
            for (int i = 0; i < businesses.Count; i++)
            {
                businesses.BuyLevels(i, 120);
                businesses.HireManager(i);
            }
            _root.GameTime.Speed = 100f;

            int billCapacity = _root.Money.BillCapacity, popupCapacity = _root.Money.PopupCapacity;
            int crowdCapacity = _root.Presenter.Crowd.Capacity;
            int maxCustomers = 0, maxBills = 0, maxParticles = 0;
            double start = _root.GameTime.ElapsedGameSeconds;
            float realStart = Time.realtimeSinceStartup;

            while (_root.GameTime.ElapsedGameSeconds - start < 3600d && Time.realtimeSinceStartup - realStart < 120f)
            {
                yield return null;
                var crowd = _root.Presenter.Crowd;
                maxCustomers = Mathf.Max(maxCustomers, crowd.ActiveCount);
                maxBills = Mathf.Max(maxBills, _root.Money.ActiveBills);
                maxParticles = Mathf.Max(maxParticles, _root.Effects.AliveParticles);
                Assert.That(crowd.ActiveCount, Is.LessThanOrEqualTo(_root.crowdConfig.maxTotal), "plafond global");
                for (int b = 0; b < crowd.BusinessCount; b++)
                    Assert.That(crowd.PresentAt(b), Is.LessThanOrEqualTo(_root.crowdConfig.maxPerBusiness), "plafond commerce " + b);
                Assert.That(crowd.CountAttachedToHidden(), Is.Zero, "client d'un commerce hors écran");
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }

            Assert.That(_root.GameTime.ElapsedGameSeconds - start, Is.GreaterThanOrEqualTo(3600d), "heure de jeu non atteinte");
            Assert.That(_root.Money.BillCapacity, Is.EqualTo(billCapacity), "pool de billets réalloué");
            Assert.That(_root.Money.PopupCapacity, Is.EqualTo(popupCapacity), "pool de popups réalloué");
            Assert.That(_root.Presenter.Crowd.Capacity, Is.EqualTo(crowdCapacity), "pool de clients réalloué");
            Assert.That(maxCustomers, Is.GreaterThan(0), "la rue doit être vivante");
            Assert.That(maxBills, Is.LessThanOrEqualTo(billCapacity));
            Assert.That(_root.Session.Economy.Money >= BigDouble.Zero, Is.True);
            Debug.Log("[M2] 1 h accélérée : clients max " + maxCustomers + ", billets max " + maxBills + ", particules max " + maxParticles);
        }

        [UnityTest]
        public IEnumerator Scrolling_Away_Removes_Customers_Of_Hidden_Business()
        {
            var businesses = _root.Session.Businesses;
            Give(1e20);
            for (int i = 0; i < businesses.Count; i++) { businesses.BuyLevels(i, 60); businesses.HireManager(i); }
            _root.Rig.FocusOn(_root.Presenter.Views[0].transform.position.z);
            yield return new WaitForSecondsRealtime(3f);
            Assert.That(_root.Presenter.Crowd.PresentAt(0), Is.GreaterThan(0), "foule devant le commerce 1");

            int last = businesses.Count - 1;
            _root.Rig.FocusOn(_root.Presenter.Views[last].transform.position.z);
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(_root.Presenter.IsVisible(0), Is.False, "commerce 1 hors écran");
            Assert.That(_root.Presenter.Crowd.PresentAt(0), Is.Zero);
            Assert.That(_root.Presenter.Crowd.CountAttachedToHidden(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator Special_Customer_Tap_Credits_Once_Through_Economy()
        {
            Give(1e6);
            _root.Session.Businesses.BuyLevels(0, 10);
            _root.Session.Businesses.BuyLevels(1, 1);
            Assert.That(_root.Specials.ForceSpawn(0), Is.True);
            yield return null;
            Assert.That(_root.Specials.ActiveIndex, Is.EqualTo(0));
            var before = _root.Session.Economy.Money;
            Assert.That(_root.Specials.TryTap(out _), Is.True);
            Assert.That(_root.Session.Economy.Money > before, Is.True);
            Assert.That(_root.Specials.TryTap(out _), Is.False, "un seul crédit");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
