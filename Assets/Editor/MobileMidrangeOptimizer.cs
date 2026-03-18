using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class MobileMidrangeOptimizer : EditorWindow
{
    private bool applyProjectAndBuildSettings = true;
    private bool applyQualitySettings = true;
    private bool applyRenderPipelineSettings = true;
    private bool optimizeTextures = true;
    private bool optimizeAudio = true;
    private bool targetOnlyMobileQualityLevels = true;

    [MenuItem("Tools/Pullback Fight/Optimization/Mid-Range Mobile Optimizer")]
    public static void OpenWindow()
    {
        var window = GetWindow<MobileMidrangeOptimizer>("Mobile Optimizer");
        window.minSize = new Vector2(460f, 340f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Mid-Range Mobile Optimization", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Applies balanced visual/performance defaults for mobile across project settings, quality, URP assets, textures, and audio imports. " +
            "This modifies project assets and import settings.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        applyProjectAndBuildSettings = EditorGUILayout.ToggleLeft("Project & Build Settings", applyProjectAndBuildSettings);
        applyQualitySettings = EditorGUILayout.ToggleLeft("Quality & Graphics Settings", applyQualitySettings);
        applyRenderPipelineSettings = EditorGUILayout.ToggleLeft("Render Pipeline Assets (URP)", applyRenderPipelineSettings);
        optimizeTextures = EditorGUILayout.ToggleLeft("Texture Importers (bulk optimize)", optimizeTextures);
        optimizeAudio = EditorGUILayout.ToggleLeft("Audio Importers (bulk optimize)", optimizeAudio);

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(!applyQualitySettings))
        {
            targetOnlyMobileQualityLevels = EditorGUILayout.ToggleLeft("Only modify quality levels with 'Mobile' in name", targetOnlyMobileQualityLevels);
        }

        EditorGUILayout.Space(12f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Selected Optimizations", GUILayout.Height(32f)))
            {
                ApplyOptimizations();
            }

            if (GUILayout.Button("Apply Everything", GUILayout.Height(32f)))
            {
                applyProjectAndBuildSettings = true;
                applyQualitySettings = true;
                applyRenderPipelineSettings = true;
                optimizeTextures = true;
                optimizeAudio = true;
                ApplyOptimizations();
            }
        }
    }

    private void ApplyOptimizations()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply Mobile Optimization",
                "This will modify project settings and may reimport many textures/audio files. Continue?",
                "Apply",
                "Cancel"))
        {
            return;
        }

        var report = new OptimizationReport();

        try
        {
            AssetDatabase.StartAssetEditing();

            if (applyProjectAndBuildSettings)
            {
                ApplyProjectAndBuildSettings(report);
            }

            if (applyQualitySettings)
            {
                ApplyQualitySettings(report, targetOnlyMobileQualityLevels);
            }

            if (applyRenderPipelineSettings)
            {
                ApplyRenderPipelineSettings(report);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        if (optimizeTextures)
        {
            OptimizeTextureImporters(report);
        }

        if (optimizeAudio)
        {
            OptimizeAudioImporters(report);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = report.GetSummary();
        Debug.Log(summary);
        EditorUtility.DisplayDialog("Mobile Optimization Complete", summary, "OK");
    }

    private static void ApplyProjectAndBuildSettings(OptimizationReport report)
    {
        EditorUtility.DisplayProgressBar("Mobile Optimizer", "Applying project and build settings...", 0.05f);

        SetValue("PlayerSettings.MTRendering", () => PlayerSettings.MTRendering, value => PlayerSettings.MTRendering = value, true, report);
        SetValue("PlayerSettings.gpuSkinning", () => PlayerSettings.gpuSkinning, value => PlayerSettings.gpuSkinning = value, true, report);
        SetValue("PlayerSettings.stripEngineCode", () => PlayerSettings.stripEngineCode, value => PlayerSettings.stripEngineCode = value, true, report);
        SetValue("PlayerSettings.SetMobileMTRendering(Android)", () => PlayerSettings.GetMobileMTRendering(BuildTargetGroup.Android), value => PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, value), true, report);
        SetValue("PlayerSettings.SetMobileMTRendering(iOS)", () => PlayerSettings.GetMobileMTRendering(BuildTargetGroup.iOS), value => PlayerSettings.SetMobileMTRendering(BuildTargetGroup.iOS, value), true, report);

        SetValue("PlayerSettings.colorSpace", () => PlayerSettings.colorSpace, value => PlayerSettings.colorSpace = value, ColorSpace.Linear, report);
        SetValue("QualitySettings.vSyncCount", () => QualitySettings.vSyncCount, value => QualitySettings.vSyncCount = value, 0, report);
        SetValue("QualitySettings.maxQueuedFrames", () => QualitySettings.maxQueuedFrames, value => QualitySettings.maxQueuedFrames = value, 2, report);

        SetValue("GraphicsSettings.useScriptableRenderPipelineBatching", () => GraphicsSettings.useScriptableRenderPipelineBatching, value => GraphicsSettings.useScriptableRenderPipelineBatching = value, true, report);
        SetValue("GraphicsSettings.logWhenShaderIsCompiled", () => GraphicsSettings.logWhenShaderIsCompiled, value => GraphicsSettings.logWhenShaderIsCompiled = value, false, report);

        SetValue("Physics.autoSyncTransforms", () => Physics.autoSyncTransforms, value => Physics.autoSyncTransforms = value, false, report);
        SetValue("Physics.defaultSolverIterations", () => Physics.defaultSolverIterations, value => Physics.defaultSolverIterations = value, 6, report);
        SetValue("Physics.defaultSolverVelocityIterations", () => Physics.defaultSolverVelocityIterations, value => Physics.defaultSolverVelocityIterations = value, 2, report);
        SetValue("Physics.reuseCollisionCallbacks", () => Physics.reuseCollisionCallbacks, value => Physics.reuseCollisionCallbacks = value, true, report);

        SetValue("Physics2D.velocityIterations", () => Physics2D.velocityIterations, value => Physics2D.velocityIterations = value, 6, report);
        SetValue("Physics2D.positionIterations", () => Physics2D.positionIterations, value => Physics2D.positionIterations = value, 8, report);

        SetValue("Time.fixedDeltaTime", () => Time.fixedDeltaTime, value => Time.fixedDeltaTime = value, 0.02f, report);
        SetValue("Time.maximumDeltaTime", () => Time.maximumDeltaTime, value => Time.maximumDeltaTime = value, 0.1f, report);

        SetAndroidBuildSettings(report);
        SetiOSBuildSettings(report);
    }

    private static void SetAndroidBuildSettings(OptimizationReport report)
    {
        EditorUtility.DisplayProgressBar("Mobile Optimizer", "Applying Android settings...", 0.1f);

        SetValue("Android.minSdkVersion", () => PlayerSettings.Android.minSdkVersion, value => PlayerSettings.Android.minSdkVersion = value, AndroidSdkVersions.AndroidApiLevel26, report);
        SetValue("Android.targetSdkVersion", () => PlayerSettings.Android.targetSdkVersion, value => PlayerSettings.Android.targetSdkVersion = value, AndroidSdkVersions.AndroidApiLevelAuto, report);
        SetValue("Android.targetArchitectures", () => PlayerSettings.Android.targetArchitectures, value => PlayerSettings.Android.targetArchitectures = value, AndroidArchitecture.ARM64, report);

        SetValue("ScriptingBackend(Android)",
            () => PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android),
            value => PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, value),
            ScriptingImplementation.IL2CPP,
            report);

        SetValue("ManagedStrippingLevel(Android)",
            () => PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Android),
            value => PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, value),
            ManagedStrippingLevel.Medium,
            report);

        SetValue("IL2CPPCompilerConfig(Android)",
            () => PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.Android),
            value => PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, value),
            Il2CppCompilerConfiguration.Master,
            report);
    }

    private static void SetiOSBuildSettings(OptimizationReport report)
    {
        EditorUtility.DisplayProgressBar("Mobile Optimizer", "Applying iOS settings...", 0.15f);

        SetValue("ScriptingBackend(iOS)",
            () => PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS),
            value => PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, value),
            ScriptingImplementation.IL2CPP,
            report);

        SetValue("ManagedStrippingLevel(iOS)",
            () => PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.iOS),
            value => PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, value),
            ManagedStrippingLevel.Medium,
            report);

        SetValue("IL2CPPCompilerConfig(iOS)",
            () => PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.iOS),
            value => PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.iOS, value),
            Il2CppCompilerConfiguration.Master,
            report);
    }

    private static void ApplyQualitySettings(OptimizationReport report, bool onlyMobileNamedQualityLevels)
    {
        EditorUtility.DisplayProgressBar("Mobile Optimizer", "Applying quality settings...", 0.2f);

        string[] qualityNames = QualitySettings.names;
        int originalQuality = QualitySettings.GetQualityLevel();
        var targetIndices = new List<int>();

        for (int i = 0; i < qualityNames.Length; i++)
        {
            bool isMobileName = qualityNames[i].IndexOf("mobile", StringComparison.OrdinalIgnoreCase) >= 0;
            if (onlyMobileNamedQualityLevels)
            {
                if (isMobileName)
                {
                    targetIndices.Add(i);
                }
            }
            else
            {
                targetIndices.Add(i);
            }
        }

        if (targetIndices.Count == 0)
        {
            targetIndices.Add(originalQuality);
            report.AddInfo("No quality level contained 'Mobile'; optimized only current quality level.");
        }

        for (int i = 0; i < targetIndices.Count; i++)
        {
            int qualityIndex = targetIndices[i];
            QualitySettings.SetQualityLevel(qualityIndex, false);

            float progress = 0.25f + ((i + 1f) / Mathf.Max(1f, targetIndices.Count)) * 0.15f;
            EditorUtility.DisplayProgressBar("Mobile Optimizer", "Configuring quality level: " + QualitySettings.names[qualityIndex], progress);

            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].pixelLightCount", () => QualitySettings.pixelLightCount, value => QualitySettings.pixelLightCount = value, 2, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].anisotropicFiltering", () => QualitySettings.anisotropicFiltering, value => QualitySettings.anisotropicFiltering = value, AnisotropicFiltering.Enable, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].antiAliasing", () => QualitySettings.antiAliasing, value => QualitySettings.antiAliasing = value, 2, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].shadowDistance", () => QualitySettings.shadowDistance, value => QualitySettings.shadowDistance = value, 35f, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].shadowResolution", () => QualitySettings.shadowResolution, value => QualitySettings.shadowResolution = value, ShadowResolution.Medium, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].shadowCascades", () => QualitySettings.shadowCascades, value => QualitySettings.shadowCascades = value, 2, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].softParticles", () => QualitySettings.softParticles, value => QualitySettings.softParticles = value, false, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].softVegetation", () => QualitySettings.softVegetation, value => QualitySettings.softVegetation = value, false, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].realtimeReflectionProbes", () => QualitySettings.realtimeReflectionProbes, value => QualitySettings.realtimeReflectionProbes = value, false, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].billboardsFaceCameraPosition", () => QualitySettings.billboardsFaceCameraPosition, value => QualitySettings.billboardsFaceCameraPosition = value, true, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].lodBias", () => QualitySettings.lodBias, value => QualitySettings.lodBias = value, 1.3f, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].masterTextureLimit", () => QualitySettings.globalTextureMipmapLimit, value => QualitySettings.globalTextureMipmapLimit = value, 0, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].streamingMipmapsActive", () => QualitySettings.streamingMipmapsActive, value => QualitySettings.streamingMipmapsActive = value, true, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].streamingMipmapsMaxLevelReduction", () => QualitySettings.streamingMipmapsMaxLevelReduction, value => QualitySettings.streamingMipmapsMaxLevelReduction = value, 2, report);
            SetValue($"Quality[{QualitySettings.names[qualityIndex]}].streamingMipmapsMemoryBudget", () => QualitySettings.streamingMipmapsMemoryBudget, value => QualitySettings.streamingMipmapsMemoryBudget = value, 384f, report);
        }

        QualitySettings.SetQualityLevel(originalQuality, false);
    }

    private static void ApplyRenderPipelineSettings(OptimizationReport report)
    {
        EditorUtility.DisplayProgressBar("Mobile Optimizer", "Applying render pipeline settings...", 0.45f);

        string[] guids = AssetDatabase.FindAssets("t:RenderPipelineAsset", new[] { "Assets" });
        int updatedAssets = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset == null)
            {
                continue;
            }

            string typeName = asset.GetType().Name;
            if (typeName.IndexOf("UniversalRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            float progress = 0.45f + ((i + 1f) / Mathf.Max(1f, guids.Length)) * 0.15f;
            EditorUtility.DisplayProgressBar("Mobile Optimizer", "Configuring URP asset: " + path, progress);

            var serialized = new SerializedObject(asset);
            bool changed = false;

            changed |= SetSerializedBool(serialized, "m_RequireDepthTexture", false, report, path + "::DepthTexture");
            changed |= SetSerializedBool(serialized, "m_RequireOpaqueTexture", false, report, path + "::OpaqueTexture");
            changed |= SetSerializedBool(serialized, "m_SupportsHDR", true, report, path + "::HDR");
            changed |= SetSerializedBool(serialized, "m_SoftShadowsSupported", false, report, path + "::SoftShadows");
            changed |= SetSerializedInt(serialized, "m_MSAA", 2, report, path + "::MSAA");
            changed |= SetSerializedFloat(serialized, "m_RenderScale", 1.0f, report, path + "::RenderScale");
            changed |= SetSerializedInt(serialized, "m_MainLightRenderingMode", 1, report, path + "::MainLightMode");
            changed |= SetSerializedBool(serialized, "m_MainLightShadowsSupported", true, report, path + "::MainLightShadows");
            changed |= SetSerializedInt(serialized, "m_MainLightShadowmapResolution", 1024, report, path + "::MainLightShadowmapResolution");
            changed |= SetSerializedInt(serialized, "m_AdditionalLightsRenderingMode", 1, report, path + "::AdditionalLightsMode");
            changed |= SetSerializedInt(serialized, "m_AdditionalLightsPerObjectLimit", 4, report, path + "::AdditionalLightsPerObjectLimit");
            changed |= SetSerializedBool(serialized, "m_AdditionalLightShadowsSupported", false, report, path + "::AdditionalLightShadows");
            changed |= SetSerializedInt(serialized, "m_ShadowCascadeCount", 2, report, path + "::ShadowCascadeCount");
            changed |= SetSerializedFloat(serialized, "m_ShadowDistance", 35f, report, path + "::ShadowDistance");
            changed |= SetSerializedInt(serialized, "m_ColorGradingMode", 0, report, path + "::ColorGradingMode");
            changed |= SetSerializedInt(serialized, "m_ColorGradingLutSize", 16, report, path + "::ColorGradingLutSize");
            changed |= SetSerializedBool(serialized, "m_SupportsCameraDepthTexture", false, report, path + "::CameraDepthTexture");
            changed |= SetSerializedBool(serialized, "m_SupportsCameraOpaqueTexture", false, report, path + "::CameraOpaqueTexture");
            changed |= SetSerializedBool(serialized, "m_UseAdaptivePerformance", true, report, path + "::AdaptivePerformance");

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                updatedAssets++;
            }
        }

        report.AddInfo("Render pipeline assets updated: " + updatedAssets);
    }

    private static void OptimizeTextureImporters(OptimizationReport report)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { "Assets" });
        int changedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            EditorUtility.DisplayProgressBar("Mobile Optimizer", "Optimizing textures... " + path, 0.6f + ((i + 1f) / Mathf.Max(1f, guids.Length)) * 0.2f);

            bool changed = false;
            bool isSprite = importer.textureType == TextureImporterType.Sprite;
            bool isNormalMap = importer.textureType == TextureImporterType.NormalMap;
            bool isUiTexture = path.IndexOf("/ui/", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("/sprites/", StringComparison.OrdinalIgnoreCase) >= 0;

            changed |= SetImporterValue(() => importer.textureCompression, value => importer.textureCompression = value, TextureImporterCompression.CompressedHQ);
            changed |= SetImporterValue(() => importer.npotScale, value => importer.npotScale = value, TextureImporterNPOTScale.ToNearest);
            changed |= SetImporterValue(() => importer.mipmapEnabled, value => importer.mipmapEnabled = value, !isSprite && !isUiTexture);

            if (!isSprite && !isUiTexture)
            {
                changed |= SetImporterValue(() => importer.streamingMipmaps, value => importer.streamingMipmaps = value, true);
            }

            changed |= UpdateTexturePlatform(importer, "Android", GetTextureSizeLimit(path, isUiTexture, isNormalMap), report);
            changed |= UpdateTexturePlatform(importer, "iPhone", GetTextureSizeLimit(path, isUiTexture, isNormalMap), report);

            if (changed)
            {
                importer.SaveAndReimport();
                changedCount++;
            }
        }

        report.AddInfo("Textures reimported: " + changedCount + " / " + guids.Length);
    }

    private static int GetTextureSizeLimit(string path, bool isUiTexture, bool isNormalMap)
    {
        if (isUiTexture)
        {
            return 2048;
        }

        if (isNormalMap)
        {
            return 1024;
        }

        string lower = path.ToLowerInvariant();
        if (lower.Contains("terrain") || lower.Contains("landscape") || lower.Contains("sky"))
        {
            return 2048;
        }

        return 1024;
    }

    private static bool UpdateTexturePlatform(TextureImporter importer, string platform, int maxTextureSize, OptimizationReport report)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
        bool changed = false;

        if (!settings.overridden)
        {
            settings.overridden = true;
            changed = true;
        }

        if (settings.maxTextureSize != maxTextureSize)
        {
            settings.maxTextureSize = maxTextureSize;
            changed = true;
        }

        if (settings.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            changed = true;
        }

        if (settings.format != TextureImporterFormat.ASTC_6x6)
        {
            settings.format = TextureImporterFormat.ASTC_6x6;
            changed = true;
        }

        if (settings.compressionQuality != 55)
        {
            settings.compressionQuality = 55;
            changed = true;
        }

        if (changed)
        {
            importer.SetPlatformTextureSettings(settings);
            report.AddChange("TexturePlatform(" + platform + "): " + importer.assetPath);
        }

        return changed;
    }

    private static void OptimizeAudioImporters(OptimizationReport report)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
        int changedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                continue;
            }

            EditorUtility.DisplayProgressBar("Mobile Optimizer", "Optimizing audio... " + path, 0.8f + ((i + 1f) / Mathf.Max(1f, guids.Length)) * 0.2f);

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            float clipLength = clip != null ? clip.length : 1f;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioImporterSampleSettings optimized = settings;

            if (clipLength >= 12f)
            {
                optimized.loadType = AudioClipLoadType.Streaming;
                optimized.compressionFormat = AudioCompressionFormat.Vorbis;
                optimized.quality = 0.6f;
                optimized.preloadAudioData = false;
            }
            else if (clipLength >= 3f)
            {
                optimized.loadType = AudioClipLoadType.CompressedInMemory;
                optimized.compressionFormat = AudioCompressionFormat.Vorbis;
                optimized.quality = 0.58f;
                optimized.preloadAudioData = true;
            }
            else
            {
                optimized.loadType = AudioClipLoadType.DecompressOnLoad;
                optimized.compressionFormat = AudioCompressionFormat.ADPCM;
                optimized.quality = 1f;
                optimized.preloadAudioData = true;
            }

            optimized.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;

            bool changed = false;
            changed |= !AudioSettingsEqual(settings, optimized);
            if (changed)
            {
                importer.defaultSampleSettings = optimized;
            }

            changed |= SetImporterValue(() => importer.loadInBackground, value => importer.loadInBackground = value, true);
            changed |= SetImporterValue(() => importer.forceToMono, value => importer.forceToMono = value, clipLength < 10f);

            changed |= SetAudioPlatformOverride(importer, "Android", optimized);
            changed |= SetAudioPlatformOverride(importer, "iOS", optimized);

            if (changed)
            {
                importer.SaveAndReimport();
                changedCount++;
            }
        }

        report.AddInfo("Audio clips reimported: " + changedCount + " / " + guids.Length);
    }

    private static bool SetAudioPlatformOverride(AudioImporter importer, string platform, AudioImporterSampleSettings sampleSettings)
    {
        AudioImporterSampleSettings platformSettings = sampleSettings;

        if (!importer.ContainsSampleSettingsOverride(platform))
        {
            importer.SetOverrideSampleSettings(platform, platformSettings);
            return true;
        }

        AudioImporterSampleSettings current = importer.GetOverrideSampleSettings(platform);
        if (!AudioSettingsEqual(current, platformSettings))
        {
            importer.SetOverrideSampleSettings(platform, platformSettings);
            return true;
        }

        return false;
    }

    private static bool AudioSettingsEqual(AudioImporterSampleSettings a, AudioImporterSampleSettings b)
    {
        return a.loadType == b.loadType &&
               a.sampleRateSetting == b.sampleRateSetting &&
               a.sampleRateOverride == b.sampleRateOverride &&
               a.compressionFormat == b.compressionFormat &&
               Mathf.Approximately(a.quality, b.quality);
    }

    private static bool SetSerializedBool(SerializedObject serialized, string propertyName, bool value, OptimizationReport report, string label)
    {
        SerializedProperty prop = serialized.FindProperty(propertyName);
        if (prop == null || prop.propertyType != SerializedPropertyType.Boolean)
        {
            return false;
        }

        if (prop.boolValue == value)
        {
            return false;
        }

        prop.boolValue = value;
        report.AddChange(label + " = " + value);
        return true;
    }

    private static bool SetSerializedInt(SerializedObject serialized, string propertyName, int value, OptimizationReport report, string label)
    {
        SerializedProperty prop = serialized.FindProperty(propertyName);
        if (prop == null || prop.propertyType != SerializedPropertyType.Integer)
        {
            return false;
        }

        if (prop.intValue == value)
        {
            return false;
        }

        prop.intValue = value;
        report.AddChange(label + " = " + value);
        return true;
    }

    private static bool SetSerializedFloat(SerializedObject serialized, string propertyName, float value, OptimizationReport report, string label)
    {
        SerializedProperty prop = serialized.FindProperty(propertyName);
        if (prop == null || prop.propertyType != SerializedPropertyType.Float)
        {
            return false;
        }

        if (Mathf.Approximately(prop.floatValue, value))
        {
            return false;
        }

        prop.floatValue = value;
        report.AddChange(label + " = " + value);
        return true;
    }

    private static bool SetImporterValue<T>(Func<T> getter, Action<T> setter, T value)
    {
        T current = getter();
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return false;
        }

        setter(value);
        return true;
    }

    private static void SetValue<T>(string label, Func<T> getter, Action<T> setter, T desired, OptimizationReport report)
    {
        T current = getter();
        if (EqualityComparer<T>.Default.Equals(current, desired))
        {
            return;
        }

        setter(desired);
        report.AddChange(label + " = " + desired);
    }

    private sealed class OptimizationReport
    {
        private readonly List<string> infoLines = new List<string>();
        private readonly List<string> changedLines = new List<string>();

        public void AddInfo(string line)
        {
            infoLines.Add(line);
        }

        public void AddChange(string line)
        {
            changedLines.Add(line);
        }

        public string GetSummary()
        {
            int shown = Mathf.Min(40, changedLines.Count);
            var summary = new System.Text.StringBuilder();

            summary.AppendLine("[MobileMidrangeOptimizer] Completed.");
            summary.AppendLine("Changes applied: " + changedLines.Count);

            for (int i = 0; i < infoLines.Count; i++)
            {
                summary.AppendLine("- " + infoLines[i]);
            }

            if (shown > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Sample changes:");
                for (int i = 0; i < shown; i++)
                {
                    summary.AppendLine("- " + changedLines[i]);
                }
            }

            if (changedLines.Count > shown)
            {
                summary.AppendLine("- ... and " + (changedLines.Count - shown) + " more changes");
            }

            return summary.ToString();
        }
    }
}
