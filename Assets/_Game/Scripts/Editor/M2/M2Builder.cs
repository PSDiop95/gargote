using System.IO;
using Gargote.Data;
using Gargote.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Génère tout le contenu M2 « Rue vivante » en placeholders : réglages (ScriptableObjects, créés une seule fois
    /// pour ne pas écraser l'équilibrage), meshes, matériaux, sprites, prefab diorama, sons, mixer, localisation, scène.
    /// Batch : unity run … -executeMethod Gargote.EditorTools.M2Builder.BuildBatch
    /// </summary>
    public static class M2Builder
    {
        private const string Root = "Assets/_Game";
        private const string Visual = Root + "/Data/Visual";
        private const string Generated = Root + "/Art/Generated";
        private const string MeshDir = Generated + "/Meshes";
        private const string SpriteDir = Generated + "/Sprites";
        private const string AudioDir = Root + "/Audio/Placeholder";
        private const string MixerPath = Root + "/Audio/Mixer/GargoteMixer.mixer";
        public const string ScenePath = Root + "/Scenes/Street.unity";
        public const string PrefabPath = Generated + "/Prefabs/BusinessDiorama.prefab";

        [MenuItem("Gargote/M2/1. Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null) return;
            // Import non interactif (synchrone) : l'importeur TMP de l'éditeur est asynchrone et ne finit pas en batch.
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Settings).Assembly);
            AssetDatabase.ImportPackage(info.resolvedPath + "/Package Resources/TMP Essential Resources.unitypackage", false);
        }

        /// <summary>Batch sans -quit : l'import de .unitypackage est asynchrone, on quitte sur son callback.</summary>
        public static void ImportTmpBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null)
            {
                EditorApplication.Exit(0);
                return;
            }
            AssetDatabase.importPackageCompleted += _ => { Debug.Log("[M2] TMP Essentials imported"); EditorApplication.Exit(0); };
            AssetDatabase.importPackageFailed += (_, error) => { Debug.LogError("[M2] TMP import failed: " + error); EditorApplication.Exit(1); };
            ImportTmpEssentials();
        }

        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        /// <summary>Régénère les réglages de disposition (rue, caméra, jour/nuit) avec les valeurs par défaut, puis toute la rue.</summary>
        [MenuItem("Gargote/M2/Reset layout defaults + rebuild")]
        public static void ResetLayoutAndBuild()
        {
            foreach (var name in new[] { "StreetConfig", "CameraConfig", "DayNightVisualConfig" })
                AssetDatabase.DeleteAsset(Visual + "/" + name + ".asset");
            Build();
        }

        public static void ResetLayoutBatch()
        {
            ResetLayoutAndBuild();
            EditorApplication.Exit(0);
        }

        [MenuItem("Gargote/M2/2. Build Street (assets + scene)")]
        public static void Build()
        {
            foreach (var dir in new[] { Visual, MeshDir, SpriteDir, Generated + "/Materials", Generated + "/Prefabs", AudioDir,
                         Root + "/Audio/Mixer", Root + "/Scenes" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            LocalizationSetup.Setup();

            var street = LoadOrCreate<StreetConfig>(Visual + "/StreetConfig.asset", DefaultStreet);
            var camera = LoadOrCreate<CameraConfig>(Visual + "/CameraConfig.asset", null);
            var crowd = LoadOrCreate<CrowdConfig>(Visual + "/CrowdConfig.asset", null);
            var juice = LoadOrCreate<JuiceConfig>(Visual + "/JuiceConfig.asset", null);
            var dayNight = LoadOrCreate<DayNightVisualConfig>(Visual + "/DayNightVisualConfig.asset", DefaultDayNight);
            var quality = LoadOrCreate<QualityConfig>(Visual + "/QualityConfig.asset", DefaultQuality);
            var hud = LoadOrCreate<HudConfig>(Visual + "/HudConfig.asset", h => { h.sheetCollapsedHeight = 560f; h.sheetExpandedHeight = 1150f; });
            var specials = LoadOrCreate<SpecialCustomersConfigAsset>(Root + "/Data/SpecialCustomersConfig.asset", DefaultSpecials);
            if (hud.font == null) { hud.font = TMP_Settings.defaultFontAsset; EditorUtility.SetDirty(hud); }
            if (quality.pipeline == null)
            {
                quality.pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                EditorUtility.SetDirty(quality);
            }

            var toon = SaveMaterial(Generated + "/Materials/Toon.mat", "Gargote/ToonVertexColor", m =>
            {
                m.SetFloat("_EmissionStrength", dayNight.lampEmissionBoost);
            });
            var particles = SaveMaterial(Generated + "/Materials/Particles.mat", "Gargote/UnlitVertexColor", null);

            var assets = LoadOrCreate<StreetAssets>(Visual + "/StreetAssets.asset", null);
            BuildMeshes(assets, street, crowd, specials);
            assets.toon = toon;
            assets.particles = particles;
            BuildSprites(assets);
            assets.businessPrefab = BuildPrefab(assets, street, toon);
            EditorUtility.SetDirty(assets);

            var audio = BuildAudio(juice);
            BuildScene(street, camera, crowd, juice, dayNight, audio, quality, hud, assets, specials);
            AssetDatabase.SaveAssets();
            Debug.Log("[M2] Street built: " + ScenePath);
        }

        // ---------- Réglages par défaut ----------

        private static T LoadOrCreate<T>(string path, System.Action<T> defaults) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            defaults?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void DefaultStreet(StreetConfig s)
        {
            s.slots = new StreetSlot[8];
            for (int i = 0; i < 8; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                s.slots[i] = new StreetSlot { position = new Vector3(side * s.businessX, 0f, i * s.slotSpacingZ), side = side };
            }
        }

        private static Gradient Gradient(params (float t, Color c)[] keys)
        {
            var g = new Gradient();
            var colors = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) colors[i] = new GradientColorKey(keys[i].c, keys[i].t);
            g.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static AnimationCurve Curve(params (float t, float v)[] keys)
        {
            var c = new AnimationCurve();
            foreach (var k in keys) c.AddKey(k.t, k.v);
            return c;
        }

        // Heures : 0 = minuit, 0,25 = 6 h, 0,5 = midi, 0,79 = 19 h (début des commerces de nuit, §3.3).
        private static void DefaultDayNight(DayNightVisualConfig d)
        {
            d.sunColor = Gradient((0f, new Color(0.30f, 0.35f, 0.60f)), (0.25f, new Color(1f, 0.62f, 0.38f)),
                (0.35f, new Color(1f, 0.92f, 0.80f)), (0.5f, new Color(1f, 0.97f, 0.90f)), (0.72f, new Color(1f, 0.76f, 0.46f)),
                (0.8f, new Color(0.95f, 0.48f, 0.36f)), (0.88f, new Color(0.32f, 0.36f, 0.62f)));
            d.sunIntensity = Curve((0f, 0.3f), (0.22f, 0.35f), (0.3f, 0.95f), (0.5f, 1.2f), (0.72f, 1f), (0.82f, 0.4f), (1f, 0.3f));
            // Nuit : « lune » haute, ombres courtes (pas de longues bandes sur la rue).
            d.sunPitch = Curve((0f, 60f), (0.2f, 50f), (0.27f, 22f), (0.5f, 62f), (0.76f, 22f), (0.84f, 55f), (1f, 60f));
            d.sunYaw = -35f;
            d.ambient = Gradient((0f, new Color(0.20f, 0.22f, 0.38f)), (0.25f, new Color(0.55f, 0.46f, 0.46f)),
                (0.5f, new Color(0.64f, 0.62f, 0.60f)), (0.78f, new Color(0.52f, 0.42f, 0.42f)), (0.88f, new Color(0.22f, 0.24f, 0.40f)));
            d.sky = Gradient((0f, new Color(0.05f, 0.07f, 0.18f)), (0.25f, new Color(0.95f, 0.62f, 0.46f)),
                (0.35f, new Color(0.56f, 0.78f, 0.95f)), (0.5f, new Color(0.46f, 0.72f, 0.95f)), (0.75f, new Color(0.95f, 0.60f, 0.36f)),
                (0.85f, new Color(0.12f, 0.12f, 0.30f)));
            d.lampIntensity = Curve((0f, 1f), (0.26f, 1f), (0.30f, 0f), (0.77f, 0f), (0.81f, 1f), (1f, 1f));
            d.lampEmissionBoost = 2.2f;
        }

        private static void DefaultQuality(QualityConfig q)
        {
            q.tiers = new[]
            {
                new QualityTierSettings { tier = QualityTier.Low, shadows = false, shadowDistance = 0f, msaa = 1, renderScale = 0.75f, maxCustomers = 12, particleScale = 0.4f, targetFrameRate = 60 },
                new QualityTierSettings { tier = QualityTier.Medium, shadows = true, shadowDistance = 40f, msaa = 1, renderScale = 0.85f, maxCustomers = 16, particleScale = 0.7f, targetFrameRate = 60 },
                new QualityTierSettings { tier = QualityTier.High, shadows = true, shadowDistance = 50f, msaa = 2, renderScale = 1f, maxCustomers = 20, particleScale = 1f, targetFrameRate = 60 },
            };
        }

        private static void DefaultSpecials(SpecialCustomersConfigAsset s)
        {
            s.minIntervalSeconds = 120;
            s.maxIntervalSeconds = 240;
            s.minUnlockedBusinesses = 2;
            s.customers = new[]
            {
                new SpecialCustomersConfigAsset.Entry { id = "official", nameKey = "special.official", weight = 3, bonusIncomeSeconds = 10, minBonus = 150, crossingSeconds = 9, color = new Color(0.45f, 0.47f, 0.52f) },
                new SpecialCustomersConfigAsset.Entry { id = "tourist", nameKey = "special.tourist", weight = 3, bonusIncomeSeconds = 15, minBonus = 200, crossingSeconds = 14, color = new Color(0.95f, 0.9f, 0.7f) },
                new SpecialCustomersConfigAsset.Entry { id = "wrestler", nameKey = "special.wrestler", weight = 2, bonusIncomeSeconds = 20, minBonus = 300, crossingSeconds = 12, color = new Color(0.85f, 0.2f, 0.2f) },
                new SpecialCustomersConfigAsset.Entry { id = "wedding", nameKey = "special.wedding", weight = 1, bonusIncomeSeconds = 60, minBonus = 1000, crossingSeconds = 15, color = new Color(0.95f, 0.8f, 0.3f) },
            };
        }

        // ---------- Rendu ----------

        private static Material SaveMaterial(string path, string shaderName, System.Action<Material> setup)
        {
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = true;
            setup?.Invoke(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void BuildMeshes(StreetAssets assets, StreetConfig street, CrowdConfig crowd, SpecialCustomersConfigAsset specials)
        {
            int businesses = street.slots.Length;
            assets.stageMeshes = new Mesh[businesses * Placeholders.StageCount];
            for (int b = 0; b < businesses; b++)
            {
                Color accent = street.businessAccents[b % street.businessAccents.Length];
                for (int s = 0; s < Placeholders.StageCount; s++)
                    assets.stageMeshes[b * Placeholders.StageCount + s] = SaveMesh(Placeholders.BusinessStage(s, accent), "Business" + b + "_Stage" + s);
            }
            assets.street = SaveMesh(Placeholders.Street(street), "Street");

            assets.customerMeshes = new Mesh[crowd.outfits.Length];
            for (int i = 0; i < crowd.outfits.Length; i++)
            {
                Color skin = crowd.skinTones[i % crowd.skinTones.Length];
                assets.customerMeshes[i] = SaveMesh(Placeholders.Person(Placeholders.PersonKind.Customer, crowd.outfits[i], skin, crowd.outfits[(i + 2) % crowd.outfits.Length]), "Customer" + i);
            }
            assets.employeeMesh = SaveMesh(Placeholders.Person(Placeholders.PersonKind.Employee, crowd.employeeOutfit, crowd.skinTones[0], new Color(0.9f, 0.3f, 0.1f)), "Employee");
            assets.managerMesh = SaveMesh(Placeholders.Person(Placeholders.PersonKind.Manager, crowd.managerOutfit, crowd.skinTones[1], crowd.managerHat), "Manager");

            var kinds = new[] { Placeholders.PersonKind.Official, Placeholders.PersonKind.Tourist, Placeholders.PersonKind.Wrestler, Placeholders.PersonKind.Delegation };
            var outfits = new[] { new Color(0.35f, 0.37f, 0.42f), new Color(0.95f, 0.92f, 0.80f), new Color(0.2f, 0.2f, 0.2f), new Color(0.98f, 0.88f, 0.45f) };
            assets.specialMeshes = new Mesh[specials.customers.Length];
            for (int i = 0; i < specials.customers.Length; i++)
            {
                int k = Mathf.Min(i, kinds.Length - 1);
                assets.specialMeshes[i] = SaveMesh(Placeholders.Person(kinds[k], outfits[k], crowd.skinTones[i % crowd.skinTones.Length], specials.customers[i].color), "Special_" + specials.customers[i].id);
            }
            assets.carryMeshes = new[] { SaveMesh(Placeholders.Carry(0), "CarryBag"), SaveMesh(Placeholders.Carry(1), "CarryPlate") };

            SaveMesh(Placeholders.LockedShell(), "LockedShell");
            SaveMesh(Placeholders.Shutter(street.shutter), "Shutter");
            SaveMesh(Placeholders.ShutterGlow(street.shutterGlow), "ShutterGlow");
            SaveMesh(Placeholders.Lamp(), "Lamp");
            SaveMesh(Placeholders.BarBack(street.progressBack), "BarBack");
            SaveMesh(Placeholders.BarFill(street.progressFill), "BarFill");
            SaveMesh(Placeholders.BillStack(new Color(0.45f, 0.75f, 0.45f)), "BillStack");
        }

        private static Mesh LoadMesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/" + name + ".asset");

        private static BusinessView BuildPrefab(StreetAssets assets, StreetConfig street, Material toon)
        {
            var root = new GameObject("BusinessDiorama");
            var view = root.AddComponent<BusinessView>();

            MeshRenderer Child(string name, Transform parent, Vector3 position, Mesh mesh, bool shadows, out MeshFilter filter)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                filter = go.GetComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = toon;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                return r;
            }

            Transform Anchor(string name, Vector3 position)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root.transform, false);
                t.localPosition = position;
                return t;
            }

            view.stageRenderer = Child("Stage", root.transform, Vector3.zero, assets.Stage(0, 0), true, out view.stageFilter);
            view.lockedShell = Child("LockedShell", root.transform, Vector3.zero, LoadMesh("LockedShell"), true, out _);
            view.shutter = Child("Shutter", root.transform, new Vector3(0f, 0f, -1.38f), LoadMesh("Shutter"), false, out _).transform;
            view.shutterGlow = Child("ShutterGlow", root.transform, new Vector3(0f, 0f, -1.38f), LoadMesh("ShutterGlow"), false, out _);
            view.lamp = Child("Lamp", root.transform, new Vector3(1.9f, 0f, -1.9f), LoadMesh("Lamp"), false, out _);
            var bar = new GameObject("Bar").transform;
            bar.SetParent(root.transform, false);
            bar.localPosition = new Vector3(0f, 4.4f, -1.6f);
            bar.localScale = new Vector3(2.2f, 1f, 1f);
            view.barRoot = bar;
            Child("BarBack", bar, Vector3.zero, LoadMesh("BarBack"), false, out _);
            view.barFill = Child("BarFill", bar, new Vector3(-0.5f, 0f, -0.03f), LoadMesh("BarFill"), false, out view.barFillFilter).transform;
            view.pile = Child("Pile", root.transform, new Vector3(0.75f, 1.08f, -1.25f), LoadMesh("BillStack"), false, out _).transform;
            view.counter = Anchor("Counter", new Vector3(0f, 0f, -1.6f));
            view.employeeAnchor = Anchor("Employee", new Vector3(-0.5f, 0f, -0.55f));
            view.managerAnchor = Anchor("Manager", new Vector3(1.05f, 0f, -0.45f));
            view.labelAnchor = Anchor("Label", new Vector3(0f, 3.8f, -1.5f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<BusinessView>();
        }

        // ---------- Sprites UI ----------

        private static void BuildSprites(StreetAssets assets)
        {
            assets.rounded = SaveSprite("Rounded", 128, 128, (x, y) => RoundedRect(x, y, 128, 128, 40), new Vector4(44, 44, 44, 44));
            assets.circle = SaveSprite("Circle", 128, 128, (x, y) => Disk(x - 64f, y - 64f, 62f), Vector4.zero);
            assets.bill = SaveSprite("Bill", 160, 96, (x, y) =>
            {
                float a = RoundedRect(x, y, 160, 96, 12);
                bool border = x < 10 || x > 149 || y < 10 || y > 85;
                float motif = Disk(x - 80f, y - 48f, 24f) > 0.5f ? 0.8f : 1f;
                float shade = border ? 0.82f : motif;
                return new Color(shade, shade, shade, a);
            }, Vector4.zero);
            assets.hand = SaveSprite("Hand", 128, 128, (x, y) =>
            {
                // Main qui pointe vers le haut : paume + index (blanc, contour sombre).
                float palm = RoundedRect(x - 30, y - 8, 70, 62, 22);
                float finger = RoundedRect(x - 50, y - 60, 26, 64, 13);
                float a = Mathf.Max(palm, finger);
                float inner = Mathf.Max(RoundedRect(x - 35, y - 13, 60, 52, 18), RoundedRect(x - 55, y - 65, 16, 54, 8));
                float shade = inner > 0.5f ? 1f : 0.15f;
                return new Color(shade, shade, shade, a);
            }, Vector4.zero);
            assets.star = SaveSprite("Star", 128, 128, (x, y) =>
            {
                float dx = x - 64f, dy = y - 64f;
                float angle = Mathf.Atan2(dy, dx), r = Mathf.Sqrt(dx * dx + dy * dy);
                float k = Mathf.Cos(5f * angle) * 0.5f + 0.5f;
                return r < Mathf.Lerp(28f, 60f, k) ? 1f : 0f;
            }, Vector4.zero);
        }

        private static float Disk(float dx, float dy, float radius) => Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);

        private static float RoundedRect(float x, float y, float w, float h, float r)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return 0f;
            float cx = Mathf.Clamp(x, r, w - r), cy = Mathf.Clamp(y, r, h - r);
            return Disk(x - cx, y - cy, r);
        }

        private static Sprite SaveSprite(string name, int w, int h, System.Func<int, int, float> alpha, Vector4 border) =>
            SaveSprite(name, w, h, (x, y) => new Color(1f, 1f, 1f, alpha(x, y)), border);

        private static Sprite SaveSprite(string name, int w, int h, System.Func<int, int, Color> pixel, Vector4 border)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    texture.SetPixel(x, y, pixel(x, y));
            texture.Apply();
            string path = SpriteDir + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- Audio ----------

        private static AudioConfig BuildAudio(JuiceConfig juice)
        {
            PlaceholderAudio.WriteAll(AudioDir);
            AssetDatabase.Refresh();
            var bank = LoadOrCreate<SoundBank>(Visual + "/SoundBank.asset", null);
            bool fresh = bank.entries.Length == 0;
            var ids = (SoundId[])System.Enum.GetValues(typeof(SoundId));
            if (fresh) bank.entries = new SoundEntry[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                string path = AudioDir + "/" + PlaceholderAudio.FileName(id);
                bool loop = id == SoundId.Grill || id == SoundId.Fry || id == SoundId.CrowdLoop || id == SoundId.MusicDay || id == SoundId.MusicNight;
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                settings.quality = 0.5f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!fresh)
                {
                    var existing = bank.Find(id);
                    if (existing != null) { existing.clips = new[] { clip }; continue; }
                    System.Array.Resize(ref bank.entries, bank.entries.Length + 1);
                    i = bank.entries.Length - 1 - (ids.Length - 1 - i);
                }
                var entry = new SoundEntry { id = id, clips = new[] { clip } };
                Defaults(entry, juice);
                bank.entries[fresh ? i : bank.entries.Length - 1] = entry;
            }
            EditorUtility.SetDirty(bank);

            var mixer = MixerFactory.CreateOrLoad(MixerPath);
            return LoadOrCreate<AudioConfig>(Visual + "/AudioConfig.asset", a =>
            {
                a.mixer = mixer;
                a.master = MixerFactory.Find(mixer, "Master");
                a.music = MixerFactory.Find(mixer, "Music");
                a.sfx = MixerFactory.Find(mixer, "SFX");
                a.voice = MixerFactory.Find(mixer, "Voice");
                a.bank = bank;
            });
        }

        private static void Defaults(SoundEntry e, JuiceConfig juice)
        {
            e.bus = AudioBus.Sfx;
            e.maxVoices = 2;
            e.minInterval = 0.05f;
            switch (e.id)
            {
                case SoundId.Tap: e.volume = 0.7f; e.minInterval = 0.03f; e.maxVoices = 4; break;
                case SoundId.Bill: e.volume = 0.35f; e.minInterval = 0.04f; e.maxVoices = 4; break;
                case SoundId.Cash: e.volume = 0.45f; e.minInterval = 0.15f; break;
                case SoundId.Buy: e.volume = 0.6f; e.minInterval = juice.buySoundMinInterval; e.maxVoices = 3; break;
                case SoundId.Milestone: e.volume = 0.9f; e.minInterval = 0.3f; e.maxVoices = 1; break;
                case SoundId.Unlock: e.volume = 0.9f; e.minInterval = 0.5f; e.maxVoices = 1; break;
                case SoundId.Shutter: e.volume = 0.8f; e.minInterval = 0.5f; e.maxVoices = 1; break;
                case SoundId.Grill: e.volume = 0.22f; e.bus = AudioBus.Ambience; break;
                case SoundId.Fry: e.volume = 0.2f; e.bus = AudioBus.Ambience; break;
                case SoundId.CrowdLoop: e.volume = 0.35f; e.bus = AudioBus.Ambience; break;
                case SoundId.Horn: e.volume = 0.3f; e.minInterval = 2f; e.maxVoices = 1; break;
                case SoundId.Collect: e.volume = 0.6f; e.minInterval = 0.08f; break;
                case SoundId.Special: e.volume = 0.8f; e.minInterval = 0.5f; e.maxVoices = 1; break;
                case SoundId.Manager: e.volume = 0.7f; e.bus = AudioBus.Voice; e.minInterval = 0.3f; e.maxVoices = 1; break;
                case SoundId.MusicDay: e.volume = 0.45f; e.bus = AudioBus.Music; break;
                case SoundId.MusicNight: e.volume = 0.45f; e.bus = AudioBus.Music; break;
                case SoundId.Button: e.volume = 0.5f; e.minInterval = 0.04f; e.maxVoices = 3; break;
            }
        }

        // ---------- Scène ----------

        private static void BuildScene(StreetConfig street, CameraConfig camera, CrowdConfig crowd, JuiceConfig juice,
            DayNightVisualConfig dayNight, AudioConfig audio, QualityConfig quality, HudConfig hud, StreetAssets assets,
            SpecialCustomersConfigAsset specials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = camera.orthographicSize;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraGo.AddComponent<CameraRig>();

            var sunGo = new GameObject("Sun", typeof(Light));
            var sun = sunGo.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.55f;
            sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var rootGo = new GameObject("GameRoot");
            var root = rootGo.AddComponent<GameRoot>();
            root.database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GargotePaths.GameDatabase);
            root.specialCustomers = specials;
            root.cameraConfig = camera;
            root.streetConfig = street;
            root.crowdConfig = crowd;
            root.juiceConfig = juice;
            root.dayNightConfig = dayNight;
            root.audioConfig = audio;
            root.qualityConfig = quality;
            root.hudConfig = hud;
            root.assets = assets;
            root.mainCamera = cam;
            root.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.skybox = null;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
