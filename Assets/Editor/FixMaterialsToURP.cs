using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Fixes pink/broken materials by migrating them to Universal Render Pipeline/Lit.
///
/// Handles THREE cases:
///   Case A — Legacy Shaders/Diffuse:  reads _MainTex, switches to URP/Lit, writes to _BaseMap
///   Case B — Standard shader with empty Albedo: matches texture by material name, writes to _BaseMap
///   Case C — Already URP/Lit but missing BaseMap: matches texture by material name
///
/// HOW TO USE:
///   1. Place this file inside any "Editor" folder under Assets/
///      e.g.  Assets/Editor/FixMaterialsToURP.cs
///   2. Unity menu → Tools → Fix Materials to URP/Lit
///   3. First dialog  → select the MATERIALS folder to fix
///   4. Second dialog → select the TEXTURES folder to search in (can be same or different)
///   5. Done — check the Console for a full report
/// </summary>
public class FixMaterialsToURP : EditorWindow
{
    private const string URP_LIT_SHADER   = "Universal Render Pipeline/Lit";
    private const string LEGACY_MAIN_TEX  = "_MainTex";
    private const string URP_BASE_MAP     = "_BaseMap";
    private const string URP_BASE_COLOR   = "_BaseColor";

    [MenuItem("Tools/Fix Materials to URP/Lit")]
    public static void RunFix()
    {
        // --- Step 1: pick materials folder ---
        string matAbsolute = EditorUtility.OpenFolderPanel(
            "Step 1 of 2 — Select MATERIALS folder", Application.dataPath, "");

        if (string.IsNullOrEmpty(matAbsolute)) { Debug.Log("Cancelled."); return; }

        if (!matAbsolute.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("Invalid Folder",
                "Please select a folder inside your project's Assets directory.", "OK");
            return;
        }

        // --- Step 2: pick textures folder ---
        string texAbsolute = EditorUtility.OpenFolderPanel(
            "Step 2 of 2 — Select TEXTURES folder (used for name-matching)", Application.dataPath, "");

        if (string.IsNullOrEmpty(texAbsolute)) { Debug.Log("Cancelled."); return; }

        if (!texAbsolute.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("Invalid Folder",
                "Please select a folder inside your project's Assets directory.", "OK");
            return;
        }

        string matRelative = "Assets" + matAbsolute.Substring(Application.dataPath.Length);
        string texRelative = "Assets" + texAbsolute.Substring(Application.dataPath.Length);

        Shader urpLitShader = Shader.Find(URP_LIT_SHADER);
        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("Shader Not Found",
                $"Could not find '{URP_LIT_SHADER}'.\nMake sure URP is installed.", "OK");
            return;
        }

        // --- Build a name→Texture lookup from the textures folder ---
        // Key = texture name (lowercase, no extension), Value = Texture asset
        var textureLookup = BuildTextureLookup(texRelative);
        Debug.Log($"FixMaterialsToURP: Found {textureLookup.Count} textures in '{texRelative}'");

        // --- Find all materials ---
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { matRelative });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("No Materials Found",
                $"No materials found in:\n{matRelative}", "OK");
            return;
        }

        int countFixed       = 0;
        int countSkipped     = 0;
        int countNoTexture   = 0;
        var noTextureList    = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat == null) continue;

                EditorUtility.DisplayProgressBar(
                    "Fixing Materials",
                    $"{mat.name}  ({i + 1}/{guids.Length})",
                    (float)(i + 1) / guids.Length);

                // Already perfect — skip
                if (mat.shader == urpLitShader &&
                    mat.HasProperty(URP_BASE_MAP) &&
                    mat.GetTexture(URP_BASE_MAP) != null)
                {
                    countSkipped++;
                    continue;
                }

                // --- Resolve the texture to assign ---
                Texture resolvedTexture = null;

                // Case A: Legacy shader still has _MainTex reference
                if (mat.HasProperty(LEGACY_MAIN_TEX))
                    resolvedTexture = mat.GetTexture(LEGACY_MAIN_TEX);

                // Case B/C: No texture found on material — match by material name
                if (resolvedTexture == null)
                {
                    string key = mat.name.ToLower();
                    if (textureLookup.TryGetValue(key, out Texture matched))
                        resolvedTexture = matched;
                }

                // --- Switch shader to URP/Lit ---
                bool wasTransparent = IsTransparent(mat);
                mat.shader = urpLitShader;

                // Apply texture to BaseMap
                if (resolvedTexture != null && mat.HasProperty(URP_BASE_MAP))
                {
                    mat.SetTexture(URP_BASE_MAP, resolvedTexture);
                }
                else
                {
                    countNoTexture++;
                    noTextureList.Add(mat.name);
                }

                // Fix magenta base color → white
                if (mat.HasProperty(URP_BASE_COLOR))
                {
                    Color c = mat.GetColor(URP_BASE_COLOR);
                    bool isMagenta = c.r > 0.85f && c.g < 0.15f && c.b > 0.85f;
                    if (isMagenta) mat.SetColor(URP_BASE_COLOR, Color.white);
                }

                // Restore transparent surface type if needed
                if (wasTransparent)
                {
                    // URP/Lit: Surface Type 1 = Transparent
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_Blend", 0f); // Alpha blend
                    mat.renderQueue = 3000;
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }

                EditorUtility.SetDirty(mat);
                countFixed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // --- Report ---
        string summary =
            $"✅ Fixed:            {countFixed} materials\n" +
            $"⏭ Already correct:  {countSkipped} materials\n" +
            $"⚠️  No texture found: {countNoTexture} materials";

        if (noTextureList.Count > 0)
            summary += "\n\nMaterials with no matching texture:\n• " + string.Join("\n• ", noTextureList);

        Debug.Log("FixMaterialsToURP:\n" + summary);
        EditorUtility.DisplayDialog("Fix Materials to URP/Lit — Complete", summary, "OK");
    }

    // -----------------------------------------------------------------------
    // Builds a dictionary: lowercase material/texture name → Texture asset
    // Searches recursively inside texRelative
    // -----------------------------------------------------------------------
    private static Dictionary<string, Texture> BuildTextureLookup(string texRelative)
    {
        var lookup = new Dictionary<string, Texture>();
        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { texRelative });

        foreach (string guid in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (tex == null) continue;

            string key = tex.name.ToLower();
            if (!lookup.ContainsKey(key))
                lookup[key] = tex;
        }
        return lookup;
    }

    // -----------------------------------------------------------------------
    // Checks if a material was set to Transparent rendering
    // -----------------------------------------------------------------------
    private static bool IsTransparent(Material mat)
    {
        // Standard shader stores this in _Mode: 0=Opaque,1=Cutout,2=Fade,3=Transparent
        if (mat.HasProperty("_Mode"))
        {
            float mode = mat.GetFloat("_Mode");
            if (mode >= 2f) return true;
        }
        // Fallback: check render queue
        return mat.renderQueue >= 3000;
    }
}
