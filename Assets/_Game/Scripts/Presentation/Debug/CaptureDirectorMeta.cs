#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Diagnostics;
using System.IO;
using Gargote.Core;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Scénario M3 (extra Android « gargoteCaptureMeta » ou argument « -gargoteCaptureMeta ») : chaque écran méta est
    /// ouvert sur la rue en jeu, capturé, avec son temps d'ouverture (cible &lt; 150 ms) et le FPS pendant 3 s écran ouvert.
    /// </summary>
    public sealed partial class CaptureDirector
    {
        private bool _meta;

        private IEnumerator MetaScenario()
        {
            var u = _root.MetaUi;
            var meta = _root.Meta;
            _report.AppendLine("Scénario M3 — écrans méta");
            _root.GameTime.ForcedHour = 17.5d;

            // Partie neuve : premiers pas, niveau 1, rails presque vides.
            yield return Wait(2.5f);
            yield return Shot("m3_01_debut_niveau1");

            // Progression : tout débloqué, de l'argent, gérants embauchés.
            meta.Level.AddRaw(meta.Level.XpForLevel(meta.Level.MaxLevel));
            Give(5e9);
            for (int i = 0; i < _root.Session.Businesses.Count; i++) _root.Session.Businesses.HireManager(i);
            meta.Tick(0.1);
            yield return Wait(0.5f);
            CloseAll();
            yield return Wait(1.5f);
            yield return Shot("m3_02_rue_rails_remplis");

            yield return OpenAndShoot("m3_03_missions", u.Missions, () => u.Missions.Show());
            meta.Chests.Tick(meta.Chests.Rules.FreeEverySeconds);
            yield return OpenAndShoot("m3_04_coffre_ouverture", u.ChestOpen, () => meta.ClaimFreeChest(), 1.2f);
            yield return OpenAndShoot("m3_05_coffre_chances", u.ChestOdds, () => u.ChestOdds.Show("gold"));
            yield return OpenAndShoot("m3_06_connexion_7j", u.Daily, () => u.Daily.Show());
            yield return OpenAndShoot("m3_07_roue", u.Wheel, () => u.Wheel.Show());
            yield return OpenAndShoot("m3_08_gerants", u.Managers, () => u.OpenTab(1));
            yield return OpenAndShoot("m3_09_gerant_detail", u.ManagerDetail, () => u.ManagerDetail.Show(0));
            yield return OpenAndShoot("m3_10_album", u.Album, () => u.Album.Show());
            yield return OpenAndShoot("m3_11_succes", u.Achievements, () => u.Achievements.Show());
            yield return OpenAndShoot("m3_12_upgrades", u.Upgrades, () => u.OpenTab(2));
            yield return OpenAndShoot("m3_13_villes", u.Cities, () => u.OpenTab(3));
            yield return OpenAndShoot("m3_14_boutique", u.Shop, () => u.OpenTab(4));
            yield return OpenAndShoot("m3_15_reglages", u.Settings, () => u.Settings.Show());

            // Prestige puis voyage : écran de confirmation, cinématique, arrivée.
            Give(_root.Session.City.GoalLifetimeEarnings * 1.01);
            yield return OpenAndShoot("m3_16_prestige_voyage", u.Prestige, () => u.Prestige.Show(true));
            var confirm = u.Prestige.CardRect.Find("Confirm/Button").GetComponent<UiButton>();
            confirm.Clicked();
            yield return Wait(2.2f);
            yield return Shot("m3_17_cinematique_voyage");
            yield return Wait(3.5f);
            CloseAll();
            yield return Wait(1f);
            yield return Shot("m3_18_arrivee_abidjan");

            File.WriteAllText(Path.Combine(_folder, "perf_m3.txt"), _report.ToString());
            UnityEngine.Debug.Log("[M3] Capture terminée : " + _folder);
            Application.Quit();
        }

        private void CloseAll()
        {
            while (_root.Meta.NextReveal() != null) { }
            var u = _root.MetaUi;
            foreach (var s in new MetaScreen[]
                     {
                         u.Missions, u.ChestOpen, u.ChestOdds, u.Daily, u.Wheel, u.Managers, u.ManagerDetail, u.Album, u.Recipe,
                         u.Achievements, u.Upgrades, u.Cities, u.Shop, u.Prestige, u.Settings,
                     })
                s.Hide();
        }

        private IEnumerator OpenAndShoot(string shot, MetaScreen screen, System.Action open, float settle = 0.6f)
        {
            CloseAll();
            yield return Wait(0.3f);
            var sw = Stopwatch.StartNew();
            open();
            sw.Stop();
            yield return Wait(settle);
            int frames = 0;
            float start = Time.realtimeSinceStartup, worst = 0f;
            while (Time.realtimeSinceStartup - start < 3f)
            {
                yield return null;
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }
            float fps = frames / (Time.realtimeSinceStartup - start);
            _report.AppendLine("  " + shot + " : ouverture " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " ms (cible < 150), " +
                               fps.ToString("0.0") + " FPS, pire frame " + (worst * 1000f).ToString("0.0") + " ms, ouvert=" + screen.IsOpen);
            yield return Shot(shot);
        }
    }
}
#endif
