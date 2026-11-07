using System.Linq;
// Editor-Automatisierung: Render-Pipeline (URP) einrichten, Szene erzeugen, Mac-App bauen.
// Aufruf: Unity -batchmode -quit -projectPath <pfad> -executeMethod ZA.Setup.Configure
//         Unity -batchmode -quit -projectPath <pfad> -executeMethod ZA.Setup.BuildMac
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace ZA
{
    public static class Setup
    {
        const string ScenePath = "Assets/Main.unity";

        [MenuItem("Zombie-Ansturm/Projekt einrichten")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Settings");
            // --- HDRP-Pipeline-Asset
            var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/Settings/HDRP.asset");
            if (pipeline == null) { pipeline = ScriptableObject.CreateInstance<HDRenderPipelineAsset>(); AssetDatabase.CreateAsset(pipeline, "Assets/Settings/HDRP.asset"); }
            var so = new SerializedObject(pipeline);
            foreach (var (name, val) in new (string, object)[] { ("m_RenderPipelineSettings.supportSSAO", true), ("m_RenderPipelineSettings.supportSSR", true), ("m_RenderPipelineSettings.supportVolumetrics", true), ("m_RenderPipelineSettings.supportVolumetricClouds", true), ("m_RenderPipelineSettings.supportMotionVectors", true), ("m_RenderPipelineSettings.supportDecals", true), ("m_RenderPipelineSettings.supportTransparentBackface", true) })
            {
                var p = so.FindProperty(name); if (p == null) { Debug.LogWarning("HDRP-Eigenschaft fehlt: " + name); continue; }
                if (val is bool b) p.boolValue = b;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i); QualitySettings.renderPipeline = pipeline; }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1);
            // HDRP-Globaleinstellungen anlegen (macht sonst der HDRP-Wizard)
            var gsType = typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings");
            var ensure = gsType?.GetMethod("Ensure", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (ensure != null) { try { var ps0 = ensure.GetParameters(); var args = new object[ps0.Length]; for (int i = 0; i < ps0.Length; i++) args[i] = ps0[i].HasDefaultValue ? ps0[i].DefaultValue : (ps0[i].ParameterType == typeof(bool) ? (object)true : null); ensure.Invoke(null, args); } catch (System.Exception e) { Debug.LogWarning("Globale HDRP-Einstellungen: " + e.Message); } }
            else Debug.LogWarning("HDRenderPipelineGlobalSettings.Ensure nicht gefunden – HDRP legt sie beim ersten Rendern selbst an.");
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // --- Shader-Referenzen in Resources (Shader.Find im Build) + Varianten
            Directory.CreateDirectory("Assets/Resources/Shaders"); Directory.CreateDirectory("Assets/Shaders");
            string hdrpPkg = Path.GetFullPath("Packages/com.unity.render-pipelines.high-definition");
            foreach (var sg in new[] { "ParticleUnlit", "ParticleLit" })
            {
                string src = Path.Combine(hdrpPkg, "Samples~/ParticleSystemShaderSamples/Shaders", sg + ".shadergraph"), dst = "Assets/Shaders/" + sg + ".shadergraph";
                if (!File.Exists(dst)) { if (File.Exists(src)) { File.Copy(src, dst); AssetDatabase.ImportAsset(dst); } else Debug.LogWarning("Partikel-Shader-Quelle fehlt: " + src); }
            }
            var lit = Shader.Find("HDRP/Lit"); var unlit = Shader.Find("HDRP/Unlit");
            void Variant(string file, Shader sh, System.Action<Material> cfg) { string p2 = $"Assets/Resources/Shaders/{file}.mat"; if (AssetDatabase.LoadAssetAtPath<Material>(p2) != null || sh == null) return; var m = new Material(sh); cfg?.Invoke(m); HDMaterial.ValidateMaterial(m); AssetDatabase.CreateAsset(m, p2); }
            Variant("Lit", lit, null); Variant("Unlit", unlit, null);
            Variant("LitNormalEmissive", lit, m => { m.EnableKeyword("_NORMALMAP"); m.SetTexture("_NormalMap", Texture2D.normalTexture); HDMaterial.SetEmissiveColor(m, Color.white); });
            Variant("LitCutout", lit, m => { HDMaterial.SetAlphaClipping(m, true); m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.EnableKeyword("_DOUBLESIDED_ON"); });
            Variant("LitTransparent", lit, m => { HDMaterial.SetSurfaceType(m, true); m.SetFloat("_BlendMode", 0); m.EnableKeyword("_BLENDMODE_ALPHA"); });
            Variant("UnlitTransparent", unlit, m => { HDMaterial.SetSurfaceType(m, true); m.SetFloat("_BlendMode", 0); m.EnableKeyword("_BLENDMODE_ALPHA"); });
            Variant("UnlitAdditive", unlit, m => { HDMaterial.SetSurfaceType(m, true); m.SetFloat("_BlendMode", 1); m.EnableKeyword("_BLENDMODE_ADD"); });
            var decal = Shader.Find("HDRP/Decal"); if (decal == null) Debug.LogWarning("HDRP/Decal-Shader nicht gefunden (Blutflecken fallen aus)");
            Variant("Decal", decal, m => { m.SetFloat("_AffectAlbedo", 1); m.SetFloat("_AffectSmoothness", 1); m.SetFloat("_AffectNormal", 0); m.SetFloat("_Smoothness", 0.75f); m.SetFloat("_DecalBlend", 1); m.EnableKeyword("_MATERIAL_AFFECTS_ALBEDO"); m.EnableKeyword("_MATERIAL_AFFECTS_MASKMAP"); m.EnableKeyword("_COLORMAP"); });
            foreach (var sg in new[] { "ParticleUnlit", "ParticleLit" }) { var sh = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/" + sg + ".shadergraph"); if (sh != null) Variant(sg, sh, null); else Debug.LogWarning("Partikel-Shader fehlt: " + sg); }

            // --- Spielereinstellungen
            PlayerSettings.companyName = "Zombie-Ansturm";
            PlayerSettings.productName = "Zombie-Ansturm";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "de.zombieansturm.game");
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var ih = ps.FindProperty("activeInputHandler"); if (ih != null) { ih.intValue = 2; ps.ApplyModifiedPropertiesWithoutUndo(); }

            // --- Szene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.02f; cam.farClipPlane = 900; cam.fieldOfView = 78; cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<HDAdditionalCameraData>(); camData.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing; camData.dithering = true;
            var boot = new GameObject("Game"); boot.AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = BuildScenes().Select(x => new EditorBuildSettingsScene(x, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Setup fertig: HDRP + Szene eingerichtet.");
        }

        static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name); if (p == null) { Debug.LogWarning("Eigenschaft fehlt: " + name); return; }
            switch (value) { case bool b: p.boolValue = b; break; case int i: p.intValue = i; break; case float f: p.floatValue = f; break; }
        }

        /// <summary>Hauptszene + optionale Stadt-Szene (aus CityExport), falls vorhanden</summary>
        static string[] BuildScenes()
        {
            var l = new System.Collections.Generic.List<string> { ScenePath };
            if (System.IO.File.Exists(CityExport.CityScenePath)) l.Add(CityExport.CityScenePath);
            return l.ToArray();
        }

        [MenuItem("Zombie-Ansturm/Mac-App bauen")]
        public static void BuildMac()
        {
            Configure();
            var opts = new BuildPlayerOptions
            {
                scenes = BuildScenes(),
                locationPathName = "Build/Zombie-Ansturm.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"Build: {report.summary.result}, {report.summary.totalSize / 1048576} MB, Fehler: {report.summary.totalErrors}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}

namespace ZA
{
    /// <summary>Importiert gekaufte Asset-Store-Pakete aus dem Unity-Hub-Cache ins Projekt</summary>
    public static class StoreImport
    {
        [UnityEditor.MenuItem("Zombie-Ansturm/Store-Pakete importieren")]
        public static void ImportAll()
        {
            string cache = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "Library/Unity/Asset Store-5.x");
            if (!System.IO.Directory.Exists(cache)) { UnityEngine.Debug.LogWarning("Kein Asset-Store-Cache: " + cache); return; }
            foreach (var f in System.IO.Directory.GetFiles(cache, "*.unitypackage", System.IO.SearchOption.AllDirectories))
            {
                UnityEngine.Debug.Log("Importiere " + f);
                UnityEditor.AssetDatabase.ImportPackage(f, false);
            }
            UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Store-Import fertig.");
        }
    }
}

namespace ZA
{
    /// <summary>Bereitet die gekauften Pakete auf: HDRP-Materialien, Humanoid-Rigs, Zombie-Animator, Prefab-Kopien in Resources/Generated</summary>
    public static class StoreAssets
    {
        const string Gen = "Assets/Resources/Generated";
        const string WeaponsRoot = "Assets/Animated FPS Weapons Part 1";
        const string ZombieRoot = "Assets/Snake3D/Zombie";
        const string AnimRoot = "Assets/Nephilite Studios/Zombie Animation Set/Animations/In Place";
        static readonly (string id, string prefab)[] WeaponMap = {
            ("pistol", "Semi-Automatic Pistol"), ("smg", "Submachine Gun"), ("shotgun", "Combat Shotgun"), ("rifle", "Assault Rifle"),
            ("sniper", "Sniper Rifle"), ("launcher", "Shoulder-Launched Missile"), ("minigun", "General-Purpose Machine Gun") };

        [UnityEditor.MenuItem("Zombie-Ansturm/Store-Assets aufbereiten")]
        public static void Prepare()
        {
            System.IO.Directory.CreateDirectory(Gen); System.IO.Directory.CreateDirectory(Gen + "/Weapons");
            // 1) HDRP-Materialien des Waffenpakets (liegt als verschachteltes Paket bei)
            string hdrpPkg = WeaponsRoot + "/HDRP_Installer.unitypackage";
            if (System.IO.File.Exists(hdrpPkg) && !UnityEditor.EditorPrefs.GetBool("ZA_WeaponsHDRP", false)) { UnityEditor.AssetDatabase.ImportPackage(hdrpPkg, false); UnityEditor.EditorPrefs.SetBool("ZA_WeaponsHDRP", true); UnityEditor.AssetDatabase.Refresh(); }
            PrepareZombies();
            PrepareWeapons();
            UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Store-Assets aufbereitet.");
        }

        static UnityEngine.AnimationClip Clip(string fbxPath)
        {
            foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxPath)) if (o is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview")) return c;
            UnityEngine.Debug.LogWarning("Clip fehlt: " + fbxPath); return null;
        }
        static void SetLoop(string fbxPath, bool loop)
        {
            var imp = UnityEditor.AssetImporter.GetAtPath(fbxPath) as UnityEditor.ModelImporter; if (imp == null) return;
            var clips = imp.clipAnimations; if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            bool changed = false; foreach (var c in clips) { if (c.loopTime != loop) { c.loopTime = loop; changed = true; } }
            if (imp.animationType != UnityEditor.ModelImporterAnimationType.Human) { imp.animationType = UnityEditor.ModelImporterAnimationType.Human; changed = true; }
            if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }
        }

        static void PrepareZombies()
        {
            if (!System.IO.Directory.Exists(ZombieRoot) || !System.IO.Directory.Exists(AnimRoot)) { UnityEngine.Debug.LogWarning("Zombie-Pakete nicht importiert."); return; }
            string A(string n) => AnimRoot + "/" + n + ".fbx";
            var loops = new[] { "zombie_aggro_idle_01", "zombie_walk_01", "zombie_walk_02", "zombie_walk_03", "zombie_run_01", "zombie_aggro_Run_F_01", "crawl/zombie_FD_crawl_F_01", "crawl/zombie_FD_crawl_idle_01" };
            foreach (var n in loops) SetLoop(A(n), true);
            foreach (var n in new[] { "zombie_light_attack_01", "zombie_swipe_attack_01", "zombie_attack_spit_01", "zombie_hit_react_F_01", "zombie_death_FD_01", "zombie_death_UP_01", "crawl/zombie_FD_crawl_attack_01", "zombie_alert_to_aggro_01" }) SetLoop(A(n), false);
            // Animator Controller
            string ctrlPath = Gen + "/ZombieAnim.controller";
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Speed", UnityEngine.AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Crawl", UnityEngine.AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Attack", "Hit", "Die", "Spit", "Scream" }) ctrl.AddParameter(t, UnityEngine.AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            // Fortbewegung: Blend Tree nach Geschwindigkeit
            var loco = sm.AddState("Locomotion");
            var tree = new UnityEditor.Animations.BlendTree { name = "Loco", blendParameter = "Speed", blendType = UnityEditor.Animations.BlendTreeType.Simple1D, useAutomaticThresholds = false };
            UnityEditor.AssetDatabase.AddObjectToAsset(tree, ctrl);
            tree.AddChild(Clip(A("zombie_aggro_idle_01")), 0f); tree.AddChild(Clip(A("zombie_walk_01")), 1.6f); tree.AddChild(Clip(A("zombie_run_01")), 4.5f);
            loco.motion = tree; sm.defaultState = loco;
            var crawl = sm.AddState("Crawl");
            var ctree = new UnityEditor.Animations.BlendTree { name = "CrawlLoco", blendParameter = "Speed", blendType = UnityEditor.Animations.BlendTreeType.Simple1D, useAutomaticThresholds = false };
            UnityEditor.AssetDatabase.AddObjectToAsset(ctree, ctrl);
            ctree.AddChild(Clip(A("crawl/zombie_FD_crawl_idle_01")), 0f); ctree.AddChild(Clip(A("crawl/zombie_FD_crawl_F_01")), 3.5f);
            crawl.motion = ctree;
            var toCrawl = loco.AddTransition(crawl); toCrawl.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Crawl"); toCrawl.hasExitTime = false; toCrawl.duration = 0.2f;
            var fromCrawl = crawl.AddTransition(loco); fromCrawl.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Crawl"); fromCrawl.hasExitTime = false; fromCrawl.duration = 0.2f;
            UnityEditor.Animations.AnimatorState OneShot(string name, string clip, string trigger, float speed = 1f)
            {
                var st = sm.AddState(name); st.motion = Clip(A(clip)); st.speed = speed;
                var tr = sm.AddAnyStateTransition(st); tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, trigger); tr.hasExitTime = false; tr.duration = 0.1f; tr.canTransitionToSelf = false;
                var back = st.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.2f;
                var back2 = st.AddTransition(crawl); back2.hasExitTime = true; back2.exitTime = 0.9f; back2.duration = 0.2f; back2.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Crawl");
                back.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Crawl");
                return st;
            }
            OneShot("Attack", "zombie_light_attack_01", "Attack", 1.3f);
            OneShot("Spit", "zombie_attack_spit_01", "Spit");
            OneShot("Hit", "zombie_hit_react_F_01", "Hit", 1.4f);
            OneShot("Scream", "zombie_alert_to_aggro_01", "Scream");
            var crawlAtk = sm.AddState("CrawlAttack"); crawlAtk.motion = Clip(A("crawl/zombie_FD_crawl_attack_01")); crawlAtk.speed = 1.3f;
            var ca = crawl.AddTransition(crawlAtk); ca.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Attack"); ca.hasExitTime = false; ca.duration = 0.1f;
            var caBack = crawlAtk.AddTransition(crawl); caBack.hasExitTime = true; caBack.exitTime = 0.9f; caBack.duration = 0.2f;
            var die = sm.AddState("Die"); die.motion = Clip(A("zombie_death_FD_01"));
            var dt = sm.AddAnyStateTransition(die); dt.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Die"); dt.hasExitTime = false; dt.duration = 0.1f; dt.canTransitionToSelf = false;
            UnityEditor.EditorUtility.SetDirty(ctrl);
            // Prefab-Varianten mit Animator in Resources
            foreach (var variant in new[] { "Default", "Blue", "Green", "Yellow" })
            {
                var src = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"{ZombieRoot}/Prefabs/HDRP/Zombie {variant}.prefab");
                if (src == null) { UnityEngine.Debug.LogWarning("Zombie-Prefab fehlt: " + variant); continue; }
                var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(src);
                UnityEditor.PrefabUtility.UnpackPrefabInstance(inst, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
                foreach (var mb in inst.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(mb);
                var anim = inst.GetComponent<UnityEngine.Animator>() ?? inst.AddComponent<UnityEngine.Animator>();
                anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = UnityEngine.AnimatorCullingMode.CullUpdateTransforms;
                if (anim.avatar == null) { var model = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(ZombieRoot + "/Models/Zombie.fbx"); var ma = model ? model.GetComponent<UnityEngine.Animator>() : null; if (ma) anim.avatar = ma.avatar; }
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(inst, $"{Gen}/Zombie_{variant}.prefab"); UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        static void PrepareWeapons()
        {
            if (!System.IO.Directory.Exists(WeaponsRoot)) { UnityEngine.Debug.LogWarning("Waffenpaket nicht importiert."); return; }
            foreach (var (id, name) in WeaponMap)
            {
                var src = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>($"{WeaponsRoot}/Prefabs/Weapons/{name}.prefab");
                if (src == null) { UnityEngine.Debug.LogWarning("Waffen-Prefab fehlt: " + name); continue; }
                var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(src);
                UnityEditor.PrefabUtility.UnpackPrefabInstance(inst, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
                foreach (var mb in inst.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(mb);
                // Fehlender Controller (z. B. Schrotflinte verweist auf eine falsche GUID): passenden Controller aus dem Paket per Name zuordnen
                foreach (var an in inst.GetComponentsInChildren<UnityEngine.Animator>(true))
                    if (an.runtimeAnimatorController == null)
                        foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:AnimatorController", new[] { WeaponsRoot + "/Animators" }))
                        { var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(name)) { an.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.RuntimeAnimatorController>(path); UnityEngine.Debug.Log($"Controller zugeordnet: {name} -> {path}"); break; } }
                foreach (var au in inst.GetComponentsInChildren<UnityEngine.AudioSource>(true)) UnityEngine.Object.DestroyImmediate(au);
                foreach (var cam in inst.GetComponentsInChildren<UnityEngine.Camera>(true)) UnityEngine.Object.DestroyImmediate(cam.gameObject);
                foreach (var li in inst.GetComponentsInChildren<UnityEngine.Light>(true)) li.enabled = false;
                foreach (var t in inst.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.layer = Layers.ViewModel;
                foreach (var r in inst.GetComponentsInChildren<UnityEngine.Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(inst, $"{Gen}/Weapons/{id}.prefab"); UnityEngine.Object.DestroyImmediate(inst);
            }
        }
    }
}
