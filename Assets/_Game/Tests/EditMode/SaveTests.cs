using System;
using System.Collections.Generic;
using System.IO;
using Gargote.Core;
using Gargote.Services.Save;
using Gargote.Systems;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Gargote.Tests
{
    public class SaveTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "gargote_save_tests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static GameSession PlayedSession()
        {
            var s = new GameSession(TestData.Definitions(1e7));
            s.Businesses.BuyLevels(0, 30);
            s.Businesses.HireManager(0);
            s.Businesses.BuyLevels(1, 3);
            s.Businesses.Tap(1);
            s.Tick(1.3, 12);
            s.Economy.TryAddAdBoost();
            s.Economy.Earn(BigDouble.FromLog10(40));
            return s;
        }

        private static void AssertSameState(GameSession expected, GameSession actual)
        {
            Assert.That(actual.Economy.Money, Is.EqualTo(expected.Economy.Money));
            Assert.That(actual.Economy.CityLifetimeEarnings, Is.EqualTo(expected.Economy.CityLifetimeEarnings));
            Assert.That(actual.Economy.TotalLifetimeEarnings, Is.EqualTo(expected.Economy.TotalLifetimeEarnings));
            Assert.That(actual.Economy.AdBoostRemainingSeconds, Is.EqualTo(expected.Economy.AdBoostRemainingSeconds).Within(1e-9));
            Assert.That(actual.Prestige.TotalStars, Is.EqualTo(expected.Prestige.TotalStars));
            Assert.That(actual.CityIndex, Is.EqualTo(expected.CityIndex));
            for (int i = 0; i < expected.Businesses.Count; i++)
            {
                Assert.That(actual.Businesses[i].Level, Is.EqualTo(expected.Businesses[i].Level));
                Assert.That(actual.Businesses[i].HasManager, Is.EqualTo(expected.Businesses[i].HasManager));
                Assert.That(actual.Businesses[i].IsRunning, Is.EqualTo(expected.Businesses[i].IsRunning));
                Assert.That(actual.Businesses[i].Progress, Is.EqualTo(expected.Businesses[i].Progress).Within(1e-9));
            }
        }

        [Test]
        public void Session_RoundTrips_Through_Json()
        {
            var original = PlayedSession();
            string json = SaveSerializer.Serialize(original.ToSaveData(Now, 1234.5));
            var data = SaveSerializer.Deserialize(json);

            Assert.That(data.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(data.SavedUtcTicks, Is.EqualTo(Now.Ticks));
            var restored = new GameSession(TestData.Definitions());
            restored.LoadFrom(data);
            AssertSameState(original, restored);
        }

        [Test]
        public void File_Save_Is_Atomic_And_Keeps_Backup()
        {
            var service = new FileSaveService(_dir);
            var session = PlayedSession();
            service.Save(session.ToSaveData(Now, 0));
            session.Economy.Earn(BigDouble.FromDouble(1));
            service.Save(session.ToSaveData(Now.AddSeconds(30), 30));

            Assert.That(File.Exists(service.MainPath));
            Assert.That(File.Exists(service.BackupPath));
            Assert.That(File.Exists(service.MainPath + ".tmp"), Is.False);

            Assert.That(service.TryLoad(out var data), Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(data.SavedUtcTicks, Is.EqualTo(Now.AddSeconds(30).Ticks));
        }

        [Test]
        public void Tampered_Save_Falls_Back_To_Backup()
        {
            var service = new FileSaveService(_dir);
            var session = PlayedSession();
            service.Save(session.ToSaveData(Now, 0));
            service.Save(session.ToSaveData(Now.AddSeconds(30), 30));

            // Le joueur « s'offre » de l'argent en éditant le fichier.
            var envelope = JObject.Parse(File.ReadAllText(service.MainPath));
            var payload = JObject.Parse(envelope.Value<string>("payload"));
            payload["Money"] = "9e99";
            envelope["payload"] = payload.ToString();
            File.WriteAllText(service.MainPath, envelope.ToString());

            Assert.That(service.TryLoad(out var data), Is.EqualTo(SaveLoadStatus.LoadedFromBackup));
            Assert.That(data.Money, Is.Not.EqualTo("9e99"));
        }

        [Test]
        public void Garbage_Save_Reports_Corruption()
        {
            var service = new FileSaveService(_dir);
            File.WriteAllText(service.MainPath, "{ not json");
            Assert.That(service.TryLoad(out var data), Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(data, Is.Null);
        }

        [Test]
        public void No_Save_Means_New_Game()
        {
            Assert.That(new FileSaveService(_dir).TryLoad(out _), Is.EqualTo(SaveLoadStatus.NoSave));
        }

        [Test]
        public void Migrations_Run_From_Older_Versions()
        {
            var migrations = new Dictionary<int, Func<JObject, JObject>>
            {
                { 0, o => { o["Money"] = o["LegacyCash"]; o.Remove("LegacyCash"); return o; } },
            };
            string v0 = "{\"Version\":0,\"LegacyCash\":\"1.5e12\",\"CityId\":\"city0\"}";
            var data = SaveSerializer.Deserialize(v0, migrations);
            Assert.That(data.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(BigDouble.Parse(data.Money).ToDouble(), Is.EqualTo(1.5e12).Within(1));
        }

        [Test]
        public void Newer_Version_Is_Rejected()
        {
            Assert.Throws<InvalidOperationException>(() => SaveSerializer.Deserialize("{\"Version\":999}"));
        }

        [Test]
        public void Unknown_Businesses_Are_Ignored_And_New_Ones_Start_Empty()
        {
            var data = new SaveData
            {
                CityId = "city0",
                Money = "500",
                Businesses = new[]
                {
                    new BusinessSaveData { Id = "removed_business", Level = 99 },
                    new BusinessSaveData { Id = "a", Level = 7, HasManager = true, IsRunning = true, Progress = 0.2 },
                },
            };
            var s = new GameSession(TestData.Definitions());
            s.LoadFrom(data);
            Assert.That(s.Businesses[0].Level, Is.EqualTo(7));
            Assert.That(s.Businesses[0].HasManager);
            Assert.That(s.Businesses[1].Level, Is.EqualTo(0));
            Assert.That(s.Economy.Money.ToDouble(), Is.EqualTo(500).Within(1e-9));
        }

        [Test]
        public void In_Memory_Service_Works_As_Mock()
        {
            var service = new InMemorySaveService();
            Assert.That(service.TryLoad(out _), Is.EqualTo(SaveLoadStatus.NoSave));
            service.Save(PlayedSession().ToSaveData(Now, 0));
            Assert.That(service.TryLoad(out var data), Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(data.Businesses.Length, Is.EqualTo(2));
        }
    }
}
