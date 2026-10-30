using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Géométries placeholder (formes simples colorées) générées par l'éditeur et sauvegardées en assets.
    /// Repère commerce : façade vers -Z (caméra), comptoir en z ≈ -1,6, origine au sol au centre.
    /// Repère personnage : pieds à l'origine, regard vers -Z, hauteur ≈ 1,1.
    /// </summary>
    public static class Placeholders
    {
        public const int StageCount = 5;
        private static readonly Color Wood = new Color(0.55f, 0.36f, 0.20f);
        private static readonly Color DarkWood = new Color(0.36f, 0.22f, 0.12f);
        private static readonly Color Metal = new Color(0.30f, 0.31f, 0.34f);
        private static readonly Color Wall = new Color(0.95f, 0.88f, 0.74f);
        private static readonly Color Ember = new Color(1f, 0.45f, 0.10f);
        private static readonly Color Cloth = new Color(0.97f, 0.95f, 0.90f);
        private static readonly Color Food = new Color(0.85f, 0.55f, 0.20f);
        private static readonly Color Window = new Color(1f, 0.85f, 0.45f);

        public static Mesh BusinessStage(int stage, Color accent)
        {
            var b = new MeshBuilder();
            switch (stage)
            {
                case 0: Cart(b, accent); break;
                case 1: Stall(b, accent); break;
                case 2: Tarp(b, accent); break;
                case 3: Shop(b, accent); break;
                default: Neon(b, accent); break;
            }
            return b.Build("Stage" + stage);
        }

        // Brouette-fourneau : caisse, roues, grill aux braises.
        private static void Cart(MeshBuilder b, Color accent)
        {
            b.BoxOnGround(new Vector3(0f, 0.35f, -0.9f), new Vector3(1.5f, 0.45f, 0.8f), accent);
            b.BoxOnGround(new Vector3(0f, 0.8f, -0.9f), new Vector3(1.3f, 0.12f, 0.6f), Metal);
            b.BoxOnGround(new Vector3(0f, 0.92f, -0.9f), new Vector3(1.1f, 0.05f, 0.45f), Ember, true);
            Wheel(b, new Vector3(-0.55f, 0.25f, -1.32f));
            Wheel(b, new Vector3(0.55f, 0.25f, -1.32f));
            b.BoxOnGround(new Vector3(0.95f, 0.55f, -0.9f), new Vector3(0.7f, 0.06f, 0.08f), DarkWood);
            b.BoxOnGround(new Vector3(-0.3f, 0f, 0.2f), new Vector3(0.5f, 0.5f, 0.5f), DarkWood);
        }

        private static void Wheel(MeshBuilder b, Vector3 center)
        {
            b.SetTransform(center, Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            b.Cylinder(new Vector3(0f, -0.05f, 0f), 0.25f, 0.1f, 8, DarkWood);
            b.ResetTransform();
        }

        // Étal : table, pieds, cagettes, marmite.
        private static void Stall(MeshBuilder b, Color accent)
        {
            Table(b, accent, 2.4f);
            b.Cylinder(new Vector3(-0.6f, 0.95f, -1.0f), 0.28f, 0.35f, 8, Metal);
            b.Cylinder(new Vector3(0.5f, 0.95f, -1.0f), 0.3f, 0.06f, 8, Cloth);
            b.Sphere(new Vector3(0.5f, 1.06f, -1.0f), 0.18f, Food, 3, 6, false, 0.6f);
            b.BoxOnGround(new Vector3(0.9f, 0f, 0.1f), new Vector3(0.6f, 0.4f, 0.5f), Wood);
            b.BoxOnGround(new Vector3(0.9f, 0.4f, 0.1f), new Vector3(0.6f, 0.4f, 0.5f), Wood);
            Lantern(b, new Vector3(-1.1f, 0.95f, -0.7f));
        }

        private static void Table(MeshBuilder b, Color accent, float width)
        {
            b.BoxOnGround(new Vector3(0f, 0.85f, -1.0f), new Vector3(width, 0.1f, 1.0f), Wood);
            b.BoxOnGround(new Vector3(0f, 0.3f, -1.48f), new Vector3(width, 0.5f, 0.04f), accent);
            float x = width * 0.5f - 0.1f;
            b.BoxOnGround(new Vector3(-x, 0f, -1.4f), new Vector3(0.08f, 0.85f, 0.08f), DarkWood);
            b.BoxOnGround(new Vector3(x, 0f, -1.4f), new Vector3(0.08f, 0.85f, 0.08f), DarkWood);
            b.BoxOnGround(new Vector3(-x, 0f, -0.6f), new Vector3(0.08f, 0.85f, 0.08f), DarkWood);
            b.BoxOnGround(new Vector3(x, 0f, -0.6f), new Vector3(0.08f, 0.85f, 0.08f), DarkWood);
        }

        private static void Lantern(MeshBuilder b, Vector3 baseCenter)
        {
            b.Cylinder(baseCenter, 0.08f, 0.2f, 6, Metal);
            b.Sphere(baseCenter + new Vector3(0f, 0.3f, 0f), 0.11f, Window, 3, 6, true);
        }

        // Bâche : étal + poteaux + toile wax inclinée + tabourets.
        private static void Tarp(MeshBuilder b, Color accent)
        {
            Table(b, accent, 2.6f);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -1.4f : 1.4f, z = i % 2 == 0 ? -1.9f : 0.4f;
                b.BoxOnGround(new Vector3(x, 0f, z), new Vector3(0.08f, i % 2 == 0 ? 2.1f : 2.5f, 0.08f), DarkWood);
            }
            b.SetTransform(new Vector3(0f, 2.3f, -0.75f), Quaternion.Euler(-10f, 0f, 0f), Vector3.one);
            b.Box(Vector3.zero, new Vector3(3.1f, 0.06f, 2.6f), accent);
            b.Box(new Vector3(0f, 0.035f, 0f), new Vector3(3.1f, 0.01f, 0.5f), Cloth);
            b.Box(new Vector3(0f, 0.035f, 0.9f), new Vector3(3.1f, 0.01f, 0.4f), Cloth);
            b.ResetTransform();
            b.Cylinder(new Vector3(-0.8f, 0.95f, -1.0f), 0.3f, 0.35f, 8, Metal);
            b.Cylinder(new Vector3(-1.6f, 0f, -2.3f), 0.2f, 0.45f, 6, accent);
            b.Cylinder(new Vector3(1.6f, 0f, -2.3f), 0.2f, 0.45f, 6, accent);
            Lantern(b, new Vector3(1.1f, 0.95f, -0.7f));
        }

        // Boutique : murs, toit, comptoir, enseigne.
        private static void Shop(MeshBuilder b, Color accent)
        {
            Shell(b, 3.0f, 2.5f, 2.6f, Wall);
            b.BoxOnGround(new Vector3(0f, 0f, -1.35f), new Vector3(2.4f, 1.0f, 0.4f), accent);
            b.BoxOnGround(new Vector3(0f, 1.0f, -1.35f), new Vector3(2.5f, 0.08f, 0.5f), Wood);
            b.BoxOnGround(new Vector3(0f, 2.55f, -1.32f), new Vector3(2.6f, 0.5f, 0.12f), accent);
            b.BoxOnGround(new Vector3(0f, 2.67f, -1.39f), new Vector3(1.8f, 0.25f, 0.02f), Cloth, true);
            b.Cylinder(new Vector3(0.7f, 1.08f, -1.3f), 0.25f, 0.3f, 8, Metal);
        }

        // Façade néon : deux niveaux, auvent rayé, enseigne lumineuse, fenêtres éclairées.
        private static void Neon(MeshBuilder b, Color accent)
        {
            Shell(b, 3.3f, 3.6f, 2.8f, Wall);
            b.BoxOnGround(new Vector3(0f, 0f, -1.45f), new Vector3(2.7f, 1.0f, 0.4f), accent);
            b.BoxOnGround(new Vector3(0f, 1.0f, -1.45f), new Vector3(2.8f, 0.08f, 0.5f), Wood);
            for (int i = 0; i < 6; i++)
            {
                float x = -1.375f + i * 0.55f;
                b.SetTransform(new Vector3(x, 2.25f, -1.75f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one);
                b.Box(Vector3.zero, new Vector3(0.55f, 0.05f, 0.9f), i % 2 == 0 ? accent : Cloth);
            }
            b.ResetTransform();
            b.BoxOnGround(new Vector3(0f, 2.8f, -1.42f), new Vector3(2.6f, 0.55f, 0.1f), Metal);
            b.BoxOnGround(new Vector3(0f, 2.9f, -1.48f), new Vector3(2.3f, 0.32f, 0.04f), accent, true);
            b.BoxOnGround(new Vector3(-0.8f, 3.45f, -1.41f), new Vector3(0.6f, 0.45f, 0.04f), Window, true);
            b.BoxOnGround(new Vector3(0.8f, 3.45f, -1.41f), new Vector3(0.6f, 0.45f, 0.04f), Window, true);
            b.BoxOnGround(new Vector3(1.75f, 0f, -1.2f), new Vector3(0.1f, 3.9f, 0.1f), accent, true);
            b.Cylinder(new Vector3(0.8f, 1.08f, -1.4f), 0.25f, 0.3f, 8, Metal);
            b.Cylinder(new Vector3(-0.8f, 1.08f, -1.4f), 0.3f, 0.06f, 8, Cloth);
        }

        /// <summary>Coque ouverte vers l'avant : murs arrière et latéraux, toit, bandeau haut.</summary>
        private static void Shell(MeshBuilder b, float width, float height, float depth, Color wall)
        {
            float hx = width * 0.5f;
            b.BoxOnGround(new Vector3(0f, 0f, depth * 0.5f - 1.3f), new Vector3(width, height, 0.15f), wall);
            b.BoxOnGround(new Vector3(-hx, 0f, -0.1f), new Vector3(0.15f, height, depth), wall);
            b.BoxOnGround(new Vector3(hx, 0f, -0.1f), new Vector3(0.15f, height, depth), wall);
            b.BoxOnGround(new Vector3(0f, height, -0.1f), new Vector3(width + 0.3f, 0.15f, depth + 0.3f), DarkWood);
            b.BoxOnGround(new Vector3(0f, height - 0.35f, -1.35f), new Vector3(width, 0.35f, 0.15f), wall);
        }

        /// <summary>Commerce fermé : coque vide (le rideau est un objet séparé, animé).</summary>
        public static Mesh LockedShell()
        {
            var b = new MeshBuilder();
            Shell(b, 3.0f, 2.5f, 2.6f, new Color(0.78f, 0.74f, 0.68f));
            return b.Build("LockedShell");
        }

        /// <summary>Rideau de fer : pivot en bas au centre, face vers -Z.</summary>
        public static Mesh Shutter(Color metal)
        {
            var b = new MeshBuilder();
            b.BoxOnGround(Vector3.zero, new Vector3(2.85f, 2.2f, 0.08f), metal);
            for (int i = 1; i < 11; i++)
                b.BoxOnGround(new Vector3(0f, i * 0.2f - 0.02f, -0.05f), new Vector3(2.85f, 0.03f, 0.02f), metal * 0.7f);
            b.BoxOnGround(new Vector3(0f, 0f, -0.06f), new Vector3(2.9f, 0.08f, 0.06f), metal * 0.5f);
            return b.Build("Shutter");
        }

        /// <summary>Lueur du rideau déblocable (émissive), placée devant le rideau.</summary>
        public static Mesh ShutterGlow(Color glow)
        {
            var b = new MeshBuilder();
            b.BoxOnGround(new Vector3(0f, 0f, -0.07f), new Vector3(2.95f, 0.06f, 0.02f), glow, true);
            b.BoxOnGround(new Vector3(-1.46f, 0f, -0.07f), new Vector3(0.05f, 2.25f, 0.02f), glow, true);
            b.BoxOnGround(new Vector3(1.46f, 0f, -0.07f), new Vector3(0.05f, 2.25f, 0.02f), glow, true);
            b.BoxOnGround(new Vector3(0f, 2.2f, -0.07f), new Vector3(2.95f, 0.06f, 0.02f), glow, true);
            return b.Build("ShutterGlow");
        }

        public static Mesh Lamp()
        {
            var b = new MeshBuilder();
            b.Cylinder(Vector3.zero, 0.06f, 2.9f, 6, Metal);
            b.BoxOnGround(new Vector3(0.25f, 2.85f, 0f), new Vector3(0.6f, 0.06f, 0.06f), Metal);
            b.Sphere(new Vector3(0.5f, 2.72f, 0f), 0.13f, Window, 3, 6, true);
            return b.Build("Lamp");
        }

        /// <summary>Barre de progression : pivot à gauche, longueur 1 (mise à l'échelle en X).</summary>
        public static Mesh BarFill(Color color)
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.16f, 0.06f), color, true);
            return b.Build("BarFill");
        }

        public static Mesh BarBack(Color color)
        {
            var b = new MeshBuilder();
            b.Box(Vector3.zero, new Vector3(1f, 0.24f, 0.05f), color);
            return b.Build("BarBack");
        }

        /// <summary>Pile de billets : hauteur 1 (mise à l'échelle en Y), pivot au sol.</summary>
        public static Mesh BillStack(Color color)
        {
            var b = new MeshBuilder();
            b.BoxOnGround(Vector3.zero, new Vector3(0.42f, 1f, 0.24f), color);
            b.BoxOnGround(new Vector3(0f, 0.45f, -0.121f), new Vector3(0.44f, 0.1f, 0.005f), Cloth);
            return b.Build("BillStack");
        }

        public enum PersonKind { Customer, Employee, Manager, Official, Tourist, Wrestler, Delegation }

        /// <summary>Personnage chibi (grosse tête) : jambes, corps conique, tête, accessoires selon le type.</summary>
        public static Mesh Person(PersonKind kind, Color outfit, Color skin, Color accent)
        {
            var b = new MeshBuilder();
            float girth = kind == PersonKind.Wrestler ? 1.35f : kind == PersonKind.Delegation ? 1.2f : 1f;
            // Ombre « blob » aplatie au sol : lisible, gratuite (même draw), remplace l'ombre temps réel des personnages.
            b.Cylinder(new Vector3(0f, 0.012f, 0f), 0.36f * girth, 0.004f, 10, new Color(0.1f, 0.08f, 0.06f, 0f));
            Color legs = kind == PersonKind.Wrestler ? skin : outfit * 0.55f;
            legs.a = 1f;
            b.BoxOnGround(new Vector3(-0.09f * girth, 0f, 0f), new Vector3(0.12f * girth, 0.3f, 0.14f), legs);
            b.BoxOnGround(new Vector3(0.09f * girth, 0f, 0f), new Vector3(0.12f * girth, 0.3f, 0.14f), legs);
            Color body = kind == PersonKind.Wrestler ? skin : outfit;
            float bodyHeight = kind == PersonKind.Delegation ? 0.6f : 0.42f;
            float bodyBase = kind == PersonKind.Delegation ? 0.05f : 0.28f;
            b.Cylinder(new Vector3(0f, bodyBase, 0f), 0.26f * girth, bodyHeight + 0.28f - bodyBase, 8, body, 0.18f * girth);
            b.Sphere(new Vector3(0f, 0.95f, 0f), 0.27f, skin, 4, 8);
            b.Sphere(new Vector3(-0.09f, 0.98f, -0.24f), 0.035f, Color.black, 2, 4);
            b.Sphere(new Vector3(0.09f, 0.98f, -0.24f), 0.035f, Color.black, 2, 4);

            switch (kind)
            {
                case PersonKind.Employee:
                    b.BoxOnGround(new Vector3(0f, 0.3f, -0.2f), new Vector3(0.32f, 0.35f, 0.04f), Cloth);
                    b.Cylinder(new Vector3(0f, 1.12f, 0f), 0.2f, 0.12f, 8, accent);
                    break;
                case PersonKind.Manager:
                    b.Cylinder(new Vector3(0f, 1.15f, 0f), 0.3f, 0.04f, 8, accent);
                    b.Cylinder(new Vector3(0f, 1.18f, 0f), 0.18f, 0.22f, 8, accent);
                    b.BoxOnGround(new Vector3(0f, 0.45f, -0.22f), new Vector3(0.1f, 0.18f, 0.03f), accent);
                    break;
                case PersonKind.Official:
                    b.BoxOnGround(new Vector3(0.32f, 0.2f, 0f), new Vector3(0.08f, 0.26f, 0.34f), DarkWood);
                    b.BoxOnGround(new Vector3(0f, 0.45f, -0.22f), new Vector3(0.06f, 0.22f, 0.03f), accent);
                    break;
                case PersonKind.Tourist:
                    b.Cylinder(new Vector3(0f, 1.12f, 0f), 0.36f, 0.03f, 10, accent);
                    b.Cylinder(new Vector3(0f, 1.14f, 0f), 0.2f, 0.12f, 8, accent);
                    b.BoxOnGround(new Vector3(0f, 0.5f, -0.25f), new Vector3(0.18f, 0.12f, 0.08f), Metal);
                    break;
                case PersonKind.Wrestler:
                    b.BoxOnGround(new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.22f, 0.3f), accent);
                    b.Sphere(new Vector3(-0.38f, 0.55f, 0f), 0.12f, accent, 3, 6, true);
                    b.Sphere(new Vector3(0.38f, 0.55f, 0f), 0.12f, accent, 3, 6, true);
                    break;
                case PersonKind.Delegation:
                    b.Cylinder(new Vector3(0f, 1.1f, 0f), 0.24f, 0.25f, 8, accent, 0.2f);
                    b.BoxOnGround(new Vector3(0f, 0.35f, -0.3f), new Vector3(0.5f, 0.06f, 0.04f), accent, true);
                    break;
            }
            return b.Build(kind.ToString());
        }

        /// <summary>Accessoire porté au départ : sac (0) ou assiette (1). Origine = main, à hauteur de taille.</summary>
        public static Mesh Carry(int kind)
        {
            var b = new MeshBuilder();
            if (kind == 0)
            {
                b.BoxOnGround(new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.24f, 0.12f), new Color(0.85f, 0.75f, 0.55f));
                b.BoxOnGround(new Vector3(0f, 0.04f, 0f), new Vector3(0.12f, 0.03f, 0.02f), new Color(0.6f, 0.5f, 0.35f));
            }
            else
            {
                b.Cylinder(Vector3.zero, 0.2f, 0.03f, 8, Cloth);
                b.Sphere(new Vector3(0f, 0.05f, 0f), 0.12f, Food, 3, 6, false, 0.6f);
            }
            return b.Build(kind == 0 ? "CarryBag" : "CarryPlate");
        }

        private static void Building(MeshBuilder b, Vector3 position, Vector3 size, StreetConfig config, System.Random rng)
        {
            Color c = config.buildingColors[rng.Next(config.buildingColors.Length)];
            float h = size.y;
            b.BoxOnGround(position, size, c);
            b.BoxOnGround(position + new Vector3(0f, h, 0f), new Vector3(size.x + 0.2f, 0.2f, size.z + 0.2f), c * 0.8f);
            for (float wy = 1.0f; wy < h - 0.6f; wy += 1.3f)
                for (float wx = -size.x * 0.5f + 0.7f; wx < size.x * 0.5f - 0.4f; wx += 1.1f)
                    b.BoxOnGround(new Vector3(position.x + wx, wy, position.z - size.z * 0.5f - 0.01f), new Vector3(0.5f, 0.6f, 0.02f),
                        Window * 0.85f, rng.NextDouble() < 0.6);
        }

        /// <summary>
        /// Décor statique de la rue : sol, chaussée, trottoirs, marquages, blocs de bâtiments aux fenêtres éclairées,
        /// monument placeholder au bout. Un seul mesh = un draw.
        /// </summary>
        public static Mesh Street(StreetConfig config)
        {
            var b = new MeshBuilder();
            var rng = new System.Random(config.seed);
            float length = config.streetEndZ - config.streetStartZ;
            float midZ = (config.streetEndZ + config.streetStartZ) * 0.5f;
            b.Plane(new Vector3(0f, -0.02f, midZ), new Vector2(60f, length + 30f), config.ground);
            b.Plane(new Vector3(0f, 0f, midZ), new Vector2(config.roadHalfWidth * 2f, length + 30f), config.road);
            float sw = config.roadHalfWidth + config.sidewalkWidth * 0.5f;
            b.BoxOnGround(new Vector3(-sw, -0.02f, midZ), new Vector3(config.sidewalkWidth, 0.1f, length + 30f), config.sidewalk);
            b.BoxOnGround(new Vector3(sw, -0.02f, midZ), new Vector3(config.sidewalkWidth, 0.1f, length + 30f), config.sidewalk);
            for (float z = config.streetStartZ - 14f; z < config.streetEndZ + 14f; z += 3f)
                b.Plane(new Vector3(0f, 0.01f, z), new Vector2(0.15f, 1.4f), config.roadLine);

            // Façades voisines : comblent la rangée entre les commerces (la rue est continue, jamais un terrain vague).
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = config.streetStartZ - 10f; z < config.streetEndZ + 12f; z += config.neighbourSpacingZ)
                {
                    bool occupied = false;
                    foreach (var slot in config.slots)
                        if (slot.side == side && Mathf.Abs(slot.position.z - z) < config.neighbourSpacingZ * 0.75f) occupied = true;
                    if (occupied) continue;
                    float h = Mathf.Lerp(config.blockHeightRange.x * 0.6f, config.blockHeightRange.x * 1.2f, (float)rng.NextDouble());
                    Building(b, new Vector3(side * config.businessX, 0f, z + 0.4f), new Vector3(3.2f, h, 2.6f), config, rng);
                }
                // Second rang, plus haut, visible quand on dézoome.
                for (float z = config.streetStartZ - 12f; z < config.streetEndZ + 14f; z += config.neighbourSpacingZ * 1.2f)
                {
                    float h = Mathf.Lerp(config.blockHeightRange.x, config.blockHeightRange.y, (float)rng.NextDouble());
                    Building(b, new Vector3(side * (config.outerRowX + (float)rng.NextDouble()), 0f, z), new Vector3(4.2f, h, 4f), config, rng);
                }
            }

            // Monument placeholder (silhouette abstraite) au bout de la rue.
            float mz = config.streetEndZ + 16f;
            b.BoxOnGround(new Vector3(0f, 0f, mz), new Vector3(8f, 1.2f, 6f), new Color(0.7f, 0.6f, 0.45f));
            b.Cylinder(new Vector3(0f, 1.2f, mz), 1.6f, 12f, 6, new Color(0.62f, 0.48f, 0.32f), 0.6f);
            b.Sphere(new Vector3(0f, 13.6f, mz), 0.9f, new Color(0.62f, 0.48f, 0.32f), 4, 6);
            return b.Build("Street");
        }
    }
}
