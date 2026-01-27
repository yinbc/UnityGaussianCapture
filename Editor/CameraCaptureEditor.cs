using UnityEngine;
using UnityEditor;
using System.IO;
using System.Globalization;
using System;

using Unity.EditorCoroutines.Editor;
using System.Collections;
using System.Collections.Generic;

public class CameraCaptureEditor : EditorWindow
{

    private int tabIndex = 0;

    private Camera cameraToUse;
    private Transform target;
    private int numRings = 4;
    private int viewsPerRing = 20;
    private float radius = 5f;
    private float height = 1.5f;
    private string outputFolder = "Output";
    private bool ShowGrid = true;
    private EditorCoroutine captureCoroutine;
    bool cancel = false;
    bool isRunning = false;
    private GameObject gizmoViewer;
    public GameObject gizmoViewerPrefab;
    bool runtimeAnim = false;
    bool TrainPostShot = false;

    private int fbs = 25;
    private float duration = 1;
    private string PostShotInstallFolder = @"C:\Program Files\Jawset Postshot\bin\postshot-cli.exe";
    private int trainStep = 5;

    private int outputFormatIndex = 0;
    private string outputFormat = "psht";

    private int profileIndex = 0;
    private string profile = "Splat3";

    private int imageFormatIndex = 0;
    private string imageFormat = "png";

    private bool useToneMapping = false;
    private float exposure = 1.0f;

    // Spherical Capture settings
    private Transform sphericalTarget;
    private int numSpherePoints = 100;
    private float sphereRadius = 5f;

    // Ellipsoid Capture settings
    private Transform ellipsoidTarget;
    private int numEllipsoidPoints = 100;
    private float ellipsoidRadiusX = 5f;
    private float ellipsoidRadiusY = 3f;
    private float ellipsoidRadiusZ = 4f;

    // Cylinder Capture settings
    private Transform cylinderTarget;
    private int numCylinderSidePoints = 50;
    private int numCylinderLayers = 3;
    private int numCylinderCapPoints = 20;
    private float cylinderRadius = 3f;
    private float cylinderHeight = 5f;

    // Combined Capture settings
    private bool useDomeInCombined = false;
    private bool useVolumeInCombined = false;
    private bool useSphericalInCombined = false;
    private bool useEllipsoidInCombined = false;
    private bool useCylinderInCombined = true;

    int w = 1920;
    int h = 1080;
    int rays = 500;
    [MenuItem("Tools/Gaussian Splatting/Capture + COLMAP")]
    public static void ShowWindow()
    {
        GetWindow<CameraCaptureEditor>("Capture + COLMAP");
    }

    private void OnGUI()
    {

        GUILayout.Space(10);
        cameraToUse = (Camera)EditorGUILayout.ObjectField("Camera", cameraToUse, typeof(Camera), true);
        w = EditorGUILayout.IntField("Width (px)", w);
        h = EditorGUILayout.IntField("Height (px)", h);
        GUILayout.Space(10);

        GUILayout.Label("Image Format", EditorStyles.boldLabel);
        imageFormatIndex = GUILayout.Toolbar(imageFormatIndex, new string[] { "PNG", "EXR" });
        imageFormat = imageFormatIndex == 0 ? "png" : "exr";

        if (imageFormat == "png")
        {
            useToneMapping = EditorGUILayout.Toggle("Use Tone Mapping (HDR)", useToneMapping);
            if (useToneMapping)
            {
                exposure = EditorGUILayout.Slider("Exposure", exposure, 0.1f, 5.0f);
            }
        }

        GUILayout.Space(10);
        rays = EditorGUILayout.IntField("PointCloud/View", rays);
        GUILayout.Space(10);
        runtimeAnim = EditorGUILayout.Toggle("Capture Runtime", runtimeAnim);

        if (runtimeAnim)
        {
            fbs = EditorGUILayout.IntField("Frames/Second", fbs);
            duration = EditorGUILayout.FloatField("Duration", duration);
        }

        TrainPostShot = EditorGUILayout.Toggle("Train On PostShot", TrainPostShot);
        if (TrainPostShot)
        {
            PostShotInstallFolder = EditorGUILayout.TextField("PostShot Install Folder", PostShotInstallFolder);
            trainStep = EditorGUILayout.IntField("Training Steps", trainStep);

            GUILayout.Label("Output Format", EditorStyles.boldLabel);
            outputFormatIndex = GUILayout.Toolbar(outputFormatIndex, new string[] { "PSHT", "PLY" });
            outputFormat = outputFormatIndex == 0 ? "psht" : "ply";
            GUILayout.Label("Training Profile", EditorStyles.boldLabel);
            profileIndex = GUILayout.Toolbar(profileIndex, new string[] { "Splat3", "MCMC", "ADC" });
            profile = new string[] { "Splat3", "MCMC", "ADC" }[profileIndex];
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Ouptput folder :", EditorStyles.label);

        outputFolder = EditorGUILayout.TextField(outputFolder);

        if (GUILayout.Button("Choose...", GUILayout.MaxWidth(80)))
        {
            string selected = EditorUtility.OpenFolderPanel("Choose an output folder", "", "");
            if (!string.IsNullOrEmpty(selected))
                outputFolder = selected;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        tabIndex = GUILayout.Toolbar(tabIndex, new string[] { "Dome Capture", "Volume Capture", "Spherical Capture", "Ellipsoid Capture", "Cylinder Capture", "Combined Capture" });


        if (tabIndex == 0)
            DrawSphericalCaptureUI();
        else if (tabIndex == 1)
            DrawVolumeCaptureUI();
        else if (tabIndex == 2)
            DrawFullSphereCaptureUI();
        else if (tabIndex == 3)
            DrawEllipsoidCaptureUI();
        else if (tabIndex == 4)
            DrawCylinderCaptureUI();
        else
            DrawCombinedCaptureUI();


        GUILayout.Space(20);



        if (isRunning)
            if (GUILayout.Button("Cancel"))
                {
                cancel = true;
                }


    }

    private static Quaternion QuaternionFromMatrix(Matrix4x4 m)
    {
        return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
    }

    private void RunPostshotBatch()
    {
        Debug.Log("Launching PostShot Training");
        string postshotPath = PostShotInstallFolder;

        if (!Directory.Exists(outputFolder))
        {
            Debug.LogError("Le dossier de sortie n'existe pas.");
            return;
        }

        string[] subDirs = Directory.GetDirectories(outputFolder);
        List<string> foldersToProcess = subDirs.Length > 0
            ? new List<string>(subDirs)
            : new List<string> { outputFolder };

        List<string> commands = new List<string>();

        foreach (string folder in foldersToProcess)
        {
            string folderName = new DirectoryInfo(folder).Name;

            string profileCLI = profile switch
            {
                "Splat3" => "Splat3",
                "MCMC" => "Splat MCMC",
                "ADC" => "Splat ADC",
                _ => throw new System.Exception("Profil inconnu")
            };

            string outputFile = Path.Combine(outputFolder, $"{folderName}.{outputFormat}");

            string command = $"\"{postshotPath}\" train -i \"{folder}\" -s {trainStep} --profile \"{profileCLI}\"";

            if (outputFormat == "ply")
                command += $" --export-splat-ply \"{outputFile}\"";
            else
                command += $" --output \"{outputFile}\"";

            commands.Add(command);
        }

        string tempBatPath = Path.Combine(Path.GetTempPath(), "postshot_batch.bat");

        File.WriteAllLines(tempBatPath, commands);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/K \"{tempBatPath}\"",
            UseShellExecute = true
        };

        System.Diagnostics.Process.Start(psi);
    }





    private void Update()
    {
        if (gizmoViewer == null)
        {

            CameraDomeGizmo[] scripts = FindObjectsOfType<CameraDomeGizmo>();
            if (scripts.Length > 0)
                foreach (CameraDomeGizmo script in scripts)
                {
                    gizmoViewer = script.gameObject;
                }
            else if (gizmoViewerPrefab != null)
                gizmoViewer = Instantiate(gizmoViewerPrefab);


        }
        else
        {
            gizmoViewer.GetComponent<CameraDomeGizmo>().target = target;
            gizmoViewer.GetComponent<CameraDomeGizmo>().numRings = numRings;
            gizmoViewer.GetComponent<CameraDomeGizmo>().viewsPerRing = viewsPerRing;
            gizmoViewer.GetComponent<CameraDomeGizmo>().radius = radius;
            gizmoViewer.GetComponent<CameraDomeGizmo>().height = height;

            gizmoViewer.GetComponent<CameraDomeGizmo>().mode = tabIndex;
            gizmoViewer.GetComponent<CameraDomeGizmo>().volumeCenter = volumeCenter;
            gizmoViewer.GetComponent<CameraDomeGizmo>().volumeSize = volumeSize;
            gizmoViewer.GetComponent<CameraDomeGizmo>().subdivX = subdivX;
            gizmoViewer.GetComponent<CameraDomeGizmo>().subdivY = subdivY;
            gizmoViewer.GetComponent<CameraDomeGizmo>().subdivZ = subdivZ;
            gizmoViewer.GetComponent<CameraDomeGizmo>().showGrid = ShowGrid;

            // Spherical capture parameters
            gizmoViewer.GetComponent<CameraDomeGizmo>().sphericalTarget = sphericalTarget;
            gizmoViewer.GetComponent<CameraDomeGizmo>().numSpherePoints = numSpherePoints;
            gizmoViewer.GetComponent<CameraDomeGizmo>().sphereRadius = sphereRadius;

            // Ellipsoid capture parameters
            gizmoViewer.GetComponent<CameraDomeGizmo>().ellipsoidTarget = ellipsoidTarget;
            gizmoViewer.GetComponent<CameraDomeGizmo>().numEllipsoidPoints = numEllipsoidPoints;
            gizmoViewer.GetComponent<CameraDomeGizmo>().ellipsoidRadiusX = ellipsoidRadiusX;
            gizmoViewer.GetComponent<CameraDomeGizmo>().ellipsoidRadiusY = ellipsoidRadiusY;
            gizmoViewer.GetComponent<CameraDomeGizmo>().ellipsoidRadiusZ = ellipsoidRadiusZ;

            // Cylinder capture parameters
            gizmoViewer.GetComponent<CameraDomeGizmo>().cylinderTarget = cylinderTarget;
            gizmoViewer.GetComponent<CameraDomeGizmo>().numCylinderSidePoints = numCylinderSidePoints;
            gizmoViewer.GetComponent<CameraDomeGizmo>().numCylinderLayers = numCylinderLayers;
            gizmoViewer.GetComponent<CameraDomeGizmo>().numCylinderCapPoints = numCylinderCapPoints;
            gizmoViewer.GetComponent<CameraDomeGizmo>().cylinderRadius = cylinderRadius;
            gizmoViewer.GetComponent<CameraDomeGizmo>().cylinderHeight = cylinderHeight;

        }
    }



    private void DrawSphericalCaptureUI()
    {
        GUILayout.Label("Capture Settings", EditorStyles.boldLabel);

        target = (Transform)EditorGUILayout.ObjectField("Target", target, typeof(Transform), true);
        numRings = EditorGUILayout.IntField("Number of Rings", numRings);
        viewsPerRing = EditorGUILayout.IntField("Views per Ring", viewsPerRing);
        radius = EditorGUILayout.FloatField("Radius", radius);
        height = EditorGUILayout.FloatField("Height", height);

        GUILayout.Space(10);
        if (!isRunning)
            if (GUILayout.Button("Capture and Export COLMAP"))
            {
                if (cameraToUse == null || target == null || string.IsNullOrEmpty(outputFolder))
                {
                    Debug.LogError("Please assign a camera and an output folder.");
                    return;
                }

                StartCaptureDome(runtimeAnim);
            }
    }
    private Vector3 volumeCenter = Vector3.zero;
    private Vector3 volumeSize = new Vector3(5, 5, 5);
    private int subdivX = 2, subdivY = 2, subdivZ = 2;

    private void DrawVolumeCaptureUI()
    {
        GUILayout.Label("Volume Settings", EditorStyles.boldLabel);
        volumeCenter = EditorGUILayout.Vector3Field("Volume Center", volumeCenter);
        volumeSize = EditorGUILayout.Vector3Field("Volume Size", volumeSize);
        subdivX = EditorGUILayout.IntField("Subdivisions X", subdivX);
        subdivY = EditorGUILayout.IntField("Subdivisions Y", subdivY);
        subdivZ = EditorGUILayout.IntField("Subdivisions Z", subdivZ);
        ShowGrid = EditorGUILayout.Toggle("Show Grid", ShowGrid);
        
        if (!isRunning)
            if (GUILayout.Button("Capture and Export COLMAP"))
            {
                if (cameraToUse == null || string.IsNullOrEmpty(outputFolder))
                {
                    Debug.LogError("Please assign a camera and an output folder.");
                    return;
                }

                StartCaptureVolume(runtimeAnim);
            }
    }

    private void DrawFullSphereCaptureUI()
    {
        GUILayout.Label("Spherical Capture Settings", EditorStyles.boldLabel);

        sphericalTarget = (Transform)EditorGUILayout.ObjectField("Target", sphericalTarget, typeof(Transform), true);
        numSpherePoints = EditorGUILayout.IntField("Number of Points", numSpherePoints);
        sphereRadius = EditorGUILayout.FloatField("Radius", sphereRadius);

        GUILayout.Space(10);
        if (!isRunning)
            if (GUILayout.Button("Capture and Export COLMAP"))
            {
                if (cameraToUse == null || sphericalTarget == null || string.IsNullOrEmpty(outputFolder))
                {
                    Debug.LogError("Please assign a camera, target and an output folder.");
                    return;
                }

                StartCaptureFullSphere(runtimeAnim);
            }
    }

    private void DrawEllipsoidCaptureUI()
    {
        GUILayout.Label("Ellipsoid Capture Settings", EditorStyles.boldLabel);

        ellipsoidTarget = (Transform)EditorGUILayout.ObjectField("Target", ellipsoidTarget, typeof(Transform), true);
        numEllipsoidPoints = EditorGUILayout.IntField("Number of Points", numEllipsoidPoints);
        ellipsoidRadiusX = EditorGUILayout.FloatField("Radius X", ellipsoidRadiusX);
        ellipsoidRadiusY = EditorGUILayout.FloatField("Radius Y", ellipsoidRadiusY);
        ellipsoidRadiusZ = EditorGUILayout.FloatField("Radius Z", ellipsoidRadiusZ);

        GUILayout.Space(10);
        if (!isRunning)
            if (GUILayout.Button("Capture and Export COLMAP"))
            {
                if (cameraToUse == null || ellipsoidTarget == null || string.IsNullOrEmpty(outputFolder))
                {
                    Debug.LogError("Please assign a camera, target and an output folder.");
                    return;
                }

                StartCaptureEllipsoid(runtimeAnim);
            }
    }

    private void DrawCylinderCaptureUI()
    {
        GUILayout.Label("Cylinder Capture Settings", EditorStyles.boldLabel);

        cylinderTarget = (Transform)EditorGUILayout.ObjectField("Target", cylinderTarget, typeof(Transform), true);
        numCylinderSidePoints = EditorGUILayout.IntField("Side Points (per layer)", numCylinderSidePoints);
        numCylinderLayers = EditorGUILayout.IntField("Side Layers", numCylinderLayers);
        numCylinderCapPoints = EditorGUILayout.IntField("Cap Points", numCylinderCapPoints);
        cylinderRadius = EditorGUILayout.FloatField("Radius", cylinderRadius);
        cylinderHeight = EditorGUILayout.FloatField("Height", cylinderHeight);

        GUILayout.Space(10);
        if (!isRunning)
            if (GUILayout.Button("Capture and Export COLMAP"))
            {
                if (cameraToUse == null || cylinderTarget == null || string.IsNullOrEmpty(outputFolder))
                {
                    Debug.LogError("Please assign a camera, target and an output folder.");
                    return;
                }

                StartCaptureCylinder(runtimeAnim);
            }
    }

    private void DrawCombinedCaptureUI()
    {
        GUILayout.Label("Combined Capture Settings", EditorStyles.boldLabel);
        GUILayout.Label("Select capture modes to combine:", EditorStyles.label);

        GUILayout.Space(5);
        useDomeInCombined = EditorGUILayout.ToggleLeft("Include Dome Capture", useDomeInCombined);
        if (useDomeInCombined)
        {
            EditorGUI.indentLevel++;
            target = (Transform)EditorGUILayout.ObjectField("Target", target, typeof(Transform), true);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(5);
        useVolumeInCombined = EditorGUILayout.ToggleLeft("Include Volume Capture", useVolumeInCombined);

        GUILayout.Space(5);
        useSphericalInCombined = EditorGUILayout.ToggleLeft("Include Spherical Capture", useSphericalInCombined);
        if (useSphericalInCombined)
        {
            EditorGUI.indentLevel++;
            sphericalTarget = (Transform)EditorGUILayout.ObjectField("Target", sphericalTarget, typeof(Transform), true);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(5);
        useEllipsoidInCombined = EditorGUILayout.ToggleLeft("Include Ellipsoid Capture", useEllipsoidInCombined);
        if (useEllipsoidInCombined)
        {
            EditorGUI.indentLevel++;
            ellipsoidTarget = (Transform)EditorGUILayout.ObjectField("Target", ellipsoidTarget, typeof(Transform), true);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(5);
        useCylinderInCombined = EditorGUILayout.ToggleLeft("Include Cylinder Capture", useCylinderInCombined);
        if (useCylinderInCombined)
        {
            EditorGUI.indentLevel++;
            cylinderTarget = (Transform)EditorGUILayout.ObjectField("Target", cylinderTarget, typeof(Transform), true);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(10);
        if (!isRunning)
            if (GUILayout.Button("Capture All Selected Modes"))
            {
                if (cameraToUse == null || string.IsNullOrEmpty(outputFolder))
                {
                    Debug.LogError("Please assign a camera and an output folder.");
                    return;
                }

                // Check if at least one mode is selected
                if (!useDomeInCombined && !useVolumeInCombined && !useSphericalInCombined && !useEllipsoidInCombined && !useCylinderInCombined)
                {
                    Debug.LogError("Please select at least one capture mode.");
                    return;
                }

                StartCaptureCombined(runtimeAnim);
            }
    }

    public IEnumerator CaptureViewsAndExportColmap(string outAdd)
    {
        isRunning = true;

        string folderPath = outputFolder+outAdd;
        Directory.CreateDirectory(folderPath);

        // Create COLMAP standard directory structure
        string imagesFolder = Path.Combine(folderPath, "images");
        string sparseFolder = Path.Combine(folderPath, "sparse", "0");
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(sparseFolder);

        // === cameras.txt ===
        string camerasTxt = Path.Combine(sparseFolder, "cameras.txt");


        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * h / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy; 

        float cx = w / 2f;
        float cy = h / 2f;


        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {w} {h} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        // === images.txt ===
        string imagesTxt = Path.Combine(sparseFolder, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            // Use ARGBFloat for EXR or tone mapping (linear, HDR), Default for PNG (sRGB, prevents dark images)
            RenderTextureFormat rtFormat = (imageFormat == "exr" || useToneMapping) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.Default;
            RenderTexture rt = new RenderTexture(w, h, 32, rtFormat);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            int imageId = 1;
            int batchSize = 40;
            int batchCounter = 0;
            int totalImages = viewsPerRing*numRings;
            int currentImage = 0;
            StreamWriter writer3D = new StreamWriter(Path.Combine(sparseFolder, "points3D.txt"));
            writer3D.WriteLine("# 3D point list with one line of data per point:");
            writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
            int pointId = 1;


            foreach (SkinnedMeshRenderer r in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (!r.GetComponent<MeshCollider>())
                {
                    r.gameObject.AddComponent<MeshCollider>();
                }

                Mesh bakedMesh = new Mesh();
                r.BakeMesh(bakedMesh);

                r.GetComponent<MeshCollider>().sharedMesh = null; 
                r.GetComponent<MeshCollider>().sharedMesh = bakedMesh;


            }

            for (int ring = 0; ring < numRings; ring++)
            {
                float elevation = Mathf.Lerp(-Mathf.PI / 4, Mathf.PI / 4, (float)ring / (numRings - 1)); 

                for (int i = 0; i < viewsPerRing; i++)
                {
                    float progress = (float)currentImage / totalImages;
                    EditorUtility.DisplayProgressBar("Capture Views", $"Image {currentImage + 1} / {totalImages} ", progress);
                    float azimuth = i * Mathf.PI * 2 / viewsPerRing;

                    float x = radius * Mathf.Cos(elevation) * Mathf.Cos(azimuth);
                    float y = radius * Mathf.Sin(elevation);
                    float z = radius * Mathf.Cos(elevation) * Mathf.Sin(azimuth);

                    Vector3 position = target.position + new Vector3(x, y + height, z);
                    cameraToUse.transform.position = position;
                    cameraToUse.transform.LookAt(target);

                    Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                    Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                    Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;

                    Matrix4x4 R = colmapMatrix;
                    R.SetColumn(3, new Vector4(0, 0, 0, 1));
                    Quaternion q = QuaternionFromMatrix(R);
                    Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                    string imageName = $"view_{imageId:D3}.{imageFormat}";
                    string imagePath = Path.Combine(imagesFolder, imageName);

                    cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                    cameraToUse.backgroundColor = new Color(0, 0, 0, 0);

                    cameraToUse.targetTexture = rt;
                    cameraToUse.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    CapturePointCloudFromCamera(cameraToUse, tex, rays, writer3D, imageId, ref pointId);

                    byte[] imageData;
                    if (imageFormat == "exr")
                    {
                        imageData = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                    }
                    else if (useToneMapping)
                    {
                        Texture2D ldrTex = ApplyToneMapping(tex, exposure);
                        imageData = ldrTex.EncodeToPNG();
                        DestroyImmediate(ldrTex);
                    }
                    else
                    {
                        imageData = tex.EncodeToPNG();
                    }
                    File.WriteAllBytes(imagePath, imageData);

                    imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                    imgWriter.WriteLine();

                    imageId++;

                    batchCounter++;
                    currentImage++;
                    if (cancel)
                    {
                        Debug.LogWarning("Capture canceled.");
                        EditorUtility.ClearProgressBar();
                        cancel = false;
                        isRunning = false;

                        yield break;
                    }

                    if (batchCounter >= batchSize)
                    {
                        batchCounter = 0;

                        cameraToUse.targetTexture = null;
                        RenderTexture.active = null;
                        GL.Clear(true, true, Color.clear);

                        tex = null;
                        rt.Release();
                        rt = null;

                        DestroyImmediate(rt);
                        DestroyImmediate(tex);

                        EditorUtility.UnloadUnusedAssetsImmediate();
                        AssetDatabase.SaveAssets();
                        EditorApplication.QueuePlayerLoopUpdate();

                        GC.Collect();
                        GC.WaitForPendingFinalizers();



                        rt = new RenderTexture(w, h, 32, rtFormat);
                        tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                        yield return null;

                    }

                }
            }
            writer3D.Close();

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(tex);
        }


        Debug.Log("Captures + COLMAP files finished !");
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        if (!runtimeAnim)
            EditorUtility.RevealInFinder(folderPath);
        isRunning = false;

        if (!runtimeAnim && TrainPostShot)
        {
            RunPostshotBatch();
        }

        yield return new WaitForEndOfFrame();
        if (EditorApplication.isPaused == true)
            EditorApplication.isPaused = false;

    }
    private IEnumerator WaitForPlayAndCapture(int captureMode)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("You need to be in Play Mode to record a sequence.");
        }
        else
        {
            yield return new EditorWaitForSeconds(0.5f);

            int totalFrames = Mathf.RoundToInt(duration * fbs);


            GameObject.FindObjectsOfType<CameraDomeGizmo>()[0].GetComponent<CameraDomeGizmo>().currentTime = 0;
            isRunning = true;
            for (int i = 0; i < totalFrames; i++)
            {
                Debug.Log($"[Editor] Step to frame {i}");


                float targetTime = i / (float)fbs;
                while (GameObject.FindObjectsOfType<CameraDomeGizmo>()[0].GetComponent<CameraDomeGizmo>().currentTime < targetTime)
                {
                    yield return null;
                }

                EditorApplication.isPaused = true;

                if (cancel)
                {
                    Debug.LogWarning("Capture canceled.");
                    EditorUtility.ClearProgressBar();
                    cancel = false;
                    isRunning = false;

                    yield break;
                }
                var window = GetWindow<CameraCaptureEditor>();

                // captureMode: 0=Dome, 1=Volume, 2=Spherical, 3=Ellipsoid, 4=Cylinder, 5=Combined
                if (captureMode == 0)
                {
                    yield return window.StartCoroutine(window.CaptureViewsAndExportColmap("/" + i + "/"));
                }
                else if (captureMode == 1)
                {
                    yield return window.StartCoroutine(window.CaptureVolumeViewsAndExportColmap("/" + i + "/"));
                }
                else if (captureMode == 2)
                {
                    yield return window.StartCoroutine(window.CaptureFullSphereViewsAndExportColmap("/" + i + "/"));
                }
                else if (captureMode == 3)
                {
                    yield return window.StartCoroutine(window.CaptureEllipsoidViewsAndExportColmap("/" + i + "/"));
                }
                else if (captureMode == 4)
                {
                    yield return window.StartCoroutine(window.CaptureCylinderViewsAndExportColmap("/" + i + "/"));
                }
                else if (captureMode == 5)
                {
                    yield return window.StartCoroutine(window.CaptureCombinedViewsAndExportColmap("/" + i + "/"));
                }
            }
            EditorUtility.RevealInFinder(outputFolder);


            if (TrainPostShot)
                RunPostshotBatch();
            EditorApplication.isPlaying = false;
        }
    }


    public IEnumerator CaptureVolumeViewsAndExportColmap(string outAdd)
    {


        isRunning = true;
        string folderPath = outputFolder + outAdd;
        Directory.CreateDirectory(folderPath);

        // Create COLMAP standard directory structure
        string imagesFolder = Path.Combine(folderPath, "images");
        string sparseFolder = Path.Combine(folderPath, "sparse", "0");
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(sparseFolder);

        // === cameras.txt ===
        string camerasTxt = Path.Combine(sparseFolder, "cameras.txt");


        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * h / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;

        float cx = w / 2f;
        float cy = h / 2f;


        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {w} {h} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        // === images.txt ===
        string imagesTxt = Path.Combine(sparseFolder, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            // Use ARGBFloat for EXR or tone mapping (linear, HDR), Default for PNG (sRGB, prevents dark images)
            RenderTextureFormat rtFormat = (imageFormat == "exr" || useToneMapping) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.Default;
            RenderTexture rt = new RenderTexture(w, h, 32, rtFormat);

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);


            int imageId = 1;
            Vector3 step = new Vector3(volumeSize.x / subdivX, volumeSize.y / subdivY, volumeSize.z / subdivZ);
            int totalCells = subdivX * subdivY * subdivZ;
            List<Vector3> directions = GenerateCustomSphericalDirections();

            int totalImages = subdivX * subdivY * subdivZ * directions.Count;
            int currentImage = 0;
            int batchSize = 40;
            int batchCounter = 0;
            int imagesSkipped = 0;

            StreamWriter writer3D = new StreamWriter(Path.Combine(sparseFolder, "points3D.txt"));
            writer3D.WriteLine("# 3D point list with one line of data per point:");
            writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
            int pointId = 1;


            for (int x = 0; x < subdivX; x++)
            {
                for (int y = 0; y < subdivY; y++)
                {
                    for (int z = 0; z < subdivZ; z++)
                    {
                        Vector3 cellCenter = volumeCenter - volumeSize / 2f + step * 0.5f + new Vector3(x * step.x, y * step.y, z * step.z);



                        foreach (Vector3 dir in directions)
                        {


                            float progress = (float)currentImage / totalImages;
                            EditorUtility.DisplayProgressBar("Capture 3D Volume", $"Image {currentImage + 1} / {totalImages} | Images Skipped : {imagesSkipped}", progress);

                            cameraToUse.transform.position = cellCenter;
                            cameraToUse.transform.rotation = Quaternion.LookRotation(dir);

                            Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                            Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                            Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;



                            Matrix4x4 R = colmapMatrix;
                            R.SetColumn(3, new Vector4(0, 0, 0, 1));
                            Quaternion q = QuaternionFromMatrix(R);
                            Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                            string imageName = $"vol_{imageId:D4}.{imageFormat}";
                            string imagePath = Path.Combine(imagesFolder, imageName);


                            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cameraToUse);
                            bool objectVisible = false;


                            foreach (SkinnedMeshRenderer r in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
                            {
                                if (!r.GetComponent<MeshCollider>())
                                {
                                    r.gameObject.AddComponent<MeshCollider>();
                                }

                                Mesh bakedMesh = new Mesh();
                                r.BakeMesh(bakedMesh);

                                r.GetComponent<MeshCollider>().sharedMesh = null;
                                r.GetComponent<MeshCollider>().sharedMesh = bakedMesh;


                            }

                            foreach (Renderer renderer in GameObject.FindObjectsOfType<Renderer>())
                            {
                                if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                                {
                                    objectVisible = true;
                                    break;
                                }
                            }

                            if (!objectVisible)
                            {
                                imagesSkipped++;
                                continue;
                            }


                            cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                            cameraToUse.backgroundColor = new Color(0, 0, 0, 0); 

                            cameraToUse.targetTexture = rt;
                            cameraToUse.Render();
                            RenderTexture.active = rt;
                            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                            tex.Apply();
                            CapturePointCloudFromCamera(cameraToUse, tex, rays, writer3D, imageId, ref pointId);

                            byte[] imageData;
                            if (imageFormat == "exr")
                            {
                                imageData = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                            }
                            else if (useToneMapping)
                            {
                                Texture2D ldrTex = ApplyToneMapping(tex, exposure);
                                imageData = ldrTex.EncodeToPNG();
                                DestroyImmediate(ldrTex);
                            }
                            else
                            {
                                imageData = tex.EncodeToPNG();
                            }
                            File.WriteAllBytes(imagePath, imageData);
                            imageData = null;


                            imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                            imgWriter.WriteLine();

                            imageId++;

                            currentImage++;

                            batchCounter++;

                            if (cancel)
                            {
                                Debug.LogWarning("Capture canceled.");
                                EditorUtility.ClearProgressBar();
                                cancel = false;
                                isRunning = false;

                                yield break;
                            }

                            if (batchCounter >= batchSize)
                            {
                                batchCounter = 0;

                                cameraToUse.targetTexture = null;
                                RenderTexture.active = null;
                                GL.Clear(true, true, Color.clear);

                                tex = null;
                                rt.Release();
                                rt = null;

                                DestroyImmediate(rt);
                                DestroyImmediate(tex);

                                EditorUtility.UnloadUnusedAssetsImmediate();
                                AssetDatabase.SaveAssets();
                                EditorApplication.QueuePlayerLoopUpdate();

                                GC.Collect();
                                GC.WaitForPendingFinalizers();



                                rt = new RenderTexture(w, h, 32, rtFormat);
                                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                                yield return null;

                            }



                        }
                    }
                }
            }

            writer3D.Close();

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;

            DestroyImmediate(rt);
            DestroyImmediate(tex);


        }


        Debug.Log("Captures + COLMAP files finished !");
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        if (!runtimeAnim)
            EditorUtility.RevealInFinder(folderPath);
        isRunning = false;

        if (!runtimeAnim && TrainPostShot)
        {
            RunPostshotBatch();
        }

        yield return new WaitForEndOfFrame();
        if (EditorApplication.isPaused == true)
            EditorApplication.isPaused = false;

    }

    public IEnumerator CaptureFullSphereViewsAndExportColmap(string outAdd)
    {
        isRunning = true;

        string folderPath = outputFolder + outAdd;
        Directory.CreateDirectory(folderPath);

        // Create COLMAP standard directory structure
        string imagesFolder = Path.Combine(folderPath, "images");
        string sparseFolder = Path.Combine(folderPath, "sparse", "0");
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(sparseFolder);

        // === cameras.txt ===
        string camerasTxt = Path.Combine(sparseFolder, "cameras.txt");

        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * h / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;

        float cx = w / 2f;
        float cy = h / 2f;

        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {w} {h} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        // === images.txt ===
        string imagesTxt = Path.Combine(sparseFolder, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            // Use ARGBFloat for EXR or tone mapping (linear, HDR), Default for PNG (sRGB, prevents dark images)
            RenderTextureFormat rtFormat = (imageFormat == "exr" || useToneMapping) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.Default;
            RenderTexture rt = new RenderTexture(w, h, 32, rtFormat);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            int imageId = 1;
            int batchSize = 40;
            int batchCounter = 0;

            StreamWriter writer3D = new StreamWriter(Path.Combine(sparseFolder, "points3D.txt"));
            writer3D.WriteLine("# 3D point list with one line of data per point:");
            writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
            int pointId = 1;

            // Generate Fibonacci sphere points
            List<Vector3> spherePoints = GenerateFibonacciSpherePoints(numSpherePoints, sphereRadius, sphericalTarget.position);

            int totalImages = spherePoints.Count;
            int currentImage = 0;

            // Bake skinned meshes for collision detection
            foreach (SkinnedMeshRenderer r in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (!r.GetComponent<MeshCollider>())
                {
                    r.gameObject.AddComponent<MeshCollider>();
                }

                Mesh bakedMesh = new Mesh();
                r.BakeMesh(bakedMesh);

                r.GetComponent<MeshCollider>().sharedMesh = null;
                r.GetComponent<MeshCollider>().sharedMesh = bakedMesh;
            }

            foreach (Vector3 position in spherePoints)
            {
                float progress = (float)currentImage / totalImages;
                EditorUtility.DisplayProgressBar("Capture Full Sphere", $"Image {currentImage + 1} / {totalImages}", progress);

                cameraToUse.transform.position = position;
                cameraToUse.transform.LookAt(sphericalTarget);

                Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;

                Matrix4x4 R = colmapMatrix;
                R.SetColumn(3, new Vector4(0, 0, 0, 1));
                Quaternion q = QuaternionFromMatrix(R);
                Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                string imageName = $"sphere_{imageId:D4}.{imageFormat}";
                string imagePath = Path.Combine(imagesFolder, imageName);

                cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                cameraToUse.backgroundColor = new Color(0, 0, 0, 0);

                cameraToUse.targetTexture = rt;
                cameraToUse.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                CapturePointCloudFromCamera(cameraToUse, tex, rays, writer3D, imageId, ref pointId);

                byte[] imageData;
                if (imageFormat == "exr")
                {
                    imageData = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                }
                else if (useToneMapping)
                {
                    Texture2D ldrTex = ApplyToneMapping(tex, exposure);
                    imageData = ldrTex.EncodeToPNG();
                    DestroyImmediate(ldrTex);
                }
                else
                {
                    imageData = tex.EncodeToPNG();
                }
                File.WriteAllBytes(imagePath, imageData);

                imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                imgWriter.WriteLine();

                imageId++;
                batchCounter++;
                currentImage++;

                if (cancel)
                {
                    Debug.LogWarning("Capture canceled.");
                    EditorUtility.ClearProgressBar();
                    cancel = false;
                    isRunning = false;

                    yield break;
                }

                if (batchCounter >= batchSize)
                {
                    batchCounter = 0;

                    cameraToUse.targetTexture = null;
                    RenderTexture.active = null;
                    GL.Clear(true, true, Color.clear);

                    tex = null;
                    rt.Release();
                    rt = null;

                    DestroyImmediate(rt);
                    DestroyImmediate(tex);

                    EditorUtility.UnloadUnusedAssetsImmediate();
                    AssetDatabase.SaveAssets();
                    EditorApplication.QueuePlayerLoopUpdate();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    rt = new RenderTexture(w, h, 32, rtFormat);
                    tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                    yield return null;
                }
            }

            writer3D.Close();

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(tex);
        }

        Debug.Log("Spherical Capture + COLMAP files finished!");
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        if (!runtimeAnim)
            EditorUtility.RevealInFinder(folderPath);
        isRunning = false;

        if (!runtimeAnim && TrainPostShot)
        {
            RunPostshotBatch();
        }

        yield return new WaitForEndOfFrame();
        if (EditorApplication.isPaused == true)
            EditorApplication.isPaused = false;
    }

    public IEnumerator CaptureEllipsoidViewsAndExportColmap(string outAdd)
    {
        isRunning = true;

        string folderPath = outputFolder + outAdd;
        Directory.CreateDirectory(folderPath);

        // Create COLMAP standard directory structure
        string imagesFolder = Path.Combine(folderPath, "images");
        string sparseFolder = Path.Combine(folderPath, "sparse", "0");
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(sparseFolder);

        // === cameras.txt ===
        string camerasTxt = Path.Combine(sparseFolder, "cameras.txt");

        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * h / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;

        float cx = w / 2f;
        float cy = h / 2f;

        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {w} {h} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        // === images.txt ===
        string imagesTxt = Path.Combine(sparseFolder, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            // Use ARGBFloat for EXR or tone mapping (linear, HDR), Default for PNG (sRGB, prevents dark images)
            RenderTextureFormat rtFormat = (imageFormat == "exr" || useToneMapping) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.Default;
            RenderTexture rt = new RenderTexture(w, h, 32, rtFormat);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            int imageId = 1;
            int batchSize = 40;
            int batchCounter = 0;

            StreamWriter writer3D = new StreamWriter(Path.Combine(sparseFolder, "points3D.txt"));
            writer3D.WriteLine("# 3D point list with one line of data per point:");
            writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
            int pointId = 1;

            // Generate Fibonacci ellipsoid points
            List<Vector3> ellipsoidPoints = GenerateFibonacciEllipsoidPoints(numEllipsoidPoints, ellipsoidRadiusX, ellipsoidRadiusY, ellipsoidRadiusZ, ellipsoidTarget.position);

            int totalImages = ellipsoidPoints.Count;
            int currentImage = 0;

            // Bake skinned meshes for collision detection
            foreach (SkinnedMeshRenderer r in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (!r.GetComponent<MeshCollider>())
                {
                    r.gameObject.AddComponent<MeshCollider>();
                }

                Mesh bakedMesh = new Mesh();
                r.BakeMesh(bakedMesh);

                r.GetComponent<MeshCollider>().sharedMesh = null;
                r.GetComponent<MeshCollider>().sharedMesh = bakedMesh;
            }

            foreach (Vector3 position in ellipsoidPoints)
            {
                float progress = (float)currentImage / totalImages;
                EditorUtility.DisplayProgressBar("Capture Ellipsoid", $"Image {currentImage + 1} / {totalImages}", progress);

                cameraToUse.transform.position = position;
                cameraToUse.transform.LookAt(ellipsoidTarget);

                Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;

                Matrix4x4 R = colmapMatrix;
                R.SetColumn(3, new Vector4(0, 0, 0, 1));
                Quaternion q = QuaternionFromMatrix(R);
                Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                string imageName = $"ellipsoid_{imageId:D4}.{imageFormat}";
                string imagePath = Path.Combine(imagesFolder, imageName);

                cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                cameraToUse.backgroundColor = new Color(0, 0, 0, 0);

                cameraToUse.targetTexture = rt;
                cameraToUse.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                CapturePointCloudFromCamera(cameraToUse, tex, rays, writer3D, imageId, ref pointId);

                byte[] imageData;
                if (imageFormat == "exr")
                {
                    imageData = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                }
                else if (useToneMapping)
                {
                    Texture2D ldrTex = ApplyToneMapping(tex, exposure);
                    imageData = ldrTex.EncodeToPNG();
                    DestroyImmediate(ldrTex);
                }
                else
                {
                    imageData = tex.EncodeToPNG();
                }
                File.WriteAllBytes(imagePath, imageData);

                imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                imgWriter.WriteLine();

                imageId++;
                batchCounter++;
                currentImage++;

                if (cancel)
                {
                    Debug.LogWarning("Capture canceled.");
                    EditorUtility.ClearProgressBar();
                    cancel = false;
                    isRunning = false;

                    yield break;
                }

                if (batchCounter >= batchSize)
                {
                    batchCounter = 0;

                    cameraToUse.targetTexture = null;
                    RenderTexture.active = null;
                    GL.Clear(true, true, Color.clear);

                    tex = null;
                    rt.Release();
                    rt = null;

                    DestroyImmediate(rt);
                    DestroyImmediate(tex);

                    EditorUtility.UnloadUnusedAssetsImmediate();
                    AssetDatabase.SaveAssets();
                    EditorApplication.QueuePlayerLoopUpdate();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    rt = new RenderTexture(w, h, 32, rtFormat);
                    tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                    yield return null;
                }
            }

            writer3D.Close();

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(tex);
        }

        Debug.Log("Ellipsoid Capture + COLMAP files finished!");
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        if (!runtimeAnim)
            EditorUtility.RevealInFinder(folderPath);
        isRunning = false;

        if (!runtimeAnim && TrainPostShot)
        {
            RunPostshotBatch();
        }

        yield return new WaitForEndOfFrame();
        if (EditorApplication.isPaused == true)
            EditorApplication.isPaused = false;
    }

    public IEnumerator CaptureCylinderViewsAndExportColmap(string outAdd)
    {
        isRunning = true;

        string folderPath = outputFolder + outAdd;
        Directory.CreateDirectory(folderPath);

        // Create COLMAP standard directory structure
        string imagesFolder = Path.Combine(folderPath, "images");
        string sparseFolder = Path.Combine(folderPath, "sparse", "0");
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(sparseFolder);

        // === cameras.txt ===
        string camerasTxt = Path.Combine(sparseFolder, "cameras.txt");

        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * h / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;

        float cx = w / 2f;
        float cy = h / 2f;

        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {w} {h} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        // === images.txt ===
        string imagesTxt = Path.Combine(sparseFolder, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            // Use ARGBFloat for EXR or tone mapping (linear, HDR), Default for PNG (sRGB, prevents dark images)
            RenderTextureFormat rtFormat = (imageFormat == "exr" || useToneMapping) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.Default;
            RenderTexture rt = new RenderTexture(w, h, 32, rtFormat);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            int imageId = 1;
            int batchSize = 40;
            int batchCounter = 0;

            StreamWriter writer3D = new StreamWriter(Path.Combine(sparseFolder, "points3D.txt"));
            writer3D.WriteLine("# 3D point list with one line of data per point:");
            writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");
            int pointId = 1;

            // Generate cylinder points (position, lookAt, isSide)
            List<(Vector3, Vector3, bool)> cylinderPoints = GenerateCylinderPoints(numCylinderSidePoints, numCylinderLayers, numCylinderCapPoints, cylinderRadius, cylinderHeight, cylinderTarget.position);

            int totalImages = cylinderPoints.Count;
            int currentImage = 0;

            // Bake skinned meshes for collision detection
            foreach (SkinnedMeshRenderer r in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (!r.GetComponent<MeshCollider>())
                {
                    r.gameObject.AddComponent<MeshCollider>();
                }

                Mesh bakedMesh = new Mesh();
                r.BakeMesh(bakedMesh);

                r.GetComponent<MeshCollider>().sharedMesh = null;
                r.GetComponent<MeshCollider>().sharedMesh = bakedMesh;
            }

            foreach (var point in cylinderPoints)
            {
                float progress = (float)currentImage / totalImages;
                EditorUtility.DisplayProgressBar("Capture Cylinder", $"Image {currentImage + 1} / {totalImages}", progress);

                Vector3 position = point.Item1;
                Vector3 lookAt = point.Item2;

                cameraToUse.transform.position = position;
                cameraToUse.transform.LookAt(lookAt);

                Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;

                Matrix4x4 R = colmapMatrix;
                R.SetColumn(3, new Vector4(0, 0, 0, 1));
                Quaternion q = QuaternionFromMatrix(R);
                Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                string imageName = $"cylinder_{imageId:D4}.{imageFormat}";
                string imagePath = Path.Combine(imagesFolder, imageName);

                cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                cameraToUse.backgroundColor = new Color(0, 0, 0, 0);

                cameraToUse.targetTexture = rt;
                cameraToUse.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                CapturePointCloudFromCamera(cameraToUse, tex, rays, writer3D, imageId, ref pointId);

                byte[] imageData;
                if (imageFormat == "exr")
                {
                    imageData = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                }
                else if (useToneMapping)
                {
                    Texture2D ldrTex = ApplyToneMapping(tex, exposure);
                    imageData = ldrTex.EncodeToPNG();
                    DestroyImmediate(ldrTex);
                }
                else
                {
                    imageData = tex.EncodeToPNG();
                }
                File.WriteAllBytes(imagePath, imageData);

                imgWriter.WriteLine($"{imageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                imgWriter.WriteLine();

                imageId++;
                batchCounter++;
                currentImage++;

                if (cancel)
                {
                    Debug.LogWarning("Capture canceled.");
                    EditorUtility.ClearProgressBar();
                    cancel = false;
                    isRunning = false;

                    yield break;
                }

                if (batchCounter >= batchSize)
                {
                    batchCounter = 0;

                    cameraToUse.targetTexture = null;
                    RenderTexture.active = null;
                    GL.Clear(true, true, Color.clear);

                    tex = null;
                    rt.Release();
                    rt = null;

                    DestroyImmediate(rt);
                    DestroyImmediate(tex);

                    EditorUtility.UnloadUnusedAssetsImmediate();
                    AssetDatabase.SaveAssets();
                    EditorApplication.QueuePlayerLoopUpdate();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    rt = new RenderTexture(w, h, 32, rtFormat);
                    tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                    yield return null;
                }
            }

            writer3D.Close();

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(tex);
        }

        Debug.Log("Cylinder Capture + COLMAP files finished!");
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        if (!runtimeAnim)
            EditorUtility.RevealInFinder(folderPath);
        isRunning = false;

        if (!runtimeAnim && TrainPostShot)
        {
            RunPostshotBatch();
        }

        yield return new WaitForEndOfFrame();
        if (EditorApplication.isPaused == true)
            EditorApplication.isPaused = false;
    }

    public IEnumerator CaptureCombinedViewsAndExportColmap(string outAdd)
    {
        isRunning = true;

        string folderPath = outputFolder + outAdd;
        Directory.CreateDirectory(folderPath);

        // Create COLMAP standard directory structure
        string imagesFolder = Path.Combine(folderPath, "images");
        string sparseFolder = Path.Combine(folderPath, "sparse", "0");
        Directory.CreateDirectory(imagesFolder);
        Directory.CreateDirectory(sparseFolder);

        // === cameras.txt ===
        string camerasTxt = Path.Combine(sparseFolder, "cameras.txt");
        float fov = cameraToUse.fieldOfView;
        float fy = 0.5f * h / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad);
        float fx = fy;
        float cx = w / 2f;
        float cy = h / 2f;

        using (StreamWriter camWriter = new StreamWriter(camerasTxt))
        {
            camWriter.WriteLine("# Camera list with one line of data per camera:");
            camWriter.WriteLine("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]");
            camWriter.WriteLine($"1 PINHOLE {w} {h} {fx.ToString(CultureInfo.InvariantCulture)} {fy.ToString(CultureInfo.InvariantCulture)} {cx} {cy}");
        }

        // Define these outside the using block so they're accessible at the end
        int globalImageId = 1;
        int totalImagesCount = 0;

        // === images.txt and points3D.txt ===
        string imagesTxt = Path.Combine(sparseFolder, "images.txt");
        using (StreamWriter imgWriter = new StreamWriter(imagesTxt))
        {
            imgWriter.WriteLine("# Image list with two lines per image:");
            imgWriter.WriteLine("# IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, IMAGE_NAME");
            imgWriter.WriteLine("# POINTS2D[] as X, Y, POINT3D_ID");

            RenderTextureFormat rtFormat = (imageFormat == "exr" || useToneMapping) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.Default;
            RenderTexture rt = new RenderTexture(w, h, 32, rtFormat);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            int globalPointId = 1;
            int batchSize = 40;
            int batchCounter = 0;

            StreamWriter writer3D = new StreamWriter(Path.Combine(sparseFolder, "points3D.txt"));
            writer3D.WriteLine("# 3D point list with one line of data per point:");
            writer3D.WriteLine("# POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)");

            // Bake skinned meshes once for all modes
            foreach (SkinnedMeshRenderer r in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (!r.GetComponent<MeshCollider>())
                    r.gameObject.AddComponent<MeshCollider>();

                Mesh bakedMesh = new Mesh();
                r.BakeMesh(bakedMesh);
                r.GetComponent<MeshCollider>().sharedMesh = null;
                r.GetComponent<MeshCollider>().sharedMesh = bakedMesh;
            }

            // Helper method to capture a single view
            System.Action<Vector3, Vector3, string> captureView = (position, lookAtPos, prefix) =>
            {
                cameraToUse.transform.position = position;
                cameraToUse.transform.LookAt(lookAtPos);

                Matrix4x4 worldToCamera = cameraToUse.worldToCameraMatrix;
                Matrix4x4 unityToColmap = Matrix4x4.Scale(new Vector3(1, -1, -1));
                Matrix4x4 colmapMatrix = unityToColmap * worldToCamera;

                Matrix4x4 R = colmapMatrix;
                R.SetColumn(3, new Vector4(0, 0, 0, 1));
                Quaternion q = QuaternionFromMatrix(R);
                Vector3 t = new Vector3(colmapMatrix.m03, colmapMatrix.m13, colmapMatrix.m23);

                string imageName = $"{prefix}_{globalImageId:D4}.{imageFormat}";
                string imagePath = Path.Combine(imagesFolder, imageName);

                cameraToUse.clearFlags = CameraClearFlags.SolidColor;
                cameraToUse.backgroundColor = new Color(0, 0, 0, 0);

                cameraToUse.targetTexture = rt;
                cameraToUse.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                CapturePointCloudFromCamera(cameraToUse, tex, rays, writer3D, globalImageId, ref globalPointId);

                byte[] imageData;
                if (imageFormat == "exr")
                    imageData = tex.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                else if (useToneMapping)
                {
                    Texture2D ldrTex = ApplyToneMapping(tex, exposure);
                    imageData = ldrTex.EncodeToPNG();
                    DestroyImmediate(ldrTex);
                }
                else
                    imageData = tex.EncodeToPNG();

                File.WriteAllBytes(imagePath, imageData);

                imgWriter.WriteLine($"{globalImageId} {q.w.ToString(CultureInfo.InvariantCulture)} {q.x.ToString(CultureInfo.InvariantCulture)} {q.y.ToString(CultureInfo.InvariantCulture)} {q.z.ToString(CultureInfo.InvariantCulture)} {t.x.ToString(CultureInfo.InvariantCulture)} {t.y.ToString(CultureInfo.InvariantCulture)} {t.z.ToString(CultureInfo.InvariantCulture)} 1 {imageName}");
                imgWriter.WriteLine();

                globalImageId++;
                batchCounter++;
            };

            int totalModes = (useDomeInCombined ? 1 : 0) + (useSphericalInCombined ? 1 : 0) + (useEllipsoidInCombined ? 1 : 0) + (useCylinderInCombined ? 1 : 0);
            int currentMode = 0;

            // Capture Dome mode
            if (useDomeInCombined && target != null)
            {
                currentMode++;
                Debug.Log($"[Combined] Capturing Dome mode ({currentMode}/{totalModes})...");

                for (int ring = 0; ring < numRings; ring++)
                {
                    float elevation = Mathf.Lerp(-Mathf.PI / 4, Mathf.PI / 4, (float)ring / (numRings - 1));
                    for (int i = 0; i < viewsPerRing; i++)
                    {
                        float azimuth = i * Mathf.PI * 2 / viewsPerRing;
                        float x = radius * Mathf.Cos(elevation) * Mathf.Cos(azimuth);
                        float y = radius * Mathf.Sin(elevation);
                        float z = radius * Mathf.Cos(elevation) * Mathf.Sin(azimuth);

                        Vector3 position = target.position + new Vector3(x, y + height, z);
                        captureView(position, target.position, "dome");

                        if (batchCounter >= batchSize)
                        {
                            batchCounter = 0;
                            cameraToUse.targetTexture = null;
                            RenderTexture.active = null;
                            GL.Clear(true, true, Color.clear);
                            DestroyImmediate(rt);
                            DestroyImmediate(tex);
                            EditorUtility.UnloadUnusedAssetsImmediate();
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            rt = new RenderTexture(w, h, 32, rtFormat);
                            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                            yield return null;
                        }

                        if (cancel)
                        {
                            Debug.LogWarning("Capture canceled.");
                            EditorUtility.ClearProgressBar();
                            cancel = false;
                            isRunning = false;
                            yield break;
                        }
                    }
                }
            }

            // Capture Spherical mode
            if (useSphericalInCombined && sphericalTarget != null)
            {
                currentMode++;
                Debug.Log($"[Combined] Capturing Spherical mode ({currentMode}/{totalModes})...");

                List<Vector3> spherePoints = GenerateFibonacciSpherePoints(numSpherePoints, sphereRadius, sphericalTarget.position);
                foreach (Vector3 position in spherePoints)
                {
                    captureView(position, sphericalTarget.position, "sphere");

                    if (batchCounter >= batchSize)
                    {
                        batchCounter = 0;
                        cameraToUse.targetTexture = null;
                        RenderTexture.active = null;
                        GL.Clear(true, true, Color.clear);
                        DestroyImmediate(rt);
                        DestroyImmediate(tex);
                        EditorUtility.UnloadUnusedAssetsImmediate();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        rt = new RenderTexture(w, h, 32, rtFormat);
                        tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        yield return null;
                    }

                    if (cancel)
                    {
                        Debug.LogWarning("Capture canceled.");
                        EditorUtility.ClearProgressBar();
                        cancel = false;
                        isRunning = false;
                        yield break;
                    }
                }
            }

            // Capture Ellipsoid mode
            if (useEllipsoidInCombined && ellipsoidTarget != null)
            {
                currentMode++;
                Debug.Log($"[Combined] Capturing Ellipsoid mode ({currentMode}/{totalModes})...");

                List<Vector3> ellipsoidPoints = GenerateFibonacciEllipsoidPoints(numEllipsoidPoints, ellipsoidRadiusX, ellipsoidRadiusY, ellipsoidRadiusZ, ellipsoidTarget.position);
                foreach (Vector3 position in ellipsoidPoints)
                {
                    captureView(position, ellipsoidTarget.position, "ellipsoid");

                    if (batchCounter >= batchSize)
                    {
                        batchCounter = 0;
                        cameraToUse.targetTexture = null;
                        RenderTexture.active = null;
                        GL.Clear(true, true, Color.clear);
                        DestroyImmediate(rt);
                        DestroyImmediate(tex);
                        EditorUtility.UnloadUnusedAssetsImmediate();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        rt = new RenderTexture(w, h, 32, rtFormat);
                        tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        yield return null;
                    }

                    if (cancel)
                    {
                        Debug.LogWarning("Capture canceled.");
                        EditorUtility.ClearProgressBar();
                        cancel = false;
                        isRunning = false;
                        yield break;
                    }
                }
            }

            // Capture Cylinder mode
            if (useCylinderInCombined && cylinderTarget != null)
            {
                currentMode++;
                Debug.Log($"[Combined] Capturing Cylinder mode ({currentMode}/{totalModes})...");

                List<(Vector3, Vector3, bool)> cylinderPoints = GenerateCylinderPoints(numCylinderSidePoints, numCylinderLayers, numCylinderCapPoints, cylinderRadius, cylinderHeight, cylinderTarget.position);
                foreach (var point in cylinderPoints)
                {
                    Vector3 position = point.Item1;
                    Vector3 lookAt = point.Item2;
                    captureView(position, lookAt, "cylinder");

                    if (batchCounter >= batchSize)
                    {
                        batchCounter = 0;
                        cameraToUse.targetTexture = null;
                        RenderTexture.active = null;
                        GL.Clear(true, true, Color.clear);
                        DestroyImmediate(rt);
                        DestroyImmediate(tex);
                        EditorUtility.UnloadUnusedAssetsImmediate();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        rt = new RenderTexture(w, h, 32, rtFormat);
                        tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        yield return null;
                    }

                    if (cancel)
                    {
                        Debug.LogWarning("Capture canceled.");
                        EditorUtility.ClearProgressBar();
                        cancel = false;
                        isRunning = false;
                        yield break;
                    }
                }
            }

            writer3D.Close();

            cameraToUse.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(tex);

            // Store total count before exiting using block
            totalImagesCount = globalImageId - 1;
        }

        Debug.Log($"Combined Capture finished! Total images captured: {totalImagesCount}");
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        if (!runtimeAnim)
            EditorUtility.RevealInFinder(folderPath);
        isRunning = false;

        if (!runtimeAnim && TrainPostShot)
        {
            RunPostshotBatch();
        }

        yield return new WaitForEndOfFrame();
        if (EditorApplication.isPaused == true)
            EditorApplication.isPaused = false;
    }

    private static void StartCaptureVolume(bool isRuntime)
    {
        var window = GetWindow<CameraCaptureEditor>();
        if (isRuntime)
        {
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.WaitForPlayAndCapture(1), window);
        }
        else
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.CaptureVolumeViewsAndExportColmap(""), window);
    }


    private static void StartCaptureDome(bool isRuntime)
    {
        var window = GetWindow<CameraCaptureEditor>();
        if (isRuntime)
        {
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.WaitForPlayAndCapture(0), window);
        }
        else
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.CaptureViewsAndExportColmap(""), window);
    }

    private static void StartCaptureFullSphere(bool isRuntime)
    {
        var window = GetWindow<CameraCaptureEditor>();
        if (isRuntime)
        {
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.WaitForPlayAndCapture(2), window);
        }
        else
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.CaptureFullSphereViewsAndExportColmap(""), window);
    }

    private static void StartCaptureEllipsoid(bool isRuntime)
    {
        var window = GetWindow<CameraCaptureEditor>();
        if (isRuntime)
        {
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.WaitForPlayAndCapture(3), window);
        }
        else
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.CaptureEllipsoidViewsAndExportColmap(""), window);
    }

    private static void StartCaptureCylinder(bool isRuntime)
    {
        var window = GetWindow<CameraCaptureEditor>();
        if (isRuntime)
        {
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.WaitForPlayAndCapture(4), window);
        }
        else
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.CaptureCylinderViewsAndExportColmap(""), window);
    }

    private static void StartCaptureCombined(bool isRuntime)
    {
        var window = GetWindow<CameraCaptureEditor>();
        if (isRuntime)
        {
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.WaitForPlayAndCapture(5), window);
        }
        else
            window.captureCoroutine = EditorCoroutineUtility.StartCoroutine(window.CaptureCombinedViewsAndExportColmap(""), window);
    }


    private List<Vector3> GenerateCustomSphericalDirections()
    {
        List<Vector3> directions = new List<Vector3>();

        for (int i = 0; i < 8; i++)
        {
            float azimuth = i * 45f;
            Quaternion rot = Quaternion.Euler(0f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }

        for (int i = 0; i < 4; i++)
        {
            float azimuth = i * 90f;
            Quaternion rot = Quaternion.Euler(45f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }

        for (int i = 0; i < 4; i++)
        {
            float azimuth = i * 90f;
            Quaternion rot = Quaternion.Euler(-45f, azimuth, 0f);
            directions.Add(rot * Vector3.forward);
        }

        directions.Add(Vector3.up);

        directions.Add(Vector3.down);

        return directions;
    }

    // Generate points uniformly distributed on a sphere using Fibonacci lattice
    private List<Vector3> GenerateFibonacciSpherePoints(int numPoints, float radius, Vector3 center)
    {
        List<Vector3> points = new List<Vector3>();
        float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden angle in radians

        for (int i = 0; i < numPoints; i++)
        {
            float y = 1.0f - (i / (float)(numPoints - 1)) * 2.0f; // y goes from 1 to -1
            float radiusAtY = Mathf.Sqrt(1.0f - y * y); // radius at y

            float theta = phi * i; // golden angle increment

            float x = Mathf.Cos(theta) * radiusAtY;
            float z = Mathf.Sin(theta) * radiusAtY;

            Vector3 point = center + new Vector3(x, y, z) * radius;
            points.Add(point);
        }

        return points;
    }

    // Generate points uniformly distributed on an ellipsoid using Fibonacci lattice
    private List<Vector3> GenerateFibonacciEllipsoidPoints(int numPoints, float radiusX, float radiusY, float radiusZ, Vector3 center)
    {
        List<Vector3> points = new List<Vector3>();
        float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden angle in radians

        for (int i = 0; i < numPoints; i++)
        {
            // Generate uniform sphere point
            float y = 1.0f - (i / (float)(numPoints - 1)) * 2.0f; // y goes from 1 to -1
            float radiusAtY = Mathf.Sqrt(1.0f - y * y); // radius at y

            float theta = phi * i; // golden angle increment

            float x = Mathf.Cos(theta) * radiusAtY;
            float z = Mathf.Sin(theta) * radiusAtY;

            // Scale by ellipsoid radii to create ellipsoid
            Vector3 point = center + new Vector3(x * radiusX, y * radiusY, z * radiusZ);
            points.Add(point);
        }

        return points;
    }

    // Generate cylinder capture points and directions
    // Returns a list of (position, lookAtTarget, isSideCamera)
    private List<(Vector3 position, Vector3 lookAt, bool isSide)> GenerateCylinderPoints(int sidePoints, int layers, int capPoints, float radius, float height, Vector3 center)
    {
        List<(Vector3, Vector3, bool)> result = new List<(Vector3, Vector3, bool)>();

        // Generate side points (horizontal cameras looking at center) at multiple height levels
        for (int layer = 0; layer < layers; layer++)
        {
            // Calculate Y position for this layer
            float layerY;
            if (layers == 1)
            {
                layerY = 0f; // Single layer at center
            }
            else
            {
                // Distribute layers evenly from -height/2 to +height/2
                layerY = -height / 2f + (layer / (float)(layers - 1)) * height;
            }

            for (int i = 0; i < sidePoints; i++)
            {
                float angle = (i / (float)sidePoints) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                // Position camera at this layer height
                Vector3 position = center + new Vector3(x, layerY, z);

                // Look at the center horizontally (same Y level)
                Vector3 lookAt = center + new Vector3(0, layerY, 0);

                result.Add((position, lookAt, true));
            }
        }

        // Generate top cap points using Fibonacci disc pattern (cameras looking at target)
        List<Vector2> topCapDisc = GenerateFibonacciDisc(capPoints, radius);
        foreach (Vector2 disc in topCapDisc)
        {
            Vector3 position = center + new Vector3(disc.x, height / 2f, disc.y);

            // Look at the target center
            Vector3 lookAt = center;

            result.Add((position, lookAt, false));
        }

        // Generate bottom cap points using Fibonacci disc pattern (cameras looking at target)
        List<Vector2> bottomCapDisc = GenerateFibonacciDisc(capPoints, radius);
        foreach (Vector2 disc in bottomCapDisc)
        {
            Vector3 position = center + new Vector3(disc.x, -height / 2f, disc.y);

            // Look at the target center
            Vector3 lookAt = center;

            result.Add((position, lookAt, false));
        }

        return result;
    }

    // Generate points uniformly distributed on a disc using Fibonacci lattice
    private List<Vector2> GenerateFibonacciDisc(int numPoints, float radius)
    {
        List<Vector2> points = new List<Vector2>();
        float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden angle

        for (int i = 0; i < numPoints; i++)
        {
            float r = radius * Mathf.Sqrt(i / (float)numPoints);
            float theta = phi * i;

            float x = r * Mathf.Cos(theta);
            float y = r * Mathf.Sin(theta);

            points.Add(new Vector2(x, y));
        }

        return points;
    }

    void CapturePointCloudFromCamera(Camera cam, Texture2D tex, int rayCount, StreamWriter writer, int imageId, ref int pointId)
{
    int width = tex.width;
    int height = tex.height;

    int sqrtRayCount = Mathf.CeilToInt(Mathf.Sqrt(rayCount));
    float stepX = width / (float)sqrtRayCount;
    float stepY = height / (float)sqrtRayCount;

        int noCloudLayer = LayerMask.NameToLayer("NoCloud");

        int layerMask = ~(1 << noCloudLayer); 

        for (int i = 0; i < sqrtRayCount; i++)
    {
        for (int j = 0; j < sqrtRayCount; j++)
        {
            float px = i * stepX + stepX / 2f;
            float py = j * stepY + stepY / 2f;

            Ray ray = cam.ScreenPointToRay(new Vector3(px, py, 0));

                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
                {
                    Vector3 worldPos = hit.point;
                worldPos = new Vector3(worldPos.x * -1, worldPos.y, worldPos.z);

                Color color = tex.GetPixel((int)px, (int)py);
                int r = Mathf.Clamp((int)(color.r * 255), 0, 255);
                int g = Mathf.Clamp((int)(color.g * 255), 0, 255);
                int b = Mathf.Clamp((int)(color.b * 255), 0, 255);


                    writer.WriteLine($"{pointId} {worldPos.x.ToString(CultureInfo.InvariantCulture)} {worldPos.y.ToString(CultureInfo.InvariantCulture)} {worldPos.z.ToString(CultureInfo.InvariantCulture)} {r} {g} {b} 1.0");
                    pointId++;
            }
        }
    }
}

    // Apply Reinhard tone mapping with exposure control
    private Texture2D ApplyToneMapping(Texture2D hdrTex, float exposureValue)
    {
        int width = hdrTex.width;
        int height = hdrTex.height;

        Texture2D ldrTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = hdrTex.GetPixels();

        for (int i = 0; i < pixels.Length; i++)
        {
            Color hdr = pixels[i] * exposureValue;

            // Reinhard tone mapping: L_out = L_in / (1 + L_in)
            pixels[i].r = hdr.r / (1.0f + hdr.r);
            pixels[i].g = hdr.g / (1.0f + hdr.g);
            pixels[i].b = hdr.b / (1.0f + hdr.b);
            pixels[i].a = hdr.a;

            // Apply gamma correction for sRGB (gamma 2.2)
            pixels[i].r = Mathf.Pow(pixels[i].r, 1.0f / 2.2f);
            pixels[i].g = Mathf.Pow(pixels[i].g, 1.0f / 2.2f);
            pixels[i].b = Mathf.Pow(pixels[i].b, 1.0f / 2.2f);
        }

        ldrTex.SetPixels(pixels);
        ldrTex.Apply();
        return ldrTex;
    }


}
