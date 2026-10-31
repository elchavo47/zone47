#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zone47.EditorTools
{
    [InitializeOnLoad]
    public static class Z47Setup
    {
        static string ROOT;   // e.g. Assets/Zone47

        static Z47Setup()
        {
            EditorApplication.delayCall += () =>
            {
                FindRoot();
                if (ROOT != null && !File.Exists(ROOT + "/Resources/Z47Refs.asset") && !SessionState.GetBool("z47_ran", false))
                {
                    SessionState.SetBool("z47_ran", true);
                    if (EditorUtility.DisplayDialog("Zone 47", "Zone 47 kurulumu yapılsın mı?\n(Modeller, malzemeler, animasyonlar, sahne ve iOS ayarları otomatik hazırlanır. 1-3 dakika sürebilir.)", "Kur", "Sonra"))
                        Run();
                }
                else AutoUpdate();
            };
            EditorApplication.playModeStateChanged += st => { if (st == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AutoUpdate; };
        }

        /// Bump this whenever a package brings new art/sounds or changes the setup code:
        /// Unity then re-runs the setup by itself once per PC (no more clicking "Kurulumu Yap").
        public const int SetupVersion = 22;
        static string VerKey => "z47_setupver_" + Application.dataPath.GetHashCode();

        static void AutoUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            FindRoot();
            if (ROOT == null || !File.Exists(ROOT + "/Resources/Z47Refs.asset")) return;
            if (EditorPrefs.GetInt(VerKey, 0) >= SetupVersion) return;
            Debug.Log("[Zone47] Yeni paket algılandı, kurulum otomatik çalışıyor (v" + SetupVersion + ")");
            Run();
        }

        static void FindRoot()
        {
            var g = AssetDatabase.FindAssets("Z47Setup t:Script");
            foreach (var id in g)
            {
                var p = AssetDatabase.GUIDToAssetPath(id);
                if (p.EndsWith("Editor/Z47Setup.cs")) { ROOT = p.Substring(0, p.Length - "/Editor/Z47Setup.cs".Length); return; }
            }
            ROOT = null;
        }

        [MenuItem("Zone47/Kurulumu Yap (Setup)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Zone 47", "Önce Play'i durdur (üstteki ▶ butonu), sonra Kurulumu Yap'ı tekrar çalıştır.", "Tamam");
                return;
            }
            FindRoot();
            if (ROOT == null) { Debug.LogError("[Zone47] Root folder not found"); return; }
            try
            {
                EnsureLayer("Viewmodel");
                EditorUtility.DisplayProgressBar("Zone 47", "Dokular ve sesler...", 0.05f);
                ConfigureTextures();
                ConfigureAudio();
                EditorUtility.DisplayProgressBar("Zone 47", "Malzemeler...", 0.2f);
                var mats = MakeMaterials();
                EditorUtility.DisplayProgressBar("Zone 47", "Karakterler...", 0.35f);
                var avatar = ConfigureCharacters(mats);
                EditorUtility.DisplayProgressBar("Zone 47", "Animasyonlar...", 0.5f);
                ConfigureAnimations(avatar);
                EditorUtility.DisplayProgressBar("Zone 47", "Animator...", 0.6f);
                var body = MakeBodyController();
                var arms = MakeArmsController();
                EditorUtility.DisplayProgressBar("Zone 47", "Silahlar...", 0.7f);
                var weapons = MakeWeapons(mats, out var grenade);
                var carbine = MakeCarbine();
                if (carbine != null) weapons = weapons.Concat(new[] { carbine }).ToArray();
                EditorUtility.DisplayProgressBar("Zone 47", "Harita...", 0.8f);
                var map = MakeMap(mats);
                var armsP = MakeArms(mats);
                EditorUtility.DisplayProgressBar("Zone 47", "Sahne ve ayarlar...", 0.9f);
                MakeRefs(mats, body, arms, weapons, grenade, map);
                var refsObj = AssetDatabase.LoadAssetAtPath<Z47Refs>(ROOT + "/Resources/Z47Refs.asset"); refsObj.armsPrefab = armsP; EditorUtility.SetDirty(refsObj);
                MakeScene();
                PlayerSetup();
                AssetDatabase.SaveAssets();
                EditorPrefs.SetInt(VerKey, SetupVersion);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Zone 47", "Kurulum tamam! ▶ Play'e basarak deneyebilirsin.\n\nEditörde: WASD yürü, sağ tık basılı tut + fare = bak, F = ateş, Q = nişan, R = şarjör, G = bomba, 1/2/3 silah, Space zıpla, C çömel, Z yat, X/V sola/sağa eğil.", "Tamam");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Zone 47", "Kurulumda hata: " + e.Message + "\nConsole ekranının görüntüsünü at.", "Tamam");
            }
        }

        static void EnsureLayer(string name)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++) if (layers.GetArrayElementAtIndex(i).stringValue == name) return;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue)) { el.stringValue = name; so.ApplyModifiedProperties(); return; }
            }
        }

        // ---------------------------------------------------------------- textures / audio
        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ROOT + "/Art" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                string n = Path.GetFileNameWithoutExtension(path).ToLower();
                bool normal = n.EndsWith("_normal");
                bool linear = n.EndsWith("_mask");
                bool ui = path.Contains("/UI/");
                bool fx = path.Contains("/FX/");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !linear && !normal;
                ti.mipmapEnabled = !ui;
                ti.alphaIsTransparency = fx || ui;
                ti.wrapMode = fx || ui ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                int max = n == "flags_atlas" ? 2048 : ui ? 1024 : path.Contains("/Characters/") || path.Contains("/Lobby/") ? 2048 : path.Contains("/Map/") ? 2048 : path.Contains("/FX/") ? 256 : 1024;
                ti.maxTextureSize = max;
                ti.anisoLevel = path.Contains("/Lobby/") || path.Contains("/Port/") ? 8 : 1;   // grazing-angle floors shimmer without it
                ti.textureCompression = TextureImporterCompression.Compressed;
                var ps = ti.GetPlatformTextureSettings("iPhone");
                ps.overridden = true; ps.maxTextureSize = max;
                ps.format = normal ? TextureImporterFormat.ASTC_5x5 : ui ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(ps);
                ti.SaveAndReimport();
            }
        }

        static void ConfigureAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ROOT + "/Art/Audio" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                if (ai == null) continue;
                bool longClip = path.Contains("music") || path.Contains("ambience") || path.Contains("rain");
                var s = ai.defaultSampleSettings;
                s.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                s.quality = 0.7f;
                ai.defaultSampleSettings = s;
                ai.forceToMono = path.Contains("step") || path.Contains("impact") || path.Contains("flyby");
                ai.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------- materials
        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Unlit => Shader.Find("Universal Render Pipeline/Unlit");
        static Shader Particles => Shader.Find("Universal Render Pipeline/Particles/Unlit");

        static T Load<T>(string rel) where T : Object => AssetDatabase.LoadAssetAtPath<T>(ROOT + "/" + rel);
        static Texture2D Tex(string rel) => Load<Texture2D>(rel);

        static Material SaveMat(Material m, string name)
        {
            Directory.CreateDirectory(ROOT + "/Materials");
            string p = ROOT + "/Materials/" + name + ".mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (old != null) { old.shader = m.shader; old.CopyPropertiesFromMaterial(m); old.shaderKeywords = m.shaderKeywords; old.renderQueue = m.renderQueue; EditorUtility.SetDirty(old); return old; }
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        static Material LitMetal(string name, Texture2D baseT, Texture2D maskT, Texture2D normalT, float smooth = 0.9f, Color? tint = null)
        {
            var m = new Material(Lit);
            m.SetFloat("_WorkflowMode", 1f);
            if (baseT) m.SetTexture("_BaseMap", baseT);
            m.SetColor("_BaseColor", tint ?? Color.white);
            if (maskT) { m.SetTexture("_MetallicGlossMap", maskT); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Metallic", 1f); }
            else m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            if (normalT) { m.SetTexture("_BumpMap", normalT); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            return SaveMat(m, name);
        }

        static Material LitSpec(string name, Texture2D baseT, Texture2D specT, Texture2D normalT, Texture2D emisT)
        {
            var m = new Material(Lit);
            m.SetFloat("_WorkflowMode", 0f);
            m.EnableKeyword("_SPECULAR_SETUP");
            if (baseT) m.SetTexture("_BaseMap", baseT);
            m.SetColor("_BaseColor", Color.white);
            if (specT) { m.SetTexture("_SpecGlossMap", specT); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            m.SetColor("_SpecColor", new Color(0.3f, 0.3f, 0.3f));
            m.SetFloat("_Smoothness", 0.75f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            if (normalT) { m.SetTexture("_BumpMap", normalT); m.EnableKeyword("_NORMALMAP"); }
            if (emisT) { m.SetTexture("_EmissionMap", emisT); m.SetColor("_EmissionColor", Color.white * 0.8f); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            return SaveMat(m, name);
        }

        static Material FXMat(string name, Texture2D t, bool additive, Color c, bool vertexColor)
        {
            var sh = Shader.Find(additive ? "Zone47/FXAdd" : "Zone47/FXAlpha");
            if (sh == null) throw new System.Exception("Zone47 FX shader bulunamadı (Zone47/Shaders klasörü eksik)");
            var m = new Material(sh);
            if (t) m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_VC", vertexColor ? 1f : 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
            return SaveMat(m, name);
        }

        static Material Transparent(Shader sh, string name, Texture2D t, bool additive, Color c)
        {
            var m = new Material(sh);
            if (t) m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            return SaveMat(m, name);
        }

        class Mats : Dictionary<string, Material> { }

        static Mats MakeMaterials()
        {
            var M = new Mats();
            string C = "Art/Characters/Textures/";
            M["Ch15_body"] = LitSpec("Swat_1", Tex(C + "Ch15_1001_base.jpg"), Tex(C + "Ch15_1001_spec.png"), Tex(C + "Ch15_1001_normal.png"), null);
            M["Ch15_body1"] = LitSpec("Swat_2", Tex(C + "Ch15_1002_base.jpg"), Tex(C + "Ch15_1002_spec.png"), Tex(C + "Ch15_1002_normal.png"), Tex(C + "Ch15_1002_emis.jpg"));
            M["Ch35_body"] = LitSpec("Gas_1", Tex(C + "Ch35_1001_base.jpg"), Tex(C + "Ch35_1001_spec.png"), Tex(C + "Ch35_1001_normal.png"), null);
            M["Ch35_body1"] = LitSpec("Gas_2", Tex(C + "Ch35_1002_base.jpg"), Tex(C + "Ch35_1002_spec.png"), Tex(C + "Ch35_1002_normal.png"), null);
            M["Ch35_body2"] = LitSpec("Gas_3", Tex(C + "Ch35_1003_base.jpg"), Tex(C + "Ch35_1003_spec.png"), Tex(C + "Ch35_1003_normal.png"), null);

            string W = "Art/Weapons/";
            foreach (var part in new[] { "Body", "Mag", "Scope", "Stock" })
                M["m4_" + part] = LitMetal("M4_" + part, Tex(W + "m4_" + part + "_base.png"), Tex(W + "m4_" + part + "_mask.png"), Tex(W + "m4_" + part + "_normal.png"));
            M["ak"] = LitMetal("AK47", Tex(W + "ak_base.png"), Tex(W + "ak_mask.png"), Tex(W + "ak_normal.png"));
            M["glock"] = LitMetal("Glock", Tex(W + "glock_base.png"), Tex(W + "glock_mask.png"), Tex(W + "glock_normal.png"));
            M["shotgun"] = LitMetal("Shotgun", Tex(W + "shotgun_base.png"), null, Tex(W + "shotgun_normal.png"), 0.45f);
            M["gren"] = LitMetal("Grenade", Tex(W + "gren_base.png"), Tex(W + "gren_mask.png"), Tex(W + "gren_normal.png"), 0.6f);
            for (int i = 0; i < 10; i++)
            {
                var b = Tex(W + "awp" + i + "_base.png");
                if (b == null) continue;
                M["awp" + i] = LitMetal("AWP_" + i, b, Tex(W + "awp" + i + "_mask.png"), Tex(W + "awp" + i + "_normal.png"));
            }

            M["map_build"] = LitMetal("Map_Buildings", Tex("Art/Map/Atlas_by_Hamza.jpg"), null, null, 0.12f);
            M["map_road"] = LitMetal("Map_Road", Tex("Art/Map/road.jpg"), null, null, 0.2f);
            M["map_ground"] = LitMetal("Map_Ground", Tex("Art/Map/ground.jpg"), null, null, 0.08f, new Color(0.92f, 0.88f, 0.8f));

            M["crate"] = LitMetal("Prop_Crate", Tex("Art/Props/crate.png"), null, null, 0.15f);
            M["concrete"] = LitMetal("Prop_Concrete", Tex("Art/Props/concrete.png"), null, null, 0.1f);
            M["sandbag"] = LitMetal("Prop_Sandbag", Tex("Art/Props/sandbag.png"), null, null, 0.05f);
            M["barrel"] = LitMetal("Prop_Barrel", Tex("Art/Props/barrel.png"), null, null, 0.35f);

            M["fx_flash"] = FXMat("FX_Flash", Tex("Art/FX/flash.png"), true, new Color(1f, 0.9f, 0.7f, 1f), false);
            M["fx_hole"] = FXMat("FX_Hole", Tex("Art/FX/hole.png"), false, Color.white, false);
            M["fx_smoke"] = FXMat("FX_Smoke", Tex("Art/FX/smoke.png"), false, Color.white, true);
            M["fx_spark"] = FXMat("FX_Spark", Tex("Art/FX/soft.png"), true, Color.white, true);
            M["fx_fire"] = FXMat("FX_Fire", Tex("Art/FX/smoke.png"), true, Color.white, true);
            M["fx_tracer"] = FXMat("FX_Tracer", Tex("Art/FX/soft.png"), true, Color.white, true);

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 2f); sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.35f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.55f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.32f, 0.28f, 0.25f));
            sky.SetFloat("_Exposure", 1.15f);
            M["sky"] = SaveMat(sky, "Skybox");
            AssetDatabase.SaveAssets();
            return M;
        }

        // ---------------------------------------------------------------- characters & animations
        static Avatar ConfigureCharacters(Mats M)
        {
            Avatar swatAvatar = null;
            foreach (var n in new[] { "SwatGuy", "GasMask" })
            {
                string p = ROOT + "/Art/Characters/" + n + ".fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var kv in M)
                    if (kv.Key.StartsWith(n == "SwatGuy" ? "Ch15_" : "Ch35_"))
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                mi.SaveAndReimport();
                if (n == "SwatGuy") swatAvatar = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().FirstOrDefault();
            }
            if (swatAvatar == null) throw new System.Exception("SwatGuy avatar oluşturulamadı");
            return swatAvatar;
        }

        static readonly string[] Loops = { "Rifle_Idle", "Walk_With_Rifle", "Rifle_Run", "Backwards_Rifle_Walk", "Strafe_Left", "Strafe_Right", "Rifle_Crouch_Walk", "Firing_Rifle" };

        static void ConfigureAnimations(Avatar src)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ROOT + "/Art/Animations" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                string n = Path.GetFileNameWithoutExtension(p);
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                mi.sourceAvatar = src;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importAnimation = true;
                var clips = mi.defaultClipAnimations;
                foreach (var c in clips)
                {
                    c.name = n;
                    c.loopTime = Loops.Contains(n);
                    c.lockRootRotation = true; c.keepOriginalOrientation = true;
                    c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                    c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
                }
                mi.clipAnimations = clips;
                mi.SaveAndReimport();
                if (FindClip(n) == null)
                {
                    // avatar copy failed for this file: let it build its own humanoid avatar
                    Debug.LogWarning("[Zone47] " + n + ": avatar kopyalanamadı, kendi avatarı oluşturuluyor");
                    mi = (ModelImporter)AssetImporter.GetAtPath(p);
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.sourceAvatar = null;
                    var c2 = mi.defaultClipAnimations;
                    foreach (var c in c2)
                    {
                        c.name = n; c.loopTime = Loops.Contains(n);
                        c.lockRootRotation = true; c.keepOriginalOrientation = true;
                        c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
                    }
                    if (c2.Length > 0) mi.clipAnimations = c2;
                    mi.SaveAndReimport();
                }
                if (FindClip(n) == null) Debug.LogWarning("[Zone47] Animasyon yok sayıldı: " + n);
            }
        }

        static AnimationClip FindClip(string n)
        {
            var p = ROOT + "/Art/Animations/" + n + ".fbx";
            return AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
        }

        static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>
        {
            { "Strafe_Left", "Walk_With_Rifle" }, { "Strafe_Right", "Walk_With_Rifle" }, { "Backwards_Rifle_Walk", "Walk_With_Rifle" },
            { "Rifle_Crouch_Walk", "Walk_With_Rifle" }, { "Firing_Rifle", "Rifle_Idle" }, { "Reloading", "Rifle_Idle" },
            { "Hit_Reaction", "Rifle_Idle" }, { "Walk_With_Rifle", "Rifle_Run" }, { "Rifle_Run", "Walk_With_Rifle" }
        };

        static AnimationClip Clip(string n)
        {
            var c = FindClip(n);
            if (c == null && Fallback.TryGetValue(n, out var f)) { Debug.LogWarning("[Zone47] " + n + " yerine " + f + " kullanılıyor"); c = FindClip(f); }
            if (c == null) throw new System.Exception("Animasyon bulunamadı: " + n);
            return c;
        }

        static AnimatorController MakeBodyController()
        {
            Directory.CreateDirectory(ROOT + "/Animation");
            string path = ROOT + "/Animation/Body.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var f in new[] { "MoveX", "MoveZ", "Speed" }) ctrl.AddParameter(f, AnimatorControllerParameterType.Float);
            foreach (var b in new[] { "Crouch", "Aim", "Dead" }) ctrl.AddParameter(b, AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Fire", "Reload", "Hit", "Die" }) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out var bt, 0);
            bt.blendType = BlendTreeType.FreeformDirectional2D;
            bt.blendParameter = "MoveX"; bt.blendParameterY = "MoveZ";
            bt.useAutomaticThresholds = false;
            bt.AddChild(Clip("Rifle_Idle"), new Vector2(0, 0));
            bt.AddChild(Clip("Walk_With_Rifle"), new Vector2(0, 0.5f));
            bt.AddChild(Clip("Rifle_Run"), new Vector2(0, 1f));
            bt.AddChild(Clip("Backwards_Rifle_Walk"), new Vector2(0, -0.5f));
            bt.AddChild(Clip("Strafe_Left"), new Vector2(-0.5f, 0));
            bt.AddChild(Clip("Strafe_Right"), new Vector2(0.5f, 0));
            sm.defaultState = loco;

            var crouch = sm.AddState("Crouch");
            crouch.motion = Clip("Rifle_Crouch_Walk");
            crouch.speedParameterActive = true; crouch.speedParameter = "Speed";
            var t1 = loco.AddTransition(crouch); t1.hasExitTime = false; t1.duration = 0.2f; t1.AddCondition(AnimatorConditionMode.If, 0, "Crouch");
            var t2 = crouch.AddTransition(loco); t2.hasExitTime = false; t2.duration = 0.2f; t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");

            var die = sm.AddState("Die"); die.motion = Clip("Dying"); die.speed = 1.7f;
            var td = sm.AddAnyStateTransition(die); td.hasExitTime = false; td.duration = 0.1f; td.canTransitionToSelf = false; td.AddCondition(AnimatorConditionMode.If, 0, "Die");
            var tr = die.AddTransition(loco); tr.hasExitTime = false; tr.duration = 0.05f; tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

            // upper body
            var mask = new AvatarMask();
            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                             || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            AssetDatabase.CreateAsset(mask, ROOT + "/Animation/UpperBody.mask");

            ctrl.AddLayer("Upper");
            var layers = ctrl.layers;
            layers[1].avatarMask = mask; layers[1].defaultWeight = 1f; layers[1].blendingMode = AnimatorLayerBlendingMode.Override; layers[1].iKPass = true;   // bots: hands on the gun via IK
            ctrl.layers = layers;
            var us = ctrl.layers[1].stateMachine;
            var empty = us.AddState("Empty"); us.defaultState = empty;
            var aim = us.AddState("Aim"); aim.motion = Clip("Firing_Rifle");
            var rel = us.AddState("Reload"); rel.motion = Clip("Reloading");
            var hit = us.AddState("Hit"); hit.motion = Clip("Hit_Reaction");

            var a1 = empty.AddTransition(aim); a1.hasExitTime = false; a1.duration = 0.15f; a1.AddCondition(AnimatorConditionMode.If, 0, "Aim"); a1.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var a2 = aim.AddTransition(empty); a2.hasExitTime = false; a2.duration = 0.25f; a2.AddCondition(AnimatorConditionMode.IfNot, 0, "Aim");
            var a3 = aim.AddTransition(empty); a3.hasExitTime = false; a3.duration = 0.05f; a3.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            foreach (var from in new[] { empty, aim })
            {
                var r = from.AddTransition(rel); r.hasExitTime = false; r.duration = 0.1f; r.AddCondition(AnimatorConditionMode.If, 0, "Reload"); r.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var h = from.AddTransition(hit); h.hasExitTime = false; h.duration = 0.05f; h.AddCondition(AnimatorConditionMode.If, 0, "Hit"); h.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            var rb = rel.AddTransition(empty); rb.hasExitTime = true; rb.exitTime = 0.92f; rb.duration = 0.15f;
            var hb = hit.AddTransition(empty); hb.hasExitTime = true; hb.exitTime = 0.6f; hb.duration = 0.2f;
            var hd = hit.AddTransition(empty); hd.hasExitTime = false; hd.duration = 0.05f; hd.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            var rd = rel.AddTransition(empty); rd.hasExitTime = false; rd.duration = 0.05f; rd.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static AnimatorController MakeArmsController()
        {
            string path = ROOT + "/Animation/Arms.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var layers = ctrl.layers; layers[0].iKPass = true; ctrl.layers = layers;
            var st = ctrl.layers[0].stateMachine.AddState("Hold");
            st.motion = Clip("Rifle_Idle");
            ctrl.layers[0].stateMachine.defaultState = st;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ---------------------------------------------------------------- weapons
        static readonly Dictionary<string, float> MuzzleDist = new Dictionary<string, float> { { "m4", 0.5504f }, { "ak", 0.6517f }, { "awp", 0.8366f }, { "glock", 0.178f }, { "shotgun", 0.58f } };

        static GameObject Prefab(GameObject root, string name)
        {
            Directory.CreateDirectory(ROOT + "/Prefabs");
            var p = PrefabUtility.SaveAsPrefabAsset(root, ROOT + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return p;
        }

        static void PrepModel(string path)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();
        }

        static WeaponDef[] MakeWeapons(Mats M, out GameObject grenade)
        {
            var list = new List<WeaponDef>();
            var weaponIds = new List<string> { "m4", "ak", "awp", "glock" };
            if (File.Exists(ROOT + "/Art/Weapons/shotgun.fbx")) weaponIds.Add("shotgun");     // "Shotgun" by Pieter Ferreira (CC BY)
            foreach (var id in weaponIds)
            {
                string path = ROOT + "/Art/Weapons/" + id + ".fbx";
                PrepModel(path);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var model = (GameObject)Object.Instantiate(asset);
                model.name = "Model";
                var root = new GameObject("W_" + id);
                model.transform.SetParent(root.transform, false);

                Transform pg = Util.FindDeep(model.transform, "PT_grip"), pm = Util.FindDeep(model.transform, "PT_muzzle"),
                          pf = Util.FindDeep(model.transform, "PT_fore"), pt = Util.FindDeep(model.transform, "PT_top");
                if (pg == null || pm == null) throw new System.Exception(id + ": PT_ noktaları yok");

                // scale so the grip→muzzle distance is real-world
                float dist = Vector3.Distance(pg.position, pm.position);
                if (dist > 1e-4f) model.transform.localScale *= MuzzleDist[id] / dist;

                Vector3 g = pg.position, m = pm.position;
                Vector3 fwd = (m - g).normalized;
                Vector3 up = Vector3.ProjectOnPlane((pt != null ? pt.position : g + Vector3.up) - g, fwd).normalized;
                if (up.sqrMagnitude < 0.1f) up = Vector3.up;
                var rot = Quaternion.LookRotation(fwd, up);
                var inv = Quaternion.Inverse(rot);
                model.transform.localRotation = inv * model.transform.localRotation;
                model.transform.localPosition = inv * (model.transform.localPosition - g);

                var def = new WeaponDef { id = id };
                def.muzzlePoint = root.transform.InverseTransformPoint(pm.position);
                def.forePoint = pf != null ? root.transform.InverseTransformPoint(pf.position) : def.muzzlePoint * 0.5f;

                // materials
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                {
                    string n = r.gameObject.name;
                    Material mat = null;
                    if (id == "m4") M.TryGetValue("m4_" + n, out mat);
                    else if (id == "awp") M.TryGetValue("awp" + n.Replace("AWP", ""), out mat);
                    else M.TryGetValue(id, out mat);
                    if (mat != null) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
                float top = 0f;
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                {
                    var b = r.bounds;
                    top = Mathf.Max(top, root.transform.InverseTransformPoint(b.max).y);
                }
                def.topY = id == "m4" ? top - 0.03f : top - 0.012f;
                foreach (var p in new[] { pg, pm, pf, pt }) if (p != null) Object.DestroyImmediate(p.gameObject);
                if (id == "m4") MountHolo(root, model, def);

                Stats(def);
                def.prefab = Prefab(root, "W_" + id);
                list.Add(def);
            }

            // grenade
            string gp = ROOT + "/Art/Weapons/gren.fbx";
            PrepModel(gp);
            var gm = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(gp));
            var groot = new GameObject("Grenade");
            gm.transform.SetParent(groot.transform, false);
            var gpg = Util.FindDeep(gm.transform, "PT_grip"); if (gpg != null) Object.DestroyImmediate(gpg.gameObject);
            Bounds gb = new Bounds(); bool first = true;
            foreach (var r in gm.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = M["gren"]; if (first) { gb = r.bounds; first = false; } else gb.Encapsulate(r.bounds); }
            float gs = gb.size.magnitude > 1e-4f ? 0.16f / gb.size.magnitude : 1f;
            gm.transform.localScale *= gs;
            gm.transform.localPosition = -gb.center * gs;
            var sc = groot.AddComponent<SphereCollider>(); sc.radius = 0.05f;
            var rb = groot.AddComponent<Rigidbody>(); rb.mass = 0.4f; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = 0.1f; rb.angularDamping = 0.8f;
            groot.AddComponent<Grenade>();
            grenade = Prefab(groot, "Grenade");
            return list.ToArray();
        }

        /// Replaces the M4 carry handle with a holographic sight; ADS looks through its lens at a glowing reticle.
        static void MountHolo(GameObject root, GameObject model, WeaponDef def)
        {
            string path = ROOT + "/Art/Weapons/holo.fbx";
            if (!File.Exists(path)) return;
            var handle = Util.FindDeep(model.transform, "Scope");
            if (handle != null) Object.DestroyImmediate(handle.gameObject);

            // The gun is aligned grip->muzzle, so the flat-top rail slopes ~7 deg down toward the muzzle.
            // Sample the rail top under the sight's footprint (1 cm bins, max height per bin) and fit a line,
            // then seat the sight on that line at the same tilt (a level sight sat on a rear bump and floated).
            const float z0 = 0.04f, zc = 0.085f; const int nb = 9;
            var top = new float[nb]; for (int i = 0; i < nb; i++) top[i] = float.MinValue;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    Vector3 p = root.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (Mathf.Abs(p.x) >= 0.02f || p.z < z0 || p.z >= z0 + nb * 0.01f) continue;
                    int k = Mathf.Clamp((int)((p.z - z0) / 0.01f), 0, nb - 1);
                    if (p.y > top[k]) top[k] = p.y;
                }
            }
            float sx = 0, sy = 0, sxx = 0, sxy = 0; int n = 0;
            for (int i = 0; i < nb; i++)
            {
                if (top[i] == float.MinValue) continue;
                float zz = z0 + i * 0.01f + 0.005f;
                sx += zz; sy += top[i]; sxx += zz * zz; sxy += zz * top[i]; n++;
            }
            float slope = 0f, rail = def.topY;
            if (n >= 2)
            {
                slope = (n * sxy - sx * sy) / (n * sxx - sx * sx);
                rail = (sy - slope * sx) / n + slope * zc;
            }
            float tilt = Mathf.Atan(-slope) * Mathf.Rad2Deg;     // + = nose down (Unity X rotation)
            Debug.Log("[Zone47] holo: ray y=" + rail.ToString("0.0000") + " egim=" + tilt.ToString("0.0") + " derece");

            PrepModel(path);
            var holo = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            holo.name = "HoloSight";
            Transform pb = Util.FindDeep(holo.transform, "PT_base"), pl = Util.FindDeep(holo.transform, "PT_lens"),
                      pfw = Util.FindDeep(holo.transform, "PT_fwd"), pup = Util.FindDeep(holo.transform, "PT_up");
            if (pb == null || pl == null || pfw == null || pup == null) { Object.DestroyImmediate(holo); return; }
            Vector3 b0 = pb.position;
            Vector3 f = (pfw.position - b0).normalized;
            var rot = Quaternion.LookRotation(f, Vector3.ProjectOnPlane(pup.position - b0, f).normalized);
            var inv = Quaternion.Inverse(rot);
            var holder = new GameObject("Sight").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(0f, rail - 0.005f, zc);   // sink 5 mm so the mount clamps onto the rail teeth
            holder.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            holo.transform.SetParent(holder, false);
            holo.transform.localRotation = inv * holo.transform.localRotation;
            holo.transform.localPosition = inv * (holo.transform.localPosition - b0);
            Vector3 lens = root.transform.InverseTransformPoint(pl.position);
            foreach (var t in new[] { pb, pl, pfw, pup }) Object.DestroyImmediate(t.gameObject);

            var body = LitMetal("Holo", Tex("Art/Weapons/holo_base.png"), Tex("Art/Weapons/holo_mask.png"), Tex("Art/Weapons/holo_normal.png"));
            var glass = FXMat("FX_HoloGlass", null, false, new Color(0.55f, 0.75f, 0.85f, 0.14f), false);
            foreach (var r in holo.GetComponentsInChildren<MeshRenderer>())
            {
                var m = r.gameObject.name.StartsWith("Lens") ? glass : body;
                r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // reticle "projected" just in front of the window
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Reticle";
            q.transform.SetParent(root.transform, false);
            q.transform.localPosition = lens + Vector3.forward * 0.003f;
            q.transform.localRotation = Quaternion.identity;
            q.transform.localScale = Vector3.one * 0.0135f;
            var rr = q.GetComponent<MeshRenderer>();
            rr.sharedMaterial = FXMat("FX_Reticle", Tex("Art/FX/reticle.png"), true, new Color(1f, 0.35f, 0.25f, 1f), false);
            rr.shadowCastingMode = ShadowCastingMode.Off;

            def.topY = lens.y;
            def.adsZ = 0.16f - lens.z;           // eye 16 cm behind the window: the sight frames the target instead of filling the screen
        }

        static void Stats(WeaponDef d)
        {
            var A = "Art/Audio/";
            switch (d.id)
            {
                case "m4":
                    d.displayName = "M4A1"; d.kind = WeaponKind.Rifle; d.damage = 26; d.fireInterval = 0.085f; d.automatic = true;
                    d.magSize = 30; d.reserve = 150; d.reloadTime = 2.3f; d.hipSpread = 2.4f; d.adsSpread = 0.35f; d.recoil = 0.9f; d.range = 180; d.adsFov = 52;
                    d.hipOffset = new Vector3(0.115f, -0.125f, 0.2f); d.shot = Load<AudioClip>(A + "m4_shot.wav"); break;
                case "ak":
                    d.displayName = "AK-47"; d.kind = WeaponKind.Rifle; d.damage = 31; d.fireInterval = 0.1f; d.automatic = true;
                    d.magSize = 30; d.reserve = 150; d.reloadTime = 2.5f; d.hipSpread = 2.8f; d.adsSpread = 0.45f; d.recoil = 1.2f; d.range = 170; d.adsFov = 52;
                    d.hipOffset = new Vector3(0.12f, -0.14f, 0.2f); d.shot = Load<AudioClip>(A + "ak_shot.wav"); break;
                case "awp":
                    d.displayName = "AWP"; d.kind = WeaponKind.Sniper; d.damage = 115; d.fireInterval = 1.25f; d.automatic = false;
                    d.magSize = 5; d.reserve = 25; d.reloadTime = 3.0f; d.hipSpread = 6f; d.adsSpread = 0.05f; d.recoil = 3.5f; d.range = 320; d.adsFov = 18; d.scope = true;
                    d.hipOffset = new Vector3(0.115f, -0.13f, 0.17f); d.shot = Load<AudioClip>(A + "awp_shot.wav"); break;
                case "glock":
                    d.displayName = "GLOCK 17"; d.kind = WeaponKind.Pistol; d.damage = 22; d.fireInterval = 0.14f; d.automatic = false;
                    d.magSize = 15; d.reserve = 75; d.reloadTime = 1.6f; d.hipSpread = 1.6f; d.adsSpread = 0.5f; d.recoil = 1.6f; d.range = 90; d.adsFov = 60;
                    d.hipOffset = new Vector3(0.1f, -0.11f, 0.26f); d.shot = Load<AudioClip>(A + "glock_shot.wav"); break;
                case "shotgun":
                    d.displayName = Settings.Turkish ? "POMPALI" : "SHOTGUN"; d.kind = WeaponKind.Shotgun; d.damage = 15; d.pellets = 8; d.fireInterval = 0.9f; d.automatic = false;
                    d.magSize = 7; d.reserve = 35; d.reloadTime = 2.6f; d.hipSpread = 5.5f; d.adsSpread = 3.6f; d.recoil = 3.4f; d.range = 55; d.adsFov = 60;
                    d.falloffStart = 7f; d.falloffEnd = 32f; d.falloffMin = 0.15f;          // deadly up close, weak at range
                    d.hipOffset = new Vector3(0.12f, -0.135f, 0.24f);
                    d.shot = Load<AudioClip>(A + "shotgun_shot.wav"); d.reloadClip = Load<AudioClip>(A + "shotgun_reload.wav"); d.pumpClip = Load<AudioClip>(A + "awp_bolt.wav"); break;
                case "carbine":
                    d.displayName = Settings.Turkish ? "KARABİNA" : "CARBINE"; d.kind = WeaponKind.Rifle; d.damage = 25; d.fireInterval = 0.078f; d.automatic = true;
                    d.magSize = 30; d.reserve = 150; d.reloadTime = 2.1f; d.hipSpread = 2.2f; d.adsSpread = 0.3f; d.recoil = 0.8f; d.range = 170; d.adsFov = 54;
                    d.hipOffset = new Vector3(0.115f, -0.165f, 0.21f);   // its tall red-dot riser sat too high at the M4's offset
                    d.shot = Load<AudioClip>(A + "carbine_shot.wav") ?? Load<AudioClip>(A + "m4_shot.wav"); break;
            }
        }

        // ---------------------------------------------------------------- animated carbine (DJMaesen "fps animated carbine")
        static ModelImporterClipAnimation CA(string n, float a, float b, bool loop) =>
            new ModelImporterClipAnimation { name = n, firstFrame = a, lastFrame = b, loopTime = loop, wrapMode = loop ? WrapMode.Loop : WrapMode.Once };

        /// Player-only 4th weapon with its own hand-made arms + animations (legacy Animation: cheap on mobile).
        /// The source take is one timeline; it is split into clips here (30 fps frame numbers).
        static WeaponDef MakeCarbine()
        {
            string path = ROOT + "/Art/Carbine/carbine.fbx";
            if (!File.Exists(path)) return null;
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.None;   // plays like the M4 now: no clips, our IK arms hold it
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();

            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.name = "Model";
            var root = new GameObject("W_carbine");
            model.transform.SetParent(root.transform, false);
            Transform pg = Util.FindDeep(model.transform, "PT_grip"), pm = Util.FindDeep(model.transform, "PT_muzzle"), pt = Util.FindDeep(model.transform, "PT_top"),
                      pf = Util.FindDeep(model.transform, "PT_fore"), pl = Util.FindDeep(model.transform, "PT_lens"), pfw = Util.FindDeep(model.transform, "PT_fwd");
            if (pg == null || pm == null || pt == null || pl == null || pfw == null) throw new System.Exception("carbine: PT_ noktalari yok");
            Vector3 g = pg.position, fwd = (pfw.position - g).normalized;
            var rot = Quaternion.LookRotation(fwd, Vector3.ProjectOnPlane(pt.position - g, fwd).normalized);
            var inv = Quaternion.Inverse(rot);
            model.transform.localRotation = inv * model.transform.localRotation;
            model.transform.localPosition = inv * (model.transform.localPosition - g);
            // PT_grip sat at the palm centre; the IK targets expect the top of the pistol grip -> root 4.5 cm higher
            model.transform.localPosition += new Vector3(0f, -0.045f, 0f);
            // gun only: the bundled arms/skeleton go (our FPS arms hold it, same as the other weapons)
            foreach (var n in new[] { "armmesh", "Null", "L_arm_Pole", "R_arm_Pole" }) { var t = Util.FindDeep(model.transform, n); if (t != null) Object.DestroyImmediate(t.gameObject); }
            foreach (var an in model.GetComponentsInChildren<Animation>()) Object.DestroyImmediate(an);

            var def = new WeaponDef { id = "carbine", animated = false };
            def.muzzlePoint = root.transform.InverseTransformPoint(pm.position);
            def.forePoint = new Vector3(0f, 0.038f, 0.3f);   // underside of the handguard (measured in Blender), like the M4's fore point
            Vector3 lens = root.transform.InverseTransformPoint(pl.position);
            def.topY = lens.y;                        // eye exactly on the lens centre
            def.adsZ = 0.2f - lens.z;                 // eye 20 cm behind the red dot
            foreach (var p in new[] { pg, pm, pt, pf, pl, pfw }) if (p != null) Object.DestroyImmediate(p.gameObject);

            var gunMat = LitMetal("Carbine", Tex("Art/Carbine/carbine_base.png"), Tex("Art/Carbine/carbine_mask.png"), Tex("Art/Carbine/carbine_normal.png"));
            var armMat = LitMetal("CarbineArms", Tex("Art/Carbine/farms_base.png"), Tex("Art/Carbine/farms_mask.png"), Tex("Art/Carbine/farms_normal.png"));
            // the model's lens texture had its dot off-centre (aim and impact disagreed) -> plain glass + our own centred dot
            var dotMat = FXMat("FX_CarbineGlass", null, true, new Color(0.10f, 0.13f, 0.17f, 1f), false);   // additive blue-ish coating sheen, never darkens
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                string n = r.gameObject.name;
                var m = n == "armmesh" ? armMat : n == "lens" ? dotMat : gunMat;
                r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            var dq = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(dq.GetComponent<Collider>());
            dq.name = "ReticleADS";                    // shown only while aiming (player toggles it)
            dq.transform.SetParent(root.transform, false);
            dq.transform.localPosition = new Vector3(0f, def.topY, lens.z + 0.004f);   // exactly on the eye line used while aiming
            dq.transform.localRotation = Quaternion.identity;
            dq.transform.localScale = Vector3.one * 0.007f;
            var dr = dq.GetComponent<MeshRenderer>();
            dr.sharedMaterial = FXMat("FX_CarbineReticle", Tex("Art/FX/reddot_reticle.png"), true, new Color(1f, 0.15f, 0.1f, 1f), false);
            dr.shadowCastingMode = ShadowCastingMode.Off;
            Stats(def);
            def.prefab = Prefab(root, "W_carbine");
            return def;
        }

        // ---------------------------------------------------------------- first-person arms
        static GameObject MakeArms(Mats M)
        {
            string path = ROOT + "/Art/Arms/fp_arms.fbx";
            if (!File.Exists(path)) { Debug.LogError("[Zone47] " + path + " bulunamadi: FPS kollar kurulmadi."); return null; }
            var mat = LitMetal("FPArms", Tex("Art/Arms/arms_base.jpg"), Tex("Art/Arms/arms_mask.png"), Tex("Art/Arms/arms_normal.png"), 1f);
            mat.SetFloat("_Metallic", 0f);
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            // Generic rig: keeps the mesh skinned to the bones so the IK actually bends the arms
            // (with "None" the mesh stays frozen in its rest pose, which sits below the camera = invisible)
            mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.name = "Model";
            foreach (var an in model.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(an);   // bones are driven by our IK only
            if (model.GetComponentInChildren<SkinnedMeshRenderer>() == null) Debug.LogError("[Zone47] fp_arms: SkinnedMeshRenderer yok, kollar bukulemez.");
            var root = new GameObject("FPArms");
            model.transform.SetParent(root.transform, false);
            Transform pe = Util.FindDeep(model.transform, "PT_eye"), pf = Util.FindDeep(model.transform, "PT_fwd"), pu = Util.FindDeep(model.transform, "PT_up");
            if (pe == null || pf == null || pu == null) throw new System.Exception("fp_arms: PT_ noktalari yok");
            Vector3 e = pe.position;
            var rot = Quaternion.LookRotation((pf.position - e).normalized, Vector3.ProjectOnPlane(pu.position - e, (pf.position - e).normalized).normalized);
            var inv = Quaternion.Inverse(rot);
            model.transform.localRotation = inv * model.transform.localRotation;
            model.transform.localPosition = inv * (model.transform.localPosition - e);
            foreach (var t in new[] { pe, pf, pu }) Object.DestroyImmediate(t.gameObject);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return Prefab(root, "FPArms");
        }

        // ---------------------------------------------------------------- map
        /// Lobby backdrop: photogrammetry scan (lighting is baked into the photos -> unlit material).
        /// Cropped to the lobby camera's view in Blender; PT_cam/PT_look/PT_soldier/PT_face mark the shot.
        static GameObject MakeLobby()
        {
            string path = ROOT + "/Art/Lobby/fort.fbx";
            if (!File.Exists(path)) return null;
            PrepModel(path);
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.name = "Lobby";
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                string tag = r.gameObject.name.Contains("u2") ? "u2_v1" : "u1_v1";
                var m = new Material(Unlit);
                m.SetTexture("_BaseMap", Tex("Art/Lobby/fort_" + tag + ".png"));
                m.SetColor("_BaseColor", new Color(1f, 0.95f, 0.88f));      // warm it toward the game's sunset grade
                r.sharedMaterial = SaveMat(m, "Lobby_" + tag);
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            }
            Directory.CreateDirectory(ROOT + "/Prefabs");
            var p = PrefabUtility.SaveAsPrefabAsset(model, ROOT + "/Prefabs/Lobby.prefab");
            Object.DestroyImmediate(model);
            return p;
        }

        /// Second map: port container yard (built procedurally in Blender, procedural textures -> fully ours).
        static GameObject MakePort()
        {
            string path = ROOT + "/Art/Port/port.fbx";
            if (!File.Exists(path)) return null;
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.addCollider = true; mi.isReadable = true;
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();
            string P = "Art/Port/";
            System.Func<string, string, float, Color, Material> PM = (name, tex, sm, tint) =>
                LitMetal("Port_" + name, Tex(P + tex + "_base.png"), Tex(P + tex + "_mask.png"), Tex(P + tex + "_normal.png"), sm, tint);
            var asphalt = PM("Asphalt", "asphalt", 1f, Color.white);
            var concrete = PM("Concrete", "concrete", 1f, Color.white);
            var floor = PM("Floor", "concrete", 1f, new Color(0.78f, 0.78f, 0.76f));
            var shed = PM("Shed", "corrugated", 1f, new Color(0.52f, 0.58f, 0.62f));
            var paint = LitMetal("Port_Paint", Tex(P + "paint_base.png"), null, null, 0.3f);
            var map = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            map.name = "Port_Map";
            foreach (var r in map.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.gameObject.name;
                Material m = n == "Ground" ? asphalt : n.StartsWith("Ground_") ? floor : n.StartsWith("Shed") ? shed : n.StartsWith("Paint") ? paint : concrete;
                r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = n.StartsWith("Paint") || n == "Ground" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = true;
                r.gameObject.isStatic = true;
                if (n.StartsWith("Paint")) foreach (var col in r.GetComponents<Collider>()) Object.DestroyImmediate(col);   // floor paint must not block
            }

            // real models placed on the Blender markers: CNT_<colour>_n_r<yaw>, PAL_<type>_n_r<yaw>, FORK_n_r<yaw>, CRANE_0_r<yaw>.
            // Yaw comes from the name (marker rotations carry Blender's axis conversion); models keep their importer rotation.
            var contObj = PortModelObj("container.fbx", "Container");
            var craneObj = PortModelObj("crane.fbx", "Crane");
            var forkObj = PortModelObj("forklift.fbx", "Forklift");
            var palRoot = PortModelObj("pallets.fbx", null);
            var contMats = new Dictionary<string, Material>();
            foreach (var c in new[] { "red", "blue", "green", "orange", "grey" })
                contMats[c] = LitMetal("Port_Container_" + c, Tex(P + "container_" + c + "_base.png"), Tex(P + "container_mask.png"), Tex(P + "container_normal.png"), 1f);
            var craneMat = LitMetal("Port_CraneModel", Tex(P + "crane_base.png"), Tex(P + "crane_mask.png"), Tex(P + "crane_normal.png"), 1f);
            var forkMat = LitMetal("Port_Forklift", Tex(P + "forklift_base.png"), Tex(P + "forklift_mask.png"), Tex(P + "forklift_normal.png"), 1f);
            var palMat = LitMetal("Port_Pallet", Tex(P + "pallet_base.png"), Tex(P + "pallet_mask.png"), Tex(P + "pallet_normal.png"), 1f);
            var props = new GameObject("Props").transform; props.SetParent(map.transform, false);
            int placed = 0;
            foreach (var t in map.GetComponentsInChildren<Transform>().ToArray())
            {
                if (t == null || t == map.transform) continue;
                string n = t.name; var parts = n.Split('_');
                GameObject src = null; Material mat = null; bool box = true;
                if (n.StartsWith("CNT_")) { src = contObj; contMats.TryGetValue(parts[1], out mat); }
                else if (n.StartsWith("CRANE")) { src = craneObj; mat = craneMat; box = false; }
                else if (n.StartsWith("FORK_")) { src = forkObj; mat = forkMat; box = false; }   // exact shape: bullets pass over the forks
                else if (n.StartsWith("PAL_") && palRoot != null) { var f = Util.FindDeep(palRoot.transform, "Pallet_" + parts[1]); src = f != null ? f.gameObject : null; mat = palMat; }
                else continue;
                if (src == null) { Debug.LogWarning("[Zone47] liman: model yok -> " + n); continue; }
                float yaw = 0f; var last = parts[parts.Length - 1];
                if (last.StartsWith("r")) float.TryParse(last.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out yaw);
                var g = Object.Instantiate(src);
                g.name = n.StartsWith("CNT_") ? "Container" : n.StartsWith("CRANE") ? "GantryCrane" : n.StartsWith("FORK_") ? "Forklift" : "Pallet";   // must not reuse marker prefixes (markers get deleted below)
                foreach (Transform ch in g.transform.Cast<Transform>().ToArray()) if (ch.GetComponent<MeshFilter>() == null) Object.DestroyImmediate(ch.gameObject);
                g.transform.SetParent(props, true);
                g.transform.SetPositionAndRotation(t.position, Quaternion.Euler(0f, yaw, 0f) * src.transform.rotation);
                g.transform.localScale = src.transform.lossyScale;
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>())
                {
                    mr.sharedMaterials = Enumerable.Repeat(mat ?? concrete, mr.sharedMaterials.Length).ToArray();
                    mr.shadowCastingMode = ShadowCastingMode.On; mr.gameObject.isStatic = true;
                    if (!box) mr.gameObject.AddComponent<MeshCollider>().sharedMesh = mr.GetComponent<MeshFilter>().sharedMesh;
                }
                // solid collision from the real-world size, independent of the model's import transform
                if (box)
                {
                    Vector3 size = n.StartsWith("CNT_") ? new Vector3(6.06f, 2.59f, 2.44f) : n.StartsWith("FORK_") ? new Vector3(1.25f, 2.1f, 3.7f)
                                 : parts[1] == "01" ? new Vector3(1.2f, 0.16f, 1.0f) : parts[1] == "02" ? new Vector3(1.51f, 0.32f, 1.41f)
                                 : parts[1] == "03" ? new Vector3(0.96f, 0.96f, 1.0f) : new Vector3(1.23f, 0.64f, 1.04f);
                    var cg = new GameObject("Col_" + parts[0]);
                    cg.transform.SetParent(props, false);
                    cg.transform.SetPositionAndRotation(t.position + Vector3.up * size.y * 0.5f, Quaternion.Euler(0f, yaw, 0f));
                    cg.AddComponent<BoxCollider>().size = size;
                    cg.isStatic = true;
                }
                placed++;
            }
            Debug.Log("[Zone47] liman: " + placed + " model yerlestirildi");
            foreach (var t in map.GetComponentsInChildren<Transform>().ToArray())
                if (t != null && (t.name.StartsWith("CNT_") || t.name.StartsWith("PAL_") || t.name.StartsWith("FORK_") || t.name.StartsWith("CRANE"))) Object.DestroyImmediate(t.gameObject);
            return Prefab(map, "Port_Map");
        }

        /// First mesh of a model under Art/Port (models were normalised in Blender: bottom-centre pivot, metres).
        /// The imported model's first object (with its importer transform), or the model root when name is null.
        static GameObject PortModelObj(string file, string name)
        {
            string path = ROOT + "/Art/Port/" + file;
            if (!File.Exists(path)) return null;
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.isReadable = true;
            mi.SaveAndReimport();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null || name == null) return root;
            var mf = root.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name.Contains(name)) ?? root.GetComponentInChildren<MeshFilter>();
            return mf != null ? mf.gameObject : root;
        }

        static Mesh PortModel(string file, string name)
        {
            string path = ROOT + "/Art/Port/" + file;
            if (!File.Exists(path)) return null;
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.isReadable = true;
            mi.SaveAndReimport();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh m && (name == null || m.name.Contains(name))) return m;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh m) return m;
            return null;
        }

        [System.Serializable] class CityMat { public string id, base_, normal, mask; public float[] color; public float smooth, metal; public float[] emission; public bool transparent; }
        [System.Serializable] class CityMats { public CityMat[] items; }

        /// Third map "Savaş Bölgesi": Blender-made city block. Materials come from materials_unity.json
        /// (one per mesh group M00..M99), textures in Art/Map3/Tex.
        static GameObject MakeCity()
        {
            string path = ROOT + "/Art/Map3/map3.fbx";
            string jpath = ROOT + "/Art/Map3/materials_unity.json";
            if (!File.Exists(path) || !File.Exists(jpath)) return null;
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.addCollider = true; mi.isReadable = true;
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();
            var data = JsonUtility.FromJson<CityMats>(File.ReadAllText(jpath).Replace("\"base\"", "\"base_\""));
            var mats = new Dictionary<string, Material>();
            foreach (var it in data.items)
            {
                var m = new Material(Lit);
                m.SetFloat("_WorkflowMode", 1f);
                Color c = it.color != null && it.color.Length >= 3 ? new Color(it.color[0], it.color[1], it.color[2], 1f) : Color.white;
                if (!string.IsNullOrEmpty(it.base_)) { m.SetTexture("_BaseMap", Tex("Art/Map3/Tex/" + it.base_ + ".png")); c = Color.white; }
                m.SetColor("_BaseColor", c);
                if (!string.IsNullOrEmpty(it.normal)) { m.SetTexture("_BumpMap", Tex("Art/Map3/Tex/" + it.normal + ".png")); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
                if (!string.IsNullOrEmpty(it.mask)) { m.SetTexture("_MetallicGlossMap", Tex("Art/Map3/Tex/" + it.mask + ".png")); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); }
                else { m.SetFloat("_Metallic", it.metal); m.SetFloat("_Smoothness", Mathf.Clamp(it.smooth, 0f, 0.9f)); }
                if (it.emission != null && it.emission.Length >= 3 && (it.emission[0] + it.emission[1] + it.emission[2]) > 0.01f)
                { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(it.emission[0], it.emission[1], it.emission[2]) * 2f); }
                if (it.transparent) { m.SetColor("_BaseColor", new Color(0.08f, 0.1f, 0.12f)); m.SetFloat("_Smoothness", 0.9f); }   // broken glazing: dark glossy, opaque (cheap on mobile)
                mats[it.id] = SaveMat(m, "City_" + it.id);
            }
            var map = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            map.name = "City_Map";
            foreach (var r in map.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.gameObject.name; string id = n.Substring(n.LastIndexOf('_') + 1);
                if (!mats.TryGetValue(id, out var m)) { Debug.LogWarning("[Zone47] sehir: malzeme yok " + n); continue; }
                r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = n.StartsWith("Ground") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = true; r.gameObject.isStatic = true;
            }
            Debug.Log("[Zone47] Savas Bolgesi: " + mats.Count + " malzeme");
            return Prefab(map, "City_Map");
        }

        static GameObject MakeMap(Mats M)
        {
            string path = ROOT + "/Art/Map/TDM_Map.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.addCollider = true;
            mi.isReadable = true;   // runtime NavMesh baking needs CPU access to the meshes on device
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();
            var map = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            map.name = "TDM_Map";
            foreach (var r in map.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.gameObject.name.ToLower();
                var mat = n.Contains("road") ? M["map_road"] : n.Contains("ground") ? M["map_ground"] : M["map_build"];
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true;
                r.gameObject.isStatic = true;
            }
            return Prefab(map, "TDM_Map");
        }

        // ---------------------------------------------------------------- refs, scene, player settings
        static void MakeRefs(Mats M, AnimatorController body, AnimatorController arms, WeaponDef[] weapons, GameObject grenade, GameObject map)
        {
            Directory.CreateDirectory(ROOT + "/Resources");
            string p = ROOT + "/Resources/Z47Refs.asset";
            var R = AssetDatabase.LoadAssetAtPath<Z47Refs>(p);
            if (R == null) { R = ScriptableObject.CreateInstance<Z47Refs>(); AssetDatabase.CreateAsset(R, p); }
            R.swatModel = Load<GameObject>("Art/Characters/SwatGuy.fbx");
            R.gasModel = Load<GameObject>("Art/Characters/GasMask.fbx");
            R.bodyController = body; R.armsController = arms;
            R.weapons = weapons; R.grenadePrefab = grenade; R.mapPrefab = map;
            R.lobbyPrefab = MakeLobby();
            var skyM = new Material(Unlit); skyM.SetTexture("_BaseMap", Tex("Art/Lobby/sky.png")); skyM.SetColor("_BaseColor", Color.white);
            R.lobbySkyMat = SaveMat(skyM, "Lobby_Sky");
            R.rainMat = FXMat("FX_Rain", Tex("Art/FX/rain.png"), false, new Color(0.82f, 0.86f, 0.9f, 0.32f), true);
            R.rain = Load<AudioClip>("Art/Audio/rain.ogg");
            R.droneLoop = Load<AudioClip>("Art/Audio/drone_loop.ogg");
            R.siren = Load<AudioClip>("Art/Audio/siren.ogg");
            R.bombWhistle = Load<AudioClip>("Art/Audio/bomb_whistle.ogg");
            R.heliLoop = Load<AudioClip>("Art/Audio/heli_loop.ogg");
            R.lobbyVideo = Load<UnityEngine.Video.VideoClip>("Art/Video/lobby_bg.mp4");
            R.dronePrefab = Load<GameObject>("Art/Drone/drone.fbx");
            var kIds = new System.Collections.Generic.List<string>(); var kTex = new System.Collections.Generic.List<Texture2D>();
            foreach (var kid in new[] { "m4", "awp", "ak", "glock", "carbine", "shotgun", "gren", "drone" }) { var kt = Tex("Art/UI/KillIcons/kill_" + kid + ".png"); if (kt != null) { kIds.Add(kid); kTex.Add(kt); } }
            R.killIconIds = kIds.ToArray(); R.killIcons = kTex.ToArray();
            R.flagAtlas = Tex("Art/UI/flags_atlas.png"); R.avatarHex = Tex("Art/UI/Frames/avatar_hex.png");
            var frl = new System.Collections.Generic.List<Texture2D>();
            for (int fi = 0; fi < 16; fi++) { var ft = Tex("Art/UI/Frames/frame_" + fi + ".png"); if (ft == null) break; frl.Add(ft); }
            R.frames = frl.ToArray();
            R.bombPrefab = Load<GameObject>("Art/Bomb/bomb.fbx"); R.bombTex = Tex("Art/Bomb/bomb_albedo.png");      // "MK2 Grenade Drone Bomb" by yousef.hameed (CC BY)      // "MQ9 Ripper military drone" by Giperione (CC BY)
            R.portPrefab = MakePort();
            R.cityPrefab = MakeCity();
            R.mapThumbs = new[] { Tex("Art/UI/Maps/map_desert.png"), Tex("Art/UI/Maps/map_port.png"), Tex("Art/UI/Maps/map_city.png") };
            R.skybox = M["sky"];
            R.crateMat = M["crate"]; R.concreteMat = M["concrete"]; R.sandbagMat = M["sandbag"]; R.barrelMat = M["barrel"];
            R.flashMat = M["fx_flash"]; R.smokeMat = M["fx_smoke"]; R.sparkMat = M["fx_spark"]; R.holeMat = M["fx_hole"]; R.tracerMat = M["fx_tracer"]; R.fireMat = M["fx_fire"];
            R.headFont = Load<Font>("Art/UI/BlackOpsOne.ttf");
            R.bodyFont = Load<Font>("Art/UI/Rajdhani.ttf");
            R.logo = Tex("Art/UI/AppIcon.png");
            R.lobbyLogo = Tex("Art/UI/logo_lobby.png");
            string IC = "Art/UI/Icons/";
            R.icFire = Tex(IC + "ic_fire.png"); R.icAim = Tex(IC + "ic_aim.png"); R.icReload = Tex(IC + "ic_reload.png"); R.icGrenade = Tex(IC + "ic_grenade.png");
            R.icCrouch = Tex(IC + "ic_crouch.png"); R.icJump = Tex(IC + "ic_jump.png"); R.icHeadshot = Tex(IC + "ic_headshot.png"); R.icPause = Tex(IC + "ic_pause.png");
            string A = "Art/Audio/";
            R.reload = Load<AudioClip>(A + "reload.wav"); R.dryFire = Load<AudioClip>(A + "dryfire.wav");
            R.explosion = Load<AudioClip>(A + "explosion.wav"); R.impact = Load<AudioClip>(A + "impact.wav");
            R.flyby = Load<AudioClip>(A + "flyby.wav"); R.pin = Load<AudioClip>(A + "pin.wav");
            R.casing = Load<AudioClip>(A + "casing.wav"); R.bodyFall = Load<AudioClip>(A + "bodyfall.wav");
            R.awpBolt = Load<AudioClip>(A + "awp_bolt.wav");
            R.breath = Load<AudioClip>(A + "breath.wav"); R.heartbeat = Load<AudioClip>(A + "heart.wav");
            R.hurt = Load<AudioClip>(A + "hurt.wav"); R.headshot = Load<AudioClip>(A + "headshot.wav");
            R.equip = Load<AudioClip>(A + "equip.wav"); R.bounce = Load<AudioClip>(A + "bounce.wav");
            R.steps = new[] { Load<AudioClip>(A + "step1.wav"), Load<AudioClip>(A + "step2.wav"), Load<AudioClip>(A + "step3.wav"), Load<AudioClip>(A + "step4.wav") };
            // surface footsteps (Nox Sound "Footsteps - Essentials", CC0)
            var sets = new System.Collections.Generic.List<StepSet>();
            foreach (var sn in new[] { "Sand", "Rock", "Metal", "Wood", "Tile", "Gravel", "Dirt" })
            {
                string dir = ROOT + "/" + A + "Steps/" + sn;
                if (!Directory.Exists(dir)) continue;
                System.Func<string, AudioClip[]> Clips = pre =>
                {
                    var l = new System.Collections.Generic.List<AudioClip>();
                    var files = Directory.GetFiles(dir, pre + "_*.ogg"); System.Array.Sort(files);
                    foreach (var f in files) { var c = AssetDatabase.LoadAssetAtPath<AudioClip>(f.Replace('\\', '/')); if (c != null) l.Add(c); }
                    return l.ToArray();
                };
                sets.Add(new StepSet { name = sn, walk = Clips("walk"), run = Clips("run"), land = Clips("land") });
            }
            R.stepSets = sets.ToArray();
            R.ambience = Load<AudioClip>(A + "ambience.ogg"); R.menuMusic = Load<AudioClip>(A + "music_menu.ogg");
            EditorUtility.SetDirty(R);
            AssetDatabase.SaveAssets();
        }

        static void MakeScene()
        {
            Directory.CreateDirectory(ROOT + "/Scenes");
            string sp = ROOT + "/Scenes/Zone47.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Zone47").AddComponent<Boot>();
            EditorSceneManager.SaveScene(scene, sp);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(sp, true) };
        }

        static void PlayerSetup()
        {
            PlayerSettings.companyName = "AYD Works";
            PlayerSettings.productName = "Zone 47";
            try { PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.aydworks.zone47"); } catch { }
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;   // iPad layouts get rejected in review
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.iOS.buildNumber = "1";
            try { PlayerSettings.SplashScreen.show = false; } catch { }
            // shadows: sharper shadow map + soft-shadow support on every URP asset (jagged staircase edges otherwise)
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var aso = new SerializedObject(asset);
                var res = aso.FindProperty("m_MainLightShadowmapResolution"); if (res != null) res.intValue = 2048;
                var soft = aso.FindProperty("m_SoftShadowsSupported"); if (soft != null) soft.boolValue = true;
                var sq = aso.FindProperty("m_SoftShadowQuality"); if (sq != null) sq.intValue = 2;          // medium
                var nb = aso.FindProperty("m_ShadowNormalBias"); if (nb != null) nb.floatValue = 0.6f;
                aso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
            var icon = Tex("Art/UI/AppIcon.png");
            if (icon != null)
            {
                try { PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any); } catch { }
            }
        }
    }
}
#endif
