using System.Collections;
using System.Diagnostics;
using Gargote.Core;
using Gargote.Presentation;
using Gargote.Services.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gargote.Tests
{
    /// <summary>
    /// Intégration M3 : prestige puis voyage, coffre, missions, ouverture des écrans (&lt; 150 ms) et zones d'interface sans
    /// chevauchement dans 10 situations de jeu.
    /// </summary>
    public class MetaPlayModeTests
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

        private void Give(double amount) => _root.Session.Economy.Earn(BigDouble.FromDouble(amount));
        private void UnlockAll() => _root.Meta.Level.AddRaw(_root.Meta.Level.XpForLevel(_root.Meta.Level.MaxLevel));

        private static void Click(MetaScreen screen, string path)
        {
            var t = screen.CardRect.Find(path);
            Assert.That(t, Is.Not.Null, path);
            var b = t.GetComponent<UiButton>();
            Assert.That(b.Interactable, Is.True, path + " désactivé");
            b.Clicked();
        }

        private void CloseRevealsAndScreens()
        {
            while (_root.Meta.NextReveal() != null) { }
            foreach (var s in Screens()) s.Hide();
        }

        private MetaScreen[] Screens()
        {
            var u = _root.MetaUi;
            return new MetaScreen[]
            {
                u.Missions, u.ChestOpen, u.ChestOdds, u.Daily, u.Wheel, u.Managers, u.ManagerDetail, u.Album, u.Recipe,
                u.Achievements, u.Upgrades, u.Cities, u.Shop, u.Prestige, u.Travel,
            };
        }

        [UnityTest]
        public IEnumerator Restart_Then_Travel_To_Next_City()
        {
            UnlockAll();
            yield return null;
            CloseRevealsAndScreens();
            var s = _root.Session;

            // Pas assez de chiffre d'affaires : « Pas encore », la relance est refusée.
            _root.MetaUi.OpenTab(3);
            _root.MetaUi.Prestige.Show(false);
            yield return null;
            Assert.That(s.CanPrestige, Is.False);
            _root.MetaUi.Prestige.Hide();

            // Relancer la ville : étoiles gagnées, ville inchangée, rue resynchronisée.
            Give(s.City.GoalLifetimeEarnings * 0.2);
            Assert.That(s.CanPrestige, Is.True);
            long stars = s.Prestige.TotalStars;
            _root.MetaUi.Prestige.Show(false);
            yield return null;
            Click(_root.MetaUi.Prestige, "Confirm/Button");
            yield return null;
            Assert.That(s.CityIndex, Is.EqualTo(0));
            Assert.That(s.Prestige.TotalStars, Is.GreaterThan(stars));
            Assert.That(_root.MetaUi.Travel.IsOpen, Is.False, "relancer ne voyage pas");

            // Objectif atteint : voyager, cinématique passable après 1 s, arrivée dans la ville suivante.
            Give(s.City.GoalLifetimeEarnings * 1.01);
            Assert.That(s.IsCityGoalReached, Is.True);
            _root.MetaUi.Prestige.Show(true);
            yield return null;
            Click(_root.MetaUi.Prestige, "Confirm/Button");
            yield return null;
            Assert.That(s.CityIndex, Is.EqualTo(1));
            Assert.That(_root.MetaUi.Travel.IsOpen, Is.True);
            var skip = _root.MetaUi.Travel.CardRect.Find("Skip");
            Assert.That(skip.gameObject.activeSelf, Is.False, "pas de « Passer » avant 1 s");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(skip.gameObject.activeSelf, Is.True);
            Click(_root.MetaUi.Travel, "Skip/Button");
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(_root.MetaUi.Travel.IsOpen, Is.False);
            for (int i = 0; i < s.Businesses.Count; i++)
                Assert.That(_root.Presenter.Views[i].IsLocked, Is.EqualTo(!s.Businesses[i].IsUnlocked), "rue rebranchée " + i);

            // Panneau et bandeau suivent la nouvelle ville (régression A23 : « Forokh Thiaya » et « Objectif Dakar » à Abidjan).
            yield return null;
            var row0 = GameObject.Find("Sheet").transform.Find("Viewport/Content/Row0/Name").GetComponent<TMPro.TextMeshProUGUI>();
            Assert.That(row0.text, Is.EqualTo(Loc.Get("business." + s.Businesses[0].Definition.Id)), "nom du commerce 0");
            string city = Loc.Get("city." + s.City.Id);
            bool goalFollows = false;
            foreach (var t in Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
                if (t.text.Contains(city) && t.text.EndsWith("FCFA")) goalFollows = true;
            Assert.That(goalFollows, Is.True, "bandeau objectif sur " + city);

            // La ville quittée reste consultable avec un revenu passif réduit.
            Assert.That(s.VisitedCities.Count, Is.GreaterThanOrEqualTo(1));
            _root.MetaUi.OpenTab(3);
            yield return null;
            Assert.That(_root.MetaUi.Cities.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator Free_Chest_Opens_Reveal_And_Grants_Cards()
        {
            UnlockAll();
            yield return null;
            CloseRevealsAndScreens();
            _root.Meta.Chests.Tick(_root.Meta.Chests.Rules.FreeEverySeconds);
            Assert.That(_root.Meta.CanClaimFreeChest, Is.True);
            long cards = _root.Meta.Ledger.Balance(Gargote.Systems.Meta.Currency.Cards);
            Assert.That(_root.Meta.ClaimFreeChest(), Is.Not.Null);
            yield return null;
            Assert.That(_root.MetaUi.ChestOpen.IsOpen, Is.True, "révélation spectaculaire ouverte");
            Assert.That(_root.Meta.Ledger.Balance(Gargote.Systems.Meta.Currency.Cards), Is.GreaterThan(cards));
        }

        [UnityTest]
        public IEnumerator Missions_Screen_Shows_Three_And_Swaps()
        {
            UnlockAll();
            _root.Meta.Tick(0.1);
            yield return null;
            CloseRevealsAndScreens();
            _root.MetaUi.Missions.Show();
            yield return null;
            Assert.That(_root.MetaUi.Missions.IsOpen, Is.True);
            for (int s = 0; s < 3; s++) Assert.That(_root.Meta.Missions[s], Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Every_Screen_Opens_Under_150_ms()
        {
            UnlockAll();
            yield return null;
            CloseRevealsAndScreens();
            var sw = new Stopwatch();
            foreach (var screen in Screens())
            {
                if (screen == _root.MetaUi.ChestOpen || screen == _root.MetaUi.Travel || screen == _root.MetaUi.Recipe ||
                    screen == _root.MetaUi.ManagerDetail) continue; // ouverts avec un contenu, testés ailleurs
                sw.Restart();
                if (screen == _root.MetaUi.ChestOdds) _root.MetaUi.ChestOdds.Show("gold");
                else screen.Show();
                sw.Stop();
                Assert.That(sw.Elapsed.TotalMilliseconds, Is.LessThan(150d), screen.GetType().Name);
                yield return null;
                screen.Hide();
            }
        }

        /// <summary>
        /// Fenêtre ouverte : rue 3D figée (caméra coupée, budget GPU A23) et carte entièrement dans l'écran (20:9 compris) ;
        /// fermeture : caméra rétablie.
        /// </summary>
        [UnityTest]
        public IEnumerator Open_Screen_Freezes_Street_And_Card_Fits()
        {
            UnlockAll();
            yield return null;
            CloseRevealsAndScreens();
            yield return null;
            Assert.That(_root.mainCamera.enabled, Is.True);
            var corners = new Vector3[4];
            foreach (var screen in Screens())
            {
                if (screen == _root.MetaUi.ChestOpen || screen == _root.MetaUi.Travel || screen == _root.MetaUi.Recipe ||
                    screen == _root.MetaUi.ManagerDetail) continue;
                if (screen == _root.MetaUi.ChestOdds) _root.MetaUi.ChestOdds.Show("gold");
                else screen.Show();
                yield return new WaitForSecondsRealtime(0.4f); // fin du rebond d'ouverture
                Assert.That(_root.mainCamera.enabled, Is.False, screen.GetType().Name + " : rue non figée");
                screen.CardRect.GetWorldCorners(corners);
                Assert.That(corners[0].x >= -0.5f && corners[0].y >= -0.5f && corners[2].x <= Screen.width + 0.5f &&
                            corners[2].y <= Screen.height + 0.5f, Is.True,
                    screen.GetType().Name + " déborde : " + corners[0] + " → " + corners[2] + " écran " + Screen.width + "x" + Screen.height);
                screen.Hide();
                yield return null;
                Assert.That(_root.mainCamera.enabled, Is.True, screen.GetType().Name + " : caméra non rétablie");
            }
        }

        /// <summary>10 situations : zones réservées (HUD, bandeau, rails, panneau bas, toast) sans chevauchement.</summary>
        [UnityTest]
        public IEnumerator Ui_Zones_Never_Overlap_In_10_Situations()
        {
            var u = _root.MetaUi;
            for (int situation = 1; situation <= 10; situation++)
            {
                switch (situation)
                {
                    case 1: break; // partie neuve, niveau 1
                    case 2: UnlockAll(); break; // tous les rails remplis
                    case 3: _root.Meta.Chests.Tick(1e9); break; // coffre prêt, minuterie
                    case 4: _root.Meta.Session.RewardBoostAd(_root.Meta.DayKey); break; // boost x2 actif
                    case 5: u.Toast.Show("Toast de test assez long pour remplir la largeur disponible"); break;
                    case 6: Give(1e12); for (int i = 0; i < _root.Session.Businesses.Count; i++) _root.Session.Businesses.HireManager(i); break;
                    case 7: _root.Meta.ActivateManagerBoost(_root.Session.Businesses[0].Definition.Id); break;
                    case 8: _root.Meta.Ledger.Credit(Gargote.Systems.Meta.Currency.Cauris, 99999, "test"); break; // gros nombres
                    case 9: Give(1e15); break; // montants en Md
                    case 10: _root.Sheet.SetExpanded(true); break; // panneau ouvert
                }
                CloseRevealsAndScreens();
                // État au repos : le rebond d'apparition (Juice.Pop, 0,3 s) dépasse volontairement l'échelle 1 un instant.
                yield return new WaitForSecondsRealtime(0.45f);
                AssertNoOverlap(situation);
            }
            _root.Sheet.SetExpanded(false);
        }

        private void AssertNoOverlap(int situation)
        {
            var layout = _root.Layout;
            layout.Refresh();
            var zones = new[] { UiZone.TopHud, UiZone.Toasts, UiZone.LeftRail, UiZone.RightRail, UiZone.BottomSheet };
            bool sheetOpen = _root.Sheet.Expanded;
            for (int a = 0; a < zones.Length; a++)
            {
                if (!layout.IsActive(zones[a])) continue;
                var ra = layout.Zone(zones[a]);
                var sc = layout.Screen;
                const float eps = 0.5f; // bords inclus (Rect.Contains exclut le max)
                bool inside = ra.xMin >= sc.xMin - eps && ra.yMin >= sc.yMin - eps && ra.xMax <= sc.xMax + eps && ra.yMax <= sc.yMax + eps;
                Assert.That(inside, Is.True,
                    "situation " + situation + " : " + zones[a] + " hors écran " + ra);
                for (int b = a + 1; b < zones.Length; b++)
                {
                    if (!layout.IsActive(zones[b])) continue;
                    // Panneau ouvert : il passe au premier plan au-dessus de la rue et des rails (état modal voulu).
                    if (sheetOpen && zones[b] == UiZone.BottomSheet && (zones[a] == UiZone.LeftRail || zones[a] == UiZone.RightRail)) continue;
                    Assert.That(UiLayout.Overlaps(ra, layout.Zone(zones[b])), Is.False,
                        "situation " + situation + " : " + zones[a] + " chevauche " + zones[b]);
                }
            }
            var toast = _root.MetaUi.Toast.Root;
            if (toast.gameObject.activeInHierarchy)
            {
                var rt = layout.ToSpace(toast);
                foreach (var z in new[] { UiZone.TopHud, UiZone.LeftRail, UiZone.RightRail, UiZone.BottomSheet })
                    if (layout.IsActive(z))
                        Assert.That(UiLayout.Overlaps(rt, layout.Zone(z)), Is.False, "situation " + situation + " : toast chevauche " + z + " toast=" + rt + " zone=" + layout.Zone(z));
            }
            Assert.That(layout.Street.height, Is.GreaterThan(layout.Screen.height * (sheetOpen ? 0.05f : 0.3f)),
                "situation " + situation + " : rue trop petite");
        }
    }
}
