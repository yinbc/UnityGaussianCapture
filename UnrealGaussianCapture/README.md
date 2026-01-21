# Unreal Gaussian Capture

A powerful Unreal Engine plugin for capturing multi-view scene data for **Gaussian Splatting** and **4D Gaussian Splatting (4DGS)** neural rendering. This plugin streamlines the process of generating training datasets in COLMAP format for neural rendering pipelines.

## Features

### 🎥 Multi-View Capture Modes
- **Dome Capture**: Spherical camera arrangement with configurable rings and views
- **Volume Capture**: 3D grid-based camera positions with 16-direction sampling

### 🎨 Advanced Rendering
- **Transparent Background Support**: RGBA rendering with alpha channel
- **High-Quality Scene Capture**: Full control over resolution and FOV
- **Real-time Preview**: Visual gizmos showing camera positions in editor

### 📊 COLMAP Export
- Automatic generation of `cameras.txt`, `images.txt`, and `points3D.txt`
- Proper coordinate system conversion (Unreal ↔ COLMAP)
- Point cloud generation via raycasting

### 🎬 Animation Sequence Capture
- Frame-by-frame capture for temporal Gaussian Splatting (4DGS)
- Configurable frame range and intervals
- Automatic per-frame dataset organization

## Installation

1. Copy the `UnrealGaussianCapture` folder to your project's `Plugins` directory
2. Restart Unreal Engine or regenerate project files
3. Enable the plugin in Edit → Plugins → Search for "Unreal Gaussian Capture"
4. Restart the editor

## Quick Start

### 1. Open the Capture Tool

- Go to **Window → Gaussian Capture** (or find it in the toolbar)

### 2. Create a Capture Actor

1. Click **"Create Capture Actor"** in the tool window
2. A `GaussianCaptureActor` will be spawned at your current viewport location
3. Configure capture settings in the Details panel:

#### Target Selection (Dome Mode)

You have two ways to specify the target that cameras will look at:

**Option A: Use Target Actor (Recommended)**
- Drag and drop any actor from your scene into the **Target Actor** field
- The capture will automatically use that actor's location
- Enable **Use Actor Bounds Center** to target the center of the actor's bounding box (useful for large objects)

**Option B: Manual Target Location**
- If **Target Actor** is empty, manually set the **Target Location** coordinates (X, Y, Z)

#### Dome Mode Settings
- **Dome Rings**: Number of horizontal elevation levels (1-20)
- **Views Per Ring**: Cameras per ring (3-36)
- **Dome Radius**: Distance from target point (cm)
- **Dome Height**: Vertical offset from target (cm)

#### Volume Mode Settings
- **Volume Center**: Center of capture volume
- **Volume Size**: Bounding box dimensions
- **Volume Subdivisions**: Grid density (1-10)

#### Render Settings
- **Transparent Background**: Enable RGBA with alpha
- **Image Width/Height**: Output resolution
- **Camera FOV**: Field of view (5-170°)

#### Point Cloud Settings
- **Ray Count**: Number of rays for point cloud generation
- **Max Ray Distance**: Maximum raycast distance (cm)

### 3. Configure Output

- Set **Output Directory** in the capture tool
- Default: `<Project>/Saved/GaussianCapture`

### 4. Start Capture

1. Click **"Start Capture"**
2. Wait for capture to complete (may take several minutes)
3. Output will include:
   - `cameras.txt` - Camera intrinsic parameters
   - `images.txt` - Camera extrinsic parameters (poses)
   - `points3D.txt` - 3D point cloud with colors
   - `image_XXXX.png` - Rendered views

## Architecture

```
UnrealGaussianCapture/
├── Source/
│   ├── UnrealGaussianCapture/          # Runtime module
│   │   ├── Public/
│   │   │   ├── GaussianCaptureActor.h          # Main capture actor
│   │   │   ├── GaussianCaptureSubsystem.h      # Capture & export system
│   │   │   └── GaussianSequenceCapturer.h      # Animation sequence support
│   │   └── Private/
│   │       ├── GaussianCaptureActor.cpp
│   │       ├── GaussianCaptureSubsystem.cpp
│   │       └── GaussianSequenceCapturer.cpp
│   │
│   └── UnrealGaussianCaptureEditor/    # Editor module
│       ├── Public/
│       │   ├── GaussianCaptureEditorWidget.h   # Main UI
│       │   ├── GaussianCaptureEditorStyle.h
│       │   └── GaussianCaptureEditorCommands.h
│       └── Private/
│           ├── GaussianCaptureEditorWidget.cpp
│           ├── GaussianCaptureEditorModule.cpp
│           ├── GaussianCaptureEditorStyle.cpp
│           └── GaussianCaptureEditorCommands.cpp
│
└── UnrealGaussianCapture.uplugin
```

## Advanced Usage

### Animation Sequence Capture (4DGS)

For capturing temporal data:

```cpp
// Blueprint or C++ example
UGaussianSequenceCapturer* Capturer = NewObject<UGaussianSequenceCapturer>();
Capturer->StartFrame = 0;
Capturer->EndFrame = 100;
Capturer->FrameInterval = 1;
Capturer->TargetFrameRate = 30.0f;
Capturer->CaptureActor = MyCaptureActor;
Capturer->OutputDirectory = TEXT("C:/MyProject/Saved/Sequence");
Capturer->StartSequenceCapture(GetWorld());

// Call in Tick
Capturer->TickCapture(DeltaTime);
```

### Custom Capture Workflow

```cpp
// Get the capture subsystem
UGaussianCaptureSubsystem* Subsystem = GEngine->GetEngineSubsystem<UGaussianCaptureSubsystem>();

// Calculate camera intrinsics
FColmapCamera Camera = Subsystem->CalculateCameraIntrinsics(1920, 1080, 90.0f);

// Capture a single view
FCameraPosition CamPos(FVector(0, 0, 100), FRotator(-90, 0, 0));
Subsystem->CaptureView(World, CamPos, OutputPath, 0, 1920, 1080, 90.0f, true);

// Generate point cloud
TArray<FColmapPoint3D> Points = Subsystem->GeneratePointCloud(World, CamPos, RenderTarget, 10000, 10000.0f);

// Export COLMAP format
Subsystem->ExportColmapFormat(OutputPath, Camera, Images, Points);
```

## Technical Details

### Coordinate System Conversion

Unreal Engine uses **Z-up, X-forward (left-handed)** coordinate system, while COLMAP uses **Y-down, Z-forward (right-handed)**. The plugin automatically handles this conversion:

```cpp
// Conversion matrix
[1,  0,  0]
[0, -1,  0]
[0,  0, -1]
```

### Camera Model

The plugin uses COLMAP's **PINHOLE** camera model:

```
Parameters: [fx, fy, cx, cy]
fx, fy = focal length
cx, cy = principal point (image center)
```

### Point Cloud Generation

- Uses raycasting from each camera view
- Samples colors from rendered texture
- Generates sqrt(RayCount) × sqrt(RayCount) point grid
- Filters transparent pixels (alpha < 10)

### Memory Management

- Captures in batches of 40 images
- Periodic garbage collection
- Render target reuse
- Automatic cleanup

## Comparison with Unity Version

This Unreal plugin provides equivalent functionality to the Unity `UnityGaussianCapture` plugin:

| Feature | Unity | Unreal |
|---------|-------|--------|
| Dome Capture | ✅ | ✅ |
| Volume Capture | ✅ | ✅ |
| COLMAP Export | ✅ | ✅ |
| Transparent Rendering | ✅ | ✅ |
| Point Cloud Generation | ✅ | ✅ |
| Animation Sequences | ✅ | ✅ |
| Editor UI | EditorWindow | Slate Widget |
| Gizmo Visualization | Gizmos | Debug Draw |

## Output Format

### COLMAP Format Structure

```
OutputDirectory/
├── cameras.txt       # Camera intrinsics
├── images.txt        # Camera extrinsics (poses)
├── points3D.txt      # 3D point cloud
└── image_0000.png    # Rendered views
    image_0001.png
    ...
```

### cameras.txt
```
# Camera list with one line of data per camera:
#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]
1 PINHOLE 1920 1080 800.0 800.0 960.0 540.0
```

### images.txt
```
# Image list with two lines of data per image:
#   IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, NAME
1 0.707 0.0 -0.707 0.0 0.0 0.0 100.0 1 image_0000.png

```

### points3D.txt
```
# 3D point list with one line of data per point:
#   POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[]
1 10.5 20.3 -5.2 255 128 64 0.0
```

## Training with Captured Data

After capturing, use your captured COLMAP dataset with Gaussian Splatting training pipelines:

### Example with 3D Gaussian Splatting
```bash
python train.py -s <output_directory> --eval
```

### Example with 4D Gaussian Splatting
```bash
python train.py -s <sequence_directory> --sequence --eval
```

## Troubleshooting

### Issue: No camera positions generated
- **Solution**: Check that Dome Rings > 0 and Views Per Ring > 2, or Volume Subdivisions > 0

### Issue: Black images captured
- **Solution**: Ensure proper lighting in the scene, check if camera is inside geometry

### Issue: Transparent background not working
- **Solution**: Enable "Transparent Background" in capture actor settings

### Issue: Point cloud is empty
- **Solution**: Increase Ray Count, check Max Ray Distance, ensure geometry is visible from cameras

### Issue: Out of memory during capture
- **Solution**: Reduce image resolution or capture in smaller batches

## License

This plugin is provided as-is for use with Gaussian Splatting research and development.

## Credits

Inspired by the Unity `UnityGaussianCapture` plugin, reimplemented for Unreal Engine with native UE architecture.

## Support

For issues, questions, or contributions, please refer to the project repository.

---

**Happy Capturing! 🎥✨**
