using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    /// <summary>
    /// Contenu et règles méta (M3) : gérants, coffres, missions, connexion, roue, niveau joueur, album, succès.
    /// Surchargeable par Remote Config (M5). Ids stables : ils servent de clés de sauvegarde et de localisation.
    /// </summary>
    [CreateAssetMenu(menuName = "Gargote/Meta/Meta Config", fileName = "MetaConfig")]
    public sealed class MetaConfigAsset : ScriptableObject
    {
        [Header("Gérants")]
        public ManagerDefinitionAsset[] managers = new ManagerDefinitionAsset[0];
        public ManagerLevelConfig managerLevels = new ManagerLevelConfig();

        [Header("Coffres")]
        public ChestDefinitionAsset[] chests = new ChestDefinitionAsset[0];
        public ChestRules chestRules = new ChestRules();

        [Header("Missions et défis du jour")]
        public MissionDefinition[] missions = new MissionDefinition[0];
        public MissionDefinition[] challenges = new MissionDefinition[0];
        public MissionRules missionRules = new MissionRules();

        [Header("Quotidien")]
        public LoginConfig login = new LoginConfig();
        public WheelConfig wheel = new WheelConfig();

        [Header("Progression longue")]
        public PlayerLevelConfig playerLevels = new PlayerLevelConfig();
        public RecipeDefinitionAsset[] recipes = new RecipeDefinitionAsset[0];
        [Tooltip("Album d'une ville complet : multiplicateur permanent de revenus")]
        public double albumCityBonusMultiplier = 1.05;
        public AchievementDefinition[] achievements = new AchievementDefinition[0];

        [Header("Divers")]
        public NotificationConfig notifications = new NotificationConfig();
        public CaurisConfig cauris = new CaurisConfig();
        [Tooltip("Anti-spam des points rouges : un rappel au plus toutes les N secondes")]
        public double minSecondsBetweenReminders = 60;

        public MetaDefinitions ToDefinitions(GameDefinitions game = null)
        {
            var defs = new MetaDefinitions
            {
                Managers = new ManagerDefinition[managers.Length],
                ManagerLevels = managerLevels,
                Chests = new ChestDefinition[chests.Length],
                ChestRules = chestRules,
                Missions = missions,
                Challenges = challenges,
                MissionRules = missionRules,
                Login = login,
                Wheel = wheel,
                PlayerLevels = playerLevels,
                Recipes = new RecipeDefinition[recipes.Length],
                AlbumCityBonusMultiplier = albumCityBonusMultiplier,
                Achievements = achievements,
                Notifications = notifications,
                Cauris = cauris,
                MinSecondsBetweenReminders = minSecondsBetweenReminders,
            };
            for (int i = 0; i < managers.Length; i++) defs.Managers[i] = managers[i].definition;
            for (int i = 0; i < chests.Length; i++) defs.Chests[i] = chests[i].definition;
            for (int i = 0; i < recipes.Length; i++) defs.Recipes[i] = recipes[i].definition;
            defs.Validate(game);
            return defs;
        }
    }
}
