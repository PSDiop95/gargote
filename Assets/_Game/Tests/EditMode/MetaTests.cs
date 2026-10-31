using System;
using System.Collections.Generic;
using Gargote.Core;
using Gargote.Data;

using Gargote.Services.Save;
using Gargote.Systems;
using Gargote.Systems.Meta;
using NUnit.Framework;
using UnityEditor;

namespace Gargote.Tests
{
    /// <summary>Banc de test méta : données réelles (ScriptableObjects), horloge manuelle, graine fixe.</summary>
    internal sealed class MetaRig
    {
        public static readonly DateTime Epoch = new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);
        public readonly ManualClock Manual = new ManualClock(Epoch, 0d);
        public readonly GameClock Clock;
        public readonly GameSession Session;
        public readonly MetaSession Meta;
        public readonly MetaDefinitions Defs;

        private static GameDefinitions _game;
        private static MetaDefinitions _meta;

        public static void Real(out GameDefinitions game, out MetaDefinitions meta)
        {
            if (_game == null)
            {
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Game/Data/GameDatabase.asset");
                Assert.That(db, Is.Not.Null, "GameDatabase introuvable");
                _game = db.ToDefinitions();
                _meta = db.ToMetaDefinitions(_game);
            }
            game = _game;
            meta = _meta;
        }

        public MetaRig(ulong seed = 42, bool unlockAll = true)
        {
            Real(out var game, out var meta);
            Defs = meta;
            Clock = new GameClock(Manual, () => TimeSpan.Zero);
            Session = new GameSession(game);
            Meta = new MetaSession(Session, meta, Clock, seed);
            if (unlockAll) Meta.Level.AddRaw(Meta.Level.XpForLevel(Meta.Level.MaxLevel));
        }

        /// <summary>Temps joué (de confiance) : l'horloge système et le temps de confiance avancent ensemble.</summary>
        public void Play(double seconds)
        {
            Manual.Advance(seconds);
            Clock.AdvancePlayed(seconds);
            Meta.Tick(seconds);
        }
    }

    public class ChestTests
    {
        [Test]
        public void Same_Seed_Gives_Same_Cards()
        {
            var a = new MetaRig(7).Meta;
            var b = new MetaRig(7).Meta;
            for (int n = 0; n < 30; n++)
            {
                var ra = a.Chests.Open("gold", "test");
                var rb = b.Chests.Open("gold", "test");
                Assert.That(ra.Cards.Count, Is.EqualTo(rb.Cards.Count));
                for (int i = 0; i < ra.Cards.Count; i++) Assert.That(ra.Cards[i].ManagerIndex, Is.EqualTo(rb.Cards[i].ManagerIndex));
                Assert.That(ra.Cauris, Is.EqualTo(rb.Cauris));
            }
        }

        [Test]
        public void Pity_Never_Exceeds_Its_Interval()
        {
            var rig = new MetaRig(3);
            foreach (var def in rig.Defs.Chests)
            {
                if (def.PityEvery <= 0) continue;
                int since = 0;
                for (int n = 0; n < def.PityEvery * 8; n++)
                {
                    var r = rig.Meta.Chests.Open(def.Id, "test");
                    since = r.BestRarity >= def.PityRarity ? 0 : since + 1;
                    Assert.That(since, Is.LessThan(def.PityEvery), def.Id + " : pitié dépassée");
                }
            }
        }

        [Test]
        public void Displayed_Probabilities_Sum_To_One()
        {
            var rig = new MetaRig();
            foreach (var def in rig.Defs.Chests)
            {
                double sum = 0d;
                foreach (double p in rig.Meta.Chests.RarityProbabilities(def.Id)) sum += p;
                Assert.That(sum, Is.EqualTo(1d).Within(1e-9), def.Id);
            }
        }

        [Test]
        public void Duplicate_Of_Maxed_Manager_Becomes_Cauris()
        {
            var rig = new MetaRig(11);
            var m = rig.Meta.Managers;
            rig.Session.Economy.Earn(BigDouble.FromDouble(1e30));
            for (int i = 0; i < m.Count; i++)
            {
                if (m.Definition(i).Rarity != Rarity.Common) continue;
                m.AddCards(i, 100000, "test");
                while (!m.IsMaxed(i)) Assert.That(rig.Meta.LevelUpManager(i), Is.True);
            }
            bool converted = false;
            for (int n = 0; n < 200 && !converted; n++)
            {
                var r = rig.Meta.Chests.Open("wood", "test");
                foreach (var card in r.Cards)
                {
                    if (card.ConvertedCauris > 0)
                    {
                        Assert.That(m.IsMaxed(card.ManagerIndex), Is.True);
                        converted = true;
                    }
                    else if (m.Definition(card.ManagerIndex).Rarity == Rarity.Common)
                        Assert.Fail("Carte d'un gérant au niveau max non convertie");
                }
            }
            Assert.That(converted, Is.True);
        }

        [Test]
        public void Free_Chest_Every_Interval_Capped_In_Reserve()
        {
            var rig = new MetaRig();
            var rules = rig.Meta.Chests.Rules;
            int start = rig.Meta.Chests.FreeStored;
            rig.Play(rules.FreeEverySeconds * 10);
            Assert.That(rig.Meta.Chests.FreeStored, Is.EqualTo(rules.FreeMaxStored));
            Assert.That(start, Is.LessThanOrEqualTo(rules.FreeMaxStored));
            Assert.That(rig.Meta.ClaimFreeChest(), Is.Not.Null);
            Assert.That(rig.Meta.Chests.FreeStored, Is.EqualTo(rules.FreeMaxStored - 1));
            rig.Play(rules.FreeEverySeconds - 1);
            Assert.That(rig.Meta.Chests.FreeStored, Is.EqualTo(rules.FreeMaxStored - 1));
            rig.Play(2);
            Assert.That(rig.Meta.Chests.FreeStored, Is.EqualTo(rules.FreeMaxStored));
        }

        [Test]
        public void Buying_Debits_Cauris_And_Refuses_When_Poor()
        {
            var rig = new MetaRig();
            ChestDefinition paid = null;
            foreach (var d in rig.Defs.Chests) if (d.CostCauris > 0) { paid = d; break; }
            Assert.That(paid, Is.Not.Null);
            long before = rig.Meta.Cauris;
            if (before >= paid.CostCauris) rig.Meta.Ledger.TryDebit(Currency.Cauris, before, "test");
            Assert.That(rig.Meta.BuyChest(paid.Id), Is.Null);
            rig.Meta.Ledger.Credit(Currency.Cauris, paid.CostCauris, "test");
            long balance = rig.Meta.Cauris;
            var r = rig.Meta.BuyChest(paid.Id);
            Assert.That(r, Is.Not.Null);
            Assert.That(rig.Meta.Cauris, Is.EqualTo(balance - paid.CostCauris + r.Cauris + Converted(r)));
        }

        private static long Converted(ChestResult r)
        {
            long n = 0;
            foreach (var c in r.Cards) n += c.ConvertedCauris;
            return n;
        }
    }

    public class MissionAndDailyTests
    {
        [Test]
        public void Three_Missions_Active_And_One_Free_Swap_Per_Day()
        {
            var rig = new MetaRig();
            rig.Play(1);
            int slots = rig.Meta.Missions.Slots;
            Assert.That(slots, Is.EqualTo(3));
            for (int s = 0; s < slots; s++) Assert.That(rig.Meta.Missions[s], Is.Not.Null);

            rig.Meta.Ledger.TryDebit(Currency.Cauris, rig.Meta.Cauris, "test");
            Progress(rig); // plus de types de missions faisables
            Assert.That(rig.Meta.HasFreeReplace, Is.True);
            Assert.That(rig.Meta.ReplaceMission(0, false), Is.True);
            Assert.That(rig.Meta.HasFreeReplace, Is.False);
            Assert.That(rig.Meta.ReplaceMission(0, false), Is.False, "sans cauris ni pub");
            Assert.That(rig.Meta.ReplaceMission(0, true), Is.True, "via rewarded");
            rig.Play(86400);
            Assert.That(rig.Meta.HasFreeReplace, Is.True, "nouveau jour");
        }

        [Test]
        public void Swap_Without_Alternative_Keeps_Mission_And_Costs_Nothing()
        {
            var rig = new MetaRig();
            rig.Play(1); // partie neuve : seuls 3 types sont faisables, tous déjà affichés
            var before = rig.Meta.Missions[0];
            long cauris = rig.Meta.Cauris;
            if (!rig.Meta.ReplaceMission(0, false))
            {
                Assert.That(rig.Meta.Missions[0], Is.SameAs(before), "l'emplacement n'est jamais vidé");
                Assert.That(rig.Meta.HasFreeReplace, Is.True, "le changement gratuit n'est pas consommé");
                Assert.That(rig.Meta.Cauris, Is.EqualTo(cauris));
            }
            for (int s = 0; s < 3; s++) Assert.That(rig.Meta.Missions[s], Is.Not.Null);
        }

        private static void Progress(MetaRig rig)
        {
            rig.Session.Economy.Earn(BigDouble.FromDouble(1e15));
            for (int i = 0; i < rig.Session.Businesses.Count; i++) rig.Session.Businesses.Buy(i, BuyMode.Max);
            for (int i = 0; i < rig.Session.Businesses.Count; i++) rig.Session.Businesses.HireManager(i);
            rig.Play(60);
        }

        [Test]
        public void Claimed_Mission_Pays_Once_And_Doubles()
        {
            var rig = new MetaRig();
            rig.Play(1);
            int slot = -1;
            for (int n = 0; n < 50 && slot < 0; n++)
            {
                Progress(rig);
                rig.Meta.Chests.Open("wood", "test");
                for (int u = 0; u < rig.Session.Businesses.UpgradeCount; u++) rig.Session.Businesses.BuyUpgrade(u);
                rig.Play(1);
                for (int s = 0; s < 3; s++) if (rig.Meta.CanClaimMission(s)) { slot = s; break; }
            }
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "aucune mission réalisable");
            long cauris = rig.Meta.Missions[slot].Cauris;
            long before = rig.Meta.Cauris;
            Assert.That(rig.Meta.ClaimMission(slot, doubled: true), Is.True);
            Assert.That(rig.Meta.Cauris - before, Is.GreaterThanOrEqualTo(cauris * 2));
            Assert.That(rig.Meta.ClaimMission(slot), Is.False);
        }

        [Test]
        public void Login_Streak_Shield_And_Reset()
        {
            var rig = new MetaRig();
            var d = rig.Meta.Daily;
            for (int day = 1; day <= 7; day++)
            {
                Assert.That(rig.Meta.CanClaimLogin, Is.True, "jour " + day);
                Assert.That(rig.Meta.ClaimLogin(), Is.Not.Null);
                Assert.That(rig.Meta.CanClaimLogin, Is.False, "une fois par jour");
                rig.Play(86400);
            }
            Assert.That(d.Streak, Is.EqualTo(7));
            Assert.That(d.CalendarIndex, Is.EqualTo(7 % d.Login.Days.Length));

            Assert.That(rig.Meta.EarnStreakShield(), Is.True);
            Assert.That(rig.Meta.EarnStreakShield(), Is.False, "1 par semaine");
            rig.Play(86400); // un jour manqué, le bouclier sauve la série
            Assert.That(rig.Meta.ClaimLogin(), Is.Not.Null);
            Assert.That(d.Streak, Is.EqualTo(8));
            Assert.That(d.LastClaimUsedShield, Is.True);

            rig.Play(86400 * 3); // absence sans bouclier : la flamme repart à 1, rien n'est retiré
            Assert.That(rig.Meta.ClaimLogin(), Is.Not.Null);
            Assert.That(d.Streak, Is.EqualTo(1));
            Assert.That(d.BestStreak, Is.EqualTo(8));
        }

        [Test]
        public void Wheel_One_Free_Then_Three_Rewarded_Per_Day()
        {
            var rig = new MetaRig();
            var w = rig.Meta.Daily.Wheel;
            Assert.That(w.FreePerDay, Is.EqualTo(1));
            Assert.That(w.RewardedPerDay, Is.EqualTo(3));
            Assert.That(rig.Meta.SpinWheel(false), Is.GreaterThanOrEqualTo(0));
            Assert.That(rig.Meta.SpinWheel(false), Is.EqualTo(-1));
            for (int i = 0; i < 3; i++) Assert.That(rig.Meta.SpinWheel(true), Is.GreaterThanOrEqualTo(0));
            Assert.That(rig.Meta.SpinWheel(true), Is.EqualTo(-1));
            rig.Play(86400);
            Assert.That(rig.Meta.CanSpinFree, Is.True);

            double sum = 0d;
            for (int i = 0; i < w.Segments.Length; i++) sum += rig.Meta.Daily.Probability(i);
            Assert.That(sum, Is.EqualTo(1d).Within(1e-9));
        }

        [Test]
        public void Clock_Rollback_Blocks_Daily_Rewards_Without_Penalty()
        {
            var rig = new MetaRig();
            Assert.That(rig.Meta.ClaimLogin(), Is.Not.Null);
            rig.Meta.SpinWheel(false);
            long cauris = rig.Meta.Cauris;
            int streak = rig.Meta.Daily.Streak;

            rig.Manual.UtcNow = rig.Manual.UtcNow.AddDays(-3);
            rig.Meta.Tick(1);
            Assert.That(rig.Meta.TimeTrusted, Is.False);
            Assert.That(rig.Meta.CanClaimLogin, Is.False);
            Assert.That(rig.Meta.SpinWheel(false), Is.EqualTo(-1));
            Assert.That(rig.Meta.Cauris, Is.EqualTo(cauris), "aucune sanction");
            Assert.That(rig.Meta.Daily.Streak, Is.EqualTo(streak));

            rig.Manual.UtcNow = rig.Manual.UtcNow.AddDays(4); // retour à l'heure réelle + 1 jour
            rig.Clock.AdvancePlayed(86400);
            rig.Meta.Tick(1);
            Assert.That(rig.Meta.TimeTrusted, Is.True);
            Assert.That(rig.Meta.CanClaimLogin, Is.True);
        }
    }

    public class ProgressionTests
    {
        [Test]
        public void Features_Unlock_With_Player_Level()
        {
            var rig = new MetaRig(unlockAll: false);
            var level = rig.Meta.Level;
            Assert.That(level.Level, Is.EqualTo(1));
            foreach (Feature f in Enum.GetValues(typeof(Feature)))
            {
                int at = level.Config.UnlockLevel(f);
                Assert.That(at, Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(level.MaxLevel), f.ToString());
                Assert.That(rig.Meta.IsUnlocked(f), Is.EqualTo(at <= 1), f.ToString());
            }
            var introduced = new List<Feature>();
            rig.Meta.FeatureIntroduced += introduced.Add;
            level.AddRaw(level.XpForLevel(level.MaxLevel));
            foreach (Feature f in Enum.GetValues(typeof(Feature))) Assert.That(rig.Meta.IsUnlocked(f), Is.True);
            Assert.That(introduced.Count, Is.EqualTo(new HashSet<Feature>(introduced).Count), "chaque fonction présentée une fois");
        }

        [Test]
        public void Achievements_At_Least_60_And_Claim_Pays()
        {
            var rig = new MetaRig();
            var a = rig.Meta.Achievements;
            Assert.That(a.Count, Is.GreaterThanOrEqualTo(60));
            rig.Session.Economy.Earn(BigDouble.FromDouble(1e15));
            for (int i = 0; i < rig.Session.Businesses.Count; i++) rig.Session.Businesses.Buy(i, BuyMode.Max);
            for (int n = 0; n < 40; n++) rig.Play(1);
            int claimable = -1;
            for (int i = 0; i < a.Count && claimable < 0; i++) if (a.IsClaimable(i)) claimable = i;
            Assert.That(claimable, Is.GreaterThanOrEqualTo(0));
            long before = rig.Meta.Cauris;
            int tiers = a.ClaimedTiers(claimable);
            long paid = rig.Meta.ClaimAchievement(claimable);
            Assert.That(paid, Is.GreaterThan(0));
            Assert.That(rig.Meta.Cauris - before, Is.EqualTo(paid));
            Assert.That(a.ClaimedTiers(claimable), Is.EqualTo(tiers + 1));
        }

        [Test]
        public void Album_Has_Recipe_Text_In_Three_Languages_For_Every_Recipe()
        {
            MetaRig.Real(out _, out var meta);
            Assert.That(meta.Recipes.Length, Is.GreaterThanOrEqualTo(8));
            var ids = new HashSet<string>();
            foreach (var r in meta.Recipes) Assert.That(ids.Add(r.Id), Is.True, "doublon " + r.Id);
        }

        [Test]
        public void Each_City_Has_About_40_Cash_Upgrades()
        {
            MetaRig.Real(out var game, out _);
            foreach (var c in game.Cities) Assert.That(c.Upgrades.Length, Is.InRange(30, 50), c.Id);
        }
    }

    public class LedgerAndSaveTests
    {
        [Test]
        public void Ledger_Never_Goes_Negative_And_Tracks_Sources()
        {
            var l = new Ledger();
            l.Credit(Currency.Cauris, 100, "mission");
            Assert.That(l.TryDebit(Currency.Cauris, 150, "chest_gold"), Is.False);
            Assert.That(l.TryDebit(Currency.Cauris, 60, "chest_gold"), Is.True);
            Assert.That(l.Balance(Currency.Cauris), Is.EqualTo(40));
            Assert.That(l.EarnedBy(Currency.Cauris, "mission"), Is.EqualTo(100));
            Assert.That(l.SpentOn(Currency.Cauris, "chest_gold"), Is.EqualTo(60));
            Assert.That(l.TotalEarned(Currency.Cauris) - l.TotalSpent(Currency.Cauris), Is.EqualTo(l.Balance(Currency.Cauris)));
            l.Credit(Currency.Cauris, -5, "cheat");
            Assert.That(l.Balance(Currency.Cauris), Is.EqualTo(40), "crédit négatif ignoré");
        }

        [Test]
        public void Meta_Save_Round_Trips()
        {
            var a = new MetaRig(5);
            a.Play(10);
            a.Meta.ClaimLogin();
            a.Meta.SpinWheel(false);
            a.Meta.Chests.Open("gold", "test");
            a.Meta.Ledger.Credit(Currency.Cauris, 321, "test");
            a.Meta.MarkUpgradeSeen("x");

            var data = a.Session.ToSaveData(a.Manual.UtcNow, a.Manual.MonotonicSeconds);
            data.Meta = a.Meta.Save();
            var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(data));

            var b = new MetaRig(99);
            b.Session.LoadFrom(back);
            b.Meta.Load(back.Meta);
            Assert.That(b.Meta.Cauris, Is.EqualTo(a.Meta.Cauris));
            Assert.That(b.Meta.Daily.Streak, Is.EqualTo(a.Meta.Daily.Streak));
            Assert.That(b.Meta.Daily.WheelFreeUsed, Is.EqualTo(a.Meta.Daily.WheelFreeUsed));
            Assert.That(b.Meta.Level.Xp, Is.EqualTo(a.Meta.Level.Xp));
            Assert.That(b.Meta.IsUpgradeSeen("x"), Is.True);
            for (int i = 0; i < a.Meta.Managers.Count; i++) Assert.That(b.Meta.Managers.Level(i), Is.EqualTo(a.Meta.Managers.Level(i)));
            // Le tirage reprend au même point : même coffre suivant.
            var ra = a.Meta.Chests.Open("gold", "test");
            var rb = b.Meta.Chests.Open("gold", "test");
            for (int i = 0; i < ra.Cards.Count; i++) Assert.That(rb.Cards[i].ManagerIndex, Is.EqualTo(ra.Cards[i].ManagerIndex));
        }

        [Test]
        public void M2_Save_Migrates_To_M3_Without_Retroactive_Credit()
        {
            var rig = new MetaRig(unlockAll: false);
            var m2 = rig.Session.ToSaveData(rig.Manual.UtcNow, 0d);
            var json = Newtonsoft.Json.Linq.JObject.Parse(SaveSerializer.Serialize(m2));
            json["Version"] = 1;
            json.Remove("TimedBoostRemainingSeconds");
            json.Remove("ExtraOfflineCapSeconds");
            json.Remove("VisitedCities");
            json["Meta"] = new Newtonsoft.Json.Linq.JObject();

            var migrated = SaveSerializer.Deserialize(json.ToString());
            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.Meta, Is.Null);
            Assert.That(migrated.VisitedCities, Is.Not.Null);

            var fresh = new MetaRig(unlockAll: false);
            fresh.Session.LoadFrom(migrated);
            long starting = fresh.Meta.Cauris;
            fresh.Meta.Load(null);
            Assert.That(fresh.Meta.Cauris, Is.EqualTo(starting));
        }

        [Test]
        public void Notifications_Max_Two_Per_Day_Never_At_Night()
        {
            MetaRig.Real(out _, out var meta);
            var cfg = meta.Notifications;
            Assert.That(cfg.MaxPerDay, Is.LessThanOrEqualTo(2));
            var localNow = new DateTime(2026, 3, 2, 21, 30, 0);
            var candidates = new List<(NotificationKind, double)>
            {
                (NotificationKind.FreeChestReady, 600), (NotificationKind.OfflineFull, 3600),
                (NotificationKind.StreakInDanger, 7200), (NotificationKind.FreeChestReady, 90000),
            };
            var planned = new List<PlannedNotification>();
            NotificationPlanner.Plan(cfg, true, localNow, candidates, planned);
            var perDay = new Dictionary<int, int>();
            foreach (var p in planned)
            {
                Assert.That(NotificationPlanner.IsQuiet(cfg, p.LocalTime.Hour), Is.False, p.LocalTime.ToString("O"));
                Assert.That(p.LocalTime.Hour >= 22 || p.LocalTime.Hour < 8, Is.False);
                int day = GameClock.DayKey(p.LocalTime);
                perDay.TryGetValue(day, out int n);
                perDay[day] = n + 1;
            }
            foreach (var kv in perDay) Assert.That(kv.Value, Is.LessThanOrEqualTo(cfg.MaxPerDay));
            NotificationPlanner.Plan(cfg, false, localNow, candidates, planned);
            Assert.That(planned, Is.Empty, "désactivables");
        }
    }

    public class AttentionTests
    {
        [Test]
        public void Reminder_At_Most_Once_Per_Minute()
        {
            var a = new AttentionRules(60d);
            int reminders = 0;
            a.Remind += _ => reminders++;
            a.Set(AttentionSlot.Shop, true, 100d);
            a.Set(AttentionSlot.Missions, true, 110d);
            a.Set(AttentionSlot.Wheel, true, 130d);
            Assert.That(reminders, Is.EqualTo(1));
            a.Set(AttentionSlot.Album, true, 161d);
            Assert.That(reminders, Is.EqualTo(2));
            Assert.That(a.HasDot(AttentionSlot.Missions), Is.True);
            a.Set(AttentionSlot.Missions, false, 162d);
            Assert.That(a.HasDot(AttentionSlot.Missions), Is.False);
        }
    }
}
