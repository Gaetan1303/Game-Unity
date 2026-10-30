#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameSetupMenu
{
    const string ArteDir = "Assets/_Project/ScriptableObjects/Artes";
    const string WorldDir = "Assets/_Project/ScriptableObjects/Worlds";
    const string SceneDir = "Assets/_Project/Scenes";
    const string Env = SpriteImporter.Root + "/Env/";

    [MenuItem("Jeu/0 - Importer les sprites")]
    public static void ImportSprites()
    {
        AssetDatabase.ImportAsset(SpriteImporter.Root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        Debug.Log("Sprites réimportés.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static ArteData MakeArte(string file, System.Action<ArteData> fill)
    {
        EnsureFolder(ArteDir);
        string path = $"{ArteDir}/{file}.asset";
        var a = AssetDatabase.LoadAssetAtPath<ArteData>(path);
        if (a == null) { a = ScriptableObject.CreateInstance<ArteData>(); AssetDatabase.CreateAsset(a, path); }
        fill(a);                       // on réécrit toujours : valeurs à jour
        EditorUtility.SetDirty(a);
        return a;
    }

    static ArteData Load(string f) => AssetDatabase.LoadAssetAtPath<ArteData>($"{ArteDir}/{f}.asset");

    [MenuItem("Jeu/1 - Créer les artes de base")]
    public static void CreateArtes()
    {
        // ---- Artes STANDARD (touche J + direction) ----
        MakeArte("CrocDemoniaque", a =>
        {
            a.displayName = "Croc démoniaque"; a.description = "Onde de choc rasante qui traverse les ennemis.";
            a.kind = ArteKind.GroundWave; a.fxSprite = "slash_03";
            a.tpCost = 8; a.cooldown = 0.8f; a.windup = 0.15f; a.recovery = 0.35f;
            a.damage = 22; a.knockback = 12; a.poise = 30; a.range = 7; a.radius = 0.8f; a.projectileSpeed = 14;
            a.tint = new Color(0.75f, 0.2f, 1f);
        });
        MakeArte("LameAscendante", a =>
        {
            a.displayName = "Lame ascendante"; a.description = "Projette l'ennemi en l'air : enchaînez en l'air !";
            a.kind = ArteKind.Launcher; a.fxSprite = "slash_02";
            a.tpCost = 6; a.cooldown = 0.6f; a.windup = 0.12f; a.recovery = 0.3f;
            a.damage = 18; a.knockback = 3; a.poise = 40; a.launch = 9; a.radius = 1.8f;
            a.tint = new Color(1f, 0.65f, 0.3f);
        });
        MakeArte("TempeteDeLames", a =>
        {
            a.displayName = "Tempête de lames"; a.description = "Rafale de coups autour du héros, finit par une mise à terre.";
            a.kind = ArteKind.Melee; a.fxSprite = "twirl_01";
            a.tpCost = 12; a.cooldown = 2.5f; a.windup = 0.1f; a.recovery = 0.4f;
            a.damage = 8; a.hits = 5; a.hitInterval = 0.12f; a.radius = 2.2f; a.knockback = 4; a.poise = 30; a.knockdown = true;
            a.tint = new Color(0.8f, 0.85f, 1f);
        });

        // ---- Artes TECHNIQUES (touche K + direction) ----
        MakeArte("Photon", a =>
        {
            a.displayName = "Photon"; a.description = "Cercle de lumière qui explose sur la cible.";
            a.kind = ArteKind.AreaAtTarget; a.fxSprite = "magic_01";
            a.tpCost = 14; a.cooldown = 2.5f; a.windup = 0.4f; a.recovery = 0.4f;
            a.damage = 30; a.knockback = 10; a.poise = 40; a.radius = 3f; a.blastDelay = 0.6f;
            a.tint = new Color(1f, 0.95f, 0.5f);
        });
        MakeArte("SphereDemoniaque", a =>
        {
            a.displayName = "Sphère démoniaque"; a.description = "Orbe sombre à tête chercheuse.";
            a.kind = ArteKind.Projectile; a.fxSprite = "star_09";
            a.tpCost = 10; a.cooldown = 1.2f; a.windup = 0.25f; a.recovery = 0.3f;
            a.damage = 20; a.knockback = 6; a.poise = 35; a.range = 14; a.radius = 1.2f; a.projectileSpeed = 11;
            a.tint = new Color(0.8f, 0.2f, 0.5f);
        });
        MakeArte("Foudre", a =>
        {
            a.displayName = "Foudre"; a.description = "Un éclair s'abat sur la cible et la projette.";
            a.kind = ArteKind.Strike; a.fxSprite = "spark_05";
            a.tpCost = 16; a.cooldown = 3f; a.windup = 0.3f; a.recovery = 0.35f;
            a.damage = 34; a.knockback = 4; a.poise = 45; a.launch = 6; a.radius = 1.8f; a.blastDelay = 0.45f;
            a.tint = new Color(0.6f, 0.8f, 1f);
        });
        MakeArte("SoinTenebreux", a =>
        {
            a.displayName = "Soin ténébreux"; a.description = "Soigne tout le groupe.";
            a.kind = ArteKind.Heal; a.fxSprite = "circle_02";
            a.tpCost = 14; a.cooldown = 6f; a.windup = 0.35f; a.recovery = 0.5f; a.healPercent = 0.35f;
            a.tint = new Color(0.5f, 1f, 0.6f);
        });
        AssetDatabase.SaveAssets();
    }

    // ---------- helpers de scène ----------
    static GameObject Actor(string name, Vector3 pos, float height, float radius)
    {
        var go = new GameObject(name);
        go.transform.position = pos + Vector3.up * (height * 0.5f);
        var cc = go.AddComponent<CharacterController>();
        cc.height = height; cc.radius = radius; cc.center = Vector3.zero;
        return go;
    }

    static SpriteRenderer Tiled(string name, string sprite, Vector3 pos, Vector3 euler, Vector2 size, Vector3 scale, int order)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(Env + sprite + ".png");
        var go = new GameObject(name);
        go.transform.position = pos; go.transform.eulerAngles = euler; go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size; sr.sortingOrder = order;
        return sr;
    }

    static void Box(string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().size = size;
    }

    /// <summary>Hauteur monde d'un sprite d'ennemi (pixels / PPU) -> sert à dimensionner la collision.</summary>
    static float SpriteHeight(string folder, string name)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteImporter.Root}/{folder}/{name}.png");
        return sp != null ? sp.rect.height / sp.pixelsPerUnit : 1.25f;
    }

    static EnemyAI Foe(string name, string sprite, float x, float z, float hp, float speed, float dmg,
                       float poise, bool boss, WorldData world, string walk, string idle = "idle", float hover = 0f)
    {
        float h = SpriteHeight("Enemies", sprite);
        var e = Actor(name, new Vector3(x, 0.05f, z), h, Mathf.Min(0.55f, h * 0.32f));
        e.AddComponent<Health>().team = Team.Enemy;
        e.AddComponent<Knockable>();
        var ai = e.AddComponent<EnemyAI>();
        ai.maxHp = hp; ai.moveSpeed = speed; ai.attackDamage = dmg; ai.maxPoise = poise;
        ai.attackRange = Mathf.Max(1.5f, h * 1.2f); ai.aggroRange = 20f;
        if (boss) { ai.superArmor = true; ai.bossSlam = true; ai.conquersWorldOnDeath = world; ai.windup = 0.6f; }
        var vis = e.AddComponent<EnemySprite>();
        vis.spriteName = sprite; vis.scale = 1f;          // échelle ENTIÈRE (norme) : un boss = un sprite plus grand
        vis.walkClip = walk; vis.idleClip = idle; vis.hover = hover;
        return ai;
    }

    static void Zone(string name, float x, string banner, int exp, int gald, params EnemyAI[] foes)
    {
        var z = new GameObject("Rencontre " + name);
        z.transform.position = new Vector3(x, 0, 0);
        var ez = z.AddComponent<EncounterZone>();
        ez.enemies = foes; ez.expReward = exp; ez.galdReward = gald; ez.banner = banner; ez.triggerRadius = 9f;
    }

    static void Ally(string name, string sprite, AllyRole role, Vector3 pos, ArteData arte, float hp)
    {
        var a = Actor(name, pos, 1.25f, 0.4f);
        a.AddComponent<Health>().team = Team.Player;
        a.AddComponent<Knockable>();
        var ai = a.AddComponent<AllyAI>();
        ai.role = role; ai.arte = arte; ai.maxHp = hp;
        a.AddComponent<AllySprite>().spriteName = sprite;
    }

    [MenuItem("Jeu/2 - Créer l'arène de test")]
    public static void BuildArena()
    {
        ImportSprites();
        CreateArtes();
        EnsureFolder(WorldDir); EnsureFolder(SceneDir);

        var world = AssetDatabase.LoadAssetAtPath<WorldData>(WorldDir + "/Catacombes.asset");
        if (world == null)
        {
            world = ScriptableObject.CreateInstance<WorldData>();
            world.worldName = "Les Catacombes"; world.description = "Premier monde conquis par le vilain.";
            AssetDatabase.CreateAsset(world, WorldDir + "/Catacombes.asset");
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // ----- décor -----
        Tiled("Sol", "floor", new Vector3(0, 0, 0), new Vector3(90, 0, 0), new Vector2(64, 28), Vector3.one, -100);
        Tiled("Mur", "wall", new Vector3(0, 8, 14.5f), Vector3.zero, new Vector2(64, 16), Vector3.one, -200);   // tuiles à l'échelle 1
        var banner = AssetDatabase.LoadAssetAtPath<Sprite>(Env + "banner.png");
        for (int x = -24; x <= 24; x += 8)
        {
            var b = new GameObject("Banniere"); b.transform.position = new Vector3(x, 5, 14.3f); 
            var sr = b.AddComponent<SpriteRenderer>(); sr.sprite = banner; sr.sortingOrder = -190;
        }
        Box("SolCollider", new Vector3(0, -0.5f, 0), new Vector3(80, 1, 30));
        Box("Mur Z+", new Vector3(0, 3, 7), new Vector3(70, 8, 1));
        Box("Mur Z-", new Vector3(0, 3, -7), new Vector3(70, 8, 1));
        Box("Mur X+", new Vector3(30, 3, 0), new Vector3(1, 8, 30));
        Box("Mur X-", new Vector3(-30, 3, 0), new Vector3(1, 8, 30));

        // ----- héros -----
        var player = Actor("Vilain", new Vector3(-8, 0.05f, 0), 2f, 0.45f);
        player.AddComponent<Health>().team = Team.Player;
        player.AddComponent<Knockable>();
        player.AddComponent<PlayerStats>();
        player.AddComponent<TargetSelector>();
        player.AddComponent<PlayerController>();
        var caster = player.AddComponent<ArteCaster>();
        caster.standard = new ArteData[] { null, Load("CrocDemoniaque"), Load("LameAscendante"), Load("TempeteDeLames") };
        caster.technical = new ArteData[] { Load("Photon"), Load("SphereDemoniaque"), Load("Foudre"), Load("SoinTenebreux") };
        player.AddComponent<HeroVisual>();
        player.AddComponent<Hud>();

        // ----- alliés IA -----
        Ally("Chevalier", "knight", AllyRole.Fighter, new Vector3(-10, 0.05f, -2), null, 150);
        Ally("Mage", "mage", AllyRole.Mage, new Vector3(-11, 0.05f, 2), Load("Photon"), 90);
        Ally("Soigneuse", "healer", AllyRole.Healer, new Vector3(-12, 0.05f, 0), Load("SoinTenebreux"), 100);

        // ----- rencontres : nom, sprite, x, z, pv, vitesse, dégâts, équilibre, boss, monde, démarche, repos, vol -----
        Zone("1", 4, "ENCOUNTER !", 35, 60,
            Foe("Slime", "slime", 2, 2, 40, 2.0f, 6, 18, false, world, "walk_hop", "idle_squash"),
            Foe("Slime", "slime", 5, -2, 40, 2.0f, 6, 18, false, world, "walk_hop", "idle_squash"),
            Foe("Chauve-souris", "bat", 6, 1, 30, 4.0f, 5, 12, false, world, "walk_flap", "idle_hover", 1.0f));
        Zone("2", 16, "ENCOUNTER !", 70, 120,
            Foe("Araignée", "spider", 14, 2, 55, 3.0f, 8, 25, false, world, "walk_scuttle"),
            Foe("Cultiste", "cultist", 17, -2, 70, 2.5f, 9, 30, false, world, "walk_waddle"),
            Foe("Araignée", "spider", 19, 2, 55, 3.0f, 8, 25, false, world, "walk_scuttle"));
        Zone("Boss", 27, "BOSS BATTLE !", 180, 400,
            Foe("Cyclope (boss)", "brute_boss", 28, 0, 320, 2.2f, 16, 90, true, world, "walk_stomp"),
            Foe("Chauve-souris", "bat", 25, 3, 30, 4.0f, 5, 12, false, world, "walk_flap", "idle_hover", 1.0f),
            Foe("Chauve-souris", "bat", 25, -3, 30, 4.0f, 5, 12, false, world, "walk_flap", "idle_hover", 1.0f));

        // ----- caméra -----
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.03f, 0.07f);
            cam.fieldOfView = 38f;
            var cf = cam.gameObject.AddComponent<CameraFollow>();
            cf.target = player.transform; cf.offset = new Vector3(0, 4f, -9f);
            cf.pixelPerfect = true; cf.pixelsPerUnit = 24f; cf.baseHeight = 180f;   // 320x180 -> x6 en 1080p
        }

        string path = SceneDir + "/TestArena.unity";
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.SaveAssets();
        Debug.Log("Arène créée : " + path + " — clique dans Game puis Play !");
    }
}
#endif
