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
            };
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
                EditorUtility.DisplayProgressBar("Zone 47", "Harita...", 0.8f);
                var map = MakeMap(mats);
                EditorUtility.DisplayProgressBar("Zone 47", "Sahne ve ayarlar...", 0.9f);
                MakeRefs(mats, body, arms, weapons, grenade, map);
                MakeScene();
                PlayerSetup();
                AssetDatabase.SaveAssets();
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Zone 47", "Kurulum tamam! ▶ Play'e basarak deneyebilirsin.\n\nEditörde: WASD yürü, sağ tık basılı tut + fare = bak, F = ateş, Q = nişan, R = şarjör, G = bomba, 1/2/3 silah, Space zıpla, C eğil.", "Tamam");
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
                int max = ui ? 1024 : path.Contains("/Characters/") ? 2048 : path.Contains("/Map/") ? 2048 : path.Contains("/FX/") ? 256 : 1024;
                ti.maxTextureSize = max;
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
                bool longClip = path.Contains("music") || path.Contains("ambience");
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
            layers[1].avatarMask = mask; layers[1].defaultWeight = 1f; layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
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
        static readonly Dictionary<string, float> MuzzleDist = new Dictionary<string, float> { { "m4", 0.5504f }, { "ak", 0.6517f }, { "awp", 0.8366f }, { "glock", 0.178f } };

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
            foreach (var id in new[] { "m4", "ak", "awp", "glock" })
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
            }
        }

        // ---------------------------------------------------------------- map
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
            R.skybox = M["sky"];
            R.crateMat = M["crate"]; R.concreteMat = M["concrete"]; R.sandbagMat = M["sandbag"]; R.barrelMat = M["barrel"];
            R.flashMat = M["fx_flash"]; R.smokeMat = M["fx_smoke"]; R.sparkMat = M["fx_spark"]; R.holeMat = M["fx_hole"]; R.tracerMat = M["fx_tracer"]; R.fireMat = M["fx_fire"];
            R.headFont = Load<Font>("Art/UI/BlackOpsOne.ttf");
            R.bodyFont = Load<Font>("Art/UI/Rajdhani.ttf");
            R.logo = Tex("Art/UI/AppIcon.png");
            string A = "Art/Audio/";
            R.reload = Load<AudioClip>(A + "reload.wav"); R.dryFire = Load<AudioClip>(A + "dryfire.wav");
            R.explosion = Load<AudioClip>(A + "explosion.wav"); R.impact = Load<AudioClip>(A + "impact.wav");
            R.flyby = Load<AudioClip>(A + "flyby.wav"); R.pin = Load<AudioClip>(A + "pin.wav");
            R.casing = Load<AudioClip>(A + "casing.wav"); R.bodyFall = Load<AudioClip>(A + "bodyfall.wav");
            R.awpBolt = Load<AudioClip>(A + "awp_bolt.wav");
            R.steps = new[] { Load<AudioClip>(A + "step1.wav"), Load<AudioClip>(A + "step2.wav"), Load<AudioClip>(A + "step3.wav"), Load<AudioClip>(A + "step4.wav") };
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
            var icon = Tex("Art/UI/AppIcon.png");
            if (icon != null)
            {
                try { PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any); } catch { }
            }
        }
    }
}
#endif
