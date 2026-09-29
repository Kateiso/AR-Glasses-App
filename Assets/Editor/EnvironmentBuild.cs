using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using Unity.XR.XREAL;

public static class EnvironmentBuild
{
    const string ScenePath = "Assets/Scenes/EnvironmentCheck.unity";

    [MenuItem("AR Project/Build Environment Check APK")]
    public static void Build() => BuildProject(false);

    [MenuItem("AR Project/Build Air Environment APK")]
    public static void BuildAir() => BuildProject(true);

    static void BuildProject(bool air)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Select Android before running this build.");

        PlayerSettings.companyName = "SmartCity";
        PlayerSettings.productName = air ? "AR Air Environment" : "AR Environment Check";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.smartcity.ar.environmentcheck");
        PlayerSettings.bundleVersion = air ? "0.4.3" : "0.1.0";
        PlayerSettings.Android.bundleVersionCode = air ? 7 : 1;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Debug);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        EditorUserBuildSettings.buildAppBundle = false;

        Directory.CreateDirectory("Assets/XR");
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.Refresh();
        if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey, out var perTarget))
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart = true;
        general.Manager.automaticLoading = true;
        general.Manager.automaticRunning = true;
        if (!XRPackageMetadataStore.AssignLoader(general.Manager, typeof(XREALXRLoader).FullName, BuildTargetGroup.Android))
            throw new InvalidOperationException("Could not assign XREAL loader.");
        var xreal = XREALSettings.GetSettings();
        if (xreal == null)
        {
            xreal = ScriptableObject.CreateInstance<XREALSettings>();
            AssetDatabase.CreateAsset(xreal, "Assets/XR/XREALSettings.asset");
            EditorBuildSettings.AddConfigObject(XREALSettings.k_SettingsKey, xreal, true);
        }
        xreal.VirtualController = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.xreal.xr/Runtime/Prefabs/XREALVirtualController.prefab");
        EditorUtility.SetDirty(xreal);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(perTarget);
        AssetDatabase.SaveAssets();

        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.xreal.xr/Runtime/Prefabs/XR Interaction Setup.prefab");
            if (prefab == null) throw new InvalidOperationException("XREAL interaction prefab is missing.");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (var transform in rig.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidOperationException("Missing XR script on " + transform.name);
            var camera = rig.GetComponentInChildren<Camera>(true);
            if (camera == null) throw new InvalidOperationException("XR rig has no camera.");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.05f;
            var probe = new GameObject("Environment Check").AddComponent<EnvironmentProbe>();
            probe.head = camera.transform;
            var material = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.1f, 0.9f, 1f) };
            AssetDatabase.CreateAsset(material, "Assets/Materials/EnvironmentBeacon.mat");
            probe.beaconMaterial = material;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        string buildScene = ScenePath;
        if (air)
        {
            buildScene = "Assets/Scenes/AirEnvironment.unity";
            if (!File.Exists(buildScene))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.xreal.xr/Runtime/Prefabs/XR Interaction Setup.prefab");
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var camera = rig.GetComponentInChildren<Camera>(true);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.05f;
                var demo = new GameObject("Air Environment").AddComponent<AirEnvironmentDemo>();
                demo.head = camera.transform;
                demo.chineseFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
                if (demo.chineseFont == null) throw new BuildFailedException("Chinese font is missing.");
                EditorSceneManager.SaveScene(scene, buildScene);
            }
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(buildScene, true) };
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Builds/Android");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { buildScene },
            locationPathName = air ? "Builds/Android/AR-Air-Environment_0.4.3.apk" : "Builds/Android/AR-Environment-Check.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        Debug.Log($"AR_BUILD_RESULT: {report.summary.result}, errors={report.summary.totalErrors}, bytes={report.summary.totalSize}");
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Environment APK build failed; inspect the build log.");
    }
}
