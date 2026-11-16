using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gloomdelve.EditorTools
{
    /// <summary>
    /// One-click project setup: Gloomdelve > Build Everything (or batch: -executeMethod Gloomdelve.EditorTools.ProjectBuilder.BatchBuildEverything).
    /// Generates art + audio, URP + input settings, tiles, configs (ScriptableObjects, values kept if they exist),
    /// prefabs, the MainMenu and Game scenes and the build scene list.
    /// </summary>
    public static class ProjectBuilder
    {
        const string G = "Assets/Game/";
        static Material spriteMat;
        static readonly Dictionary<string, SpriteAnim> anims = new Dictionary<string, SpriteAnim>();

        [MenuItem("Gloomdelve/Build Everything", priority = 0)]
        public static void BuildEverything()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Gloomdelve", "Settings", 0.05f);
                EnsureFolders();
                SetupProjectSettings();
                SetupURP();
                EditorUtility.DisplayProgressBar("Gloomdelve", "Art", 0.15f);
                ArtGenerator.GenerateAll();
                EditorUtility.DisplayProgressBar("Gloomdelve", "Audio", 0.35f);
                AudioGenerator.GenerateAll();
                EditorUtility.DisplayProgressBar("Gloomdelve", "Assets", 0.5f);
                spriteMat = SpriteMaterial();
                var input = InputActions();
                CreateAnims();
                var tileSets = CreateTileSets();
                var font = CreatePixelFont();
                var sfx = CreateSfxLibrary();
                var cfg = CreateConfigs(tileSets, sfx);
                EditorUtility.DisplayProgressBar("Gloomdelve", "Prefabs", 0.65f);
                CreatePrefabs(cfg, font);
                CreateServicesPrefab(cfg, sfx);
                EditorUtility.DisplayProgressBar("Gloomdelve", "Scenes", 0.85f);
                UIBuilder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                UIBuilder.panelSprite = ArtGenerator.S("UI/panel");
                UIBuilder.buttonSprite = ArtGenerator.S("UI/button");
                BuildMenuScene(cfg);
                BuildGameScene(cfg, input, font);
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(G + "Scenes/MainMenu.unity", true),
                    new EditorBuildSettingsScene(G + "Scenes/Game.unity", true),
                };
                AssetDatabase.SaveAssets();
                ArtGenerator.WritePreview(Path.Combine(Directory.GetCurrentDirectory(), "Logs/art_preview.png"));
                Debug.Log("[Builder] Build Everything finished.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static void BatchBuildEverything()
        {
            BuildEverything();
            EditorApplication.Exit(0);
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { "Settings", "Data", "Data/Levels", "Animations", "Tiles", "Prefabs", "Prefabs/Enemies", "Prefabs/Items",
                         "Prefabs/Traps", "Prefabs/Level", "Scenes", "Resources", "Audio", "Art" })
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), G + f));
            AssetDatabase.Refresh();
        }

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            created = a == null;
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject => LoadOrCreate<T>(path, out _);

        // ------------------------------------------------------------------ settings

        static void SetupProjectSettings()
        {
            PlayerSettings.companyName = "Gloomdelve Team";
            PlayerSettings.productName = "Gloomdelve";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.gloomdelve.game");
            QualitySettings.vSyncCount = 1;
            Time.fixedDeltaTime = GameConst.Dt;
            var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset");
            if (tm.Length > 0)
            {
                var so = new SerializedObject(tm[0]);
                var p = so.FindProperty("Fixed Timestep");
                if (p != null)
                {
                    if (p.propertyType == SerializedPropertyType.Float) p.floatValue = GameConst.Dt;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void SetupURP()
        {
            string rdPath = G + "Settings/URP_Renderer.asset", urpPath = G + "Settings/URP_Asset.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rdPath);
            }
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(urp, urpPath);
            }
            urp.supportsHDR = false;
            urp.msaaSampleCount = 1;
            urp.renderScale = 1f;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(cur, false);
            AssetDatabase.SaveAssets();
        }

        static Material SpriteMaterial()
        {
            string path = G + "Settings/SpriteUnlit.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh) { name = "SpriteUnlit" };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ input

        static InputActionAsset InputActions()
        {
            string path = G + "Settings/GameInput.inputactions";
            string B(string action, string p) => "{\"name\":\"\",\"id\":\"" + Guid.NewGuid() + "\",\"path\":\"" + p + "\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"\",\"action\":\"" + action + "\",\"isComposite\":false,\"isPartOfComposite\":false}";
            string Part(string name, string p) => "{\"name\":\"" + name + "\",\"id\":\"" + Guid.NewGuid() + "\",\"path\":\"" + p + "\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"\",\"action\":\"Move\",\"isComposite\":false,\"isPartOfComposite\":true}";
            string A(string name, string type, string ctrl) => "{\"name\":\"" + name + "\",\"type\":\"" + type + "\",\"id\":\"" + Guid.NewGuid() + "\",\"expectedControlType\":\"" + ctrl + "\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":" + (type == "Value" ? "true" : "false") + "}";
            var actions = new List<string>
            {
                A("Move", "Value", "Vector2"), A("Jump", "Button", "Button"), A("Attack", "Button", "Button"), A("Use", "Button", "Button"),
                A("Swap", "Button", "Button"), A("Interact", "Button", "Button"), A("Walk", "Button", "Button"), A("Pause", "Button", "Button"),
            };
            var bindings = new List<string>
            {
                "{\"name\":\"Arrows\",\"id\":\"" + Guid.NewGuid() + "\",\"path\":\"2DVector\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"\",\"action\":\"Move\",\"isComposite\":true,\"isPartOfComposite\":false}",
                Part("up", "<Keyboard>/upArrow"), Part("down", "<Keyboard>/downArrow"), Part("left", "<Keyboard>/leftArrow"), Part("right", "<Keyboard>/rightArrow"),
                B("Move", "<Gamepad>/leftStick"), B("Move", "<Gamepad>/dpad"),
                B("Jump", "<Keyboard>/z"), B("Jump", "<Keyboard>/space"), B("Jump", "<Gamepad>/buttonSouth"),
                B("Attack", "<Keyboard>/x"), B("Attack", "<Gamepad>/buttonWest"),
                B("Use", "<Keyboard>/c"), B("Use", "<Gamepad>/buttonEast"),
                B("Swap", "<Keyboard>/d"), B("Swap", "<Gamepad>/rightShoulder"),
                B("Interact", "<Keyboard>/a"), B("Interact", "<Gamepad>/buttonNorth"),
                B("Walk", "<Keyboard>/leftShift"), B("Walk", "<Keyboard>/rightShift"), B("Walk", "<Gamepad>/leftTrigger"),
                B("Pause", "<Keyboard>/escape"), B("Pause", "<Keyboard>/p"), B("Pause", "<Gamepad>/start"),
            };
            string json = "{\n\"name\":\"GameInput\",\n\"maps\":[{\"name\":\"Player\",\"id\":\"" + Guid.NewGuid() + "\",\"actions\":[" +
                          string.Join(",", actions) + "],\"bindings\":[" + string.Join(",", bindings) + "]}],\n\"controlSchemes\":[]\n}";
            File.WriteAllText(path, json);   // always regenerated so new actions are never missing
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        // ------------------------------------------------------------------ animations

        static SpriteAnim Anim(string name, Sprite[] frames, float fps, bool loop = true)
        {
            var a = LoadOrCreate<SpriteAnim>(G + "Animations/" + name + ".asset");
            a.frames = frames; a.fps = fps; a.loop = loop;
            EditorUtility.SetDirty(a);
            anims[name] = a;
            return a;
        }

        static Sprite[] F(params string[] rel)
        {
            var a = new Sprite[rel.Length];
            for (int i = 0; i < rel.Length; i++) a[i] = ArtGenerator.S(rel[i]);
            return a;
        }

        static void CreateAnims()
        {
            anims.Clear();
            const string p = "Sprites/Player/";
            Anim("player_idle", F(p + "idle_0", p + "idle_1"), 1.6f);
            Anim("player_run", ArtGenerator.Frames(p + "run", 4), 12f);
            Anim("player_jump", F(p + "jump_0"), 1);
            Anim("player_fall", F(p + "fall_0"), 1);
            Anim("player_crouch", F(p + "crouch_0"), 1);
            Anim("player_crawl", ArtGenerator.Frames(p + "crawl", 2), 6f);
            Anim("player_climb", ArtGenerator.Frames(p + "climb", 2), 8f);
            Anim("player_hang", F(p + "hang_0"), 1);
            Anim("player_whip", ArtGenerator.Frames(p + "whip", 2), 10f, false);
            Anim("player_throw", F(p + "throw_0"), 1);
            Anim("player_stunned", ArtGenerator.Frames(p + "stunned", 2), 6f);
            Anim("player_dead", F(p + "dead_0"), 1);
            Anim("player_lookup", F(p + "lookup_0"), 1);
            const string e = "Sprites/Enemies/";
            Anim("snake_walk", ArtGenerator.Frames(e + "snake", 2), 4f);
            Anim("bat_hang", F(e + "bat_2"), 1); Anim("bat_fly", F(e + "bat_0", e + "bat_1"), 10f);
            Anim("emberbat_hang", F(e + "emberbat_2"), 1); Anim("emberbat_fly", F(e + "emberbat_0", e + "emberbat_1"), 12f);
            Anim("spider_hang", F(e + "spider_2"), 1); Anim("spider_idle", F(e + "spider_0"), 1); Anim("spider_jump", F(e + "spider_1"), 1);
            Anim("toad_idle", F(e + "toad_0"), 1); Anim("toad_jump", F(e + "toad_1"), 1);
            Anim("brute_walk", F(e + "brute_0", e + "brute_1"), 4f); Anim("brute_charge", F(e + "brute_2"), 1); Anim("brute_dazed", F(e + "brute_3"), 1);
            Anim("wraith_float", ArtGenerator.Frames(e + "wraith", 2), 3f);
            Anim("hawk_fly", F(e + "hawk_0", e + "hawk_1"), 7f);
            Anim("hawk_dive", F(e + "hawk_2"), 1);
            Anim("keeper_idle", ArtGenerator.Frames("Sprites/Shopkeeper/idle", 2), 1.5f);
            Anim("keeper_angry", ArtGenerator.Frames("Sprites/Shopkeeper/angry", 2), 6f);
            Anim("bomb_idle", F("Sprites/Items/bomb_0"), 1);
            Anim("bomb_lit", F("Sprites/Items/bomb_1", "Sprites/Items/bomb_2"), 8f);
            Anim("flame", ArtGenerator.Frames("Sprites/Traps/flame", 3), 14f);
            Anim("explosion", ArtGenerator.Frames("Center/explosion", 6), 18f, false);
        }

        // ------------------------------------------------------------------ tiles

        static TileBase MakeTile(string area, string rel)
        {
            string path = $"{G}Tiles/{area}/{Path.GetFileName(rel)}.asset";
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), $"{G}Tiles/{area}"));
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, path); }
            t.sprite = ArtGenerator.S(rel);
            t.colliderType = Tile.ColliderType.None;
            t.color = Color.white;
            EditorUtility.SetDirty(t);
            return t;
        }

        static Dictionary<string, TileSet> CreateTileSets()
        {
            var result = new Dictionary<string, TileSet>();
            foreach (var area in new[] { "Mossroot", "Emberdeep", "Vault", "Sunfall" })
            {
                string d = "Tiles/" + area + "/";
                var ts = LoadOrCreate<TileSet>($"{G}Data/TileSet_{area}.asset");
                ts.dirt = new TileBase[32];
                for (int v = 0; v < 2; v++) for (int m = 0; m < 16; m++) ts.dirt[m + 16 * v] = MakeTile(area, $"{d}dirt_{m:00}_{v}");
                ts.bedrock = new TileBase[16];
                for (int m = 0; m < 16; m++) ts.bedrock[m] = MakeTile(area, $"{d}bedrock_{m:00}");
                ts.brick = new TileBase[16];
                for (int m = 0; m < 16; m++) ts.brick[m] = MakeTile(area, $"{d}brick_{m:00}");
                ts.background = new TileBase[4];
                for (int v = 0; v < 4; v++) ts.background[v] = MakeTile(area, $"{d}bg_{v}");
                ts.shopBackground = MakeTile(area, d + "bg_shop");
                ts.platform = MakeTile(area, d + "platform");
                ts.ladder = MakeTile(area, d + "ladder");
                ts.spikes = MakeTile(area, d + "spikes");
                ts.trapBlock = MakeTile(area, d + "trapblock");
                ts.ventBlock = MakeTile(area, d + "ventblock");
                ts.rubble = MakeTile(area, d + "rubble");
                ts.goldOverlay = MakeTile(area, d + "gold_overlay");
                ts.gemOverlay = MakeTile(area, d + "gem_overlay");
                ts.decorFloor = new TileBase[4];
                for (int i = 0; i < 4; i++) ts.decorFloor[i] = MakeTile(area, $"{d}decor_floor_{i}");
                ts.decorCeiling = new TileBase[3];
                for (int i = 0; i < 3; i++) ts.decorCeiling[i] = MakeTile(area, $"{d}decor_ceiling_{i}");
                ts.outdoor = area == "Sunfall";
                EditorUtility.SetDirty(ts);
                result[area] = ts;
            }
            return result;
        }

        static PixelFont CreatePixelFont()
        {
            var f = LoadOrCreate<PixelFont>(G + "Data/PixelFont.asset");
            f.characters = ArtGenerator.FontChars;
            f.glyphs = new Sprite[f.characters.Length];
            for (int i = 0; i < f.characters.Length; i++) f.glyphs[i] = ArtGenerator.S($"Font/glyph_{i:00}");
            f.advance = 4f / 16f;
            EditorUtility.SetDirty(f);
            return f;
        }

        static SfxLibrary CreateSfxLibrary()
        {
            var lib = LoadOrCreate<SfxLibrary>(G + "Data/SfxLibrary.asset");
            var vol = new Dictionary<string, float>
            {
                ["fuse"] = 0.35f, ["coin"] = 0.6f, ["land"] = 0.5f, ["jump"] = 0.45f, ["grab"] = 0.5f, ["climb"] = 0.4f,
                ["explosion"] = 1.2f, ["menu_select"] = 0.5f, ["rumble"] = 0.6f, ["warning"] = 0.9f, ["wraith"] = 1f,
            };
            var old = new Dictionary<string, SfxLibrary.Entry>();
            foreach (var e in lib.sounds) if (e != null && e.id != null) old[e.id] = e;
            lib.sounds.Clear();
            foreach (var id in AudioGenerator.SfxIds)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioGenerator.Root}Sfx/{id}.wav");
                var e = old.TryGetValue(id, out var o) ? o : new SfxLibrary.Entry { id = id, volume = vol.TryGetValue(id, out var v) ? v : 0.8f };
                e.clips = new[] { clip };
                lib.sounds.Add(e);
            }
            lib.music.Clear();
            foreach (var id in AudioGenerator.MusicIds)
                lib.music.Add(new SfxLibrary.Music { id = id, clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioGenerator.Root}Music/{id}.wav"), volume = id == "menu" ? 0.6f : 0.55f });
            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ------------------------------------------------------------------ configs

        static GameConfig CreateConfigs(Dictionary<string, TileSet> ts, SfxLibrary sfx)
        {
            var player = LoadOrCreate<PlayerConfig>(G + "Data/PlayerConfig.asset");

            var shop = LoadOrCreate<ShopConfig>(G + "Data/ShopConfig.asset", out bool newShop);
            if (newShop || shop.offers.Count == 0)
            {
                shop.offers = new List<ShopOffer>
                {
                    new ShopOffer { id = "bombs", displayName = "Bomb Bag", price = 350, resource = Res.Bombs, amount = 3, description = "+3 bombs" },
                    new ShopOffer { id = "ropes", displayName = "Rope Pile", price = 250, resource = Res.Ropes, amount = 3, description = "+3 ropes" },
                    new ShopOffer { id = "heart", displayName = "Heart Tonic", price = 500, resource = Res.Health, amount = 1, description = "+1 health" },
                    new ShopOffer { id = "springboots", displayName = "Spring Boots", price = 900, isUpgrade = true, description = "jump about 40% higher" },
                    new ShopOffer { id = "ironwhip", displayName = "Iron Whip", price = 750, isUpgrade = true, description = "whip deals 2 damage and reaches further" },
                    new ShopOffer { id = "compass", displayName = "Compass", price = 450, isUpgrade = true, description = "marks the exit on screen" },
                    new ShopOffer { id = "blastpowder", displayName = "Blast Powder", price = 800, isUpgrade = true, description = "bigger bomb blasts" },
                };
            }
            // Defaults for the built-in offers (also migrates shop assets saved before resources / modifiers existed).
            foreach (var o in shop.offers)
            {
                if (o.modifiers == null) o.modifiers = new StatModifiers();
                if (!o.isUpgrade && string.IsNullOrEmpty(o.resource))
                    o.resource = o.id == "bombs" ? Res.Bombs : o.id == "ropes" ? Res.Ropes : o.id == "heart" ? Res.Health : "";
                if (o.isUpgrade && o.modifiers.IsZero)
                {
                    if (o.id == "springboots") o.modifiers.jumpBonus = 0.17f;
                    else if (o.id == "ironwhip") { o.modifiers.whipDamageBonus = 1; o.modifiers.whipRangeBonus = 0.45f; }
                    else if (o.id == "blastpowder") o.modifiers.blastRadiusBonus = 0.9f;
                }
            }
            var icons = new Dictionary<string, string>
            {
                ["bombs"] = "Sprites/Items/bombbag", ["ropes"] = "Sprites/Items/ropepile", ["heart"] = "Sprites/Items/heart",
                ["springboots"] = "Sprites/Items/springboots", ["ironwhip"] = "Sprites/Items/ironwhip", ["compass"] = "Sprites/Items/compass",
                ["blastpowder"] = "Sprites/Items/blastpowder",
            };
            foreach (var o in shop.offers) if (icons.TryGetValue(o.id, out var ic)) o.icon = ArtGenerator.S(ic);
            EditorUtility.SetDirty(shop);

            var moss = LoadOrCreate<AreaConfig>(G + "Data/Area1_Mossroot.asset", out bool newMoss);
            if (newMoss)
            {
                moss.displayName = "Mossroot Caverns"; moss.areaNumber = 1; moss.musicId = "area1";
                moss.cameraBackground = new Color(0.08f, 0.06f, 0.05f);
                moss.groundEnemies = new[] { new WeightedId("snake", 1f) };
                moss.ceilingEnemies = new[] { new WeightedId("bat", 1f), new WeightedId("spider", 1f) };
                moss.spikes = true; moss.arrowTraps = true; moss.stalactites = true; moss.crushers = false; moss.flameVents = false;
                moss.spikeDensity = 0.035f; moss.arrowTrapDensity = 0.02f;
            }
            moss.tiles = ts["Mossroot"];
            if (string.IsNullOrEmpty(moss.requiredGear)) moss.requiredGear = "rope";
            EditorUtility.SetDirty(moss);

            var ember = LoadOrCreate<AreaConfig>(G + "Data/Area2_Emberdeep.asset", out bool newEmber);
            if (newEmber)
            {
                ember.displayName = "Emberdeep Mines"; ember.areaNumber = 2; ember.musicId = "area2";
                ember.cameraBackground = new Color(0.08f, 0.03f, 0.035f);
                ember.groundEnemies = new[] { new WeightedId("toad", 2f), new WeightedId("brute", 1f) };
                ember.ceilingEnemies = new[] { new WeightedId("emberbat", 1f) };
                ember.items = new[] { new WeightedId("rock", 2f), new WeightedId("pot", 2f), new WeightedId("crate", 1.5f) };
                ember.spikes = true; ember.arrowTraps = true; ember.stalactites = false; ember.crushers = true; ember.flameVents = true;
                ember.spikeDensity = 0.03f; ember.arrowTrapDensity = 0.012f; ember.groundEnemyChance = 0.5f; ember.ceilingEnemyChance = 0.45f;
                ember.goldVeinChance = 0.045f; ember.gemVeinChance = 0.012f;
            }
            ember.tiles = ts["Emberdeep"];
            if (string.IsNullOrEmpty(ember.requiredGear)) ember.requiredGear = "bomb";
            EditorUtility.SetDirty(ember);

            var sunfall = LoadOrCreate<AreaConfig>(G + "Data/Area4_Sunfall.asset", out bool newSun);
            if (newSun)
            {
                sunfall.displayName = "Sunfall Cliffs"; sunfall.areaNumber = 4; sunfall.musicId = "area3";
                sunfall.cameraBackground = new Color(0.19f, 0.27f, 0.55f);
                sunfall.outdoor = true; sunfall.skyRows = 7; sunfall.roomsX = 4; sunfall.roomsY = 3; sunfall.treeChance = 0.3f;
                sunfall.groundEnemies = new[] { new WeightedId("snake", 1f), new WeightedId("toad", 1f) };
                sunfall.ceilingEnemies = new[] { new WeightedId("bat", 1f) };
                sunfall.airEnemies = new[] { new WeightedId("hawk", 1f) };
                sunfall.airEnemiesPerLevel = 2f;
                sunfall.spikes = true; sunfall.arrowTraps = true; sunfall.stalactites = false; sunfall.crushers = false; sunfall.flameVents = false;
                sunfall.spikeDensity = 0.03f; sunfall.arrowTrapDensity = 0.012f;
                sunfall.requiredGear = "glider"; sunfall.grantGear = "glider";
            }
            sunfall.tiles = ts["Sunfall"];
            EditorUtility.SetDirty(sunfall);

            var vault = LoadOrCreate<AreaConfig>(G + "Data/Area3_Vault.asset", out bool newVault);
            if (newVault)
            {
                vault.displayName = "The Sunken Vault"; vault.areaNumber = 3; vault.musicId = "final";
                vault.cameraBackground = new Color(0.05f, 0.05f, 0.08f);
                vault.groundEnemies = new[] { new WeightedId("snake", 1f), new WeightedId("toad", 1f) };
                vault.ceilingEnemies = new[] { new WeightedId("bat", 1f) };
            }
            vault.tiles = ts["Vault"];
            EditorUtility.SetDirty(vault);

            var cfg = LoadOrCreate<GameConfig>(G + "Data/GameConfig.asset");
            cfg.player = player;
            cfg.shop = shop;
            cfg.sfx = sfx;
            var gBomb = LoadOrCreate<EquipmentDef>(G + "Data/Gear_Bomb.asset", out bool newBomb);
            if (newBomb)
            {
                gBomb.id = "bomb"; gBomb.displayName = "Bombs";
                gBomb.description = "Use: throw a lit bomb.  DOWN + use: place it.  UP + use: throw high.";
                gBomb.terrainHint = "Blasts dirt and cracked rubble (never bedrock).";
            }
            if (string.IsNullOrEmpty(gBomb.gateBanner)) gBomb.gateBanner = "Rubble blocks the tunnels ({0}) - you will need your BOMBS.  Hold R1 / D to change gear.";
            if (string.IsNullOrEmpty(gBomb.gateSign)) gBomb.gateSign = "Cracked rubble blocks the tunnel and the rock around it is unbreakable. Equip your BOMBS (hold R1 / D) and blast it.";
            if (string.IsNullOrEmpty(gBomb.supplyPickupId)) gBomb.supplyPickupId = "bombbag";
            if (string.IsNullOrEmpty(gBomb.ammoLabel)) gBomb.ammoLabel = "bomb(s)";
            gBomb.icon = ArtGenerator.S("UI/icon_bomb");
            EditorUtility.SetDirty(gBomb);
            var gRope = LoadOrCreate<EquipmentDef>(G + "Data/Gear_Rope.asset", out bool newRope);
            if (newRope)
            {
                gRope.id = "rope"; gRope.displayName = "Rope";
                gRope.description = "Use: throw a rope up - it hooks onto a ceiling or a wall beside it.  DOWN + use at a ledge: drop it down.";
                gRope.terrainHint = "Climbs sheer cliffs.  Vanishes after a while.";
            }
            if (string.IsNullOrEmpty(gRope.gateBanner)) gRope.gateBanner = "Sheer cliffs ahead ({0}) - you will need your ROPE.  Hold R1 / D to change gear.";
            if (string.IsNullOrEmpty(gRope.gateSign)) gRope.gateSign = "A sheer cliff of hard stone - too high to climb and too hard to blast. Equip your ROPE (hold R1 / D) and throw it right beside the cliff.";
            if (string.IsNullOrEmpty(gRope.supplyPickupId)) gRope.supplyPickupId = "ropepile";
            if (string.IsNullOrEmpty(gRope.ammoLabel)) gRope.ammoLabel = "rope(s)";
            gRope.icon = ArtGenerator.S("UI/icon_rope");
            EditorUtility.SetDirty(gRope);
            var gGlide = LoadOrCreate<EquipmentDef>(G + "Data/Gear_Glider.asset", out bool newGlide);
            if (newGlide)
            {
                gGlide.id = "glider"; gGlide.displayName = "Glider";
                gGlide.description = "Equip it, jump, then HOLD JUMP while falling to glide slowly and far.  Never used up.";
                gGlide.terrainHint = "Crosses wide chasms in the open air.  No fall damage while gliding.";
            }
            if (string.IsNullOrEmpty(gGlide.gateBanner)) gGlide.gateBanner = "Wide thorn chasms ahead ({0}) - equip your GLIDER and hold JUMP while falling.";
            if (string.IsNullOrEmpty(gGlide.gateSign)) gGlide.gateSign = "A wide chasm full of thorns - far too wide to jump. Equip your GLIDER (hold R1 / D), jump and HOLD JUMP to glide across.";
            gGlide.icon = ArtGenerator.S("Sprites/Player/glider");
            EditorUtility.SetDirty(gGlide);
            // Built-in gear first; equipment a designer added to the list by hand is kept.
            var gearList = new List<EquipmentDef> { gBomb, gRope, gGlide };
            if (cfg.equipment != null) foreach (var extra in cfg.equipment) if (extra != null && !gearList.Contains(extra)) gearList.Add(extra);
            cfg.equipment = gearList.ToArray();
            cfg.roomTemplates = AssetDatabase.LoadAssetAtPath<TextAsset>(G + "Data/RoomTemplates.txt");
            var final = AssetDatabase.LoadAssetAtPath<TextAsset>(G + "Data/Levels/FinalVault.txt");
            var tut = AssetDatabase.LoadAssetAtPath<TextAsset>(G + "Data/Levels/Tutorial.txt");
            // The stage list is only written when it is empty, so stages / areas added by hand in GameConfig survive a rebuild.
            if (cfg.stages == null || cfg.stages.Length == 0) cfg.stages = new[]
            {
                new StageDef { displayName = "1-1", kind = LevelKind.Area, area = moss, levelNumber = 1 },
                new StageDef { displayName = "1-2", kind = LevelKind.Area, area = moss, levelNumber = 2 },
                new StageDef { displayName = "1-3", kind = LevelKind.Area, area = moss, levelNumber = 3 },
                new StageDef { displayName = "2-1", kind = LevelKind.Area, area = ember, levelNumber = 1 },
                new StageDef { displayName = "2-2", kind = LevelKind.Area, area = ember, levelNumber = 2 },
                new StageDef { displayName = "2-3", kind = LevelKind.Area, area = ember, levelNumber = 3 },
                new StageDef { displayName = "3-1", kind = LevelKind.Area, area = sunfall, levelNumber = 1 },
                new StageDef { displayName = "3-2", kind = LevelKind.Area, area = sunfall, levelNumber = 2 },
                new StageDef { displayName = "3-3", kind = LevelKind.Area, area = sunfall, levelNumber = 3 },
                new StageDef { displayName = "THE SUNKEN VAULT", kind = LevelKind.Final, area = vault, levelNumber = 1, handcrafted = final },
            };
            cfg.tutorial = new StageDef { displayName = "TUTORIAL", kind = LevelKind.Tutorial, area = moss, levelNumber = 1, handcrafted = tut };
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject NewEntityGO(string name, Sprite sprite, SpriteAnim anim, int order, out SpriteRenderer sr)
        {
            var root = new GameObject(name);
            var sp = new GameObject("Sprite");
            sp.transform.SetParent(root.transform, false);
            sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = spriteMat;
            sr.sortingOrder = order;
            if (anim != null) { var a = sp.AddComponent<SpriteAnimator>(); a.initial = anim; }
            return root;
        }

        static T SavePrefab<T>(GameObject root, string folder) where T : Component
        {
            string path = $"{G}Prefabs/{folder}/{root.name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<T>();
        }

        // Collectible highlighting: glow colour, bob (auto-collected pickups), hint arrow (DOWN + X items).
        static readonly Dictionary<string, (Color color, bool bob, bool hint)> highlights = new Dictionary<string, (Color, bool, bool)>
        {
            ["Rock"] = (new Color(1f, 1f, 1f, 0.6f), false, true),
            ["Pot"] = (new Color(1f, 1f, 1f, 0.6f), false, true),
            ["Crate"] = (new Color(1f, 1f, 1f, 0.6f), false, true),
            ["Arrow"] = (new Color(1f, 1f, 1f, 0.55f), false, true),
            ["Bomb"] = (new Color(1f, 0.6f, 0.4f, 0.65f), false, true),
            ["Idol"] = (new Color(1f, 0.85f, 0.3f, 0.7f), false, true),
            ["Heartstone"] = (new Color(1f, 0.35f, 0.45f, 0.8f), false, true),
            ["GoldNugget"] = (new Color(1f, 0.85f, 0.3f, 0.85f), true, false),
            ["GoldChunk"] = (new Color(1f, 0.85f, 0.3f, 0.9f), true, false),
            ["Gem"] = (new Color(0.4f, 1f, 0.6f, 0.9f), true, false),
            ["BombBag"] = (new Color(1f, 1f, 1f, 0.75f), true, false),
            ["RopePile"] = (new Color(1f, 1f, 1f, 0.75f), true, false),
            ["Heart"] = (new Color(1f, 0.35f, 0.45f, 0.95f), true, false),
        };

        static T Make<T>(string id, string folder, string display, Vector2 size, Sprite sprite, SpriteAnim anim, int order, Action<T, GameObject, SpriteRenderer> setup = null)
            where T : Entity
        {
            var root = NewEntityGO(id, sprite, anim, order, out var sr);
            var e = root.AddComponent<T>();
            e.displayName = display;
            e.size = size;
            setup?.Invoke(e, root, sr);
            if (highlights.TryGetValue(id, out var hl))
            {
                var h = root.AddComponent<Highlight>();
                h.glowColor = hl.color; h.bob = hl.bob; h.showHint = hl.hint;
                h.glowSprite = ArtGenerator.S("Center/glow");
                h.hintSprite = ArtGenerator.S("Center/hint_arrow");
                h.material = spriteMat;
                h.glowScale = hl.bob ? 3.2f : 2.4f;
            }
            return SavePrefab<T>(root, folder);
        }

        static void CreatePrefabs(GameConfig cfg, PixelFont font)
        {
            var reg = LoadOrCreate<PrefabRegistry>(G + "Data/PrefabRegistry.asset");
            // Entries added by hand (new enemies / items made as prefabs in the editor) are kept across rebuilds.
            var manualEntries = new List<PrefabRegistry.Entry>(reg.entries);
            reg.entries.Clear();
            void Reg(string id, Entity e) => reg.entries.Add(new PrefabRegistry.Entry { id = id, prefab = e });
            string I(string n) => "Sprites/Items/" + n;

            // player
            {
                var root = NewEntityGO("Player", ArtGenerator.S("Sprites/Player/idle_0"), anims["player_idle"], 20, out _);
                var whip = new GameObject("Whip");
                whip.transform.SetParent(root.transform, false);
                var wsr = whip.AddComponent<SpriteRenderer>();
                wsr.sharedMaterial = spriteMat; wsr.sortingOrder = 21; wsr.enabled = false;
                var p = root.AddComponent<PlayerController>();
                p.displayName = "explorer";
                p.cfg = cfg.player;
                p.animIdle = anims["player_idle"]; p.animRun = anims["player_run"]; p.animJump = anims["player_jump"]; p.animFall = anims["player_fall"];
                p.animCrouch = anims["player_crouch"]; p.animCrawl = anims["player_crawl"]; p.animClimb = anims["player_climb"]; p.animHang = anims["player_hang"];
                p.animWhip = anims["player_whip"]; p.animThrow = anims["player_throw"]; p.animStunned = anims["player_stunned"]; p.animDead = anims["player_dead"];
                p.animLookUp = anims["player_lookup"];
                p.whipRenderer = wsr;
                var glider = new GameObject("Glider");
                glider.transform.SetParent(root.transform, false);
                var gsr = glider.AddComponent<SpriteRenderer>();
                gsr.sprite = ArtGenerator.S("Sprites/Player/glider"); gsr.sharedMaterial = spriteMat; gsr.sortingOrder = 21; gsr.enabled = false;
                p.gliderRenderer = gsr;
                p.whipBackSprite = ArtGenerator.S("Sprites/Player/whip_back");
                p.whipFrontSprite = ArtGenerator.S("Sprites/Player/whip_front");
                reg.player = SavePrefab<PlayerController>(root, "");
            }

            // enemies
            Reg("snake", Make<Snake>("Snake", "Enemies", "snake", new Vector2(0.75f, 0.45f), ArtGenerator.S("Sprites/Enemies/snake_0"), anims["snake_walk"], 14,
                (e, go, sr) => { e.animWalk = anims["snake_walk"]; e.bloodColor = new Color(0.3f, 0.6f, 0.2f); }));
            Reg("bat", Make<Bat>("Bat", "Enemies", "bat", new Vector2(0.6f, 0.5f), ArtGenerator.S("Sprites/Enemies/bat_2"), anims["bat_hang"], 14,
                (e, go, sr) => { e.animHang = anims["bat_hang"]; e.animFly = anims["bat_fly"]; }));
            Reg("emberbat", Make<Bat>("EmberBat", "Enemies", "ember bat", new Vector2(0.6f, 0.5f), ArtGenerator.S("Sprites/Enemies/emberbat_2"), anims["emberbat_hang"], 14,
                (e, go, sr) => { e.animHang = anims["emberbat_hang"]; e.animFly = anims["emberbat_fly"]; e.speed = 4.6f; e.accel = 12f; e.wakeRange = 7f; e.bloodColor = new Color(1f, 0.5f, 0.2f); }));
            Reg("spider", Make<Spider>("Spider", "Enemies", "spider", new Vector2(0.6f, 0.5f), ArtGenerator.S("Sprites/Enemies/spider_2"), anims["spider_hang"], 14,
                (e, go, sr) => { e.animHang = anims["spider_hang"]; e.animIdle = anims["spider_idle"]; e.animJump = anims["spider_jump"]; e.bloodColor = new Color(0.3f, 0.6f, 0.3f); }));
            Reg("toad", Make<Toad>("Toad", "Enemies", "cinder toad", new Vector2(0.7f, 0.55f), ArtGenerator.S("Sprites/Enemies/toad_0"), anims["toad_idle"], 14,
                (e, go, sr) => { e.animIdle = anims["toad_idle"]; e.animJump = anims["toad_jump"]; e.bloodColor = new Color(0.9f, 0.5f, 0.2f); }));
            Reg("brute", Make<Brute>("Brute", "Enemies", "mole brute", new Vector2(0.85f, 0.9f), ArtGenerator.S("Sprites/Enemies/brute_0"), anims["brute_walk"], 14,
                (e, go, sr) => { e.maxHealth = 3; e.knockbackScale = 1.6f; e.goldDrop = 100; e.animWalk = anims["brute_walk"]; e.animCharge = anims["brute_charge"]; e.animDazed = anims["brute_dazed"]; }));
            Reg("hawk", Make<Hawk>("Hawk", "Enemies", "hawk", new Vector2(0.6f, 0.5f), ArtGenerator.S("Sprites/Enemies/hawk_0"), anims["hawk_fly"], 14,
                (e, go, sr) => { e.animFly = anims["hawk_fly"]; e.animDive = anims["hawk_dive"]; e.bloodColor = new Color(0.6f, 0.2f, 0.15f); }));
            Reg("tree", Make<Scenery>("Tree", "Level", "tree", new Vector2(1f, 2.5f), ArtGenerator.S("Sprites/Props/tree_0"), null, -6,
                (e, go, sr) => { e.flipWithFacing = false; e.variants = new[] { ArtGenerator.S("Sprites/Props/tree_0"), ArtGenerator.S("Sprites/Props/tree_1") }; }));
            Reg("wraith", Make<Wraith>("Wraith", "Enemies", "Gloom Wraith", new Vector2(0.8f, 1.0f), ArtGenerator.S("Sprites/Enemies/wraith_0"), anims["wraith_float"], 30,
                (e, go, sr) => { e.contactDamage = 99; e.stompable = false; e.animFloat = anims["wraith_float"]; }));
            Reg("shopkeeper", Make<Shopkeeper>("Shopkeeper", "Enemies", "shopkeeper", new Vector2(0.6f, 0.85f), ArtGenerator.S("Sprites/Shopkeeper/idle_0"), anims["keeper_idle"], 14,
                (e, go, sr) => { e.maxHealth = 6; e.animIdle = anims["keeper_idle"]; e.animAngry = anims["keeper_angry"]; e.goldDrop = 300; e.stompable = true; }));

            // items
            Reg("rock", Make<Item>("Rock", "Items", "rock", new Vector2(0.45f, 0.35f), ArtGenerator.S(I("rock")), null, 12, (e, g, s) => { e.flipWithFacing = false; }));
            Reg("pot", Make<Breakable>("Pot", "Items", "pot", new Vector2(0.55f, 0.6f), ArtGenerator.S(I("pot")), null, 12,
                (e, g, s) => { e.breakSpeed = 7.5f; e.flipWithFacing = false; e.debrisColor = new Color(0.72f, 0.42f, 0.26f); }));
            Reg("crate", Make<Breakable>("Crate", "Items", "crate", new Vector2(0.7f, 0.65f), ArtGenerator.S(I("crate")), null, 12,
                (e, g, s) =>
                {
                    e.breakSpeed = 10f; e.flipWithFacing = false; e.breakSfx = "crate_break"; e.debrisColor = new Color(0.6f, 0.42f, 0.24f);
                    e.contents = new[] { new WeightedId("bombbag", 3f), new WeightedId("ropepile", 3f), new WeightedId("gold_big", 2f), new WeightedId("gem", 1f), new WeightedId("heart", 2f) };
                }));
            Reg("arrow", Make<Arrow>("Arrow", "Items", "arrow", new Vector2(0.6f, 0.2f), ArtGenerator.S(I("arrow")), null, 12, (e, g, s) => { e.damageSpeed = 5f; e.bounce = 0.2f; }));
            Reg("bomb", Make<Bomb>("Bomb", "Items", "bomb", new Vector2(0.45f, 0.45f), ArtGenerator.S(I("bomb_0")), anims["bomb_idle"], 12,
                (e, g, s) => { e.animLit = anims["bomb_lit"]; e.bounce = 0.45f; e.damageSpeed = 8f; e.flipWithFacing = false; }));
            Reg("idol", Make<Treasure>("Idol", "Items", "golden idol", new Vector2(0.5f, 0.7f), ArtGenerator.S(I("idol")), null, 12,
                (e, g, s) => { e.heavy = true; e.value = 1200; e.flipWithFacing = false; }));
            Reg("heartstone", Make<Treasure>("Heartstone", "Items", "Heartstone", new Vector2(0.55f, 0.7f), ArtGenerator.S(I("heartstone")), null, 12,
                (e, g, s) => { e.heavy = true; e.isHeartstone = true; e.glow = new Color(1f, 0.4f, 0.5f); e.flipWithFacing = false; }));

            // pickups
            Reg("gold_small", Make<Pickup>("GoldNugget", "Items", "gold nugget", new Vector2(0.3f, 0.25f), ArtGenerator.S(I("gold_small")), null, 11, (e, g, s) => { e.value = 50; e.flipWithFacing = false; }));
            Reg("gold_big", Make<Pickup>("GoldChunk", "Items", "gold chunk", new Vector2(0.5f, 0.35f), ArtGenerator.S(I("gold_big")), null, 11, (e, g, s) => { e.value = 150; e.flipWithFacing = false; }));
            Reg("gem", Make<Pickup>("Gem", "Items", "emerald", new Vector2(0.4f, 0.4f), ArtGenerator.S(I("gem")), null, 11, (e, g, s) => { e.value = 300; e.sfx = "gem"; e.textColor = new Color(0.5f, 1f, 0.7f); e.flipWithFacing = false; }));
            Reg("bombbag", Make<Pickup>("BombBag", "Items", "bomb bag", new Vector2(0.6f, 0.55f), ArtGenerator.S(I("bombbag")), null, 11, (e, g, s) => { e.resource = Res.Bombs; e.label = "BOMBS"; e.value = 3; e.sfx = "pickup"; e.textColor = Color.white; e.flipWithFacing = false; }));
            Reg("ropepile", Make<Pickup>("RopePile", "Items", "rope pile", new Vector2(0.6f, 0.45f), ArtGenerator.S(I("ropepile")), null, 11, (e, g, s) => { e.resource = Res.Ropes; e.label = "ROPES"; e.value = 3; e.sfx = "pickup"; e.textColor = Color.white; e.flipWithFacing = false; }));
            Reg("heart", Make<Pickup>("Heart", "Items", "heart", new Vector2(0.5f, 0.45f), ArtGenerator.S(I("heart")), null, 11,
                (e, g, s) => { e.resource = Res.Health; e.label = "HP"; e.value = 1; e.sfx = "buy"; e.textColor = new Color(1f, 0.45f, 0.5f); e.flipWithFacing = false; }));
            Reg("rope", Make<RopeProjectile>("Rope", "Items", "rope", new Vector2(0.4f, 0.4f), ArtGenerator.S("Center/rope_hook"), null, 5,
                (e, g, s) =>
                {
                    e.segmentSprite = ArtGenerator.S("Center/rope_seg"); e.topSprite = ArtGenerator.S("Center/rope_top"); e.bottomSprite = ArtGenerator.S("Center/rope_end");
                    e.lipSprite = ArtGenerator.S("Center/rope_lip"); e.pitonSprite = ArtGenerator.S("Center/rope_piton");
                    e.material = spriteMat; e.flipWithFacing = false;
                }));

            // traps
            Reg("arrowtrap", Make<ArrowTrap>("ArrowTrap", "Traps", "arrow trap", new Vector2(1f, 1f), ArtGenerator.S("Sprites/Traps/arrowtrap_armed"), null, 2,
                (e, g, s) => { e.armedSprite = ArtGenerator.S("Sprites/Traps/arrowtrap_armed"); e.emptySprite = ArtGenerator.S("Sprites/Traps/arrowtrap_empty"); }));
            Reg("stalactite", Make<Stalactite>("Stalactite", "Traps", "stalactite", new Vector2(0.5f, 0.9f), ArtGenerator.S("Sprites/Traps/stalactite"), null, 6, (e, g, s) => { e.flipWithFacing = false; }));
            Reg("crusher", Make<Crusher>("Crusher", "Traps", "crusher", new Vector2(0.9f, 0.95f), ArtGenerator.S("Sprites/Traps/crusher"), null, 6,
                (e, g, s) =>
                {
                    e.flipWithFacing = false;
                    var ch = new GameObject("Chain");
                    ch.transform.SetParent(g.transform, false);
                    var csr = ch.AddComponent<SpriteRenderer>();
                    csr.sprite = ArtGenerator.S("Sprites/Traps/chain"); csr.sharedMaterial = spriteMat; csr.sortingOrder = 5;
                    csr.drawMode = SpriteDrawMode.Tiled; csr.size = new Vector2(1f, 0.1f);
                    e.chain = csr;
                }));
            Reg("flamevent", Make<FlameVent>("FlameVent", "Traps", "flame vent", new Vector2(1f, 0.2f), null, null, 8,
                (e, g, s) =>
                {
                    e.flipWithFacing = false;
                    s.sprite = ArtGenerator.S("Sprites/Traps/flame_0");
                    var anim = s.gameObject.AddComponent<SpriteAnimator>();
                    anim.initial = anims["flame"];
                    e.flame = s; e.flameAnim = anim; e.flameLoop = anims["flame"];
                }));

            // level objects
            Reg("door_entrance", Make<Door>("DoorEntrance", "Level", "entrance", new Vector2(0.8f, 1.2f), ArtGenerator.S("Sprites/Props/door_entrance"), null, 3,
                (e, g, s) => { e.isExit = false; e.flipWithFacing = false; e.openSprite = ArtGenerator.S("Sprites/Props/door_entrance"); }));
            Reg("door_exit", Make<Door>("DoorExit", "Level", "exit", new Vector2(0.8f, 1.2f), ArtGenerator.S("Sprites/Props/door_exit"), null, 3,
                (e, g, s) => { e.isExit = true; e.flipWithFacing = false; e.openSprite = ArtGenerator.S("Sprites/Props/door_exit"); e.lockedSprite = ArtGenerator.S("Sprites/Props/door_locked"); }));
            Reg("sign", Make<Sign>("Sign", "Level", "sign", new Vector2(0.9f, 1f), ArtGenerator.S("Sprites/Props/sign"), null, 3, (e, g, s) => { e.flipWithFacing = false; }));
            Reg("altar", Make<Prop>("Altar", "Level", "altar", new Vector2(1f, 0.4f), ArtGenerator.S("Sprites/Props/altar"), null, 4, (e, g, s) => { e.flipWithFacing = false; }));
            Reg("shopsign", Make<Prop>("ShopSign", "Level", "shop sign", new Vector2(1f, 1f), ArtGenerator.S("Sprites/Props/shopsign"), null, 3, (e, g, s) => { e.flipWithFacing = false; }));
            Reg("shopitem", Make<ShopItem>("ShopItem", "Level", "shop item", new Vector2(0.8f, 0.8f), null, null, 12,
                (e, g, s) =>
                {
                    e.flipWithFacing = false;
                    e.icon = s;
                    s.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                    var tag = new GameObject("Price");
                    tag.transform.SetParent(g.transform, false);
                    tag.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                    var pt = tag.AddComponent<PixelText>();
                    pt.font = font; pt.material = spriteMat; pt.sortingOrder = 60;
                    e.priceTag = pt;
                }));

            foreach (var m in manualEntries)
                if (m != null && m.prefab != null && !string.IsNullOrEmpty(m.id) && !reg.entries.Exists(x => x.id == m.id)) reg.entries.Add(m);
            cfg.prefabs = reg;
            EditorUtility.SetDirty(reg);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
        }

        static void CreateServicesPrefab(GameConfig cfg, SfxLibrary sfx)
        {
            var go = new GameObject("GameServices");
            var gs = go.AddComponent<GameServices>();
            gs.config = cfg;
            var am = go.AddComponent<AudioManager>();
            am.library = sfx;
            PrefabUtility.SaveAsPrefabAsset(go, G + "Resources/GameServices.prefab");
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ scenes

        static Camera MakeCamera(Color bg)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.75f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = -50f; cam.farClipPlane = 50f;
            go.transform.position = new Vector3(0, 0, -10);
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();
            return cam;
        }

        static void BuildMenuScene(GameConfig cfg)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera(new Color(0.06f, 0.045f, 0.05f));
            UIBuilder.EventSystem();
            var canvas = UIBuilder.Canvas("MenuCanvas", 0);
            var t = canvas.transform;
            var gold = new Color(1f, 0.84f, 0.38f);

            var vig = UIBuilder.Img(t, "Vignette", ArtGenerator.S("UI/vignette"), new Color(0f, 0f, 0f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(vig.rectTransform);
            // decorative characters
            UIBuilder.Img(t, "Hero", ArtGenerator.S("Sprites/Player/idle_0"), Color.white, new Vector2(0.2f, 0.32f), Vector2.zero, new Vector2(240, 240));
            UIBuilder.Img(t, "Bat", ArtGenerator.S("Sprites/Enemies/bat_0"), Color.white, new Vector2(0.82f, 0.62f), Vector2.zero, new Vector2(170, 170));
            UIBuilder.Img(t, "Snake", ArtGenerator.S("Sprites/Enemies/snake_0"), Color.white, new Vector2(0.8f, 0.25f), Vector2.zero, new Vector2(200, 200));
            UIBuilder.Img(t, "Heartstone", ArtGenerator.S("Sprites/Items/heartstone"), Color.white, new Vector2(0.5f, 1f), new Vector2(-560, -140), new Vector2(120, 120));
            UIBuilder.Img(t, "Idol", ArtGenerator.S("Sprites/Items/idol"), Color.white, new Vector2(0.5f, 1f), new Vector2(560, -140), new Vector2(120, 120));

            UIBuilder.Label(t, "Title", "GLOOMDELVE", 112, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(1400, 150), true, FontStyle.Bold);
            UIBuilder.Label(t, "Subtitle", "a cave-delving roguelike platformer", 34, new Color(0.85f, 0.8f, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(1200, 50));

            var menu = new GameObject("MainMenu").AddComponent<MainMenuController>();
            menu.config = cfg;

            var main = UIBuilder.Node(t, "MainPanel");
            UIBuilder.Place(UIBuilder.Rect(main), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(520, 470));
            var mt = main.transform;
            Vector2 bs = new Vector2(460, 64);
            menu.startButton = UIBuilder.Button(mt, "Start", "START RUN", new Vector2(0.5f, 1f), new Vector2(0, -36), bs);
            menu.tutorialButton = UIBuilder.Button(mt, "Tutorial", "TUTORIAL", new Vector2(0.5f, 1f), new Vector2(0, -108), bs);
            menu.controlsButton = UIBuilder.Button(mt, "Controls", "CONTROLS", new Vector2(0.5f, 1f), new Vector2(0, -180), bs);
            menu.settingsButton = UIBuilder.Button(mt, "Settings", "SETTINGS", new Vector2(0.5f, 1f), new Vector2(0, -252), bs);
            menu.recordsButton = UIBuilder.Button(mt, "Records", "RECORDS", new Vector2(0.5f, 1f), new Vector2(0, -324), bs);
            menu.quitButton = UIBuilder.Button(mt, "Quit", "QUIT", new Vector2(0.5f, 1f), new Vector2(0, -396), bs);
            menu.mainPanel = main;

            menu.controlsPanel = UIBuilder.ControlsPanel(t, out menu.controlsBody, out menu.controlsBack);
            menu.settingsPanel = UIBuilder.Settings(t);
            var rec = UIBuilder.Panel(t, "RecordsPanel", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(760, 640));
            UIBuilder.Label(rec.transform, "Title", "RECORDS", 46, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(700, 70));
            menu.recordsText = UIBuilder.Label(rec.transform, "Body", "", 30, Color.white, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(620, 380));
            menu.resetRecordsButton = UIBuilder.Button(rec.transform, "Reset", "RESET RECORDS", new Vector2(0.5f, 0f), new Vector2(-170, 50), new Vector2(320, 70));
            menu.recordsBack = UIBuilder.Button(rec.transform, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(170, 50), new Vector2(300, 70));
            menu.recordsPanel = rec.gameObject;
            menu.versionText = UIBuilder.Label(t, "Version", "", 22, new Color(1, 1, 1, 0.5f), TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(20, 16), new Vector2(900, 40), false);
            UIBuilder.Label(t, "Hint", "Arrow keys / Enter to navigate   ·   Z / Space jump   ·   X whip   ·   C use gear   ·   hold D gear wheel   ·   A enter", 22,
                new Color(1, 1, 1, 0.55f), TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-20, 16), new Vector2(1300, 40), false);

            EditorSceneManager.SaveScene(scene, G + "Scenes/MainMenu.unity");
        }

        static Tilemap MakeTilemap(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            r.sharedMaterial = spriteMat;
            r.mode = TilemapRenderer.Mode.Chunk;
            return tm;
        }

        static void BuildGameScene(GameConfig cfg, InputActionAsset input, PixelFont font)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = MakeCamera(new Color(0.08f, 0.06f, 0.05f));
            var follow = cam.gameObject.AddComponent<CameraFollow>();

            // outdoor sky backdrop (enabled by GameDirector in outdoor areas)
            var skyGo = new GameObject("Sky");
            skyGo.transform.SetParent(cam.transform, false);
            var sky = skyGo.AddComponent<SkyBackdrop>();
            sky.cam = cam;
            SpriteRenderer SkySr(string name, string sprite, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(skyGo.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = ArtGenerator.S(sprite); r.sharedMaterial = spriteMat; r.sortingOrder = order;
                return r;
            }
            sky.gradient = SkySr("Gradient", "Sky/gradient", -40);
            sky.layers = new[] { SkySr("Mountains", "Sky/mountains", -38), SkySr("Hills", "Sky/hills", -36) };
            sky.parallax = new[] { 0.08f, 0.22f };
            sky.layerHeight = new[] { -1.2f, -3.4f };
            sky.clouds = new[] { SkySr("Cloud0", "Sky/cloud_0", -37), SkySr("Cloud1", "Sky/cloud_1", -37), SkySr("Cloud2", "Sky/cloud_0", -37) };
            skyGo.SetActive(false);
            UIBuilder.EventSystem();

            // world
            var worldGo = new GameObject("World");
            var world = worldGo.AddComponent<World>();
            world.config = cfg;
            var grid = new GameObject("Grid");
            grid.transform.SetParent(worldGo.transform, false);
            grid.AddComponent<Grid>();
            var lr = grid.AddComponent<LevelRenderer>();
            lr.background = MakeTilemap(grid.transform, "Background", -20);
            lr.background.color = new Color(0.85f, 0.85f, 0.85f);
            lr.decor = MakeTilemap(grid.transform, "Decor", -10);
            lr.decor.color = new Color(0.55f, 0.55f, 0.62f);   // decor is dimmed so it never reads as a collectible
            lr.terrain = MakeTilemap(grid.transform, "Terrain", 0);
            lr.overlay = MakeTilemap(grid.transform, "Overlay", 1);
            world.levelRenderer = lr;
            var ents = new GameObject("Entities");
            ents.transform.SetParent(worldGo.transform, false);
            world.entityRoot = ents.transform;

            // fx
            var fxGo = new GameObject("Fx");
            var fx = fxGo.AddComponent<Fx>();
            fx.pixel = ArtGenerator.S("Center/pixel");
            fx.smokeFrames = ArtGenerator.Frames("Center/smoke", 4);
            fx.explosionAnim = anims["explosion"];
            fx.font = font;
            fx.spriteMaterial = spriteMat;
            fx.cameraFollow = follow;

            // director
            var dirGo = new GameObject("GameDirector");
            var dir = dirGo.AddComponent<GameDirector>();
            var reader = dirGo.AddComponent<PlayerInputReader>();
            reader.actions = input;
            dir.config = cfg; dir.world = world; dir.cameraFollow = follow; dir.cam = cam; dir.input = reader;
            dir.sky = sky;

            // UI
            var gold = new Color(1f, 0.84f, 0.38f);
            var canvas = UIBuilder.Canvas("GameCanvas", 10);
            var t = canvas.transform;
            var hudGo = UIBuilder.Node(t, "HUD");
            UIBuilder.Stretch(UIBuilder.Rect(hudGo));
            var hud = hudGo.AddComponent<HUD>();
            var vig = UIBuilder.Img(hudGo.transform, "DamageVignette", ArtGenerator.S("UI/vignette"), new Color(0.8f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(vig.rectTransform);
            hud.damageVignette = vig;
            var bar = UIBuilder.Panel(hudGo.transform, "TopLeft", new Vector2(0f, 1f), new Vector2(16, -16), new Vector2(640, 84));
            Text Stat(string name, string icon, float x, out Image img)
            {
                img = UIBuilder.Img(bar.transform, name + "Icon", ArtGenerator.S(icon), Color.white, new Vector2(0f, 0.5f), new Vector2(x, 0), new Vector2(48, 48));
                img.rectTransform.pivot = new Vector2(0f, 0.5f);
                var tx = UIBuilder.Label(bar.transform, name, "0", 40, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(x + 56, 0), new Vector2(120, 60), true, FontStyle.Bold);
                tx.rectTransform.pivot = new Vector2(0f, 0.5f);
                return tx;
            }
            hud.healthText = Stat("Health", "UI/icon_heart", 20, out hud.heartIcon);
            hud.bombText = Stat("Bombs", "UI/icon_bomb", 150, out _);
            hud.ropeText = Stat("Ropes", "UI/icon_rope", 280, out _);
            hud.goldText = Stat("Gold", "UI/icon_gold", 410, out _);
            hud.goldText.color = gold;
            hud.goldText.rectTransform.sizeDelta = new Vector2(180, 60);
            hud.upgradesText = UIBuilder.Label(hudGo.transform, "Upgrades", "", 24, new Color(0.7f, 1f, 0.75f), TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28, -108), new Vector2(900, 40));
            hud.upgradesText.rectTransform.pivot = new Vector2(0f, 1f);
            hud.upgradesText.rectTransform.anchoredPosition = new Vector2(28, -216);

            var gearBox = UIBuilder.Panel(hudGo.transform, "Gear", new Vector2(0f, 1f), new Vector2(16, -108), new Vector2(420, 96));
            gearBox.rectTransform.pivot = new Vector2(0f, 1f);
            var gframe = UIBuilder.Img(gearBox.transform, "IconFrame", UIBuilder.buttonSprite, new Color(1f, 0.8f, 0.35f), new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(58, 58), true);
            gframe.rectTransform.pivot = new Vector2(0f, 0.5f);
            hud.equipIcon = UIBuilder.Img(gframe.transform, "Icon", ArtGenerator.S("UI/icon_bomb"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            hud.equipName = UIBuilder.Label(gearBox.transform, "Name", "BOMBS", 30, gold, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(86, 13), new Vector2(200, 36), true, FontStyle.Bold);
            hud.equipName.rectTransform.pivot = new Vector2(0f, 0.5f);
            hud.equipAmmo = UIBuilder.Label(gearBox.transform, "Ammo", "x4", 30, Color.white, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-22, 13), new Vector2(100, 36), true, FontStyle.Bold);
            hud.equipAmmo.rectTransform.pivot = new Vector2(1f, 0.5f);
            var ghint = UIBuilder.Label(gearBox.transform, "Hint", "C use  /  hold D or R1 to change", 18, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(86, -17), new Vector2(316, 22), false);
            ghint.rectTransform.pivot = new Vector2(0f, 0.5f);

            // Rows are placed by their vertical centre (pivot y = 0.5) so everything stays inside the frame.
            var tr = UIBuilder.Panel(hudGo.transform, "TopRight", new Vector2(1f, 1f), new Vector2(-16, -16), new Vector2(460, 150));
            var rowPivot = new Vector2(1f, 0.5f);
            hud.stageText = UIBuilder.Label(tr.transform, "Stage", "1-1", 28, gold, TextAnchor.MiddleRight, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.Place(hud.stageText.rectTransform, new Vector2(1f, 1f), new Vector2(-28, -38), new Vector2(404, 36), rowPivot);
            var clock = UIBuilder.Img(tr.transform, "Clock", ArtGenerator.S("UI/icon_clock"), Color.white, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.Place(clock.rectTransform, new Vector2(1f, 1f), new Vector2(-178, -80), new Vector2(34, 34), rowPivot);
            hud.timeText = UIBuilder.Label(tr.transform, "Time", "00:00.0", 36, Color.white, TextAnchor.MiddleRight, new Vector2(1f, 1f), Vector2.zero, Vector2.zero, true, FontStyle.Bold);
            UIBuilder.Place(hud.timeText.rectTransform, new Vector2(1f, 1f), new Vector2(-28, -80), new Vector2(140, 44), rowPivot);
            hud.totalTimeText = UIBuilder.Label(tr.transform, "Total", "", 20, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleRight, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.Place(hud.totalTimeText.rectTransform, new Vector2(1f, 1f), new Vector2(-28, -116), new Vector2(404, 26), rowPivot);

            // Waypoint marker (Compass upgrade): full-screen layer, marker positioned at runtime.
            var comp = UIBuilder.Node(hudGo.transform, "Waypoint");
            UIBuilder.Stretch(UIBuilder.Rect(comp));
            hud.wpGroup = comp.AddComponent<CanvasGroup>();
            hud.wpGroup.blocksRaycasts = false; hud.wpGroup.interactable = false;
            var mk = UIBuilder.Node(comp.transform, "Marker");
            UIBuilder.Place(UIBuilder.Rect(mk), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));
            hud.wpMarker = (RectTransform)mk.transform;
            var c0 = new Vector2(0.5f, 0.5f);
            hud.wpPing = UIBuilder.Img(mk.transform, "Ping", ArtGenerator.S("UI/wp_ping"), Color.white, c0, Vector2.zero, new Vector2(76, 76));
            hud.wpTail = UIBuilder.Img(mk.transform, "Tail", ArtGenerator.S("UI/wp_tail"), Color.white, c0, new Vector2(0, -43), new Vector2(18, 18));
            UIBuilder.Img(mk.transform, "Fill", ArtGenerator.S("UI/wp_fill"), new Color(0.06f, 0.08f, 0.11f, 0.82f), c0, Vector2.zero, new Vector2(72, 72));
            hud.wpRing = UIBuilder.Img(mk.transform, "Ring", ArtGenerator.S("UI/wp_ring"), Color.white, c0, Vector2.zero, new Vector2(72, 72));
            hud.wpExitIcon = ArtGenerator.S("UI/wp_exit");
            hud.wpGoalIcon = ArtGenerator.S("UI/wp_goal");
            hud.wpIcon = UIBuilder.Img(mk.transform, "Icon", hud.wpExitIcon, Color.white, c0, new Vector2(0, 1), new Vector2(38, 38));
            var piv = UIBuilder.Node(mk.transform, "ArrowPivot");
            UIBuilder.Place(UIBuilder.Rect(piv), c0, Vector2.zero, Vector2.zero);
            hud.wpArrowPivot = (RectTransform)piv.transform;
            hud.wpChevron = UIBuilder.Img(piv.transform, "Chevron", ArtGenerator.S("UI/wp_chevron"), Color.white, c0, new Vector2(54, 0), new Vector2(28, 28));
            hud.compassText = UIBuilder.Label(mk.transform, "Dist", "", 22, Color.white, TextAnchor.MiddleCenter, c0, new Vector2(0, -68), new Vector2(160, 28), true, FontStyle.Bold);
            hud.compassText.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            hud.compassRoot = comp;

            var prompt = UIBuilder.Panel(hudGo.transform, "Prompt", new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1500, 80));
            hud.promptPanel = prompt.gameObject;
            hud.promptText = UIBuilder.Label(prompt.transform, "Text", "", 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 76));

            var banner = UIBuilder.Node(hudGo.transform, "Banner");
            UIBuilder.Place(UIBuilder.Rect(banner), new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1600, 160));
            hud.bannerGroup = banner.AddComponent<CanvasGroup>();
            hud.bannerTitle = UIBuilder.Label(banner.transform, "Title", "", 58, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(1600, 80), true, FontStyle.Bold);
            hud.bannerSub = UIBuilder.Label(banner.transform, "Sub", "", 30, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1600, 50));

            var msg = UIBuilder.Node(hudGo.transform, "Message");
            UIBuilder.Place(UIBuilder.Rect(msg), new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(1600, 60));
            hud.messageGroup = msg.AddComponent<CanvasGroup>();
            hud.messageText = UIBuilder.Label(msg.transform, "Text", "", 30, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 60));

            var hurt = UIBuilder.Node(hudGo.transform, "Hurt");
            UIBuilder.Place(UIBuilder.Rect(hurt), new Vector2(0.5f, 0f), new Vector2(0, 160), new Vector2(1200, 60));
            hud.hurtGroup = hurt.AddComponent<CanvasGroup>();
            hud.hurtText = UIBuilder.Label(hurt.transform, "Text", "", 32, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 60), true, FontStyle.Bold);
            dir.hud = hud;

            // gear wheel (hold R1 / D)
            var wheelGo = UIBuilder.Node(t, "GearWheel");
            UIBuilder.Stretch(UIBuilder.Rect(wheelGo));
            var wheel = wheelGo.AddComponent<EquipmentWheel>();
            var wroot = UIBuilder.Img(wheelGo.transform, "Root", null, new Color(0f, 0f, 0f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(wroot.rectTransform);
            wheel.root = wroot.gameObject;
            var wpanel = UIBuilder.Panel(wroot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(940, 500));
            UIBuilder.Label(wpanel.transform, "Title", "GEAR", 40, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(800, 50), true, FontStyle.Bold);
            var row = UIBuilder.Node(wpanel.transform, "Row");
            UIBuilder.Place(UIBuilder.Rect(row), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(800, 150));
            wheel.row = (RectTransform)row.transform;
            var slot = UIBuilder.Img(row.transform, "SlotTemplate", UIBuilder.buttonSprite, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124, 124), true);
            UIBuilder.Img(slot.transform, "Icon", ArtGenerator.S("UI/icon_bomb"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(84, 84));
            UIBuilder.Label(slot.transform, "Ammo", "x4", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, -22), new Vector2(120, 36), true, FontStyle.Bold);
            wheel.slotTemplate = slot.gameObject;
            wheel.nameText = UIBuilder.Label(wpanel.transform, "Name", "", 34, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 148), new Vector2(900, 44), true, FontStyle.Bold);
            wheel.descText = UIBuilder.Label(wpanel.transform, "Desc", "", 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter, new Vector2(0.5f, 0f), new Vector2(0, 84), new Vector2(900, 70));
            wheel.hintText = UIBuilder.Label(wpanel.transform, "Hint", "", 20, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(860, 30), false);
            wheel.input = reader;
            wheel.director = dir;
            dir.gearWheel = wheel;

            // transition
            var trGo = UIBuilder.Node(t, "Transition");
            UIBuilder.Stretch(UIBuilder.Rect(trGo));
            var trs = trGo.AddComponent<TransitionScreen>();
            var trRoot = UIBuilder.Img(trGo.transform, "Root", null, new Color(0.03f, 0.02f, 0.03f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(trRoot.rectTransform);
            trs.root = trRoot.gameObject;
            trs.title = UIBuilder.Label(trRoot.transform, "Title", "", 64, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1600, 90), true, FontStyle.Bold);
            trs.body = UIBuilder.Label(trRoot.transform, "Body", "", 34, Color.white, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(760, 260));
            trs.hint = UIBuilder.Label(trRoot.transform, "Hint", "", 26, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(1200, 50));
            dir.transition = trs;

            // pause
            var pauseGo = UIBuilder.Node(t, "Pause");
            UIBuilder.Stretch(UIBuilder.Rect(pauseGo));
            var pm = pauseGo.AddComponent<PauseMenu>();
            var proot = UIBuilder.Dimmer(pauseGo.transform, "Root");
            pm.root = proot.gameObject;
            var pmain = UIBuilder.Panel(proot.transform, "MainPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 820));
            UIBuilder.Label(pmain.transform, "Title", "PAUSED", 56, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -55), new Vector2(700, 80), true, FontStyle.Bold);
            Vector2 pb = new Vector2(480, 64);
            pm.resumeButton = UIBuilder.Button(pmain.transform, "Resume", "RESUME", new Vector2(0.5f, 1f), new Vector2(0, -130), pb);
            pm.controlsButton = UIBuilder.Button(pmain.transform, "Controls", "CONTROLS", new Vector2(0.5f, 1f), new Vector2(0, -204), pb);
            pm.settingsButton = UIBuilder.Button(pmain.transform, "Settings", "SETTINGS / VOLUME", new Vector2(0.5f, 1f), new Vector2(0, -278), pb);
            pm.restartButton = UIBuilder.Button(pmain.transform, "Restart", "NEW RUN", new Vector2(0.5f, 1f), new Vector2(0, -352), pb);
            pm.retrySeedButton = UIBuilder.Button(pmain.transform, "RetrySeed", "RESTART SAME SEED", new Vector2(0.5f, 1f), new Vector2(0, -426), pb);
            pm.menuButton = UIBuilder.Button(pmain.transform, "Menu", "MAIN MENU", new Vector2(0.5f, 1f), new Vector2(0, -500), pb);
            pm.quitButton = UIBuilder.Button(pmain.transform, "Quit", "QUIT GAME", new Vector2(0.5f, 1f), new Vector2(0, -574), pb);
            pm.infoText = UIBuilder.Label(pmain.transform, "Info", "", 22, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(720, 90));
            pm.mainPanel = pmain.gameObject;
            pm.controlsPanel = UIBuilder.ControlsPanel(proot.transform, out pm.controlsBody, out pm.controlsBack);
            pm.settingsPanel = UIBuilder.Settings(proot.transform);
            dir.pauseMenu = pm;

            // results
            var resGo = UIBuilder.Node(t, "Results");
            UIBuilder.Stretch(UIBuilder.Rect(resGo));
            var rs = resGo.AddComponent<ResultsScreen>();
            var rroot = UIBuilder.Dimmer(resGo.transform, "Root");
            rs.root = rroot.gameObject;
            var rp = UIBuilder.Panel(rroot.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 860));
            rs.title = UIBuilder.Label(rp.transform, "Title", "YOU DIED", 72, Color.red, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1000, 100), true, FontStyle.Bold);
            rs.body = UIBuilder.Label(rp.transform, "Body", "", 32, Color.white, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(820, 380));
            rs.news = UIBuilder.Label(rp.transform, "News", "", 30, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(1000, 50), true, FontStyle.Bold);
            rs.records = UIBuilder.Label(rp.transform, "Records", "", 22, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 185), new Vector2(1040, 70));
            rs.newRunButton = UIBuilder.Button(rp.transform, "NewRun", "NEW RUN  (R)", new Vector2(0.5f, 0f), new Vector2(-340, 70), new Vector2(320, 76), out rs.newRunLabel);
            rs.retrySeedButton = UIBuilder.Button(rp.transform, "RetrySeed", "SAME SEED", new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(320, 76));
            rs.menuButton = UIBuilder.Button(rp.transform, "Menu", "MAIN MENU", new Vector2(0.5f, 0f), new Vector2(340, 70), new Vector2(320, 76));
            dir.results = rs;

            EditorSceneManager.SaveScene(scene, G + "Scenes/Game.unity");
        }

        // ------------------------------------------------------------------ build

        [MenuItem("Gloomdelve/Build Windows (Builds/Windows)", priority = 20)]
        public static void BuildWindows()
        {
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds/Windows");
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { G + "Scenes/MainMenu.unity", G + "Scenes/Game.unity" },
                locationPathName = Path.Combine(outDir, "Gloomdelve.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Build] Result: {report.summary.result}, size {report.summary.totalSize / (1024 * 1024)} MB, errors {report.summary.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Gloomdelve/Open Main Menu Scene", priority = 40)]
        public static void OpenMenu() => EditorSceneManager.OpenScene(G + "Scenes/MainMenu.unity");

        [MenuItem("Gloomdelve/Open Game Scene", priority = 41)]
        public static void OpenGame() => EditorSceneManager.OpenScene(G + "Scenes/Game.unity");
    }
}
