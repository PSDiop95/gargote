using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Crée l'AudioMixer Master / Music / SFX / Voice avec volumes exposés. Unity n'offre pas d'API publique :
    /// on passe par l'API éditeur interne (réflexion). En cas d'échec, l'asset est laissé et un avertissement explique
    /// les 4 paramètres à exposer à la main (le jeu fonctionne sans mixer).
    /// </summary>
    internal static class MixerFactory
    {
        public static readonly string[] Groups = { "Music", "SFX", "Voice" };
        public static readonly string[] Params = { "MusicVolume", "SfxVolume", "VoiceVolume" };
        public const string MasterParam = "MasterVolume";

        public static AudioMixer CreateOrLoad(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (existing != null) return existing;
            try
            {
                var editorAssembly = typeof(Editor).Assembly;
                var controllerType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerController", true);
                var groupType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
                var create = controllerType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var controller = create.Invoke(null, new object[] { path });
                var master = controllerType.GetProperty("masterGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(controller);
                var createGroup = controllerType.GetMethod("CreateNewGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var addChild = controllerType.GetMethod("AddChildToParent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                Expose(controllerType, groupType, controller, master, MasterParam);
                for (int i = 0; i < Groups.Length; i++)
                {
                    var group = createGroup.Invoke(controller, new object[] { Groups[i], false });
                    addChild.Invoke(controller, new[] { group, master });
                    Expose(controllerType, groupType, controller, group, Params[i]);
                }
                EditorUtility.SetDirty((UnityEngine.Object)controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[M2] AudioMixer automatique impossible (" + e.GetBaseException().Message + "). " +
                                 "Créer Assets/_Game/Audio/Mixer/GargoteMixer.mixer avec les groupes Music, SFX, Voice " +
                                 "et exposer MasterVolume, MusicVolume, SfxVolume, VoiceVolume.");
            }
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
        }

        private static void Expose(Type controllerType, Type groupType, object controller, object group, string name)
        {
            var editorAssembly = typeof(Editor).Assembly;
            var guid = groupType.GetMethod("GetGUIDForVolume", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(group, null);
            var pathType = editorAssembly.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
            var parameterPath = Activator.CreateInstance(pathType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { group, guid }, null);
            controllerType.GetMethod("AddExposedParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(controller, new[] { parameterPath });

            // Renomme le paramètre exposé (par défaut « MyExposedParam »).
            var exposedProperty = controllerType.GetProperty("exposedParameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var array = (Array)exposedProperty.GetValue(controller);
            for (int i = 0; i < array.Length; i++)
            {
                object item = array.GetValue(i);
                var guidField = item.GetType().GetField("guid");
                if (!guidField.GetValue(item).Equals(guid)) continue;
                item.GetType().GetField("name").SetValue(item, name);
                array.SetValue(item, i);
            }
            exposedProperty.SetValue(controller, array);
        }

        public static AudioMixerGroup Find(AudioMixer mixer, string name)
        {
            if (mixer == null) return null;
            var groups = mixer.FindMatchingGroups(name);
            foreach (var g in groups) if (g.name == name) return g;
            return groups.Length > 0 ? groups[0] : null;
        }
    }
}
