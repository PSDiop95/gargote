using System;
using Gargote.Core;
using Gargote.Systems.Meta;
using TMPro;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Réglages propres à l'appareil (son, effets, qualité) : PlayerPrefs, jamais dans la sauvegarde ni le cloud.
    /// Les notifications vivent dans la sauvegarde méta (elles suivent le joueur).
    /// </summary>
    public sealed class DeviceSettings
    {
        private const string Prefix = "gargote.settings.";
        public bool Music = true, Sfx = true, ReducedEffects;
        /// <summary>−1 = automatique (détection RAM / GPU), sinon <see cref="QualityTier"/>.</summary>
        public int Quality = -1;

        public event Action Changed;

        public static DeviceSettings Load() => new DeviceSettings
        {
            Music = PlayerPrefs.GetInt(Prefix + "music", 1) == 1,
            Sfx = PlayerPrefs.GetInt(Prefix + "sfx", 1) == 1,
            ReducedEffects = PlayerPrefs.GetInt(Prefix + "reduced", 0) == 1,
            Quality = PlayerPrefs.GetInt(Prefix + "quality", -1),
        };

        public void Save()
        {
            PlayerPrefs.SetInt(Prefix + "music", Music ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "sfx", Sfx ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "reduced", ReducedEffects ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "quality", Quality);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Écran Réglages (bouton ≡ du HUD) : notifications, musique, sons, effets réduits, qualité.</summary>
    public sealed class SettingsScreen : MetaScreen
    {
        private const float RowHeight = 150f;
        private readonly MetaUiContext _ctx;
        private readonly DeviceSettings _settings;
        private readonly ListRow _notifications, _music, _sfx, _reduced, _quality;
        private readonly string _on, _off;
        private readonly string[] _qualityNames;

        public SettingsScreen(RectTransform parent, MetaUiContext ctx, DeviceSettings settings)
            : base(parent, "Settings", ctx.Hud, ctx.Assets, new Vector2(960f, 130f + 5 * RowHeight + 40f), Loc.Get("settings.title"))
        {
            _ctx = ctx;
            _settings = settings;
            _on = Loc.Get("settings.on");
            _off = Loc.Get("settings.off");
            _qualityNames = new[]
            {
                Loc.Get("settings.quality_auto"), Loc.Get("settings.quality_low"), Loc.Get("settings.quality_medium"), Loc.Get("settings.quality_high"),
            };
            var content = MetaUiKit.ScrollList(Card, "List", new Vector2(24f, 24f), new Vector2(-24f, -130f), out _);
            content.sizeDelta = new Vector2(0f, 5 * RowHeight);
            _notifications = Row(content, 0, "settings.notifications", "settings.notifications_hint", ToggleNotifications);
            _music = Row(content, 1, "settings.music", null, () => { _settings.Music = !_settings.Music; _settings.Save(); Refresh(); });
            _sfx = Row(content, 2, "settings.sfx", null, () => { _settings.Sfx = !_settings.Sfx; _settings.Save(); Refresh(); });
            _reduced = Row(content, 3, "settings.reduced_effects", "settings.reduced_hint",
                () => { _settings.ReducedEffects = !_settings.ReducedEffects; _settings.Save(); Refresh(); });
            _quality = Row(content, 4, "settings.quality", null,
                () => { _settings.Quality = _settings.Quality >= 2 ? -1 : _settings.Quality + 1; _settings.Save(); Refresh(); });
        }

        private ListRow Row(RectTransform content, int index, string key, string hintKey, Action click)
        {
            var row = ListRow.Build(content, index, RowHeight, Hud, Assets, "Setting", 0.32f);
            row.Name.text = Loc.Get(key);
            row.Sub.text = hintKey != null ? Loc.Get(hintKey) : string.Empty;
            row.SetBadge(null);
            row.Action.Clicked = click;
            return row;
        }

        private void ToggleNotifications()
        {
            var meta = _ctx.Meta;
            if (meta.NotificationsEnabled) { meta.NotificationsEnabled = false; Refresh(); return; }
            meta.NotificationsEnabled = true;
            _ctx.RequestNotificationPermission?.Invoke(granted =>
            {
                if (!granted) meta.NotificationsEnabled = false;
                Refresh();
            });
            Refresh();
        }

        private void SetToggle(ListRow row, bool on)
        {
            row.Action.Label.text = on ? _on : _off;
            var c = on ? Hud.accent : Hud.panel;
            row.Action.EnabledColor = c;
            row.Action.Background.color = c;
        }

        public override void Refresh()
        {
            SetToggle(_notifications, _ctx.Meta.NotificationsEnabled);
            SetToggle(_music, _settings.Music);
            SetToggle(_sfx, _settings.Sfx);
            SetToggle(_reduced, _settings.ReducedEffects);
            _quality.Action.Label.text = _qualityNames[_settings.Quality + 1];
        }
    }

    /// <summary>
    /// Demande douce avant la demande système (§7.6) : après le premier coffre gratuit, jamais au premier lancement.
    /// « Non merci » ne redemande pas (la bascule reste disponible dans Réglages).
    /// </summary>
    public sealed class NotificationPromptScreen : MetaScreen
    {
        private readonly MetaUiContext _ctx;

        public NotificationPromptScreen(RectTransform parent, MetaUiContext ctx)
            : base(parent, "NotificationPrompt", ctx.Hud, ctx.Assets, new Vector2(900f, 700f), Loc.Get("settings.notifications"), false)
        {
            _ctx = ctx;
            MetaUiKit.Paragraph(Card, "Text", Hud, Hud.fontSizeSmall, Hud.text, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(60f, 260f), new Vector2(-60f, -130f), TextAlignmentOptions.Center).text = Loc.Get("notif.permission");
            var yes = MetaUiKit.Button(Card, "Allow", Hud, Assets, Loc.Get("notif.allow"), Hud.fontSizeMedium * 0.85f,
                new Vector2(0.52f, 0f), new Vector2(0.94f, 0f), new Vector2(0f, 60f), new Vector2(0f, 190f), Hud.accent);
            yes.Clicked = () =>
            {
                Hide();
                _ctx.Meta.NotificationsEnabled = true;
                _ctx.RequestNotificationPermission?.Invoke(granted => _ctx.Meta.NotificationsEnabled = granted);
            };
            var no = MetaUiKit.Button(Card, "Deny", Hud, Assets, Loc.Get("notif.deny"), Hud.fontSizeSmall,
                new Vector2(0.06f, 0f), new Vector2(0.48f, 0f), new Vector2(0f, 60f), new Vector2(0f, 190f), Hud.panelLight);
            no.Clicked = () =>
            {
                Hide();
                _ctx.Meta.NotificationsEnabled = false;
            };
        }

        public override void Refresh() { }
    }
}
