using System;
using System.Collections.Generic;
using Gargote.Systems.Meta;

namespace Gargote.Services.Notifications
{
    /// <summary>
    /// Notifications locales (§7.6). Le planning (max 2 par jour, jamais 22 h - 8 h) est calculé par
    /// <see cref="NotificationPlanner"/> ; le service ne fait que programmer ce qu'on lui donne.
    /// </summary>
    public interface INotificationService
    {
        bool HasPermission { get; }
        /// <summary>Demande l'autorisation système ; <paramref name="onDone"/> reçoit la réponse.</summary>
        void RequestPermission(Action<bool> onDone);
        /// <summary>Remplace toutes les notifications programmées par celles-ci.</summary>
        void Schedule(IReadOnlyList<PlannedNotification> planned, Func<NotificationKind, string> text);
        void CancelAll();
    }

    /// <summary>Mock éditeur / tests : garde la dernière programmation en mémoire.</summary>
    public sealed class MockNotificationService : INotificationService
    {
        public bool Granted = true;
        public readonly List<(NotificationKind Kind, DateTime At, string Text)> Scheduled = new List<(NotificationKind, DateTime, string)>();
        public bool HasPermission { get; private set; }

        public void RequestPermission(Action<bool> onDone)
        {
            HasPermission = Granted;
            onDone?.Invoke(Granted);
        }

        public void Schedule(IReadOnlyList<PlannedNotification> planned, Func<NotificationKind, string> text)
        {
            Scheduled.Clear();
            if (!HasPermission) return;
            foreach (var p in planned) Scheduled.Add((p.Kind, p.LocalTime, text != null ? text(p.Kind) : p.Kind.ToString()));
        }

        public void CancelAll() => Scheduled.Clear();
    }
}
