using System;
using System.Collections.Generic;
using Gargote.Core;

namespace Gargote.Systems.Meta
{
    /// <summary>Onglets et boutons qui portent un point rouge.</summary>
    public enum AttentionSlot
    {
        Managers = 0,
        Upgrades = 1,
        Cities = 2,
        Shop = 3,
        Missions = 4,
        Login = 5,
        Wheel = 6,
        Album = 7,
        Achievements = 8,
        Challenges = 9,
        Prestige = 10,
    }

    /// <summary>
    /// Règle unique des points rouges : un point = une action possible maintenant qui rapporte quelque chose
    /// (réclamer, ouvrir, acheter pour la première fois). Jamais pour une simple information.
    /// Les rappels animés (bulle, pulsation) sont limités à un par <see cref="MetaDefinitions.MinSecondsBetweenReminders"/>
    /// toutes fonctionnalités confondues ; le point statique, lui, reste affiché tant que l'action existe.
    /// </summary>
    public sealed class AttentionRules
    {
        private const int SlotCount = 11;
        private readonly bool[] _dot = new bool[SlotCount];
        private readonly bool[] _reminded = new bool[SlotCount];
        private readonly double _minGap;
        private double _lastReminder = double.NegativeInfinity;

        public event Action<AttentionSlot> Remind;

        public AttentionRules(double minSecondsBetweenReminders) { _minGap = Math.Max(0d, minSecondsBetweenReminders); }

        public bool HasDot(AttentionSlot slot) => _dot[(int)slot];
        public double LastReminderSeconds => _lastReminder;

        /// <summary>Met à jour un point. Un point qui apparaît peut déclencher un rappel, si le délai anti-spam est passé.</summary>
        public void Set(AttentionSlot slot, bool on, double nowSeconds)
        {
            int i = (int)slot;
            _dot[i] = on;
            if (!on) { _reminded[i] = false; return; }
            if (_reminded[i] || nowSeconds - _lastReminder < _minGap) return;
            _reminded[i] = true;
            _lastReminder = nowSeconds;
            Remind?.Invoke(slot);
        }
    }

    public enum NotificationKind
    {
        FreeChestReady = 0,
        OfflineFull = 1,
        StreakInDanger = 2,
        DailyReady = 3,
    }

    public readonly struct PlannedNotification
    {
        public readonly NotificationKind Kind;
        /// <summary>Heure locale de déclenchement.</summary>
        public readonly DateTime LocalTime;

        public PlannedNotification(NotificationKind kind, DateTime localTime)
        {
            Kind = kind;
            LocalTime = localTime;
        }
    }

    /// <summary>
    /// Plan des notifications locales, recalculé à chaque mise en arrière-plan : au plus N par jour local, jamais dans
    /// la plage de silence (décalées à sa fin), désactivables. Logique pure : le service (mobile ou mock) ne fait que
    /// programmer la liste.
    /// </summary>
    public static class NotificationPlanner
    {
        public static void Plan(NotificationConfig config, bool enabled, DateTime localNow,
            IReadOnlyList<(NotificationKind kind, double inSeconds)> candidates, List<PlannedNotification> into)
        {
            into.Clear();
            if (!enabled || candidates == null) return;
            var sorted = new List<(NotificationKind kind, DateTime at)>(candidates.Count);
            foreach (var c in candidates)
                if (c.inSeconds >= 0d && !double.IsInfinity(c.inSeconds))
                    sorted.Add((c.kind, OutOfQuietHours(config, localNow.AddSeconds(Math.Max(60d, c.inSeconds)))));
            sorted.Sort((a, b) => a.at.CompareTo(b.at));
            var perDay = new Dictionary<int, int>();
            foreach (var s in sorted)
            {
                int day = GameClock.DayKey(s.at);
                perDay.TryGetValue(day, out int n);
                if (n >= config.MaxPerDay) continue;
                bool duplicate = false;
                foreach (var p in into) if (p.Kind == s.kind) duplicate = true;
                if (duplicate) continue;
                perDay[day] = n + 1;
                into.Add(new PlannedNotification(s.kind, s.at));
            }
        }

        public static bool IsQuiet(NotificationConfig config, int hour)
        {
            int start = config.QuietStartHour, end = config.QuietEndHour;
            if (start == end) return false;
            return start < end ? hour >= start && hour < end : hour >= start || hour < end;
        }

        /// <summary>Repousse une heure tombant dans la plage de silence à la fin de celle-ci.</summary>
        public static DateTime OutOfQuietHours(NotificationConfig config, DateTime local)
        {
            if (!IsQuiet(config, local.Hour)) return local;
            var end = local.Date.AddHours(config.QuietEndHour);
            if (end <= local) end = end.AddDays(1);
            return end;
        }
    }
}
