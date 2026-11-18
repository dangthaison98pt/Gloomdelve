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
        static Material spriteMat;   // lit by 2D lights: terrain, characters, items
        static Material unlitMat;    // ignores lighting: sky, glows, sparks, world-space text
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
                ApplyAppIcon();
                EditorUtility.DisplayProgressBar("Gloomdelve", "Audio", 0.35f);
                AudioGenerator.GenerateAll();
                EditorUtility.DisplayProgressBar("Gloomdelve", "Assets", 0.5f);
                spriteMat = SpriteMaterial("SpriteLit", "Universal Render Pipeline/2D/Sprite-Lit-Default");
                unlitMat = SpriteMaterial("SpriteUnlit", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
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
                TmpFonts.Build();
                UIBuilder.font = TmpFonts.font; UIBuilder.matPlain = TmpFonts.plain; UIBuilder.matShadow = TmpFonts.shadow; UIBuilder.matTitle = TmpFonts.title; UIBuilder.matMenu = TmpFonts.menu;
                UIBuilder.panelSprite = ArtGenerator.S("UI/panel");
                UIBuilder.buttonSprite = ArtGenerator.S("UI/button");
                UIBuilder.slotSprite = ArtGenerator.S("UI/slot");
                UIBuilder.softPanelSprite = ArtGenerator.S("UI/panel_soft");
                UIBuilder.softRowSprite = ArtGenerator.S("UI/row_soft");
                UIBuilder.softSlotSprite = ArtGenerator.S("UI/slot_soft");
                // button pictures for the input tokens in UI texts ("[jump]", "[gear]"...)
                var glyphs = LoadOrCreate<InputGlyphSet>(G + "Data/InputGlyphs.asset");
                Sprite GS(string n) => ArtGenerator.S("UI/Glyphs/" + n);
                Sprite key = GS("key"), pill = GS("pill"), darkBtn = GS("pad_dark");
                Color ink = new Color(0.17f, 0.14f, 0.19f), light = new Color(0.97f, 0.96f, 0.93f);
                // PlayStation symbols in their usual colours, on a dark button
                Color psCross = new Color(0.55f, 0.75f, 1f), psCircle = new Color(1f, 0.45f, 0.45f), psSquare = new Color(1f, 0.55f, 0.95f), psTriangle = new Color(0.35f, 0.92f, 0.7f);
                // sw = the label printed on a Switch pad for the same physical button (B is at the bottom, A on the right)
                InputGlyphSet.Entry GE(string token, string kb, Sprite pad, string padLabel, bool darkInk = false, string ps = null, Color? psInk = null, Sprite psShape = null, string sw = null) => new InputGlyphSet.Entry
                {
                    token = token, keyboard = key, keyboardLabel = kb, keyboardInk = ink,
                    gamepad = pad, gamepadLabel = padLabel, gamepadInk = darkInk ? ink : light,
                    playstation = ps != null ? (psShape != null ? psShape : darkBtn) : null, playstationLabel = ps, playstationInk = psInk ?? light,
                    nintendo = sw != null ? (psShape != null ? psShape : darkBtn) : null, nintendoLabel = sw, nintendoInk = light,
                };
                glyphs.entries = new[]
                {
                    GE("jump", "Z", GS("pad_green"), "A", false, "\u00D7", psCross, null, "B"), GE("confirm", "Enter", GS("pad_green"), "A", false, "\u00D7", psCross, null, "B"),
                    GE("whip", "X", GS("pad_blue"), "X", false, "\u25A1", psSquare, null, "Y"), GE("use", "C", GS("pad_red"), "B", false, "\u25CB", psCircle, null, "A"),
                    GE("enter", "A", GS("pad_yellow"), "Y", true, "\u25B2", psTriangle, null, "X"),
                    GE("gear", "D", pill, "R1", false, "R1", null, pill, "R"), GE("walk", "Shift", pill, "LT", false, "L2", null, pill, "ZL"),
                    GE("pause", "Esc", pill, "Start", false, "Options", null, pill, "+"), GE("inventory", "Tab", pill, "Select", false, "Share", null, pill, "\u2212"),
                    GE("up", "\u25B2", darkBtn, "\u25B2"), GE("down", "\u25BC", darkBtn, "\u25BC"),
                    GE("left", "\u25C4", darkBtn, "\u25C4"), GE("right", "\u25BA", darkBtn, "\u25BA"),
                    GE("move", "\u25C4 \u25B2 \u25BC \u25BA", darkBtn, "L"),
                    // not a button: the "beaten without damage" badge, so any UI text can show it with "[flawless]"
                    new InputGlyphSet.Entry { token = "flawless", keyboard = GS("flawless"), keyboardLabel = "" },
                };
                EditorUtility.SetDirty(glyphs);
                UIBuilder.glyphSet = glyphs;
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

        /// <summary>The icon of the window, the taskbar and the .exe (drawn by ArtGenerator.AppIconArt). Called once the art exists.</summary>
        static void ApplyAppIcon()
        {
            var appIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(G + "Art/UI/app_icon.png");
            if (appIcon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { appIcon }, IconKind.Any);
            else Debug.LogWarning("[Build] No app icon found (Assets/Game/Art/UI/app_icon.png).");
        }

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
            // 2D renderer: Light2D lighting + post-processing (bloom, vignette, colour grading).
            const string urpPackage = "Packages/com.unity.render-pipelines.universal";
            string rdPath = G + "Settings/URP_Renderer2D.asset", urpPath = G + "Settings/URP_Asset.asset";
            var rd = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rdPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(rd, rdPath);
                ResourceReloader.ReloadAllNullIn(rd, urpPackage);
            }
            var rdSo = new SerializedObject(rd);
            var ppProp = rdSo.FindProperty("m_PostProcessData");
            if (ppProp != null && ppProp.objectReferenceValue == null)
            {
                ppProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(urpPackage + "/Runtime/Data/PostProcessData.asset");
                rdSo.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(rd);
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(urp, urpPath);
            }
            var urpSo = new SerializedObject(urp);
            var list = urpSo.FindProperty("m_RendererDataList");
            if (list != null)
            {
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = rd;
                var def = urpSo.FindProperty("m_DefaultRendererIndex");
                if (def != null) def.intValue = 0;
                urpSo.ApplyModifiedPropertiesWithoutUndo();
            }
            urp.supportsHDR = true;
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

        static Material SpriteMaterial(string name, string shaderName)
        {
            string path = G + "Settings/" + name + ".mat";
            var sh = Shader.Find(shaderName);
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { if (m.shader != sh) { m.shader = sh; EditorUtility.SetDirty(m); } return m; }
            m = new Material(sh) { name = name };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Post-processing look of the game scene. Values in an existing profile are kept (tune them in the asset).</summary>
        static VolumeProfile PostProfile()
        {
            string path = G + "Settings/PostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var bloom = Add<Bloom>();
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.6f);
            var vig = Add<Vignette>();
            vig.intensity.Override(0.3f); vig.smoothness.Override(0.45f);
            var col = Add<ColorAdjustments>();
            col.contrast.Override(8f); col.saturation.Override(6f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
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
                A("Swap", "Button", "Button"), A("Interact", "Button", "Button"), A("Walk", "Button", "Button"), A("Pause", "Button", "Button"), A("Inventory", "Button", "Button"),
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
                B("Inventory", "<Keyboard>/tab"), B("Inventory", "<Keyboard>/i"), B("Inventory", "<Gamepad>/select"),
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
            Anim("frostbat_hang", F(e + "frostbat_2"), 1); Anim("frostbat_fly", F(e + "frostbat_0", e + "frostbat_1"), 10f);
            Anim("penguin_idle", F(e + "penguin_0"), 1); Anim("penguin_windup", F(e + "penguin_1"), 1); Anim("penguin_slide", F(e + "penguin_2"), 1);
            Anim("yeti_idle", F(e + "yeti_0"), 1); Anim("yeti_throw", F(e + "yeti_1"), 1);
            Anim("eggsac_pulse", F(e + "eggsac_0", e + "eggsac_1"), 3f);
            // Gearworks
            Anim("sentry_walk", F(e + "sentry_0", e + "sentry_1"), 4f); Anim("sentry_jab", F(e + "sentry_2"), 1);
            Anim("drone_hover", F(e + "drone_0", e + "drone_1"), 14f); Anim("drone_aim", F(e + "drone_2", e + "drone_1"), 14f);
            Anim("beetle_walk", F(e + "beetle_0", e + "beetle_1"), 9f); Anim("beetle_primed", F(e + "beetle_2", e + "beetle_1"), 14f);
            // Sporegloom
            Anim("puffshroom_walk", F(e + "puffshroom_0", e + "puffshroom_1"), 4f); Anim("puffshroom_swell", F(e + "puffshroom_2"), 1); Anim("puffshroom_deflated", F(e + "puffshroom_3"), 1);
            Anim("moth_hang", F(e + "moth_2"), 1); Anim("moth_fly", F(e + "moth_0", e + "moth_1"), 14f);
            Anim("lurecap_dormant", F(e + "lurecap_0"), 1); Anim("lurecap_idle", F(e + "lurecap_1"), 1); Anim("lurecap_jump", F(e + "lurecap_2"), 1);
            const string bo = "Sprites/Bosses/";
            Anim("venom_fly", F(bo + "venom_0", bo + "venom_1"), 10f);
            Anim("shock_run", F(bo + "shock_0", bo + "shock_1"), 12f);
            Anim("broodmother_idle", F(bo + "broodmother_0"), 1); Anim("broodmother_jump", F(bo + "broodmother_1"), 1); Anim("broodmother_fall", F(bo + "broodmother_2"), 1);
            Anim("forgebrute_walk", F(bo + "forgebrute_0", bo + "forgebrute_1"), 4f); Anim("forgebrute_charge", F(bo + "forgebrute_2"), 1); Anim("forgebrute_dazed", F(bo + "forgebrute_3"), 1);
            Anim("yetiking_idle", F(bo + "yetiking_0"), 1); Anim("yetiking_throw", F(bo + "yetiking_1"), 1);
            Anim("stormhawk_fly", F(bo + "stormhawk_0", bo + "stormhawk_1"), 6f); Anim("stormhawk_dive", F(bo + "stormhawk_2"), 1);
            Anim("warden_float", F(bo + "warden_0", bo + "warden_1"), 3f);
            Anim("foreman_walk", F(bo + "foreman_0", bo + "foreman_1"), 3f); Anim("foreman_charge", F(bo + "foreman_2"), 1); Anim("foreman_dazed", F(bo + "foreman_1"), 1);
            Anim("sporeheart_beat", F(bo + "sporeheart_0", bo + "sporeheart_1"), 2.4f); Anim("sporeheart_swell", F(bo + "sporeheart_2"), 1);
            Anim("hawk_fly", F(e + "hawk_0", e + "hawk_1"), 7f);
            Anim("hawk_dive", F(e + "hawk_2"), 1);
            Anim("keeper_idle", ArtGenerator.Frames("Sprites/Shopkeeper/idle", 2), 1.5f);
            Anim("keeper_angry", ArtGenerator.Frames("Sprites/Shopkeeper/angry", 2), 6f);
            Anim("bomb_idle", F("Sprites/Items/bomb_0"), 1);
            Anim("bomb_lit", F("Sprites/Items/bomb_1", "Sprites/Items/bomb_2"), 8f);
            Anim("flame", ArtGenerator.Frames("Sprites/Traps/flame", 3), 14f);
            Anim("explosion", ArtGenerator.Frames("Center/explosion", 6), 18f, false);
            // boss counter-attack effects (played once over the boss's head)
            foreach (var fxName in new[] { "bite", "buck", "frost", "gust" })
                Anim("cfx_" + fxName, ArtGenerator.Frames("Center/cfx_" + fxName, 4), 13f, false);
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
            // "Village" is the Frost art used in the open: the snow of Brindlemoor under the night sky
            foreach (var setName in new[] { "Mossroot", "Emberdeep", "Vault", "Sunfall", "Frost", "Sporegloom", "Gearworks", "Village" })
            {
                string area = setName == "Village" ? "Frost" : setName;
                string d = "Tiles/" + area + "/";
                var ts = LoadOrCreate<TileSet>($"{G}Data/TileSet_{setName}.asset");
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
                ts.shade = new TileBase[16];
                for (int m = 0; m < 16; m++) ts.shade[m] = MakeTile("Common", $"Tiles/Common/shade_{m:00}");
                ts.decorFloor = new TileBase[4];
                for (int i = 0; i < 4; i++) ts.decorFloor[i] = MakeTile(area, $"{d}decor_floor_{i}");
                ts.decorCeiling = new TileBase[3];
                for (int i = 0; i < 3; i++) ts.decorCeiling[i] = MakeTile(area, $"{d}decor_ceiling_{i}");
                ts.outdoor = setName == "Sunfall" || setName == "Village";
                EditorUtility.SetDirty(ts);
                result[setName] = ts;
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
                    new ShopOffer { id = "ironwhip", displayName = "Tempered Blade", price = 750, isUpgrade = true, description = "blade deals 2 damage and reaches further" },
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
            // Wearable equipment: every upgrade has a slot, and the frozen caves get their own two pieces.
            // village-only wares: the Healer's draught and the Lamplighter's oil (never on the mountain shelves: not equipment)
            if (shop.Find("vigor") == null)
                shop.offers.Add(new ShopOffer { id = "vigor", displayName = "Heart Draught", price = 400, resource = "vigor", amount = 1, description = "+1 heart until you sleep" });
            if (shop.Find("lampoil") == null)
                shop.offers.Add(new ShopOffer { id = "lampoil", displayName = "Lamp Oil", price = 200, resource = "lampoil", amount = 120, description = "the Reaper comes 2 minutes later" });
            if (shop.Find("iceboots") == null)
                shop.offers.Add(new ShopOffer { id = "iceboots", displayName = "Ice Cleats", price = 600, isUpgrade = true, slot = "feet",
                    description = "spiked soles: no sliding on ice", modifiers = new StatModifiers { iceGrip = 1f } });
            if (shop.Find("warmcoat") == null)
                shop.offers.Add(new ShopOffer { id = "warmcoat", displayName = "Fur Coat", price = 650, isUpgrade = true, slot = "body",
                    description = "snowballs only slow you instead of freezing you", modifiers = new StatModifiers { coldResist = 1f } });
            if (shop.Find("sporemask") == null)
                shop.offers.Add(new ShopOffer { id = "sporemask", displayName = "Spore Mask", price = 550, isUpgrade = true, slot = "charm",
                    description = "spore clouds cannot make you dizzy", modifiers = new StatModifiers { sporeResist = 1f } });
            if (shop.Find("heavyboots") == null)
                shop.offers.Add(new ShopOffer { id = "heavyboots", displayName = "Heavy Boots", price = 600, isUpgrade = true, slot = "feet",
                    description = "iron soles: conveyor belts cannot carry you", modifiers = new StatModifiers { beltGrip = 1f } });
            if (shop.Find("magnetcharm") == null)
                shop.offers.Add(new ShopOffer { id = "magnetcharm", displayName = "Magnet Charm", price = 650, isUpgrade = true, slot = "charm",
                    description = "pulls shields aside: your blade cuts through a sentry's guard", modifiers = new StatModifiers { shieldPierce = 1f } });
            if (shop.Find("lantern") == null)
                shop.offers.Add(new ShopOffer { id = "lantern", displayName = "Lantern", price = 600, isUpgrade = true, slot = "charm",
                    description = "your light reaches much further in the dark", modifiers = new StatModifiers { lightBonus = 0.6f } });
            foreach (var o in shop.offers)
            {
                if (!o.isUpgrade || !string.IsNullOrEmpty(o.slot)) continue;
                o.slot = o.id == "springboots" ? "feet" : o.id == "ironwhip" ? "hand" : "charm";
            }
            var icons = new Dictionary<string, string>
            {
                ["iceboots"] = "Sprites/Items/iceboots", ["warmcoat"] = "Sprites/Items/warmcoat", ["sporemask"] = "Sprites/Items/sporemask", ["heavyboots"] = "Sprites/Items/heavyboots", ["magnetcharm"] = "Sprites/Items/magnetcharm", ["lantern"] = "Sprites/Items/lantern",
                ["bombs"] = "Sprites/Items/bombbag", ["ropes"] = "Sprites/Items/ropepile", ["heart"] = "Sprites/Items/heart",
                ["springboots"] = "Sprites/Items/springboots", ["ironwhip"] = "Sprites/Items/ironwhip", ["compass"] = "Sprites/Items/compass",
                ["blastpowder"] = "Sprites/Items/blastpowder",
            };
            icons["vigor"] = "Sprites/NPC/ware_vigor"; icons["lampoil"] = "Sprites/NPC/ware_lampoil";
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
            if (moss.ambientIntensity < 0f)
            {
                moss.ambientColor = new Color(0.66f, 0.74f, 0.9f); moss.ambientIntensity = 0.58f;
                moss.torchColor = new Color(1f, 0.74f, 0.42f); moss.torchChance = 0.07f;
                moss.ambientParticles = AmbientParticles.Dust; moss.ambientParticleCount = 45;
                moss.backdropTint = new Color(0.42f, 0.36f, 0.34f);
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
            if (ember.ambientIntensity < 0f)
            {
                ember.ambientColor = new Color(0.95f, 0.62f, 0.52f); ember.ambientIntensity = 0.55f;
                ember.torchColor = new Color(1f, 0.6f, 0.3f); ember.torchChance = 0.05f;
                ember.ambientParticles = AmbientParticles.Embers; ember.ambientParticleCount = 35;
                ember.backdropTint = new Color(0.5f, 0.24f, 0.2f);
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
            if (sunfall.ambientIntensity < 0f)
            {
                sunfall.ambientColor = new Color(1f, 0.95f, 0.88f); sunfall.ambientIntensity = 0.92f;
                sunfall.playerLightIntensity = 0.25f; sunfall.torchChance = 0f;
                sunfall.ambientParticles = AmbientParticles.Pollen; sunfall.ambientParticleCount = 30;
            }
            // The cliffs are a climb: enter at the bottom, leave at the top, the path goes up as well as sideways.
            if (sunfall.pathUp <= 0f)
            {
                sunfall.pathUp = 2f; sunfall.pathSide = 3f; sunfall.pathDown = 0.3f; sunfall.pathStartBottom = true;
                sunfall.roomsX = 4; sunfall.roomsY = 4;
            }
            sunfall.tiles = ts["Sunfall"];
            EditorUtility.SetDirty(sunfall);

            var frost = LoadOrCreate<AreaConfig>(G + "Data/Area5_Frost.asset", out bool newFrost);
            if (newFrost)
            {
                frost.displayName = "Frostdeep Hollows"; frost.areaNumber = 5; frost.musicId = "area4";
                frost.cameraBackground = new Color(0.05f, 0.08f, 0.14f);
                frost.groundGrip = 0.3f;
                frost.groundEnemies = new[] { new WeightedId("snake", 1f), new WeightedId("brute", 1f) };
                frost.ceilingEnemies = new[] { new WeightedId("bat", 2f), new WeightedId("spider", 1f) };
                frost.spikes = true; frost.arrowTraps = true; frost.stalactites = true; frost.crushers = false; frost.flameVents = false;
                frost.spikeDensity = 0.03f; frost.arrowTrapDensity = 0.015f; frost.ceilingHazardChance = 0.75f;
                frost.gemVeinChance = 0.014f;
                frost.requiredGear = ""; frost.gatesByLevel = new[] { 0, 0, 0 };
            }
            if (frost.ambientIntensity < 0f)
            {
                frost.ambientColor = new Color(0.72f, 0.86f, 1f); frost.ambientIntensity = 0.7f;
                frost.torchColor = new Color(1f, 0.78f, 0.5f); frost.torchChance = 0.05f;
                frost.ambientParticles = AmbientParticles.Snow; frost.ambientParticleCount = 55;
                frost.backdropTint = new Color(0.4f, 0.52f, 0.72f);
            }
            // its own creatures (also upgrades a Frost area saved before they existed)
            if (!System.Array.Exists(frost.groundEnemies, w => w.id == "penguin"))
            {
                frost.groundEnemies = new[] { new WeightedId("penguin", 2f), new WeightedId("yeti", 1f), new WeightedId("snake", 1f) };
                frost.ceilingEnemies = new[] { new WeightedId("frostbat", 2f), new WeightedId("spider", 1f) };
            }
            // shops in the cold sell what the cold calls for, and the first frozen level always has one
            if (frost.shopFeatured == null || frost.shopFeatured.Length == 0)
            {
                frost.shopFeatured = new[] { "iceboots", "warmcoat" };
                frost.shopChanceByLevel = new[] { 1f, 1f, 0.5f };
            }
            frost.tiles = ts["Frost"];
            EditorUtility.SetDirty(frost);

            // Brindlemoor itself: the village in the snow, at night. No enemies, no traps - the player walks through it
            // before every run and meets the people they have brought home.
            var villageArea = LoadOrCreate<AreaConfig>(G + "Data/Area0_Village.asset", out bool newVillage);
            if (newVillage)
            {
                villageArea.displayName = "Brindlemoor"; villageArea.areaNumber = 0; villageArea.musicId = "area4";
                villageArea.outdoor = true; villageArea.skyRows = 0; villageArea.treeChance = 0f;
                villageArea.roomsX = 2; villageArea.roomsY = 2;
                villageArea.cameraBackground = new Color(0.04f, 0.05f, 0.12f);
                villageArea.groundEnemies = new WeightedId[0]; villageArea.ceilingEnemies = new WeightedId[0];
                villageArea.spikes = false; villageArea.arrowTraps = false; villageArea.stalactites = false;
                villageArea.ambientColor = new Color(0.62f, 0.7f, 1f); villageArea.ambientIntensity = 0.62f;
                villageArea.playerLightIntensity = 0.7f; villageArea.torchChance = 0f;
                villageArea.ambientParticles = AmbientParticles.Snow; villageArea.ambientParticleCount = 70;
                villageArea.skyTint = new Color(0.3f, 0.36f, 0.62f);
                villageArea.layoutVersion = 2;
            }
            // a quiet night: the lamps of the houses light the street, the player carries only a little light
            villageArea.playerLightIntensity = 0.25f; villageArea.ambientIntensity = 0.55f;
            villageArea.ambientParticles = AmbientParticles.Snow; villageArea.ambientParticleCount = 170;      // it is snowing in Brindlemoor
            villageArea.tiles = ts["Village"];
            EditorUtility.SetDirty(villageArea);

            // Sporegloom: a fungus forest in the dark. Almost no ambient light: the player's own light, the glowing
            // plants and the bounce mushrooms are all there is to see by. No arrow traps (unfair in the dark).
            var spore = LoadOrCreate<AreaConfig>(G + "Data/Area6_Sporegloom.asset", out bool newSpore);
            if (newSpore)
            {
                spore.displayName = "Sporegloom"; spore.areaNumber = 6; spore.musicId = "area1";
                spore.cameraBackground = new Color(0.03f, 0.025f, 0.055f);
                spore.groundEnemies = new[] { new WeightedId("puffshroom", 2f), new WeightedId("lurecap", 1.5f), new WeightedId("snake", 1f) };
                spore.ceilingEnemies = new[] { new WeightedId("moth", 2f), new WeightedId("spider", 1f), new WeightedId("bat", 1f) };
                spore.spikes = true; spore.arrowTraps = false; spore.stalactites = true; spore.crushers = false; spore.flameVents = false;
                spore.spikeDensity = 0.03f; spore.arrowTrapDensity = 0f; spore.ceilingHazardChance = 0.4f;
                spore.gemVeinChance = 0.012f;
                spore.requiredGear = ""; spore.gatesByLevel = new[] { 0, 0, 0 };
                spore.bounceCapChance = 0.06f; spore.glowPlantChance = 0.12f;
                spore.shopFeatured = new[] { "sporemask" };
                spore.shopChanceByLevel = new[] { 1f, 0.6f, 0.5f };
            }
            if (spore.ambientIntensity < 0f)
            {
                spore.ambientColor = new Color(0.55f, 0.62f, 0.95f); spore.ambientIntensity = 0.17f;
                spore.playerLightIntensity = 0.9f; spore.playerLightRadius = 5.5f;
                spore.torchChance = 0f;
                spore.ambientParticles = AmbientParticles.Pollen; spore.ambientParticleCount = 60;
                spore.backdropTint = new Color(0.3f, 0.24f, 0.44f);
            }
            // both helpers of the dark are on every shelf here (they make the area easier; neither is needed to get through)
            if (spore.shopFeatured == null || !System.Array.Exists(spore.shopFeatured, f => f == "lantern"))
                spore.shopFeatured = new[] { "lantern", "sporemask" };
            spore.tiles = ts["Sporegloom"];
            EditorUtility.SetDirty(spore);

            // Gearworks: an old machine hall. Conveyor belts on the long floors, crushers overhead, three machines.
            var gear = LoadOrCreate<AreaConfig>(G + "Data/Area7_Gearworks.asset", out bool newGear);
            if (newGear)
            {
                gear.displayName = "Gearworks"; gear.areaNumber = 7; gear.musicId = "area2";
                gear.cameraBackground = new Color(0.07f, 0.06f, 0.055f);
                gear.groundEnemies = new[] { new WeightedId("sentry", 2f), new WeightedId("beetle", 2f) };
                gear.ceilingEnemies = new[] { new WeightedId("drone", 2f), new WeightedId("bat", 1f) };
                gear.items = new[] { new WeightedId("rock", 3f), new WeightedId("pot", 1f), new WeightedId("crate", 2f) };
                gear.spikes = true; gear.arrowTraps = true; gear.stalactites = false; gear.crushers = true; gear.flameVents = false;
                gear.spikeDensity = 0.025f; gear.arrowTrapDensity = 0.012f; gear.ceilingHazardChance = 0.5f;
                gear.goldVeinChance = 0.04f;
                gear.requiredGear = ""; gear.gatesByLevel = new[] { 0, 0, 0 };
                gear.conveyorChance = 0.6f;
                gear.shopFeatured = new[] { "heavyboots", "magnetcharm" };
                gear.shopChanceByLevel = new[] { 1f, 0.6f, 0.5f };
            }
            if (gear.ambientIntensity < 0f)
            {
                gear.ambientColor = new Color(1f, 0.86f, 0.68f); gear.ambientIntensity = 0.6f;
                gear.torchColor = new Color(1f, 0.7f, 0.35f); gear.torchChance = 0.06f;
                gear.ambientParticles = AmbientParticles.Embers; gear.ambientParticleCount = 18;
                gear.backdropTint = new Color(0.42f, 0.34f, 0.26f);
            }
            if (gear.sawChance <= 0f) gear.sawChance = 0.45f;        // its own trap: saw blades on rails
            gear.tiles = ts["Gearworks"];
            EditorUtility.SetDirty(gear);

            var vault = LoadOrCreate<AreaConfig>(G + "Data/Area3_Vault.asset", out bool newVault);
            if (newVault)
            {
                vault.displayName = "The Sunken Vault"; vault.areaNumber = 3; vault.musicId = "final";
                vault.cameraBackground = new Color(0.05f, 0.05f, 0.08f);
                vault.groundEnemies = new[] { new WeightedId("snake", 1f), new WeightedId("toad", 1f) };
                vault.ceilingEnemies = new[] { new WeightedId("bat", 1f) };
            }
            if (vault.ambientIntensity < 0f)
            {
                vault.ambientColor = new Color(0.6f, 0.68f, 1f); vault.ambientIntensity = 0.52f;
                vault.torchColor = new Color(0.6f, 0.85f, 1f); vault.torchChance = 0.08f;
                vault.ambientParticles = AmbientParticles.Spirits; vault.ambientParticleCount = 30;
                vault.backdropTint = new Color(0.3f, 0.34f, 0.55f);
            }
            vault.tiles = ts["Vault"];
            EditorUtility.SetDirty(vault);

            var cfg = LoadOrCreate<GameConfig>(G + "Data/GameConfig.asset");
            // Level shapes (layout version 2): every level is about twice as long as the first 4x4 maps, and each
            // area leads its own way - down, up, or across.
            {
                void Shape(string file, int rx, int ry, bool acrossMap, float up, float side, float down, bool fromBottom)
                {
                    var a = AssetDatabase.LoadAssetAtPath<AreaConfig>(G + "Data/" + file + ".asset");
                    if (a == null || a.layoutVersion >= 2) return;
                    a.roomsX = rx; a.roomsY = ry;
                    a.pathAcross = acrossMap; a.pathUp = up; a.pathSide = side; a.pathDown = down; a.pathStartBottom = fromBottom;
                    a.layoutVersion = 2;
                    EditorUtility.SetDirty(a);
                }
                Shape("Area1_Mossroot", 5, 7, false, 0f, 3f, 1f, false);        // a descent
                Shape("Area2_Emberdeep", 5, 7, false, 0f, 3f, 1f, false);       // a descent
                Shape("Area3_Vault", 5, 7, false, 0f, 3f, 1f, false);
                Shape("Area4_Sunfall", 5, 7, false, 2f, 3f, 0.3f, true);        // a climb
                Shape("Area5_Frost", 10, 4, true, 0.7f, 3f, 0.9f, false);       // across: long icy runs
                Shape("Area6_Sporegloom", 5, 7, false, 0f, 3f, 1f, false);      // a descent
                Shape("Area7_Gearworks", 10, 4, true, 1f, 3f, 1f, false);       // across, changing floors often
                // twice the level, twice the time before the Reaper comes
                if (cfg.pressureSpawnTime <= 300f) { cfg.pressureWarnTime = 540f; cfg.pressureSpawnTime = 600f; EditorUtility.SetDirty(cfg); }
            }

            // room-template symbols for the climbing traps
            void CustomMarker(string sym, string label, string spawn, float chance, MarkerPlacement place, TileType tile, Color col)
            {
                if (cfg.customMarkers.Exists(m => m.symbol == sym)) return;
                cfg.customMarkers.Add(new MarkerDef { symbol = sym, label = label, spawnId = spawn, chance = chance, placement = place, tile = tile, color = col });
            }
            CustomMarker("P", "Caged villager", "captive", 1f, MarkerPlacement.OnFloor, TileType.Empty, new Color(1f, 0.85f, 0.5f));
            CustomMarker("r", "Rock dropper", "rockdropper", 0.8f, MarkerPlacement.UnderCeiling, TileType.Empty, new Color(0.7f, 0.65f, 0.6f));
            CustomMarker("<", "Conveyor running left", "conveyor", 1f, MarkerPlacement.OnFloor, TileType.Empty, new Color(0.6f, 0.6f, 0.7f));
            CustomMarker(">", "Conveyor running right", "conveyor_r", 1f, MarkerPlacement.OnFloor, TileType.Empty, new Color(0.7f, 0.7f, 0.6f));
            CustomMarker("m", "Bounce mushroom", "bouncecap", 1f, MarkerPlacement.OnFloor, TileType.Empty, new Color(0.4f, 1f, 0.9f));
            CustomMarker("l", "Glowing mushroom", "glowshroom", 1f, MarkerPlacement.OnFloor, TileType.Empty, new Color(0.3f, 0.8f, 0.7f));
            CustomMarker("c", "Crumbling platform", "crumble", 1f, MarkerPlacement.Anywhere, TileType.Platform, new Color(0.7f, 0.5f, 0.3f));
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
            // Input names inside gear texts become tokens, drawn as button pictures of the device in use.
            foreach (var gdef in new[] { gBomb, gRope, gGlide })
            {
                gdef.description = InputTokens(gdef.description);
                gdef.gateBanner = InputTokens(gdef.gateBanner);
                gdef.gateSign = InputTokens(gdef.gateSign);
                EditorUtility.SetDirty(gdef);
            }
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
            // The frozen area joins an existing stage list after the mines (or before the final level), once.
            if (!System.Array.Exists(cfg.stages, st => st.area == frost))
            {
                var list = new List<StageDef>(cfg.stages);
                int at = list.FindLastIndex(st => st.area == ember) + 1;
                if (at == 0) { at = list.FindIndex(st => st.kind == LevelKind.Final); if (at < 0) at = list.Count; }
                for (int lv = 3; lv >= 1; lv--)
                    list.Insert(at, new StageDef { displayName = "", kind = LevelKind.Area, area = frost, levelNumber = lv });
                // renumber "area-level" names along the run
                int areaNo = 0; AreaConfig prev = null;
                foreach (var st in list)
                {
                    if (st.kind != LevelKind.Area) continue;
                    if (st.area != prev) { areaNo++; prev = st.area; }
                    st.displayName = $"{areaNo}-{st.levelNumber}";
                }
                cfg.stages = list.ToArray();
            }
            // Run structure: every area is three levels and a boss arena; after the last area a camp (chest + shop),
            // then the final vault with its guardian.
            if (!System.Array.Exists(cfg.stages, st => st.kind == LevelKind.Boss))
            {
                TextAsset Lv(string n) => AssetDatabase.LoadAssetAtPath<TextAsset>(G + "Data/Levels/" + n + ".txt");
                var runList = new List<StageDef>();
                var order = new (AreaConfig area, string arena)[] { (moss, "Boss_Mossroot"), (ember, "Boss_Emberdeep"), (frost, "Boss_Frost"), (sunfall, "Boss_Sunfall") };
                for (int a = 0; a < order.Length; a++)
                {
                    for (int lv = 1; lv <= 3; lv++)
                        runList.Add(new StageDef { displayName = $"{a + 1}-{lv}", kind = LevelKind.Area, area = order[a].area, levelNumber = lv });
                    runList.Add(new StageDef { displayName = $"{a + 1}-4", kind = LevelKind.Boss, area = order[a].area, levelNumber = 4, handcrafted = Lv(order[a].arena) });
                }
                runList.Add(new StageDef { displayName = "THE LAST CAMP", kind = LevelKind.Rest, area = vault, levelNumber = 1, handcrafted = Lv("Camp") });
                runList.Add(new StageDef { displayName = "THE SUNKEN VAULT", kind = LevelKind.Final, area = vault, levelNumber = 1, handcrafted = final });
                cfg.stages = runList.ToArray();
            }
            // Sporegloom joins an existing run right after the first area and its boss: three levels (it has no
            // boss of its own yet). Areas and bosses are renumbered along the run.
            if (!System.Array.Exists(cfg.stages, st => st.area == spore))
            {
                var list = new List<StageDef>(cfg.stages);
                int at = list.FindLastIndex(st => st.area == moss) + 1;
                if (at == 0) { at = list.FindIndex(st => st.kind == LevelKind.Rest || st.kind == LevelKind.Final); if (at < 0) at = list.Count; }
                for (int lv = 3; lv >= 1; lv--)
                    list.Insert(at, new StageDef { displayName = "", kind = LevelKind.Area, area = spore, levelNumber = lv });
                int areaNo = 0; AreaConfig prev = null;
                foreach (var st in list)
                {
                    if (st.kind != LevelKind.Area && st.kind != LevelKind.Boss) continue;
                    if (st.area != prev) { areaNo++; prev = st.area; }
                    st.displayName = $"{areaNo}-{(st.kind == LevelKind.Boss ? 4 : st.levelNumber)}";
                }
                cfg.stages = list.ToArray();
            }
            // ...and its boss arena follows its three levels (also added to a run saved before the Rot Heart existed)
            if (!System.Array.Exists(cfg.stages, st => st.area == spore && st.kind == LevelKind.Boss))
            {
                var list = new List<StageDef>(cfg.stages);
                int at = list.FindLastIndex(st => st.area == spore) + 1;
                if (at > 0)
                {
                    list.Insert(at, new StageDef { displayName = "", kind = LevelKind.Boss, area = spore, levelNumber = 4,
                        handcrafted = AssetDatabase.LoadAssetAtPath<TextAsset>(G + "Data/Levels/Boss_Sporegloom.txt") });
                    int areaNo = 0; AreaConfig prev = null;
                    foreach (var st in list)
                    {
                        if (st.kind != LevelKind.Area && st.kind != LevelKind.Boss) continue;
                        if (st.area != prev) { areaNo++; prev = st.area; }
                        st.displayName = $"{areaNo}-{(st.kind == LevelKind.Boss ? 4 : st.levelNumber)}";
                    }
                    cfg.stages = list.ToArray();
                }
            }
            // Gearworks joins the catalogue as its own block (three levels + boss arena) before the ending stages.
            // Which areas a run actually visits is drawn per run (RunPlan), so its place in this list does not matter.
            if (!System.Array.Exists(cfg.stages, st => st.area == gear))
            {
                var list = new List<StageDef>(cfg.stages);
                int at = list.FindIndex(st => st.kind == LevelKind.Rest || st.kind == LevelKind.Final);
                if (at < 0) at = list.Count;
                int blocks = 1; AreaConfig prevA = null;
                foreach (var st in list) if ((st.kind == LevelKind.Area || st.kind == LevelKind.Boss) && st.area != prevA) { blocks++; prevA = st.area; }
                var add = new List<StageDef>();
                for (int lv = 1; lv <= 3; lv++) add.Add(new StageDef { displayName = $"{blocks}-{lv}", kind = LevelKind.Area, area = gear, levelNumber = lv });
                add.Add(new StageDef { displayName = $"{blocks}-4", kind = LevelKind.Boss, area = gear, levelNumber = 4,
                    handcrafted = AssetDatabase.LoadAssetAtPath<TextAsset>(G + "Data/Levels/Boss_Gearworks.txt") });
                list.InsertRange(at, add);
                cfg.stages = list.ToArray();
            }
            cfg.tutorial = new StageDef { displayName = "TUTORIAL", kind = LevelKind.Tutorial, area = moss, levelNumber = 1, handcrafted = tut };
            cfg.village = new StageDef { displayName = "BRINDLEMOOR", kind = LevelKind.Rest, area = villageArea, levelNumber = 1,
                handcrafted = AssetDatabase.LoadAssetAtPath<TextAsset>(G + "Data/Levels/Village.txt") };
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static string InputTokens(string t)
        {
            if (string.IsNullOrEmpty(t)) return t;
            return t.Replace("Hold R1 / D", "Hold [gear]").Replace("hold R1 / D", "hold [gear]")
                    .Replace("HOLD JUMP", "HOLD [jump]").Replace("hold JUMP", "hold [jump]")
                    .Replace("DOWN + use", "[down] + [use]").Replace("UP + use", "[up] + [use]").Replace("Use:", "[use] :");
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
            ["Heartstone"] = (new Color(1f, 0.82f, 0.35f, 0.85f), false, true),        // the Sunstone (the prefab keeps its old name)
            ["GoldNugget"] = (new Color(1f, 0.85f, 0.3f, 0.85f), true, false),
            ["GoldChunk"] = (new Color(1f, 0.85f, 0.3f, 0.9f), true, false),
            ["Gem"] = (new Color(0.4f, 1f, 0.6f, 0.9f), true, false),
            ["BombBag"] = (new Color(1f, 1f, 1f, 0.75f), true, false),
            ["RopePile"] = (new Color(1f, 1f, 1f, 0.75f), true, false),
            ["Heart"] = (new Color(1f, 0.35f, 0.45f, 0.95f), true, false),
        };

        // Light carried by a prefab (GlowLight): colour, radius, intensity, flicker, slow pulse, lit from the start, offset.
        static readonly Dictionary<string, (Color color, float radius, float intensity, float flicker, bool pulse, bool on, Vector2 offset)> glows =
            new Dictionary<string, (Color, float, float, float, bool, bool, Vector2)>
        {
            ["Bomb"] = (new Color(1f, 0.6f, 0.25f), 3.2f, 1.3f, 0.45f, false, false, new Vector2(0f, 0.4f)),
            ["FlameVent"] = (new Color(1f, 0.5f, 0.15f), 5.5f, 1.9f, 0.3f, false, false, new Vector2(0f, 1f)),
            ["Gem"] = (new Color(0.4f, 1f, 0.6f), 2.2f, 0.75f, 0.2f, true, true, new Vector2(0f, 0.2f)),
            ["Idol"] = (new Color(1f, 0.82f, 0.3f), 3.2f, 0.95f, 0.15f, true, true, new Vector2(0f, 0.35f)),
            ["Heartstone"] = (new Color(1f, 0.8f, 0.4f), 5.6f, 1.6f, 0.2f, true, true, new Vector2(0f, 0.35f)),
            ["Heart"] = (new Color(1f, 0.35f, 0.4f), 2f, 0.6f, 0.2f, true, true, new Vector2(0f, 0.2f)),
            ["EmberBat"] = (new Color(1f, 0.5f, 0.2f), 2.6f, 0.8f, 0.3f, false, true, new Vector2(0f, 0.3f)),
            ["Wraith"] = (new Color(1f, 0.25f, 0.2f), 5f, 1.2f, 0.2f, true, true, new Vector2(0f, 0.5f)),
            ["Warden"] = (new Color(0.75f, 0.5f, 1f), 6f, 1.3f, 0.2f, true, true, new Vector2(0f, 0.9f)),
            ["GloomOrb"] = (new Color(0.75f, 0.5f, 1f), 2.4f, 1f, 0.2f, false, true, new Vector2(0f, 0.15f)),
            ["Shopkeeper"] = (new Color(1f, 0.8f, 0.5f), 7f, 0.9f, 0.08f, false, true, new Vector2(0f, 1.5f)),
            ["DoorExit"] = (new Color(0.6f, 0.9f, 1f), 3.6f, 0.8f, 0.15f, true, true, new Vector2(0f, 0.7f)),
            // Sporegloom: the plants are the lamps of the dark; a sleeping lurecap glows exactly like one
            ["Glowshroom"] = (new Color(0.35f, 1f, 0.82f), 4.2f, 0.95f, 0.2f, true, true, new Vector2(0f, 0.4f)),
            ["Lurecap"] = (new Color(0.35f, 1f, 0.82f), 4.2f, 0.95f, 0.2f, true, true, new Vector2(0f, 0.4f)),
            ["BounceCap"] = (new Color(0.4f, 1f, 0.9f), 5f, 1.1f, 0.25f, true, true, new Vector2(0f, 0.5f)),
            ["SporeHeart"] = (new Color(0.95f, 0.45f, 1f), 7f, 1.25f, 0.35f, true, true, new Vector2(0f, 0.8f)),
            ["FirePatch"] = (new Color(1f, 0.5f, 0.15f), 3.6f, 1.3f, 0.35f, false, true, new Vector2(0f, 0.4f)),
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
                h.material = unlitMat;
                h.glowScale = hl.bob ? 3.2f : 2.4f;
            }
            if (glows.TryGetValue(id, out var gl))
            {
                var glow = root.AddComponent<GlowLight>();
                glow.color = gl.color; glow.radius = gl.radius; glow.intensity = gl.intensity; glow.flicker = gl.flicker;
                glow.pulse = gl.pulse; glow.startOn = gl.on; glow.offset = gl.offset;
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
            Sprite BS2(string n) => ArtGenerator.S("Sprites/Bosses/" + n);
            string I(string n) => "Sprites/Items/" + n;

            // player
            {
                var root = NewEntityGO("Player", ArtGenerator.S("Sprites/Player/idle_0"), anims["player_idle"], 20, out _);
                var whip = new GameObject("Whip");
                whip.transform.SetParent(root.transform, false);
                var wsr = whip.AddComponent<SpriteRenderer>();
                wsr.sharedMaterial = spriteMat; wsr.sortingOrder = 21; wsr.enabled = false;
                var p = root.AddComponent<PlayerController>();
                p.displayName = GameConst.HeroName;
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
                (e, go, sr) => { e.contactDamage = 2; e.goldDrop = 100; e.animHang = anims["emberbat_hang"]; e.animFly = anims["emberbat_fly"]; e.speed = 4.6f; e.accel = 12f; e.wakeRange = 7f; e.bloodColor = new Color(1f, 0.5f, 0.2f); }));
            Reg("spider", Make<Spider>("Spider", "Enemies", "spider", new Vector2(0.6f, 0.5f), ArtGenerator.S("Sprites/Enemies/spider_2"), anims["spider_hang"], 14,
                (e, go, sr) => { e.animHang = anims["spider_hang"]; e.animIdle = anims["spider_idle"]; e.animJump = anims["spider_jump"]; e.bloodColor = new Color(0.3f, 0.6f, 0.3f); }));
            Reg("toad", Make<Toad>("Toad", "Enemies", "cinder toad", new Vector2(0.7f, 0.55f), ArtGenerator.S("Sprites/Enemies/toad_0"), anims["toad_idle"], 14,
                (e, go, sr) => { e.animIdle = anims["toad_idle"]; e.animJump = anims["toad_jump"]; e.bloodColor = new Color(0.9f, 0.5f, 0.2f); }));
            Reg("brute", Make<Brute>("Brute", "Enemies", "mole brute", new Vector2(0.85f, 0.9f), ArtGenerator.S("Sprites/Enemies/brute_0"), anims["brute_walk"], 14,
                (e, go, sr) => { e.maxHealth = 3; e.contactDamage = 2; e.knockbackScale = 1.6f; e.goldDrop = 150; e.animWalk = anims["brute_walk"]; e.animCharge = anims["brute_charge"]; e.animDazed = anims["brute_dazed"]; }));
            // frozen caves
            Reg("penguin", Make<Penguin>("Penguin", "Enemies", "penguin", new Vector2(0.65f, 0.6f), ArtGenerator.S("Sprites/Enemies/penguin_0"), anims["penguin_idle"], 14,
                (e, go, sr) => { e.contactDamage = 2; e.animIdle = anims["penguin_idle"]; e.animWindup = anims["penguin_windup"]; e.animSlide = anims["penguin_slide"]; e.bloodColor = new Color(0.8f, 0.9f, 1f); }));
            Reg("yeti", Make<Yeti>("Yeti", "Enemies", "yeti", new Vector2(0.8f, 1.0f), ArtGenerator.S("Sprites/Enemies/yeti_0"), anims["yeti_idle"], 14,
                (e, go, sr) => { e.maxHealth = 2; e.goldDrop = 100; e.animIdle = anims["yeti_idle"]; e.animThrow = anims["yeti_throw"]; e.bloodColor = new Color(0.85f, 0.92f, 1f); }));
            Reg("frostbat", Make<FrostBat>("FrostBat", "Enemies", "frost bat", new Vector2(0.6f, 0.5f), ArtGenerator.S("Sprites/Enemies/frostbat_2"), anims["frostbat_hang"], 14,
                (e, go, sr) => { e.animHang = anims["frostbat_hang"]; e.animFly = anims["frostbat_fly"]; e.speed = 3.9f; e.bloodColor = new Color(0.7f, 0.88f, 1f); }));
            Reg("snowball", Make<Snowball>("Snowball", "Enemies", "snowball", new Vector2(0.3f, 0.3f), ArtGenerator.S(I("snowball")), null, 16,
                (e, go, sr) => { e.flipWithFacing = false; }));
            Reg("icechunk", Make<IceChunk>("IceChunk", "Items", "ice chunk", new Vector2(0.45f, 0.4f), ArtGenerator.S(I("icechunk")), null, 12,
                (e, g, s) => { e.flipWithFacing = false; e.impactSfx = "thud"; }));
            // Gearworks: its machines, their bolt, the conveyor belt
            Reg("sentry", Make<Sentry>("Sentry", "Enemies", "sentry", new Vector2(0.75f, 0.95f), ArtGenerator.S("Sprites/Enemies/sentry_0"), anims["sentry_walk"], 14,
                (e, go, sr) => { e.maxHealth = 2; e.goldDrop = 100; e.animWalk = anims["sentry_walk"]; e.animJab = anims["sentry_jab"]; e.bloodColor = new Color(0.85f, 0.75f, 0.45f); }));
            Reg("drone", Make<Drone>("Drone", "Enemies", "drone", new Vector2(0.6f, 0.55f), ArtGenerator.S("Sprites/Enemies/drone_0"), anims["drone_hover"], 14,
                (e, go, sr) => { e.goldDrop = 100; e.animHover = anims["drone_hover"]; e.animAim = anims["drone_aim"]; e.bloodColor = new Color(0.7f, 0.75f, 0.85f); }));
            Reg("beetle", Make<ScrapBeetle>("ScrapBeetle", "Enemies", "scrap beetle", new Vector2(0.7f, 0.4f), ArtGenerator.S("Sprites/Enemies/beetle_0"), anims["beetle_walk"], 14,
                (e, go, sr) => { e.speed = 2f; e.animWalk = anims["beetle_walk"]; e.animPrimed = anims["beetle_primed"]; e.bloodColor = new Color(0.7f, 0.65f, 0.55f); }));
            Reg("bolt", Make<BossShot>("Bolt", "Enemies", "bolt", new Vector2(0.3f, 0.3f), ArtGenerator.S("Sprites/Bosses/bolt"), null, 32,
                (e, go, sr) => { e.flipWithFacing = false; e.lifetime = 5f; e.burstColor = new Color(1f, 0.6f, 0.3f); }));
            Reg("gearshot", Make<BossShot>("GearShot", "Enemies", "gear", new Vector2(0.45f, 0.45f), ArtGenerator.S("Sprites/Bosses/gear"), null, 32,
                (e, go, sr) => { e.flipWithFacing = false; e.burstColor = new Color(0.9f, 0.75f, 0.4f); }));
            foreach (var (cid, cname, cdir) in new[] { ("conveyor", "Conveyor", 0), ("conveyor_r", "ConveyorRight", 1) })
                Reg(cid, Make<Conveyor>(cname, "Traps", "conveyor belt", new Vector2(1f, 0.25f), ArtGenerator.S("Sprites/Props/conveyor_0"), null, 2,
                    (e, go, sr) => { e.flipWithFacing = false; e.forceDir = cdir; e.frames = new[] { ArtGenerator.S("Sprites/Props/conveyor_0"), ArtGenerator.S("Sprites/Props/conveyor_1"), ArtGenerator.S("Sprites/Props/conveyor_2") }; }));
            Reg("sawblade", Make<SawBlade>("SawBlade", "Traps", "saw blade", new Vector2(0.7f, 0.7f), ArtGenerator.S("Sprites/Traps/saw"), null, 7,
                (e, go, sr) =>
                {
                    e.flipWithFacing = false;
                    sr.transform.localPosition = new Vector3(0f, 0.35f, 0f);        // the disc turns about its centre
                    var rg = new GameObject("Rail");
                    rg.transform.SetParent(go.transform, false);
                    var rsr = rg.AddComponent<SpriteRenderer>();
                    rsr.sprite = ArtGenerator.S("Sprites/Traps/sawrail"); rsr.sharedMaterial = spriteMat; rsr.sortingOrder = 1;
                    rsr.drawMode = SpriteDrawMode.Tiled; rsr.size = new Vector2(1f, 3f / 16f);
                    e.rail = rsr;
                }));
            Reg("press", Make<Press>("Press", "Traps", "stamping press", new Vector2(0.9f, 0.95f), ArtGenerator.S("Sprites/Traps/crusher"), null, 6,
                (e, go, sr) => { e.flipWithFacing = false; }));
            Reg("webpatch", Make<WebPatch>("WebPatch", "Traps", "web", new Vector2(2.4f, 0.45f), ArtGenerator.S("Sprites/Props/web"), null, 9,
                (e, go, sr) => { e.flipWithFacing = false; }));
            Reg("eggsac", Make<EggSac>("EggSac", "Enemies", "egg sac", new Vector2(0.6f, 0.65f), ArtGenerator.S("Sprites/Enemies/eggsac_0"), anims["eggsac_pulse"], 13,
                (e, go, sr) => { e.goldDrop = 0; e.heartDropChance = 0f; e.animPulse = anims["eggsac_pulse"]; e.flipWithFacing = false; e.bloodColor = new Color(0.85f, 0.9f, 0.75f); }));
            Reg("foreman", Make<Foreman>("Foreman", "Enemies", "Foreman", new Vector2(1.6f, 1.9f), BS2("foreman_0"), anims["foreman_walk"], 15,
                (e, go, sr) => { e.maxHealth = 16; e.contactDamage = 2; e.goldDrop = 0; e.heartDropChance = 0f; e.rewardId = "heartvessel"; e.animWalk = anims["foreman_walk"]; e.animCharge = anims["foreman_charge"]; e.animDazed = anims["foreman_dazed"]; e.animCounter = anims["foreman_charge"]; e.counterFx = anims["cfx_buck"]; e.counterMotion = Boss.CounterMotion.Burst; e.counterFxScale = 1.6f; e.bloodColor = new Color(0.85f, 0.7f, 0.4f); }));
            // Sporegloom: its creatures, the spore cloud and the plants
            Reg("puffshroom", Make<Puffshroom>("Puffshroom", "Enemies", "puffshroom", new Vector2(0.7f, 0.75f), ArtGenerator.S("Sprites/Enemies/puffshroom_0"), anims["puffshroom_walk"], 14,
                (e, go, sr) => { e.animWalk = anims["puffshroom_walk"]; e.animSwell = anims["puffshroom_swell"]; e.animDeflated = anims["puffshroom_deflated"]; e.bloodColor = new Color(0.7f, 1f, 0.5f); }));
            Reg("moth", Make<GloomMoth>("GloomMoth", "Enemies", "gloom moth", new Vector2(0.55f, 0.5f), ArtGenerator.S("Sprites/Enemies/moth_2"), anims["moth_hang"], 14,
                (e, go, sr) => { e.animHang = anims["moth_hang"]; e.animFly = anims["moth_fly"]; e.goldDrop = 50; e.bloodColor = new Color(0.8f, 0.75f, 1f); }));
            Reg("lurecap", Make<Lurecap>("Lurecap", "Enemies", "lurecap", new Vector2(0.7f, 0.6f), ArtGenerator.S("Sprites/Enemies/lurecap_0"), anims["lurecap_dormant"], 14,
                (e, go, sr) => { e.maxHealth = 2; e.goldDrop = 100; e.animDormant = anims["lurecap_dormant"]; e.animIdle = anims["lurecap_idle"]; e.animJump = anims["lurecap_jump"]; e.jumpX = 3.8f; e.jumpY = 10f; e.bloodColor = new Color(0.4f, 1f, 0.85f); }));
            Reg("sporecloud", Make<SporeCloud>("SporeCloud", "Enemies", "spore cloud", new Vector2(3.4f, 2.6f), ArtGenerator.S("Center/sporecloud"), null, 25,
                (e, go, sr) => { e.flipWithFacing = false; sr.transform.localPosition = new Vector3(0f, 1.3f, 0f); }));
            Reg("bouncecap", Make<BounceCap>("BounceCap", "Traps", "bounce cap", new Vector2(1.3f, 0.6f), ArtGenerator.S("Sprites/Props/bouncecap_0"), null, 4,
                (e, go, sr) => { e.flipWithFacing = false; e.restSprite = ArtGenerator.S("Sprites/Props/bouncecap_0"); e.squashSprite = ArtGenerator.S("Sprites/Props/bouncecap_1"); }));
            Reg("glowshroom", Make<Scenery>("Glowshroom", "Level", "glowing mushroom", new Vector2(0.8f, 0.6f), ArtGenerator.S("Sprites/Props/glowshroom_0"), null, 1,
                (e, go, sr) => { e.flipWithFacing = false; e.variants = new[] { ArtGenerator.S("Sprites/Props/glowshroom_0"), ArtGenerator.S("Sprites/Props/glowshroom_1"), ArtGenerator.S("Sprites/Props/glowshroom_2") }; }));
            // bosses (one per area + the final guardian), their projectile, and the treasure chest
            Sprite BS(string n) => ArtGenerator.S("Sprites/Bosses/" + n);
            Reg("broodmother", Make<Broodmother>("Broodmother", "Enemies", "Broodmother", new Vector2(1.5f, 1.0f), BS("broodmother_0"), anims["broodmother_idle"], 15,
                (e, go, sr) => { e.maxHealth = 14; e.contactDamage = 2; e.goldDrop = 0; e.heartDropChance = 0f; e.rewardId = "heartvessel"; e.animIdle = anims["broodmother_idle"]; e.animJump = anims["broodmother_jump"]; e.animFall = anims["broodmother_fall"]; e.animCounter = anims["broodmother_jump"]; e.counterFx = anims["cfx_bite"]; e.counterMotion = Boss.CounterMotion.Lunge; e.counterFxScale = 1.2f; e.bloodColor = new Color(0.5f, 0.3f, 0.6f); }));
            Reg("forgebrute", Make<ForgeBrute>("ForgeBrute", "Enemies", "Forge Brute", new Vector2(1.6f, 1.7f), BS("forgebrute_0"), anims["forgebrute_walk"], 15,
                (e, go, sr) => { e.maxHealth = 16; e.contactDamage = 2; e.goldDrop = 0; e.heartDropChance = 0f; e.rewardId = "heartvessel"; e.animWalk = anims["forgebrute_walk"]; e.animCharge = anims["forgebrute_charge"]; e.animCounter = anims["forgebrute_charge"]; e.counterFx = anims["cfx_buck"]; e.counterMotion = Boss.CounterMotion.Buck; e.animDazed = anims["forgebrute_dazed"]; e.bloodColor = new Color(0.9f, 0.4f, 0.2f); }));
            Reg("yetiking", Make<YetiKing>("YetiKing", "Enemies", "Yeti King", new Vector2(1.5f, 1.9f), BS("yetiking_0"), anims["yetiking_idle"], 15,
                (e, go, sr) => { e.maxHealth = 16; e.contactDamage = 2; e.goldDrop = 0; e.heartDropChance = 0f; e.rewardId = "heartvessel"; e.animIdle = anims["yetiking_idle"]; e.animThrow = anims["yetiking_throw"]; e.animCounter = anims["yetiking_throw"]; e.counterFx = anims["cfx_frost"]; e.counterMotion = Boss.CounterMotion.Burst; e.counterFxScale = 2.2f; e.counterFxOnBody = true; e.bloodColor = new Color(0.85f, 0.92f, 1f); }));
            Reg("stormhawk", Make<StormHawk>("StormHawk", "Enemies", "Storm Hawk", new Vector2(1.3f, 0.9f), BS("stormhawk_0"), anims["stormhawk_fly"], 15,
                (e, go, sr) => { e.maxHealth = 12; e.contactDamage = 2; e.goldDrop = 0; e.heartDropChance = 0f; e.rewardId = "heartvessel"; e.diveSpeed = 18.5f; e.sweepSpeed = 15f; e.animFly = anims["stormhawk_fly"]; e.animDive = anims["stormhawk_dive"]; e.animCounter = anims["stormhawk_fly"]; e.counterFx = anims["cfx_gust"]; e.counterMotion = Boss.CounterMotion.Flap; e.counterFxScale = 1.5f; e.bloodColor = new Color(0.5f, 0.6f, 0.9f); }));
            Reg("sporeheart", Make<SporeHeart>("SporeHeart", "Enemies", "Rot Heart", new Vector2(1.7f, 1.6f), BS("sporeheart_0"), anims["sporeheart_beat"], 15,
                (e, go, sr) => { e.maxHealth = 14; e.contactDamage = 0; e.stompable = false; e.goldDrop = 0; e.heartDropChance = 0f; e.rewardId = "heartvessel"; e.flipWithFacing = false; e.animBeat = anims["sporeheart_beat"]; e.animSwell = anims["sporeheart_swell"]; e.bloodColor = new Color(0.62f, 1f, 0.5f); }));
            // things the reworked bosses use: fire on the floor, the snow boulder, the tendril
            Reg("firepatch", Make<FirePatch>("FirePatch", "Traps", "fire", new Vector2(0.8f, 0.75f), null, null, 8,
                (e, g, s) =>
                {
                    e.flipWithFacing = false;
                    s.sprite = ArtGenerator.S("Sprites/Traps/flame_0");
                    var fa = s.gameObject.AddComponent<SpriteAnimator>();
                    fa.initial = anims["flame"];
                }));
            Reg("snowboulder", Make<RollingBall>("SnowBoulder", "Enemies", "snow boulder", new Vector2(0.6f, 0.6f), BS("snowboulder"), null, 16,
                (e, g, s) => { e.flipWithFacing = false; }));
            Reg("tendril", Make<BossShot>("Tendril", "Enemies", "tendril", new Vector2(1.6f, 0.45f), BS("tendril"), null, 32,
                (e, g, s) => { e.flipWithFacing = false; e.flipByVelocity = true; e.unbreakable = true; e.lifetime = 6f; e.burstColor = new Color(0.62f, 1f, 0.5f); }));
            Reg("warden", Make<Warden>("Warden", "Enemies", "Hollow Warden", new Vector2(1.4f, 1.8f), BS("warden_0"), anims["warden_float"], 31,
                (e, go, sr) => { e.maxHealth = 24; e.contactDamage = 2; e.stompable = false; e.goldDrop = 0; e.heartDropChance = 0f; e.rewardId = ""; e.animFloat = anims["warden_float"]; e.bloodColor = new Color(0.7f, 0.4f, 1f); }));
            Reg("gloomorb", Make<BossShot>("GloomOrb", "Enemies", "gloom orb", new Vector2(0.35f, 0.35f), BS("orb"), null, 32,
                (e, go, sr) => { e.flipWithFacing = false; }));
            Reg("venomshot", Make<BossShot>("VenomShot", "Enemies", "venom", new Vector2(0.4f, 0.4f), BS("venom_0"), anims["venom_fly"], 32,
                (e, go, sr) => { e.flipWithFacing = false; e.faceVelocity = true; e.burstColor = new Color(0.55f, 0.95f, 0.4f); }));
            Reg("feathershot", Make<BossShot>("FeatherShot", "Enemies", "feather", new Vector2(0.35f, 0.35f), BS("feather"), null, 32,
                (e, go, sr) => { e.flipWithFacing = false; e.faceVelocity = true; e.burstColor = new Color(0.8f, 0.9f, 1f); }));
            Reg("shockwave", Make<BossShot>("Shockwave", "Enemies", "shockwave", new Vector2(0.6f, 0.6f), BS("shock_0"), anims["shock_run"], 32,
                (e, go, sr) => { e.flipWithFacing = false; e.flipByVelocity = true; e.damage = 2; e.lifetime = 3f; e.burstColor = new Color(0.9f, 0.9f, 0.95f); }));
            // traps for climbs
            Reg("rockdropper", Make<RockDropper>("RockDropper", "Traps", "rock dropper", new Vector2(0.9f, 0.75f), ArtGenerator.S("Sprites/Traps/dropper"), null, 3,
                (e, g, s) => { e.flipWithFacing = false; }));
            Reg("crumble", Make<CrumblePlatform>("CrumblePlatform", "Traps", "rotten platform", new Vector2(1f, 0.3f), ArtGenerator.S("Sprites/Traps/crumble"), null, 2,
                (e, g, s) => { e.flipWithFacing = false; }));
            Reg("chest", Make<Chest>("Chest", "Level", "chest", new Vector2(1.0f, 0.8f), ArtGenerator.S("Sprites/Props/chest_0"), null, 4,
                (e, g, s) => { e.flipWithFacing = false; e.openSprite = ArtGenerator.S("Sprites/Props/chest_1"); }));
            Reg("hawk", Make<Hawk>("Hawk", "Enemies", "hawk", new Vector2(0.6f, 0.5f), ArtGenerator.S("Sprites/Enemies/hawk_0"), anims["hawk_fly"], 14,
                (e, go, sr) => { e.contactDamage = 2; e.goldDrop = 100; e.animFly = anims["hawk_fly"]; e.animDive = anims["hawk_dive"]; e.bloodColor = new Color(0.6f, 0.2f, 0.15f); }));
            Reg("tree", Make<Scenery>("Tree", "Level", "tree", new Vector2(1f, 2.5f), ArtGenerator.S("Sprites/Props/tree_0"), null, -6,
                (e, go, sr) => { e.flipWithFacing = false; e.variants = new[] { ArtGenerator.S("Sprites/Props/tree_0"), ArtGenerator.S("Sprites/Props/tree_1") }; }));
            Reg("wraith", Make<Wraith>("Wraith", "Enemies", "the Reaper", new Vector2(0.8f, 1.4f), ArtGenerator.S("Sprites/Enemies/wraith_0"), anims["wraith_float"], 30,
                (e, go, sr) => { e.contactDamage = 99; e.stompable = false; e.goldDrop = 0; e.heartDropChance = 0f; e.animFloat = anims["wraith_float"]; }));
            Reg("shopkeeper", Make<Shopkeeper>("Shopkeeper", "Enemies", "shopkeeper", new Vector2(0.6f, 0.85f), ArtGenerator.S("Sprites/Shopkeeper/idle_0"), anims["keeper_idle"], 14,
                (e, go, sr) => { e.maxHealth = 6; e.contactDamage = 3; e.animIdle = anims["keeper_idle"]; e.animAngry = anims["keeper_angry"]; e.goldDrop = 300; e.stompable = true; }));

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
            Reg("heartstone", Make<Treasure>("Heartstone", "Items", "Sunstone", new Vector2(0.55f, 0.7f), ArtGenerator.S(I("heartstone")), null, 12,
                (e, g, s) => { e.heavy = true; e.isHeartstone = true; e.glow = new Color(1f, 0.82f, 0.4f); e.flipWithFacing = false; }));

            // pickups
            Reg("gold_small", Make<Pickup>("GoldNugget", "Items", "gold nugget", new Vector2(0.3f, 0.25f), ArtGenerator.S(I("gold_small")), null, 11, (e, g, s) => { e.value = 50; e.flipWithFacing = false; }));
            Reg("gold_big", Make<Pickup>("GoldChunk", "Items", "gold chunk", new Vector2(0.5f, 0.35f), ArtGenerator.S(I("gold_big")), null, 11, (e, g, s) => { e.value = 150; e.flipWithFacing = false; }));
            Reg("gem", Make<Pickup>("Gem", "Items", "emerald", new Vector2(0.4f, 0.4f), ArtGenerator.S(I("gem")), null, 11, (e, g, s) => { e.value = 300; e.sfx = "gem"; e.textColor = new Color(0.5f, 1f, 0.7f); e.flipWithFacing = false; }));
            Reg("bombbag", Make<Pickup>("BombBag", "Items", "bomb bag", new Vector2(0.6f, 0.55f), ArtGenerator.S(I("bombbag")), null, 11, (e, g, s) => { e.resource = Res.Bombs; e.label = "BOMBS"; e.value = 3; e.sfx = "pickup"; e.textColor = Color.white; e.flipWithFacing = false; }));
            Reg("ropepile", Make<Pickup>("RopePile", "Items", "rope pile", new Vector2(0.6f, 0.45f), ArtGenerator.S(I("ropepile")), null, 11, (e, g, s) => { e.resource = Res.Ropes; e.label = "ROPES"; e.value = 3; e.sfx = "pickup"; e.textColor = Color.white; e.flipWithFacing = false; }));
            Reg("heart", Make<Pickup>("Heart", "Items", "heart", new Vector2(0.5f, 0.45f), ArtGenerator.S(I("heart")), null, 11,
                (e, g, s) => { e.resource = Res.Health; e.label = "HP"; e.value = 1; e.sfx = "buy"; e.textColor = new Color(1f, 0.45f, 0.5f); e.flipWithFacing = false; }));
            Reg("heartvessel", Make<Pickup>("HeartVessel", "Items", "heart vessel", new Vector2(0.6f, 0.55f), ArtGenerator.S(I("heartvessel")), null, 11,
                (e, g, s) => { e.resource = Res.MaxHealth; e.label = "MAX HEALTH"; e.value = 1; e.sfx = "idol"; e.textColor = new Color(1f, 0.85f, 0.35f); e.flipWithFacing = false; }));
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
                (e, g, s) => { e.isExit = false; e.flipWithFacing = false; e.openSprite = ArtGenerator.S("Sprites/Props/door_entrance"); e.housingSprite = ArtGenerator.S("Sprites/Props/door_housing"); }));
            Reg("door_exit", Make<Door>("DoorExit", "Level", "exit", new Vector2(0.8f, 1.2f), ArtGenerator.S("Sprites/Props/door_exit"), null, 3,
                (e, g, s) => { e.isExit = true; e.flipWithFacing = false; e.openSprite = ArtGenerator.S("Sprites/Props/door_exit"); e.lockedSprite = ArtGenerator.S("Sprites/Props/door_locked"); e.housingSprite = ArtGenerator.S("Sprites/Props/door_housing"); }));
            {
                // the people of Brindlemoor: caged on the mountain, at home in the village
                var looks = new Sprite[Villagers.All.Length];
                for (int i = 0; i < looks.Length; i++) looks[i] = ArtGenerator.S("Sprites/NPC/villager_" + i);
                Reg("captive", Make<Captive>("Captive", "Level", "caged villager", new Vector2(1.1f, 1.3f), looks[0], null, 4, (e, g, s) =>
                {
                    e.flipWithFacing = false; e.looks = looks;
                    var cg = new GameObject("Cage");
                    cg.transform.SetParent(g.transform, false);
                    var csr = cg.AddComponent<SpriteRenderer>();
                    csr.sprite = ArtGenerator.S("Sprites/NPC/cage"); csr.sharedMaterial = spriteMat; csr.sortingOrder = 5;
                    e.cage = csr;
                }));
                // the keeper inside their shop, and the elder inside the hall
                Reg("villagerhome", Make<VillagerHome>("VillagerHome", "Level", "keeper", new Vector2(1f, 1.3f), looks[0], null, 3, (e, g, s) => { e.flipWithFacing = false; e.looks = looks; }));
                Reg("elder", Make<Elder>("Elder", "Level", "elder", new Vector2(1f, 1.3f), ArtGenerator.S("Sprites/NPC/villager_6"), null, 3, (e, g, s) => { e.flipWithFacing = false; }));
                // the street: every house front, with a lantern by the door and light spilling from the windows
                var dark = new Sprite[8]; var lit = new Sprite[8]; var rooms = new Sprite[8];
                for (int i = 0; i < 8; i++)
                {
                    dark[i] = ArtGenerator.S($"Sprites/Village/front_{i}_0"); lit[i] = ArtGenerator.S($"Sprites/Village/front_{i}_1");
                    rooms[i] = ArtGenerator.S($"Sprites/Village/room_{i}");
                }
                GlowLight Lamp(GameObject parent, string name, Vector2 at, float radius, float intensity, float flicker)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(parent.transform, false);
                    go.transform.localPosition = at;
                    var gl = go.AddComponent<GlowLight>();
                    gl.color = new Color(1f, 0.78f, 0.45f); gl.radius = radius; gl.intensity = intensity; gl.flicker = flicker; gl.offset = Vector2.zero;
                    return gl;
                }
                Reg("building", Make<VillageBuilding>("VillageBuilding", "Level", "house", new Vector2(1.2f, 1.7f), dark[0], null, 1, (e, g, s) =>
                {
                    e.flipWithFacing = false; e.darkFronts = dark; e.litFronts = lit;
                    e.lights = new[]
                    {
                        Lamp(g, "Lantern", new Vector2(-0.85f, 1.9f), 3.4f, 0.5f, 0.2f),
                        Lamp(g, "Windows", new Vector2(0f, 3.8f), 5.5f, 0.22f, 0.05f),
                    };
                    // smoke from the chimney while the fire is lit (the building puts it over its own chimney)
                    var smokeGo = new GameObject("Smoke");
                    smokeGo.transform.SetParent(g.transform, false);
                    var smoke = smokeGo.AddComponent<ChimneySmoke>();
                    smoke.frames = ArtGenerator.Frames("Center/smoke", 4);
                    smoke.material = unlitMat;
                    e.smoke = smoke;
                }));
                // lamp posts stand where two houses meet; a string of lights runs from each to the next
                foreach (bool withString in new[] { true, false })
                    Reg(withString ? "streetlamp" : "streetlamp_end", Make<Prop>(withString ? "StreetLamp" : "StreetLampEnd", "Level", "street lamp", new Vector2(0.4f, 1f), ArtGenerator.S("Sprites/Village/lamp_post"), null, 2, (e, g, s) =>
                    {
                        e.flipWithFacing = false;
                        s.transform.localPosition = new Vector3(-0.5f, 0f, 0f);       // the post stands on the line between two cells
                        Lamp(g, "Light", new Vector2(-0.5f, 4.2f), 3.8f, 0.42f, 0.12f);
                        if (!withString) return;
                        var wire = new GameObject("StringLights");
                        wire.transform.SetParent(g.transform, false);
                        wire.transform.localPosition = new Vector3(-0.5f, 4.55f, 0f);
                        var wsr = wire.AddComponent<SpriteRenderer>();
                        wsr.sprite = ArtGenerator.S("Sprites/Village/string_lights"); wsr.sharedMaterial = unlitMat; wsr.sortingOrder = 2;
                    }));
                foreach (var (pid, pname, sprite) in new[] { ("bench", "Bench", "bench"), ("firtree", "FirTree", "fir_tree"), ("snowman", "Snowman", "snowman"), ("woodpile", "WoodPile", "wood_pile"), ("cratestack", "CrateStack", "crates") })
                    Reg(pid, Make<Prop>(pname, "Level", pid, new Vector2(1f, 1f), ArtGenerator.S("Sprites/Village/" + sprite), null, 2, (e, g, s) =>
                    {
                        e.flipWithFacing = false;
                        if (pid == "firtree") Lamp(g, "Candles", new Vector2(0f, 1.8f), 3.2f, 0.4f, 0.25f);
                    }));
                Reg("pavement", Make<Prop>("Pavement", "Level", "street", new Vector2(1f, 0.2f), ArtGenerator.S("Sprites/Village/street_ground"), null, 2, (e, g, s) =>
                {
                    e.flipWithFacing = false;
                    s.transform.localPosition = new Vector3(-1.5f, 0f, 0f);       // placed in the second cell of the street: the picture starts at the level's left edge
                }));
                Reg("interior", Make<VillageInterior>("VillageInterior", "Level", "door", new Vector2(1.2f, 1.7f), rooms[0], null, 2, (e, g, s) =>
                {
                    e.flipWithFacing = false; e.rooms = rooms;
                    s.transform.localPosition = new Vector3(5.5f, 3f, 0f);        // the picture is the whole room; the entity stands at its door
                    Lamp(g, "RoomLight", new Vector2(5.5f, 3.4f), 11f, 0.55f, 0.06f);
                }));
            }
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
                    pt.font = font; pt.material = unlitMat; pt.sortingOrder = 60;
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
            var camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            cam.allowHDR = true;
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

            // Picture of the cave mouth: always covers the screen, whatever its shape (ArtGenerator.MenuArt).
            var art = UIBuilder.Img(t, "Backdrop", ArtGenerator.S("UI/menu_bg"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(art.rectTransform);
            var fit = art.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 16f / 9f;
            // the traveller's scarf, blowing in the wind: a small animation laid exactly over its place in the picture
            // (anchored by picture coordinates, so it stays put however the picture is scaled or cropped)
            {
                var r = ArtGenerator.MenuScarfRect;
                var scarf = UIBuilder.Img(art.transform, "Scarf", ArtGenerator.S("UI/menu_scarf_0"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var srt = scarf.rectTransform;
                srt.anchorMin = new Vector2(r.xMin / (float)ArtGenerator.MenuArtW, r.yMin / (float)ArtGenerator.MenuArtH);
                srt.anchorMax = new Vector2(r.xMax / (float)ArtGenerator.MenuArtW, r.yMax / (float)ArtGenerator.MenuArtH);
                srt.offsetMin = srt.offsetMax = Vector2.zero;
                var sa = scarf.gameObject.AddComponent<MenuSpriteAnim>();
                sa.frames = new Sprite[ArtGenerator.ScarfFrames];
                for (int f = 0; f < sa.frames.Length; f++) sa.frames[f] = ArtGenerator.S("UI/menu_scarf_" + f);
            }
            // snow falling over the picture, behind everything else on the screen
            var snow = UIBuilder.Node(t, "Snow");
            UIBuilder.Stretch(UIBuilder.Rect(snow));
            snow.AddComponent<MenuSnow>();
            var vig = UIBuilder.Img(t, "Vignette", ArtGenerator.S("UI/vignette"), new Color(0f, 0f, 0f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(vig.rectTransform);

            // The title, "Brindlemoor: The Sunstone Hunt":
            // the series name in tall gold capitals with a larger first letter, the name of this part spaced out
            // under it, then one line of story.
            var title = UIBuilder.Label(t, "Title", "<size=178>B</size>RINDLEMOOR", 128, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1700, 200));
            title.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            UIBuilder.GoldGradient(title);
            string spaced = string.Join(" ", GameConst.PartName.ToUpperInvariant().Replace(" ", "  ").ToCharArray());
            var part = UIBuilder.Label(t, "Part", spaced, 40, new Color(0.96f, 0.9f, 0.74f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -236), new Vector2(1500, 56));
            var story = UIBuilder.Label(t, "Subtitle", "Winter will not leave Brindlemoor. Bring the Sunstone home.", 28, new Color(0.84f, 0.88f, 0.98f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -296), new Vector2(1400, 44), true, FontStyle.Italic);

            var menu = new GameObject("MainMenu").AddComponent<MainMenuController>();
            menu.config = cfg;
            menu.titleObjects = new[] { title.gameObject, part.gameObject, story.gameObject };
            // everything built from here on in this scene - the list, Settings, Controls, Challenge, Records - gets
            // the title-screen look (quiet panels, strip buttons, serif texts). Switched off again before saving.
            UIBuilder.menuStyle = true;

            // the main list: plain words, a gold marker beside the one in focus
            var main = UIBuilder.Node(t, "MainPanel");
            UIBuilder.Place(UIBuilder.Rect(main), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(620, 420));
            var shade = UIBuilder.Img(main.transform, "Shade", ArtGenerator.S("UI/menu_shade"), new Color(0.02f, 0.03f, 0.1f, 0.78f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(900, 640));
            var mt = main.transform;
            Vector2 bs = new Vector2(560, 60);
            Button Entry(string name, string label, int row)
            {
                var b = UIBuilder.TextButton(mt, name, label, new Vector2(0.5f, 1f), new Vector2(0, -20 - row * 66), bs, 42, out var tx);
                return b;
            }
            menu.startButton = Entry("Start", "Start Run", 0);
            menu.tutorialButton = Entry("Tutorial", "Tutorial", 1);
            menu.bossButton = Entry("BossTest", "Challenge", 2);
            menu.settingsButton = Entry("Settings", "Settings", 3);
            menu.recordsButton = Entry("Records", "Records", 4);
            menu.quitButton = Entry("Quit", "Quit", 5);
            var ptrImg = UIBuilder.Img(t, "Pointer", ArtGenerator.S("UI/menu_pointer"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 33));
            var ptr = menu.gameObject.AddComponent<MenuPointer>();
            ptr.pointer = ptrImg.rectTransform; ptr.scope = main.transform;
            menu.mainPanel = main;

            menu.keysPanel = UIBuilder.KeysPanel(t);
            // boss test: one button per boss stage of the run (area bosses and the final guardian)
            {
                var bossReg = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(G + "Data/PrefabRegistry.asset");
                var bossStages = new List<int>();
                var bossNames = new List<string>();
                var bossKeys = new List<string>();        // display name as written on the prefab: the key of its record
                for (int si = 0; si < cfg.stages.Length; si++)
                {
                    var st = cfg.stages[si];
                    if ((st.kind != LevelKind.Boss && st.kind != LevelKind.Final) || st.handcrafted == null) continue;
                    string bossId = "";
                    foreach (var line in st.handcrafted.text.Split('\n'))
                    {
                        string l = line.Trim();
                        if (l == "---") break;
                        if (l.StartsWith("boss:")) bossId = l.Substring(5).Trim();
                    }
                    // looked up in the registry asset itself (cfg.prefabs is not reliable while scenes are being rebuilt)
                    var entry = bossId.Length > 0 && bossReg != null ? bossReg.entries.Find(en => en != null && en.id == bossId) : null;
                    var prefab = entry != null ? entry.prefab : null;
                    if (prefab == null) continue;
                    bossStages.Add(si);
                    bossKeys.Add(prefab.displayName);
                    bossNames.Add(prefab.displayName.ToUpperInvariant());       // just the name: areas come in a different order every run
                }
                float rowStep = 72f;
                var bp = UIBuilder.Panel(t, "BossPanel", new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(820, 330 + bossStages.Count * rowStep));
                UIBuilder.Label(bp.transform, "Title", "CHALLENGE", 46, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(700, 64));
                UIBuilder.Label(bp.transform, "Hint", "Pick a boss and fight it straight away.\nDying starts the fight again. Your best time against each boss is kept.", 22, new Color(1f, 1f, 1f, 0.75f),
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -112), new Vector2(760, 64));
                menu.bossButtons = new Button[bossStages.Count];
                menu.bossBest = new TMPro.TMP_Text[bossStages.Count];
                menu.bossNames = bossKeys.ToArray();
                for (int i = 0; i < bossStages.Count; i++)
                {
                    menu.bossButtons[i] = UIBuilder.Button(bp.transform, "Boss_" + i, bossNames[i], new Vector2(0.5f, 1f), new Vector2(0, -196 - i * rowStep), new Vector2(700, 62));
                    // best time and the no-damage badge, at the right end of the button
                    menu.bossBest[i] = UIBuilder.Label(menu.bossButtons[i].transform, "Best", "", 22, new Color(1f, 0.84f, 0.38f), TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(220, 50), false);
                }
                menu.bossStages = bossStages.ToArray();
                menu.bossBack = UIBuilder.Button(bp.transform, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(320, 66));
                menu.bossPanel = bp.gameObject;
            }
            menu.settingsPanel = UIBuilder.Settings(t);
            var rec = UIBuilder.Panel(t, "RecordsPanel", new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1180, 820));
            UIBuilder.Label(rec.transform, "Title", "RECORDS", 46, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(700, 70));
            menu.recordsText = UIBuilder.Label(rec.transform, "Body", "", 28, Color.white, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(-270, -196), new Vector2(520, 460));
            UIBuilder.Label(rec.transform, "RunsHead", "RUNS", 24, new Color(1f, 0.84f, 0.38f), TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(-270, -146), new Vector2(520, 34), true, FontStyle.Bold);
            UIBuilder.Label(rec.transform, "BossHead", "CHALLENGE  -  best time against each boss", 24, new Color(1f, 0.84f, 0.38f), TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(290, -146), new Vector2(520, 34), true, FontStyle.Bold);
            menu.bossRecordsText = UIBuilder.Label(rec.transform, "Bosses", "", 28, Color.white, TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(290, -196), new Vector2(520, 460));
            menu.bossRecordsText.lineSpacing = 35f;
            menu.recordsText.lineSpacing = 20f;
            menu.recordsBack = UIBuilder.Button(rec.transform, "Back", "BACK", new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(320, 70));   // records cannot be reset from the game
            menu.recordsPanel = rec.gameObject;
            menu.versionText = UIBuilder.Label(t, "Version", "", 22, new Color(1, 1, 1, 0.5f), TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(20, 16), new Vector2(900, 40), false);
            UIBuilder.Label(t, "Hint", "[move] navigate   ·   [confirm] select   ·   [jump] jump   ·   [whip] blade   ·   [use] use gear   ·   hold [gear] gear wheel   ·   [enter] enter", 22,
                new Color(1, 1, 1, 0.55f), TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-20, 16), new Vector2(1300, 40), false);

            // Loading screen (menu -> game): its own canvas above everything, kept across scene changes.
            {
                var lc = UIBuilder.Canvas("LoadingCanvas", 100);
                var ls = lc.gameObject.AddComponent<LoadingScreen>();
                var cover = UIBuilder.Node(lc.transform, "Cover");
                UIBuilder.Stretch(UIBuilder.Rect(cover));
                ls.cover = cover;
                ls.group = cover.AddComponent<CanvasGroup>();
                var mid = new Vector2(0.5f, 0.5f);
                var back = UIBuilder.Img(cover.transform, "Back", null, new Color(0.035f, 0.045f, 0.11f, 1f), mid, Vector2.zero, Vector2.zero);
                UIBuilder.Stretch(back.rectTransform);
                back.raycastTarget = true;
                var lsnow = UIBuilder.Node(cover.transform, "Snow");
                UIBuilder.Stretch(UIBuilder.Rect(lsnow));
                var lsn = lsnow.AddComponent<MenuSnow>();
                lsn.farFlakes = 70; lsn.nearFlakes = 24;
                var lt = UIBuilder.Label(cover.transform, "Title", "<size=92>B</size>RINDLEMOOR", 66, Color.white, TextAnchor.MiddleCenter, mid, new Vector2(0, 150), new Vector2(1400, 120));
                lt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                string lspaced = string.Join(" ", GameConst.PartName.ToUpperInvariant().Replace(" ", "  ").ToCharArray());
                UIBuilder.Label(cover.transform, "Part", lspaced, 24, new Color(0.96f, 0.9f, 0.74f), TextAnchor.MiddleCenter, mid, new Vector2(0, 62), new Vector2(1200, 40));
                ls.tip = UIBuilder.Label(cover.transform, "Tip", "", 30, new Color(0.84f, 0.88f, 0.98f, 0.95f), TextAnchor.MiddleCenter, mid, new Vector2(0, -70), new Vector2(1300, 110), true, FontStyle.Italic);
                var track = UIBuilder.Img(cover.transform, "Track", ArtGenerator.S("Center/pixel"), new Color(1f, 0.84f, 0.38f, 0.22f), mid, new Vector2(0, -190), new Vector2(600, 4));
                var lfill = UIBuilder.Img(track.transform, "Fill", ArtGenerator.S("Center/pixel"), new Color(1f, 0.84f, 0.38f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0, 4));
                ls.fill = lfill.rectTransform; ls.fillWidth = 600f;
                ls.loadingText = UIBuilder.Label(cover.transform, "Loading", "Loading...", 24, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft, mid, new Vector2(0, -232), new Vector2(120, 36));
                ls.tips = new[]
                {
                    "Bombs open walls. They open you just as easily.",
                    "A rope reaches where a jump cannot.",
                    "Hold [gear] to choose what you carry in your hand.",
                    "Shopkeepers remember a thief for the rest of the run.",
                    "Stay too long in one level and something comes looking for you.",
                    "Every boss gives itself away just before it strikes.",
                    "Boots, coats and charms from the shops make a hard place easier - none of them is needed.",
                    "Hanging from a ledge is a good place to think.",
                };
            }
            UIBuilder.menuStyle = false;
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
                r.sprite = ArtGenerator.S(sprite); r.sharedMaterial = unlitMat; r.sortingOrder = order;
                return r;
            }
            sky.gradient = SkySr("Gradient", "Sky/gradient", -41);
            sky.sun = SkySr("Sun", "Sky/sun", -39);
            sky.moonSprite = ArtGenerator.S("Sky/moon");
            sky.stars = SkySr("Stars", "Sky/stars", -40);
            sky.stars.gameObject.SetActive(false);
            sky.layers = new[] { SkySr("Range", "Sky/range", -38), SkySr("Mountains", "Sky/mountains", -37), SkySr("Hills", "Sky/hills", -35) };
            sky.parallax = new[] { 0.04f, 0.1f, 0.24f };
            sky.layerHeight = new[] { 0.8f, -0.6f, -3.0f };
            sky.clouds = new[]
            {
                SkySr("Cloud0", "Sky/cloud_0", -36), SkySr("Cloud1", "Sky/cloud_1", -36), SkySr("Cloud2", "Sky/cloud_0", -36),
                SkySr("Cloud3", "Sky/cloud_1", -38), SkySr("Cloud4", "Sky/cloud_0", -38),
            };
            skyGo.SetActive(false);

            // indoor parallax rock layers (enabled by GameDirector in indoor areas)
            var caveGo = new GameObject("CaveBackdrop");
            caveGo.transform.SetParent(cam.transform, false);
            var cave = caveGo.AddComponent<CaveBackdrop>();
            cave.cam = cam;
            SpriteRenderer CaveSr(string name, string sprite, int order, float alpha)
            {
                var go = new GameObject(name);
                go.transform.SetParent(caveGo.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = ArtGenerator.S(sprite); r.sharedMaterial = spriteMat; r.sortingOrder = order;
                r.color = new Color(0.1f, 0.08f, 0.12f, alpha);
                return r;
            }
            cave.layers = new[] { CaveSr("Far", "Sky/cave_0", -19, 0.22f), CaveSr("Near", "Sky/cave_1", -18, 0.3f) };
            cave.parallax = new[] { 0.55f, 0.78f };

            // post-processing + lighting + atmosphere
            var postGo = new GameObject("PostFX");
            var vol = postGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = PostProfile();
            var lightGo = new GameObject("Lighting");
            var lighting = lightGo.AddComponent<LevelLighting>();
            lighting.torchFrames = ArtGenerator.Frames("Sprites/Props/torch_flame", 3);
            lighting.torchBracket = ArtGenerator.S("Sprites/Props/torch");
            lighting.glowMaterial = unlitMat;
            lighting.litMaterial = spriteMat;
            var ambGo = new GameObject("Ambience");
            var amb = ambGo.AddComponent<Ambience>();
            amb.pixel = ArtGenerator.S("Center/pixel");
            amb.litMaterial = spriteMat;
            amb.glowMaterial = unlitMat;
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
            lr.shade = MakeTilemap(grid.transform, "Shade", -15);
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
            fx.glowMaterial = unlitMat;
            fx.cameraFollow = follow;

            // director
            var dirGo = new GameObject("GameDirector");
            var dir = dirGo.AddComponent<GameDirector>();
            var reader = dirGo.AddComponent<PlayerInputReader>();
            reader.actions = input;
            dir.config = cfg; dir.world = world; dir.cameraFollow = follow; dir.cam = cam; dir.input = reader;
            dir.sky = sky;
            dir.caveBackdrop = cave;

            // UI
            var gold = new Color(1f, 0.84f, 0.38f);
            // the in-game UI wears the title-screen look: quiet panels, strip buttons, serif texts
            UIBuilder.menuStyle = true;
            var canvas = UIBuilder.Canvas("GameCanvas", 10);
            var t = canvas.transform;
            var hudGo = UIBuilder.Node(t, "HUD");
            UIBuilder.Stretch(UIBuilder.Rect(hudGo));
            var hud = hudGo.AddComponent<HUD>();
            var vig = UIBuilder.Img(hudGo.transform, "DamageVignette", ArtGenerator.S("UI/vignette"), new Color(0.8f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(vig.rectTransform);
            hud.damageVignette = vig;
            var bar = UIBuilder.Panel(hudGo.transform, "TopLeft", new Vector2(0f, 1f), new Vector2(16, -16), new Vector2(490, 84));
            TMPro.TMP_Text Stat(string name, string icon, float x, out Image img)
            {
                img = UIBuilder.Img(bar.transform, name + "Icon", ArtGenerator.S(icon), Color.white, new Vector2(0f, 0.5f), new Vector2(x, 0), new Vector2(48, 48));
                img.rectTransform.pivot = new Vector2(0f, 0.5f);
                var tx = UIBuilder.Label(bar.transform, name, "0", 40, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(x + 56, 0), new Vector2(120, 60), true, FontStyle.Bold);
                tx.rectTransform.pivot = new Vector2(0f, 0.5f);
                return tx;
            }
            hud.healthText = Stat("Health", "UI/icon_heart", 20, out hud.heartIcon);
            // health (current/limit) and gold only: bombs and ropes are counted on the gear slot below
            hud.healthText.rectTransform.sizeDelta = new Vector2(150, 60);
            hud.goldText = Stat("Gold", "UI/icon_gold", 240, out _);
            hud.goldText.color = gold;
            hud.goldText.rectTransform.sizeDelta = new Vector2(180, 60);
            hud.upgradesText = UIBuilder.Label(hudGo.transform, "Upgrades", "", 24, new Color(0.7f, 1f, 0.75f), TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28, -108), new Vector2(900, 40));
            hud.upgradesText.rectTransform.pivot = new Vector2(0f, 1f);
            hud.upgradesText.rectTransform.anchoredPosition = new Vector2(28, -216);

            // Item in hand: just the icon with its count in the corner, the button that changes it on top and the
            // button that uses it underneath. Its name pops up over the player when it is switched.
            var gearBox = UIBuilder.Node(hudGo.transform, "Gear");
            UIBuilder.Place(UIBuilder.Rect(gearBox), new Vector2(0f, 1f), new Vector2(24, -110), new Vector2(104, 172));
            var gframe = UIBuilder.Img(gearBox.transform, "IconFrame", UIBuilder.Slot, new Color(1f, 0.8f, 0.35f), new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(104, 104), true);
            hud.equipIcon = UIBuilder.Img(gframe.transform, "Icon", ArtGenerator.S("UI/icon_bomb"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(76, 76));
            hud.equipName = null;
            hud.equipAmmo = UIBuilder.Label(gframe.transform, "Ammo", "4", 30, Color.white, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-12, 8), new Vector2(90, 36), true, FontStyle.Bold);
            // arrows either side of the button say "this one switches"
            UIBuilder.Label(gearBox.transform, "ChangeKey", "\u25C4 [gear] \u25BA", 22, new Color(1f, 0.84f, 0.38f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(150, 32), false);
            UIBuilder.Label(gearBox.transform, "UseKey", "[use]", 24, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(104, 32), false);
            // boss health bar (top centre, only during a boss fight)
            // no frame: just the boss's name and its health bar
            var bossBar = UIBuilder.Node(hudGo.transform, "BossBar");
            UIBuilder.Place(UIBuilder.Rect(bossBar), new Vector2(0.5f, 1f), new Vector2(0, -112), new Vector2(600, 76));
            hud.bossRoot = bossBar.gameObject;
            hud.bossName = UIBuilder.Label(bossBar.transform, "Name", "", 24, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(560, 28), true, FontStyle.Bold);
            var bossBack = UIBuilder.Img(bossBar.transform, "Back", UIBuilder.buttonSprite, new Color(0.12f, 0.08f, 0.1f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(544, 22), true);
            hud.bossFill = UIBuilder.Img(bossBack.transform, "Fill", ArtGenerator.S("Center/pixel"), new Color(0.86f, 0.2f, 0.24f), new Vector2(0f, 0.5f), new Vector2(6, 0), new Vector2(532, 12));
            hud.bossFillWidth = 532f;
            bossBar.gameObject.SetActive(false);
            // equipment being worn: a row of small icons next to it
            hud.wornIcons = new Image[5];
            for (int wi = 0; wi < hud.wornIcons.Length; wi++)
            {
                hud.wornIcons[wi] = UIBuilder.Img(hudGo.transform, "Worn" + wi, null, Color.white, new Vector2(0f, 1f), new Vector2(144 + wi * 54, -146), new Vector2(46, 46));
                hud.wornIcons[wi].enabled = false;
            }

            // Rows are placed by their vertical centre (pivot y = 0.5) so everything stays inside the frame.
            var tr = UIBuilder.Panel(hudGo.transform, "TopRight", new Vector2(1f, 1f), new Vector2(-16, -16), new Vector2(460, 150));
            var rowPivot = new Vector2(1f, 0.5f);
            hud.stageText = UIBuilder.Label(tr.transform, "Stage", "1-1", 28, gold, TextAnchor.MiddleRight, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.Place(hud.stageText.rectTransform, new Vector2(1f, 1f), new Vector2(-28, -38), new Vector2(404, 36), rowPivot);
            // a long area name shrinks to stay on its one line inside the frame instead of wrapping over the clock
            hud.stageText.enableAutoSizing = true; hud.stageText.fontSizeMin = 14; hud.stageText.fontSizeMax = 28;
            hud.stageText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            var clock = UIBuilder.Img(tr.transform, "Clock", ArtGenerator.S("UI/icon_clock"), Color.white, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UIBuilder.Place(clock.rectTransform, new Vector2(1f, 1f), new Vector2(-178, -80), new Vector2(34, 34), rowPivot);
            hud.timeText = UIBuilder.Label(tr.transform, "Time", "00:00.0", 36, Color.white, TextAnchor.MiddleRight, new Vector2(1f, 1f), Vector2.zero, Vector2.zero, true, FontStyle.Bold);
            UIBuilder.Place(hud.timeText.rectTransform, new Vector2(1f, 1f), new Vector2(-28, -80), new Vector2(140, 44), rowPivot);
            hud.corners = new[] { bar.gameObject, gearBox, tr.gameObject };
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
            hud.compassRoot = comp;

            var prompt = UIBuilder.Panel(hudGo.transform, "Prompt", new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1500, 80));
            hud.promptPanel = prompt.gameObject;
            hud.promptText = UIBuilder.Label(prompt.transform, "Text", "", 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 76));

            var banner = UIBuilder.Node(hudGo.transform, "Banner");
            UIBuilder.Place(UIBuilder.Rect(banner), new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1600, 160));
            hud.bannerGroup = banner.AddComponent<CanvasGroup>();
            hud.bannerTitle = UIBuilder.Label(banner.transform, "Title", "", 58, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(1600, 80), true, FontStyle.Bold);
            hud.bannerSub = UIBuilder.Label(banner.transform, "Sub", "", 30, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -128), new Vector2(1600, 50));

            // area introduction card (start of every level)
            var intro = UIBuilder.Node(hudGo.transform, "AreaIntro");
            UIBuilder.Place(UIBuilder.Rect(intro), new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(1600, 220));
            hud.introGroup = intro.AddComponent<CanvasGroup>();
            hud.introGroup.alpha = 0f; hud.introGroup.blocksRaycasts = false; hud.introGroup.interactable = false;
            hud.introName = UIBuilder.Label(intro.transform, "Name", "", 88, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(1600, 110), true, FontStyle.Bold);
            hud.introName.fontSharedMaterial = UIBuilder.matTitle;
            var rule = UIBuilder.Img(intro.transform, "Rule", ArtGenerator.S("Center/pixel"), new Color(1f, 0.84f, 0.38f, 0.9f), new Vector2(0.5f, 1f), new Vector2(0, -138), new Vector2(560, 4), false);
            rule.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            hud.introRule = rule.rectTransform;
            var gem = UIBuilder.Img(intro.transform, "Gem", ArtGenerator.S("Center/pixel"), new Color(1f, 0.9f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -138), new Vector2(16, 16), false);
            gem.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            hud.introGem = gem.rectTransform;
            hud.introRuleWidth = 560f;

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
            // Compact: no panel, no title, no description. A row of slots over the dimmed game, arrows either side
            // of the chosen one, its name underneath and one small line saying how to move.
            var row = UIBuilder.Node(wroot.transform, "Row");
            UIBuilder.Place(UIBuilder.Rect(row), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(900, 130));
            wheel.row = (RectTransform)row.transform;
            var slot = UIBuilder.Img(row.transform, "SlotTemplate", UIBuilder.Slot, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112), true);
            UIBuilder.Img(slot.transform, "Icon", ArtGenerator.S("UI/icon_bomb"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(80, 80));
            UIBuilder.Label(slot.transform, "Ammo", "4", 28, Color.white, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-12, 8), new Vector2(90, 34), true, FontStyle.Bold);
            wheel.slotTemplate = slot.gameObject;
            var arrowL = UIBuilder.Label(row.transform, "ArrowLeft", "\u25C4", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 40), true);
            var arrowR = UIBuilder.Label(row.transform, "ArrowRight", "\u25BA", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 40), true);
            wheel.arrowLeft = arrowL.rectTransform; wheel.arrowRight = arrowR.rectTransform;
            wheel.spacing = 132f;
            wheel.nameText = UIBuilder.Label(wroot.transform, "Name", "", 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1200, 56), true, FontStyle.Bold);
            wheel.descText = null;
            wheel.hintText = UIBuilder.Label(wroot.transform, "Hint", "", 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -116), new Vector2(600, 34), true);
            wheel.input = reader;
            wheel.director = dir;
            dir.gearWheel = wheel;

            // equipment screen (Tab / Select)
            var invGo = UIBuilder.Node(t, "Inventory");
            UIBuilder.Stretch(UIBuilder.Rect(invGo));
            var inv = invGo.AddComponent<InventoryScreen>();
            var iroot = UIBuilder.Img(invGo.transform, "Root", null, new Color(0f, 0f, 0f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIBuilder.Stretch(iroot.rectTransform);
            inv.root = iroot.gameObject;
            var ipanel = UIBuilder.Panel(iroot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1040, 680));
            UIBuilder.Label(ipanel.transform, "Title", "EQUIPMENT", 40, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(800, 50), true, FontStyle.Bold);
            var igrid = UIBuilder.Node(ipanel.transform, "Grid");
            UIBuilder.Place(UIBuilder.Rect(igrid), new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(900, 10), new Vector2(0.5f, 0.5f));
            inv.grid = (RectTransform)igrid.transform;
            var cell = UIBuilder.Img(igrid.transform, "CellTemplate", UIBuilder.Slot, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116, 116), true);
            UIBuilder.Img(cell.transform, "Icon", ArtGenerator.S("Sprites/Items/springboots"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(84, 84));
            UIBuilder.Label(cell.transform, "Slot", "FEET", 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, -26), new Vector2(150, 24), false);
            var wornMark = UIBuilder.Img(cell.transform, "Worn", UIBuilder.buttonSprite, new Color(1f, 0.82f, 0.3f), new Vector2(1f, 1f), new Vector2(-8, -8), new Vector2(22, 22), true);
            inv.cellTemplate = cell.gameObject;
            inv.emptyText = UIBuilder.Label(ipanel.transform, "Empty", "Nothing to wear yet. Shops sell boots, coats and charms.", 26, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(900, 60));
            inv.nameText = UIBuilder.Label(ipanel.transform, "Name", "", 32, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(940, 44), true, FontStyle.Bold);
            inv.descText = UIBuilder.Label(ipanel.transform, "Desc", "", 24, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter, new Vector2(0.5f, 0f), new Vector2(0, 86), new Vector2(940, 60));
            inv.hintText = UIBuilder.Label(ipanel.transform, "Hint", "", 22, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(960, 34), false);
            inv.input = reader;
            inv.director = dir;
            dir.inventory = inv;

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
            var pmain = UIBuilder.Panel(proot.transform, "MainPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 746));
            UIBuilder.Label(pmain.transform, "Title", "PAUSED", 56, gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -55), new Vector2(700, 80), true, FontStyle.Bold);
            Vector2 pb = new Vector2(480, 64);
            pm.resumeButton = UIBuilder.Button(pmain.transform, "Resume", "RESUME", new Vector2(0.5f, 1f), new Vector2(0, -130), pb);
            pm.settingsButton = UIBuilder.Button(pmain.transform, "Settings", "SETTINGS", new Vector2(0.5f, 1f), new Vector2(0, -204), pb);
            pm.restartButton = UIBuilder.Button(pmain.transform, "Restart", "NEW RUN", new Vector2(0.5f, 1f), new Vector2(0, -278), pb);
            pm.retrySeedButton = UIBuilder.Button(pmain.transform, "RetrySeed", "RESTART SAME SEED", new Vector2(0.5f, 1f), new Vector2(0, -352), pb);
            pm.menuButton = UIBuilder.Button(pmain.transform, "Menu", "MAIN MENU", new Vector2(0.5f, 1f), new Vector2(0, -426), pb);
            pm.quitButton = UIBuilder.Button(pmain.transform, "Quit", "QUIT GAME", new Vector2(0.5f, 1f), new Vector2(0, -500), pb);
            pm.infoText = UIBuilder.Label(pmain.transform, "Info", "", 22, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(720, 90));
            pm.mainPanel = pmain.gameObject;
            pm.keysPanel = UIBuilder.KeysPanel(proot.transform);
            pm.settingsPanel = UIBuilder.Settings(proot.transform);
            dir.pauseMenu = pm;

            // results
            var resGo = UIBuilder.Node(t, "Results");
            UIBuilder.Stretch(UIBuilder.Rect(resGo));
            var rs = resGo.AddComponent<ResultsScreen>();
            var rroot = UIBuilder.Dimmer(resGo.transform, "Root");
            rs.root = rroot.gameObject;
            // Layout, top to bottom: big title, how it ended, how far it got, a rule, four stat tiles,
            // then (bottom up) the buttons, the seed, lifetime records and any "new best" news.
            var rp = UIBuilder.Panel(rroot.transform, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 800));
            var top = new Vector2(0.5f, 1f); var bottom = new Vector2(0.5f, 0f);
            rs.title = UIBuilder.Label(rp.transform, "Headline", "YOU DIED", 84, Color.red, TextAnchor.MiddleCenter, top, new Vector2(0, -52), new Vector2(1000, 100), true, FontStyle.Bold);
            // a long headline ("TUTORIAL COMPLETE") shrinks to stay on its one line instead of wrapping over the text below
            rs.title.enableAutoSizing = true; rs.title.fontSizeMax = rs.title.fontSize; rs.title.fontSizeMin = 40; rs.title.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            rs.cause = UIBuilder.Label(rp.transform, "Cause", "", 30, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter, top, new Vector2(0, -158), new Vector2(960, 40));
            rs.reached = UIBuilder.Label(rp.transform, "Reached", "", 26, gold, TextAnchor.MiddleCenter, top, new Vector2(0, -204), new Vector2(960, 34), true, FontStyle.Bold);
            UIBuilder.Img(rp.transform, "Rule", ArtGenerator.S("Center/pixel"), new Color(1f, 0.84f, 0.38f, 0.45f), top, new Vector2(0, -256), new Vector2(820, 3));
            var statsGo = UIBuilder.Node(rp.transform, "Stats");
            UIBuilder.Place(UIBuilder.Rect(statsGo), top, new Vector2(0, -282), new Vector2(960, 150));
            rs.statsRoot = statsGo;
            TMPro.TMP_Text Tile(string label, float x, Color valueColor, string icon)
            {
                var tile = UIBuilder.Img(statsGo.transform, label, UIBuilder.Slot, new Color(0.62f, 0.5f, 0.3f), new Vector2(0.5f, 1f), new Vector2(x, 0), new Vector2(222, 150), true);
                float lx = 0f;
                if (icon != null)
                {
                    UIBuilder.Img(tile.transform, "Icon", ArtGenerator.S(icon), Color.white, new Vector2(0.5f, 1f), new Vector2(-42, -16), new Vector2(30, 30));
                    lx = 16f;
                }
                UIBuilder.Label(tile.transform, "Label", label, 20, new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(lx, -16), new Vector2(160, 30), false, FontStyle.Bold);
                return UIBuilder.Label(tile.transform, "Value", "0", 46, valueColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(210, 64), true, FontStyle.Bold);
            }
            rs.statGold = Tile("GOLD", -357, gold, "UI/icon_gold");
            rs.statTime = Tile("TIME", -119, Color.white, "UI/icon_clock");
            rs.statKills = Tile("KILLS", 119, Color.white, null);
            rs.statLevels = Tile("LEVELS", 357, Color.white, null);
            rs.body = UIBuilder.Label(rp.transform, "Body", "", 30, Color.white, TextAnchor.UpperCenter, top, new Vector2(0, -190), new Vector2(880, 240));
            rs.news = UIBuilder.Label(rp.transform, "News", "", 30, gold, TextAnchor.MiddleCenter, bottom, new Vector2(0, 286), new Vector2(1000, 44), true, FontStyle.Bold);
            rs.records = UIBuilder.Label(rp.transform, "Records", "", 22, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, bottom, new Vector2(0, 204), new Vector2(1000, 66));
            rs.seedText = UIBuilder.Label(rp.transform, "Seed", "", 20, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, bottom, new Vector2(0, 160), new Vector2(1000, 30), false);
            rs.newRunButton = UIBuilder.Button(rp.transform, "NewRun", "NEW RUN", bottom, new Vector2(-340, 60), new Vector2(320, 76), out rs.newRunLabel);
            rs.retrySeedButton = UIBuilder.Button(rp.transform, "RetrySeed", "SAME SEED", bottom, new Vector2(0, 60), new Vector2(320, 76));
            rs.menuButton = UIBuilder.Button(rp.transform, "Menu", "MAIN MENU", bottom, new Vector2(340, 60), new Vector2(320, 76));
            dir.results = rs;

            UIBuilder.menuStyle = false;
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
