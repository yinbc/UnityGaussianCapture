using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tool to compare Game View rendering vs Camera.Render() output
/// This helps identify why the same camera produces different results
/// </summary>
public class GameViewVsCameraRenderComparison : EditorWindow
{
    private Camera cameraToCompare;
    private int captureWidth = 1920;
    private int captureHeight = 1080;
    private string outputPath = "Assets/RenderComparison";

    [MenuItem("Tools/Gaussian Splatting/Compare Game View vs Camera.Render()")]
    public static void ShowWindow()
    {
        GetWindow<GameViewVsCameraRenderComparison>("Game vs Render Comparison");
    }

    private void OnGUI()
    {
        GUILayout.Label("Game View vs Camera.Render() Comparison", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool captures the same camera using two methods:\n" +
            "1. Game View rendering (normal Unity rendering)\n" +
            "2. Camera.Render() (what the capture plugin uses)\n\n" +
            "Compare the two images to see the differences.",
            MessageType.Info);

        GUILayout.Space(10);

        cameraToCompare = (Camera)EditorGUILayout.ObjectField("Camera", cameraToCompare, typeof(Camera), true);
        captureWidth = EditorGUILayout.IntField("Width", captureWidth);
        captureHeight = EditorGUILayout.IntField("Height", captureHeight);
        outputPath = EditorGUILayout.TextField("Output Folder", outputPath);

        GUILayout.Space(10);

        GUI.enabled = cameraToCompare != null;

        if (GUILayout.Button("Capture Both Methods (Play Mode)", GUILayout.Height(40)))
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Please enter Play Mode first!\n\nGame View rendering only works in Play Mode.", "OK");
            }
            else
            {
                CaptureBothMethods();
            }
        }

        GUILayout.Space(5);

        if (GUILayout.Button("Capture Camera.Render() Only (Editor Mode)", GUILayout.Height(30)))
        {
            CaptureCameraRenderOnly();
        }

        GUI.enabled = true;

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "INSTRUCTIONS:\n\n" +
            "1. Assign the camera you want to test\n" +
            "2. Enter Play Mode\n" +
            "3. Click 'Capture Both Methods'\n" +
            "4. Check the output folder for two images:\n" +
            "   - gameview_capture.png (normal rendering)\n" +
            "   - camerarender_capture.png (plugin method)\n" +
            "5. Compare the images to identify differences",
            MessageType.None);
    }

    private void CaptureBothMethods()
    {
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // Method 1: Capture from Game View (normal Unity rendering)
        CaptureFromGameView();

        // Method 2: Capture using Camera.Render() (plugin method)
        CaptureCameraRenderOnly();

        EditorUtility.DisplayDialog("Capture Complete",
            $"Both captures saved to:\n{Path.GetFullPath(outputPath)}\n\n" +
            "Files:\n" +
            "- gameview_capture.png (normal rendering)\n" +
            "- camerarender_capture.png (plugin method)\n\n" +
            "Compare these images to identify differences!",
            "Open Folder", "OK");

        if (EditorUtility.DisplayDialog("Capture Complete",
            $"Both captures saved to:\n{Path.GetFullPath(outputPath)}\n\n" +
            "Files:\n" +
            "- gameview_capture.png (normal rendering)\n" +
            "- camerarender_capture.png (plugin method)\n\n" +
            "Compare these images to identify differences!",
            "Open Folder", "OK"))
        {
            EditorUtility.RevealInFinder(Path.Combine(outputPath, "gameview_capture.png"));
        }

        LogRenderingDifferences();
    }

    private void CaptureFromGameView()
    {
        // Store original camera settings
        RenderTexture originalRT = cameraToCompare.targetTexture;

        // Create temporary RenderTexture for Game View style capture
        RenderTexture gameViewRT = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);

        // Assign to camera
        cameraToCompare.targetTexture = gameViewRT;

        // Let camera render normally (this goes through full rendering pipeline)
        cameraToCompare.Render();

        // Read pixels
        RenderTexture.active = gameViewRT;
        Texture2D screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        screenshot.Apply();

        // Save
        byte[] bytes = screenshot.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(outputPath, "gameview_capture.png"), bytes);

        // Cleanup
        cameraToCompare.targetTexture = originalRT;
        RenderTexture.active = null;
        DestroyImmediate(screenshot);
        gameViewRT.Release();
        DestroyImmediate(gameViewRT);

        Debug.Log("✓ Game View style capture saved");
    }

    private void CaptureCameraRenderOnly()
    {
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // This mimics exactly what CameraCaptureEditor.cs does
        RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 32, RenderTextureFormat.ARGBFloat);
        Texture2D tex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);

        // Store original settings
        CameraClearFlags originalClearFlags = cameraToCompare.clearFlags;
        Color originalBackgroundColor = cameraToCompare.backgroundColor;
        RenderTexture originalTargetTexture = cameraToCompare.targetTexture;

        // Apply plugin's settings
        cameraToCompare.clearFlags = CameraClearFlags.SolidColor;
        cameraToCompare.backgroundColor = new Color(0, 0, 0, 0);
        cameraToCompare.targetTexture = rt;

        // Render exactly as the plugin does
        cameraToCompare.Render();

        // Read pixels
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        tex.Apply();

        // Save
        File.WriteAllBytes(Path.Combine(outputPath, "camerarender_capture.png"), tex.EncodeToPNG());

        // Restore original settings
        cameraToCompare.clearFlags = originalClearFlags;
        cameraToCompare.backgroundColor = originalBackgroundColor;
        cameraToCompare.targetTexture = originalTargetTexture;
        RenderTexture.active = null;

        // Cleanup
        DestroyImmediate(rt);
        DestroyImmediate(tex);

        Debug.Log("✓ Camera.Render() style capture saved (mimicking plugin)");
    }

    private void LogRenderingDifferences()
    {
        Debug.Log("=== ANALYZING DIFFERENCES ===");
        Debug.Log("\n【KEY DIFFERENCES BETWEEN METHODS】\n");

        Debug.Log("1. CLEAR FLAGS:");
        Debug.Log($"   Camera current setting: {cameraToCompare.clearFlags}");
        Debug.Log("   Plugin forces: CameraClearFlags.SolidColor");
        Debug.Log("   → If your camera uses Skybox, this removes the skybox!");
        Debug.Log("");

        Debug.Log("2. BACKGROUND COLOR:");
        Debug.Log($"   Camera current: {cameraToCompare.backgroundColor}");
        Debug.Log("   Plugin forces: RGBA(0,0,0,0) - black transparent");
        Debug.Log("   → This can affect lighting calculations!");
        Debug.Log("");

        Debug.Log("3. RENDERTEXTURE FORMAT:");
        Debug.Log("   Game View: Uses default framebuffer");
        Debug.Log("   Plugin uses: RenderTextureFormat.ARGBFloat (HDR)");
        Debug.Log($"   Color Space: {PlayerSettings.colorSpace}");
        if (PlayerSettings.colorSpace == ColorSpace.Gamma)
        {
            Debug.Log("   ⚠️ WARNING: Using ARGBFloat in Gamma space may cause brightness issues!");
        }
        Debug.Log("");

        Debug.Log("4. POST-PROCESSING:");
        var postProcessLayer = cameraToCompare.GetComponent(System.Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime"));
        if (postProcessLayer != null)
        {
            Debug.Log("   ⚠️ Camera has Post Process Layer!");
            Debug.Log("   Camera.Render() may not apply post-processing in Editor mode");
        }
        else
        {
            Debug.Log("   No post-processing detected");
        }
        Debug.Log("");

        Debug.Log("5. RENDERING CONTEXT:");
        Debug.Log($"   Is Playing: {EditorApplication.isPlaying}");
        Debug.Log("   Game View: Full Unity rendering pipeline");
        Debug.Log("   Camera.Render(): Immediate render, may skip some effects");
        Debug.Log("");

        Debug.Log("=== RECOMMENDATIONS ===");
        Debug.Log("Compare the two saved images and check:");
        Debug.Log("• Is the background different? → Clear Flags issue");
        Debug.Log("• Is the brightness different? → Color space / RenderTexture format issue");
        Debug.Log("• Are effects missing? → Post-processing issue");
        Debug.Log("• Is lighting different? → Ambient/environment issue");
        Debug.Log("\nSee RENDERING_DIFFERENCES.md for detailed solutions!");
    }
}
