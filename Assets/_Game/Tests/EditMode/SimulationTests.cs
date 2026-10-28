using System.IO;
using Gargote.EditorTools;
using Gargote.Simulation;
using NUnit.Framework;

namespace Gargote.Tests
{
    public class SimulationTests
    {
        private static PlayerProfile Casual() => new PlayerProfile
        {
            Name = "test", SessionsPerDay = 3, SessionMinutes = 5, FirstSessionMinutes = 10, WatchesAds = true,
        };

        [Test]
        public void Simulation_Is_Deterministic_And_Writes_Csv()
        {
            var defs = EconomySimulator.WithProxyCities(TestData.Definitions(10, 1), 2);
            var settings = new SimulationSettings { Days = 2, Profiles = new[] { Casual() } };

            var a = new EconomySimulator(defs, settings).Run();
            var b = new EconomySimulator(defs, settings).Run();
            var sa = a.Profiles[0].Snapshots;
            var sb = b.Profiles[0].Snapshots;
            Assert.That(sa.Count, Is.EqualTo(sb.Count));
            Assert.That(sa[sa.Count - 1].TotalLifetimeEarnings, Is.EqualTo(sb[sb.Count - 1].TotalLifetimeEarnings));
            Assert.That(a.Profiles[0].FirstEvent("unlock"), Is.Not.Null);
            Assert.That(a.Profiles[0].FirstEvent("manager"), Is.Not.Null);

            var w = new StringWriter();
            a.WriteTimelineCsv(w);
            StringAssert.StartsWith("profile,day,session", w.ToString());
        }

        /// <summary>
        /// Simulation complète 30 jours sur les ScriptableObjects réels : les cibles de §4.5 doivent passer.
        /// Lente (~1 min) : catégorie dédiée pour pouvoir l'exclure des passes rapides.
        /// </summary>
        [Test, Category("Simulation"), Timeout(600000)]
        public void Real_Balance_Meets_Progression_Targets()
        {
            var report = SimulationRunner.Run(out string log);
            Assert.That(report.AllTargetsPass, log);
        }
    }
}
