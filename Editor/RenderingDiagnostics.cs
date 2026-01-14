using UnityEngine;
using UnityEditor;
using System.Text;

public class RenderingDiagnostics : EditorWindow
{
    private Camera selectedCamera;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Gaussian Splatting/Rendering Diagnostics")]
    public static void ShowWindow()
    {
        GetWindow<RenderingDiagnostics>("Rendering Diagnostics");
    }

    private void OnGUI()
    {
        GUILayout.Label("Rendering Diagnostics Tool", EditorStyles.boldLabel);
        GUILayout.Label("This tool shows why rendered images may differ from Editor view", EditorStyles.helpBox);
        GUILayout.Space(10);

        selectedCamera = (Camera)EditorGUILayout.ObjectField("Camera to Analyze", selectedCamera, typeof(Camera), true);

        GUILayout.Space(10);

        if (GUILayout.Button("Analyze Rendering Settings", GUILayout.Height(30)))
        {
            if (selectedCamera != null)
            {
                AnalyzeRenderingSettings();
            }
            else
            {
                Debug.LogWarning("Please select a camera first!");
            }
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Compare: Editor View vs Camera.Render()", GUILayout.Height(30)))
        {
            if (selectedCamera != null)
            {
                CompareRenderingMethods();
            }
            else
            {
                Debug.LogWarning("Please select a camera first!");
            }
        }
    }

    private void AnalyzeRenderingSettings()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("=== RENDERING DIAGNOSTICS REPORT ===\n");

        // 1. Project Settings
        report.AppendLine("【1. PROJECT SETTINGS】");
        report.AppendLine($"  Color Space: {PlayerSettings.colorSpace}");
        report.AppendLine($"    ⚠️ Linear vs Gamma affects brightness significantly!");
        report.AppendLine($"  Graphics API: {SystemInfo.graphicsDeviceType}");
        report.AppendLine($"  HDR Support: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)}");
        report.AppendLine();

        // 2. Camera Settings
        report.AppendLine("【2. CAMERA SETTINGS】");
        report.AppendLine($"  Camera Name: {selectedCamera.name}");
        report.AppendLine($"  Clear Flags: {selectedCamera.clearFlags}");
        report.AppendLine($"  Background Color: {selectedCamera.backgroundColor}");
        report.AppendLine($"  Culling Mask: {LayerMask.LayerToName(selectedCamera.cullingMask)}");
        report.AppendLine($"  HDR: {selectedCamera.allowHDR}");
        report.AppendLine($"  MSAA: {selectedCamera.allowMSAA}");
        report.AppendLine($"  Use Occlusion Culling: {selectedCamera.useOcclusionCulling}");
        report.AppendLine();

        // 3. Render Settings (Lighting)
        report.AppendLine("【3. SCENE LIGHTING】");
        report.AppendLine($"  Ambient Mode: {RenderSettings.ambientMode}");

        if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
        {
            report.AppendLine($"  Ambient Color: {RenderSettings.ambientLight}");
        }
        else if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
        {
            report.AppendLine($"  Sky Color: {RenderSettings.ambientSkyColor}");
            report.AppendLine($"  Equator Color: {RenderSettings.ambientEquatorColor}");
            report.AppendLine($"  Ground Color: {RenderSettings.ambientGroundColor}");
        }
        else if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox)
        {
            report.AppendLine($"  Skybox: {RenderSettings.skybox?.name ?? "None"}");
        }

        report.AppendLine($"  Ambient Intensity: {RenderSettings.ambientIntensity}");
        report.AppendLine($"  Reflection Intensity: {RenderSettings.reflectionIntensity}");
        report.AppendLine($"  Reflection Bounces: {RenderSettings.reflectionBounces}");
        report.AppendLine();

        // 4. Scene Lights
        report.AppendLine("【4. SCENE LIGHTS】");
        Light[] lights = GameObject.FindObjectsOfType<Light>();
        if (lights.Length == 0)
        {
            report.AppendLine("  ⚠️ WARNING: No lights found in scene!");
            report.AppendLine("  This will make objects very dark!");
        }
        else
        {
            foreach (Light light in lights)
            {
                if (light.enabled)
                {
                    report.AppendLine($"  • {light.name}");
                    report.AppendLine($"    Type: {light.type}");
                    report.AppendLine($"    Intensity: {light.intensity}");
                    report.AppendLine($"    Color: {light.color}");
                    report.AppendLine($"    Mode: {light.lightmapBakeType}");
                    if (light.type == LightType.Directional)
                    {
                        report.AppendLine($"    Rotation: {light.transform.rotation.eulerAngles}");
                    }
                }
            }
        }
        report.AppendLine();

        // 5. Fog Settings
        report.AppendLine("【5. FOG SETTINGS】");
        report.AppendLine($"  Fog Enabled: {RenderSettings.fog}");
        if (RenderSettings.fog)
        {
            report.AppendLine($"  Fog Color: {RenderSettings.fogColor}");
            report.AppendLine($"  Fog Mode: {RenderSettings.fogMode}");
            report.AppendLine($"  Fog Density: {RenderSettings.fogDensity}");
        }
        report.AppendLine();

        // 6. Quality Settings
        report.AppendLine("【6. QUALITY SETTINGS】");
        report.AppendLine($"  Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        report.AppendLine($"  Pixel Light Count: {QualitySettings.pixelLightCount}");
        report.AppendLine($"  Shadows: {QualitySettings.shadows}");
        report.AppendLine($"  Shadow Resolution: {QualitySettings.shadowResolution}");
        report.AppendLine($"  Shadow Distance: {QualitySettings.shadowDistance}");
        report.AppendLine($"  Anti Aliasing: {QualitySettings.antiAliasing}x");
        report.AppendLine($"  Realtime Reflection Probes: {QualitySettings.realtimeReflectionProbes}");
        report.AppendLine();

        // 7. Rendering Pipeline
        report.AppendLine("【7. RENDER PIPELINE】");
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (pipeline == null)
        {
            report.AppendLine("  Using Built-in Render Pipeline");
        }
        else
        {
            report.AppendLine($"  Using SRP: {pipeline.GetType().Name}");
        }
        report.AppendLine();

        // 8. Key Differences
        report.AppendLine("【8. EDITOR VIEW vs CAMERA.RENDER() DIFFERENCES】");
        report.AppendLine("  Editor Scene View uses:");
        report.AppendLine("    • Scene Lighting toggle (can use custom lighting)");
        report.AppendLine("    • Gizmos and overlays");
        report.AppendLine("    • Potentially different rendering path");
        report.AppendLine("    • Post-processing may not apply");
        report.AppendLine();
        report.AppendLine("  Camera.Render() uses:");
        report.AppendLine("    • Exact camera settings (clearFlags, etc.)");
        report.AppendLine("    • Scene lighting as-is");
        report.AppendLine("    • May skip some real-time effects");
        report.AppendLine("    • Renders to RenderTexture (format matters!)");
        report.AppendLine();

        report.AppendLine("=== END OF REPORT ===");

        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Analysis Complete", "Full report has been logged to Console.\n\nCheck the Console window for detailed information.", "OK");
    }

    private void CompareRenderingMethods()
    {
        StringBuilder comparison = new StringBuilder();
        comparison.AppendLine("=== EDITOR VIEW vs CAMERA.RENDER() COMPARISON ===\n");

        comparison.AppendLine("Possible reasons for differences:\n");

        // Check color space
        comparison.AppendLine($"1. COLOR SPACE: {PlayerSettings.colorSpace}");
        if (PlayerSettings.colorSpace == ColorSpace.Linear)
        {
            comparison.AppendLine("   Linear space needs proper HDR and tonemapping");
            comparison.AppendLine("   ⚠️ RenderTexture format must support HDR!");
        }
        else
        {
            comparison.AppendLine("   Gamma space - simpler but less accurate");
        }
        comparison.AppendLine();

        // Check camera settings
        comparison.AppendLine("2. CAMERA CLEAR FLAGS");
        comparison.AppendLine($"   Current: {selectedCamera.clearFlags}");
        comparison.AppendLine($"   Background: {selectedCamera.backgroundColor}");
        if (selectedCamera.clearFlags == CameraClearFlags.SolidColor && selectedCamera.backgroundColor == Color.black)
        {
            comparison.AppendLine("   ⚠️ Black background with alpha=0 may cause dark rendering!");
            comparison.AppendLine("   Solution: Use Skybox or adjust background color");
        }
        comparison.AppendLine();

        // Check HDR
        comparison.AppendLine("3. HDR SETTINGS");
        comparison.AppendLine($"   Camera HDR: {selectedCamera.allowHDR}");
        comparison.AppendLine($"   System HDR Support: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)}");
        if (!selectedCamera.allowHDR && PlayerSettings.colorSpace == ColorSpace.Linear)
        {
            comparison.AppendLine("   ⚠️ Linear color space works best with HDR enabled!");
        }
        comparison.AppendLine();

        // Check post-processing
        comparison.AppendLine("4. POST-PROCESSING");
        var postProcessLayer = selectedCamera.GetComponent(System.Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime"));
        var postProcessVolume = GameObject.FindObjectOfType(System.Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime"));

        if (postProcessLayer != null || postProcessVolume != null)
        {
            comparison.AppendLine("   Post-processing detected in scene!");
            comparison.AppendLine("   ⚠️ Post-processing may not work with Camera.Render()");
            comparison.AppendLine("   This is a common cause of differences!");
        }
        else
        {
            comparison.AppendLine("   No post-processing detected");
        }
        comparison.AppendLine();

        // Check lighting
        comparison.AppendLine("5. LIGHTING DIFFERENCES");
        Light[] lights = GameObject.FindObjectsOfType<Light>();
        int realtimeLights = 0;
        int bakedLights = 0;

        foreach (Light light in lights)
        {
            if (light.enabled)
            {
                if (light.lightmapBakeType == LightmapBakeType.Realtime)
                    realtimeLights++;
                else if (light.lightmapBakeType == LightmapBakeType.Baked)
                    bakedLights++;
            }
        }

        comparison.AppendLine($"   Realtime Lights: {realtimeLights}");
        comparison.AppendLine($"   Baked Lights: {bakedLights}");
        comparison.AppendLine($"   Ambient Mode: {RenderSettings.ambientMode}");

        if (realtimeLights == 0 && RenderSettings.ambientIntensity < 0.5f)
        {
            comparison.AppendLine("   ⚠️ Very low ambient light and no realtime lights!");
            comparison.AppendLine("   This will make everything very dark!");
        }
        comparison.AppendLine();

        // Recommendations
        comparison.AppendLine("【RECOMMENDATIONS】");
        comparison.AppendLine("To make Camera.Render() match Editor view:");
        comparison.AppendLine("  1. Check 'Scene Lighting' toggle in Scene view");
        comparison.AppendLine("     (It may be using Editor's default lighting)");
        comparison.AppendLine("  2. Add sufficient ambient/directional lighting");
        comparison.AppendLine("  3. Ensure RenderTexture format matches color space");
        comparison.AppendLine("  4. Disable post-processing if not needed");
        comparison.AppendLine("  5. Use Skybox clear flags for outdoor scenes");
        comparison.AppendLine();

        comparison.AppendLine("=== END OF COMPARISON ===");

        Debug.Log(comparison.ToString());
        EditorUtility.DisplayDialog("Comparison Complete", "Comparison report has been logged to Console.\n\nCheck the Console for detailed analysis and recommendations.", "OK");
    }
}
