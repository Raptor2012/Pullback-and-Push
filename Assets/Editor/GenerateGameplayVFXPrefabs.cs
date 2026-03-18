using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateGameplayVFXPrefabs
{
    private const string RootFolder = "Assets/Prefab/VFX";
    private const string MaterialFolder = "Assets/Prefab/VFX/Materials";

    [MenuItem("Tools/Pullback Fight/Generate Gameplay VFX Prefabs")]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/Prefab");
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialFolder);

        Material sharedVfxMaterial = GetOrCreateVfxMaterial();

        CreateTireSmokePrefab(sharedVfxMaterial);
        CreateExhaustPrefab(sharedVfxMaterial);
        CreateSparksPrefab(sharedVfxMaterial);
        CreateSpeedLinesPrefab(sharedVfxMaterial);
        CreateRagdollTrailPrefab(sharedVfxMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GenerateGameplayVFXPrefabs] Generated: TireSmoke, Exhaust, Sparks, SpeedLines, RagdollTrail in Assets/Prefab/VFX");
    }

    [MenuItem("Tools/Pullback Fight/Generate Speedline Prefab")]
    public static void GenerateSpeedlinePrefabOnly()
    {
        EnsureFolder("Assets/Prefab");
        EnsureFolder(RootFolder);
        EnsureFolder(MaterialFolder);

        Material sharedVfxMaterial = GetOrCreateVfxMaterial();
        CreateSpeedLinesPrefab(sharedVfxMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GenerateGameplayVFXPrefabs] Generated: SpeedLines in Assets/Prefab/VFX");
    }

    private static void CreateTireSmokePrefab(Material material)
    {
        string path = $"{RootFolder}/TireSmoke.prefab";

        GameObject root = new GameObject("TireSmoke");
        var ps = root.AddComponent<ParticleSystem>();
        var renderer = root.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.32f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 35f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.05f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(1f, 1.25f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 0f),
                new GradientColorKey(new Color(0.75f, 0.75f, 0.75f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.32f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.25f;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = material;
        renderer.sortingFudge = 2f;

        SavePrefab(root, path);
    }

    private static void CreateExhaustPrefab(Material material)
    {
        string path = $"{RootFolder}/Exhaust.prefab";

        GameObject root = new GameObject("Exhaust");
        var ps = root.AddComponent<ParticleSystem>();
        var renderer = root.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.2f, 0.2f, 0.45f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.03f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, -0.05f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(1f, 1.35f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.22f, 0.22f, 0.22f), 0f),
                new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.45f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.22f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 0.4f;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = material;
        renderer.sortingFudge = 2f;

        SavePrefab(root, path);
    }

    private static void CreateSparksPrefab(Material material)
    {
        string path = $"{RootFolder}/Sparks.prefab";

        GameObject root = new GameObject("Sparks");
        var ps = root.AddComponent<ParticleSystem>();
        var renderer = root.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 0.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.65f, 0.1f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2.5f;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.06f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.7f),
                new GradientColorKey(new Color(0.6f, 0.2f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 0.65f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
        trails.inheritParticleColor = true;
        trails.sizeAffectsWidth = true;
        trails.sizeAffectsLifetime = false;

        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 0.55f;
        renderer.velocityScale = 0.35f;
        renderer.material = material;

        SavePrefab(root, path);
    }

    private static void CreateRagdollTrailPrefab(Material material)
    {
        string path = $"{RootFolder}/RagdollTrail.prefab";

        GameObject root = new GameObject("RagdollTrail");
        TrailRenderer trail = root.AddComponent<TrailRenderer>();

        trail.time = 0.5f;
        trail.minVertexDistance = 0.03f;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(1f, 0f));
        trail.widthMultiplier = 0.18f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.85f, 0.9f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;

        trail.material = material;

        SavePrefab(root, path);
    }

    private static void CreateSpeedLinesPrefab(Material material)
    {
        string path = $"{RootFolder}/SpeedLines.prefab";

        GameObject root = new GameObject("SpeedLines");
        var ps = root.AddComponent<ParticleSystem>();
        var renderer = root.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(22f, 34f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.92f, 1f, 0.55f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 512;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 85f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.angle = 6f;
        shape.radius = 0.35f;
        shape.length = 0.8f;

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-14f, -22f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.75f, 0.88f, 1f), 0f),
                new GradientColorKey(new Color(0.92f, 0.96f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.75f, 0.12f),
                new GradientAlphaKey(0.55f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.lengthScale = 5.2f;
        renderer.velocityScale = 0.85f;
        renderer.cameraVelocityScale = 0.5f;
        renderer.material = material;

        SavePrefab(root, path);
    }

    private static Material GetOrCreateVfxMaterial()
    {
        string materialPath = $"{MaterialFolder}/GameplayVFX_Particles.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader)
        {
            name = "GameplayVFX_Particles"
        };

        // Conservative blend setup that works across URP particle shader variants.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 2f);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        AssetDatabase.CreateAsset(mat, materialPath);
        return mat;
    }

    private static void SavePrefab(GameObject root, string prefabPath)
    {
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;

        string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent!, name);
    }
}
