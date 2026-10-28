using UnityEditor;
using UnityEngine;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Réglages joueur du projet (identité, portrait). Batchmode :
    /// -executeMethod Gargote.EditorTools.ProjectSetup.Apply
    /// </summary>
    public static class ProjectSetup
    {
        private const string CompanyName = "Smart IA Services";
        private const string ProductName = "Gargote Tycoon";
        private const string ApplicationId = "com.smartiaservices.gargotetycoon";

        [MenuItem("Gargote/Project/Apply player settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, ApplicationId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Gargote] Player settings applied.");
        }
    }
}
