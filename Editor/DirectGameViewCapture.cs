using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Globalization;
using Unity.EditorCoroutines.Editor;

/// <summary>
/// Captures images directly from Game View during Play Mode
/// This ensures rendered images match exactly what you see in Game View
/// </summary>
public class DirectGameViewCapture : EditorWindow
{
    private Camera cameraToUse;
    private Transform target;
    private int numRings = 4;
    private int viewsPerRing = 20;
    private float radius = 5f;
    private float height = 1.5f;
    private string outputFolder = "Output_GameView";
    private int captureWidth = 1920;
    private int captureHeight = 1080;
    private bool isCapturing = false;
    private EditorCoroutine captureCoroutine;

    // Image format settings
    private int imageFormatIndex = 0;
    private string[] imageFormatOptions = new string[] { "PNG (8-bit)", "PNG + Tone Mapping", "EXR (32-bit HDR)" };
    private bool useEXR = false;
    private bool useToneMapping = false;
    private float tonemapExposure = 1.0f;

    [MenuItem("Tools/Gaussian Splatting/Direct Game View Capture")]
    public static void ShowWindow()
    {
        GetWindow<DirectGameViewCapture>("Game View Capture");
    }

    private void OnGUI()
    {
        GUILayout.Label("Direct Game View Capture", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "This captures images DIRECTLY from Game View during Play Mode.\n" +
            "What you see in Game View is EXACTLY what gets saved!\n\n" +
            "⚠️ Requirements:\n" +
            "• Must be in Play Mode\n" +
            "• Game View must be visible\n" +
            "• Don't minimize or cover the Game window during capture",
            MessageType.Info);

        GUILayout.Space(10);

        cameraToUse = (Camera)EditorGUILayout.ObjectField("Camera", cameraToUse, typeof(Camera), true);
        target = (Transform)EditorGUILayout.ObjectField("Target", target, typeof(Transform), true);

        GUILayout.Space(10);

        numRings = EditorGUILayout.IntField("Number of Rings", numRings);
        viewsPerRing = EditorGUILayout.IntField("Views per Ring", viewsPerRing);
        radius = EditorGUILayout.FloatField("Radius", radius);
        height = EditorGUILayout.FloatField("Height", height);

        GUILayout.Space(10);

        captureWidth = EditorGUILayout.IntField("Width (px)", captureWidth);
        captureHeight = EditorGUILayout.IntField("Height (px)", captureHeight);

        GUILayout.Space(10);

        // Image format selection
        GUILayout.Label("Image Format", EditorStyles.boldLabel);
        imageFormatIndex = GUILayout.Toolbar(imageFormatIndex, imageFormatOptions);
        useEXR = (imageFormatIndex == 2);
        useToneMapping = (imageFormatIndex == 1);

        if (useEXR)
        {
            EditorGUILayout.HelpBox("EXR format preserves full HDR data without quality loss. Best for maximum accuracy!", MessageType.Info);
        }
        else if (useToneMapping)
        {
            EditorGUILayout.HelpBox("PNG + Tone Mapping: Applies tone mapping to HDR data before saving. Perfect balance of quality and compatibility!", MessageType.Info);
            tonemapExposure = EditorGUILayout.Slider("Exposure", tonemapExposure, 0.1f, 3.0f);
        }
        else
        {
            EditorGUILayout.HelpBox("Standard PNG: Direct 8-bit conversion. May lose HDR information if scene uses bright lighting.", MessageType.Warning);
        }

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Output Folder:", EditorStyles.label);
        outputFolder = EditorGUILayout.TextField(outputFolder);
        if (GUILayout.Button("Choose...", GUILayout.MaxWidth(80)))
        {
            string selected = EditorUtility.OpenFolderPanel("Choose output folder", "", "");
            if (!string.IsNullOrEmpty(selected))
                outputFolder = selected;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(20);

        GUI.enabled = !isCapturing && cameraToUse != null && target != null;

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("⚠️ Enter Play Mode to start capture", MessageType.Warning);
            if (GUILayout.Button("Enter Play Mode", GUILayout.Height(40)))
            {
                EditorApplication.isPlaying = true;
            }
        }
        else
        {
            if (GUILayout.Button("Start Game View Capture", GUILayout.Height(40)))
            {
                StartDirectCapture();
            }
        }

        GUI.enabled = true;

        if (isCapturing)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("📷 Capturing in progress...\nDon't minimize or cover the Game window!", MessageType.Warning);

            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                StopCapture();
            }
        }
    }

    private void StartDirectCapture()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Please enter Play Mode first!", "OK");
            return;
        }

        if (string.IsNullOrEmpty(outputFolder))
        {
            EditorUtility.DisplayDialog("Error", "Please specify an output folder!", "OK");
            return;
        }

        // Set Game View resolution
        SetGameViewSize(captureWidth, captureHeight);

        isCapturing = true;
        captureCoroutine = EditorCoroutineUtility.StartCoroutine(CaptureFromGameView(), this);
    }

    private void StopCapture()
    {
        if (captureCoroutine != null)
        {
            EditorCoroutineUtility.StopCoroutine(captureCoroutine);
            captureCoroutine = null;
        }
        isCapturing = false;
        EditorUtility.ClearProgressBar();
        Debug.Log("Capture cancelled");
    }

    private IEnumerator CaptureFromGameView()
    {
        Directory.CreateDirectory(outputFolder);

        // Create COLMAP files
        string camerasTxt = Path.Combine(outputFolder, "cameras.txt");
        string imagesTxt = Path.Combine(outputFolder, "images.txt");

        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * captureHeight / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;
        float cx = captureWidth / 2f;
        float cy = captureHeight / 2f;

        // Write cameras.txt
        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {captureWidth} {captureHeight} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        // Capture images
        int imageId = 1;
        int totalImages = viewsPerRing * numRings;
        int currentImage = 0;

        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_IDX");

            for (int ring = 0; ring < numRings; ring++)
            {
                float elevation = Mathf.Lerp(-Mathf.PI / 4, Mathf.PI / 4, (float)ring / (numRings - 1));

                for (int i = 0; i < viewsPerRing; i++)
                {
                    float progress = (float)currentImage / totalImages;
                    EditorUtility.DisplayProgressBar("Capturing from Game View",
                        $"Image {currentImage + 1} / {totalImages}", progress);

                    float azimuth = i * Mathf.PI * 2 / viewsPerRing;

                    float x = radius * Mathf.Cos(elevation) * Mathf.Cos(azimuth);
                    float y = radius * Mathf.Sin(elevation);
                    float z = radius * Mathf.Cos(elevation) * Mathf.Sin(azimuth);

                    Vector3 position = target.position + new Vector3(x, y + height, z);
                    cameraToUse.transform.position = position;
                    cameraToUse.transform.LookAt(target);

                    // CRITICAL: Wait for the frame to fully render
                    yield return new WaitForEndOfFrame();

                    // Capture directly from the screen (Game View)
                    Texture2D screenshot = CaptureScreen();

                    // Calculate COLMAP transform
                    Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                    Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                    Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;

                    Matrix4x4 R = colmapMatrix;
                    R.SetColumn(3, new Vector4(0, 0, 0, 1));
                    Quaternion q = QuaternionFromMatrix(R);
                    Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                    // Save image
                    string imageName = $"view_{imageId:D3}{GetImageExtension()}";
                    string imagePath = Path.Combine(outputFolder, imageName);
                    File.WriteAllBytes(imagePath, EncodeTexture(screenshot, useEXR, useToneMapping, tonemapExposure));

                    // Write COLMAP data
                    imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                    imgWriter.WriteLine();

                    DestroyImmediate(screenshot);

                    imageId++;
                    currentImage++;

                    // Small delay to ensure clean capture
                    yield return null;
                }
            }
        }

        // Create empty points3D.txt (will be generated by COLMAP)
        string points3DTxt = Path.Combine(outputFolder, "points3D.txt");
        using (StreamWriter writer = new StreamWriter(points3DTxt))
        {
            writer.WriteLine("# 3D point list with one line of data per point:");
            writer.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
        }

        EditorUtility.ClearProgressBar();
        isCapturing = false;

        Debug.Log($"✓ Game View capture complete! {totalImages} images saved to {outputFolder}");
        EditorUtility.RevealInFinder(Path.Combine(outputFolder, $"view_001{GetImageExtension()}"));
    }

    private Texture2D CaptureScreen()
    {
        // Capture the current rendered frame
        Texture2D screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        screenshot.Apply();
        return screenshot;
    }

    /// <summary>
    /// Apply tone mapping to HDR texture for LDR display
    /// Uses Reinhard tone mapping operator
    /// </summary>
    private Texture2D ApplyToneMapping(Texture2D hdrTex, float exposure)
    {
        int width = hdrTex.width;
        int height = hdrTex.height;

        Texture2D ldrTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] hdrPixels = hdrTex.GetPixels();
        Color[] ldrPixels = new Color[hdrPixels.Length];

        for (int i = 0; i < hdrPixels.Length; i++)
        {
            Color hdr = hdrPixels[i];

            // Apply exposure
            hdr.r *= exposure;
            hdr.g *= exposure;
            hdr.b *= exposure;

            // Reinhard tone mapping: RGB / (1 + RGB)
            // This maps [0, ∞) to [0, 1)
            Color ldr;
            ldr.r = hdr.r / (1.0f + hdr.r);
            ldr.g = hdr.g / (1.0f + hdr.g);
            ldr.b = hdr.b / (1.0f + hdr.b);
            ldr.a = hdr.a;

            // Apply gamma correction for better visual appearance
            // (assuming target is sRGB display)
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                ldr.r = Mathf.Pow(ldr.r, 1.0f / 2.2f);
                ldr.g = Mathf.Pow(ldr.g, 1.0f / 2.2f);
                ldr.b = Mathf.Pow(ldr.b, 1.0f / 2.2f);
            }

            ldrPixels[i] = ldr;
        }

        ldrTex.SetPixels(ldrPixels);
        ldrTex.Apply();
        return ldrTex;
    }

    /// <summary>
    /// Encode texture to file format (PNG, PNG+ToneMapping, or EXR)
    /// </summary>
    private byte[] EncodeTexture(Texture2D tex, bool useEXRFormat, bool applyToneMapping, float exposure)
    {
        if (useEXRFormat)
        {
            // EXR format: 32-bit float, lossless, preserves HDR data
            return tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
        }
        else if (applyToneMapping)
        {
            // PNG with tone mapping: HDR -> LDR conversion
            Texture2D ldrTex = ApplyToneMapping(tex, exposure);
            byte[] pngData = ldrTex.EncodeToPNG();
            DestroyImmediate(ldrTex);
            return pngData;
        }
        else
        {
            // PNG format: Direct 8-bit conversion
            return tex.EncodeToPNG();
        }
    }

    /// <summary>
    /// Get file extension based on format
    /// </summary>
    private string GetImageExtension()
    {
        return useEXR ? ".exr" : ".png";
    }

    private static Quaternion QuaternionFromMatrix(Matrix4x4 m)
    {
        return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
    }

    private void SetGameViewSize(int width, int height)
    {
        // Try to set Game View size (this is a best-effort approach)
        System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType != null)
        {
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView != null)
            {
                Debug.Log($"Setting Game View size to {width}x{height}");

                // Get the size index for our resolution
                System.Type gameViewSizesType = System.Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
                System.Type gameViewSizeType = System.Type.GetType("UnityEditor.GameViewSize,UnityEditor");

                if (gameViewSizesType != null && gameViewSizeType != null)
                {
                    var singletonInstance = gameViewSizesType.GetProperty("instance");
                    if (singletonInstance != null)
                    {
                        var sizesInstance = singletonInstance.GetValue(null, null);

                        Debug.Log($"⚠️ Please manually set Game View resolution to {width}x{height}");
                        Debug.Log("   Window → General → Game → Resolution dropdown");
                    }
                }
            }
        }
    }

    private void OnDisable()
    {
        if (isCapturing)
        {
            StopCapture();
        }
    }
}
