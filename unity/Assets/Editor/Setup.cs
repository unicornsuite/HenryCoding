// Editor-Automatisierung: Render-Pipeline (URP) einrichten, Szene erzeugen, Mac-App bauen.
// Aufruf: Unity -batchmode -quit -projectPath <pfad> -executeMethod ZA.Setup.Configure
//         Unity -batchmode -quit -projectPath <pfad> -executeMethod ZA.Setup.BuildMac
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
            // Shader-Referenzen in Resources, damit Shader.Find() auch in Builds funktioniert
            Directory.CreateDirectory("Assets/Resources/Shaders");
            foreach (var (file, shader) in new[] { ("Lit", "Universal Render Pipeline/Lit"), ("Unlit", "Universal Render Pipeline/Unlit"), ("ParticlesUnlit", "Universal Render Pipeline/Particles/Unlit"), ("ParticlesLit", "Universal Render Pipeline/Particles/Lit") })
            {
                string path = $"Assets/Resources/Shaders/{file}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) { var sh = Shader.Find(shader); if (sh != null) AssetDatabase.CreateAsset(new Material(sh), path); else Debug.LogWarning("Shader fehlt: " + shader); }
            }
            // Varianten-Referenzen (Normal Map, Emission, Alpha-Clip, Transparenz), sonst entfernt Unity die Shader-Varianten im Build
            var lit = Shader.Find("Universal Render Pipeline/Lit"); var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            void Variant(string file, Shader sh, System.Action<Material> cfg) { string p2 = $"Assets/Resources/Shaders/{file}.mat"; if (AssetDatabase.LoadAssetAtPath<Material>(p2) != null || sh == null) return; var m = new Material(sh); cfg(m); AssetDatabase.CreateAsset(m, p2); }
            Variant("LitNormalEmissive", lit, m => { m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_EMISSION"); m.SetTexture("_BumpMap", Texture2D.normalTexture); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; });
            Variant("LitCutout", lit, m => { m.SetFloat("_AlphaClip", 1); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0); });
            Variant("LitTransparent", lit, m => { m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_EMISSION"); m.SetFloat("_ZWrite", 0); m.renderQueue = 3000; });
            Variant("UnlitTransparent", unlit, m => { m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetFloat("_ZWrite", 0); m.renderQueue = 3000; });
            Variant("UnlitAdditive", unlit, m => { m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); m.SetFloat("_ZWrite", 0); m.renderQueue = 3000; });
            // --- URP: Renderer + Pipeline-Asset
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Renderer.asset");
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, "Assets/Settings/Renderer.asset");
            }
            rendererData.renderingMode = RenderingMode.Forward;
            // SSAO (Umgebungsverdeckung) als Renderer-Feature hinzufügen
            bool hasSsao = false; foreach (var f in rendererData.rendererFeatures) if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") hasSsao = true;
            if (!hasSsao)
            {
                var ssaoType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (ssaoType != null)
                {
                    var feat = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType); feat.name = "SSAO";
                    AssetDatabase.AddObjectToAsset(feat, rendererData);
                    var rso = new SerializedObject(rendererData); var list = rso.FindProperty("m_RendererFeatures"); list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
                    var map = rso.FindProperty("m_RendererFeatureMap"); if (map != null) { map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = feat.GetInstanceID(); }
                    rso.ApplyModifiedPropertiesWithoutUndo();
                    var fso = new SerializedObject(feat); var st = fso.FindProperty("m_Settings");
                    if (st != null) { var inten = st.FindPropertyRelative("Intensity"); if (inten != null) inten.floatValue = 1.2f; var rad = st.FindPropertyRelative("Radius"); if (rad != null) rad.floatValue = 0.35f; var ds = st.FindPropertyRelative("Downsample"); if (ds != null) ds.boolValue = true; fso.ApplyModifiedPropertiesWithoutUndo(); }
                    EditorUtility.SetDirty(rendererData);
                }
                else Debug.LogWarning("SSAO-Feature nicht gefunden");
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/URP.asset");
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, "Assets/Settings/URP.asset");
            }
            var so = new SerializedObject(pipeline);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_AdditionalLightsPerObjectLimit", 8);
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_ShadowDistance", 120f);
            Set(so, "m_ShadowCascadeCount", 4);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", 4);
            Set(so, "m_RequireOpaqueTexture", false);
            Set(so, "m_RequireDepthTexture", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i); QualitySettings.renderPipeline = pipeline; }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1);

            // --- Spielereinstellungen
            PlayerSettings.companyName = "Zombie-Ansturm";
            PlayerSettings.productName = "Zombie-Ansturm";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "de.zombieansturm.game");
            // Beide Eingabesysteme erlauben (wir nutzen das neue)
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var ih = ps.FindProperty("activeInputHandler"); if (ih != null) { ih.intValue = 2; ps.ApplyModifiedPropertiesWithoutUndo(); }

            // --- Szene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 700; cam.fieldOfView = 78; cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>(); camData.renderPostProcessing = true; camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var boot = new GameObject("Game"); boot.AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Setup fertig: URP + Szene eingerichtet.");
        }

        static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name); if (p == null) { Debug.LogWarning("Eigenschaft fehlt: " + name); return; }
            switch (value) { case bool b: p.boolValue = b; break; case int i: p.intValue = i; break; case float f: p.floatValue = f; break; }
        }

        [MenuItem("Zombie-Ansturm/Mac-App bauen")]
        public static void BuildMac()
        {
            Configure();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
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
