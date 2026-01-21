# Unreal Gaussian Capture - Examples

This document provides practical examples for using the Unreal Gaussian Capture plugin.

## Example 1: Basic Dome Capture

### Setup
1. Create a new level or open an existing scene
2. Place your target object in the scene (e.g., a character, prop, or static mesh)
3. Open the Gaussian Capture tool: **Window → Gaussian Capture**
4. Click **"Create Capture Actor"**

### Configuration
In the Details panel of the GaussianCaptureActor:

**Method 1: Using Target Actor (Recommended)**
```
Capture Mode: Dome
Target Actor: [Drag your object here from the Outliner]
Use Actor Bounds Center: true  // Automatically targets object center
Dome Rings: 3
Views Per Ring: 8
Dome Radius: 500.0
Dome Height: 200.0
Transparent Background: true
Image Width: 1920
Image Height: 1080
Camera FOV: 90.0
Ray Count: 10000
Max Ray Distance: 10000.0
```

**Method 2: Using Manual Target Location**
```
Capture Mode: Dome
Target Actor: None
Target Location: (0, 0, 100)  // Manually set coordinates
Dome Rings: 3
Views Per Ring: 8
Dome Radius: 500.0
Dome Height: 200.0
Transparent Background: true
Image Width: 1920
Image Height: 1080
Camera FOV: 90.0
Ray Count: 10000
Max Ray Distance: 10000.0
```

### Capture
1. Set output directory: `C:/GaussianData/MyObject`
2. Click **"Start Capture"**
3. Wait for completion

### Result
- Total views: 24 (3 rings × 8 views)
- Output: COLMAP format dataset ready for training

---

## Example 2: High-Resolution Volume Capture

### Use Case
Capturing a large environment or room interior with dense sampling.

### Configuration
```
Capture Mode: Volume
Volume Center: (0, 0, 150)
Volume Size: (800, 800, 400)
Volume Subdivisions: 5
Transparent Background: false
Image Width: 2560
Image Height: 1440
Camera FOV: 75.0
Ray Count: 25000
Max Ray Distance: 15000.0
```

### Capture Details
- Total grid cells: 6³ = 216
- Views per cell: 16 directions
- Total views: 3,456
- Estimated time: 20-30 minutes (depending on scene complexity)

---

## Example 3: Animation Sequence Capture (4DGS)

### Scenario
Capture a character animation for temporal Gaussian Splatting.

### Blueprint Setup

Create a Blueprint Actor with the following code:

```cpp
// Header (.h)
UCLASS()
class AMySequenceCaptureController : public AActor
{
    GENERATED_BODY()

public:
    UPROPERTY(EditAnywhere, BlueprintReadWrite)
    AGaussianCaptureActor* CaptureActor;

    UPROPERTY(EditAnywhere, BlueprintReadWrite)
    UGaussianSequenceCapturer* SequenceCapturer;

    virtual void BeginPlay() override;
    virtual void Tick(float DeltaTime) override;

private:
    bool bHasStarted = false;
};

// Source (.cpp)
void AMySequenceCaptureController::BeginPlay()
{
    Super::BeginPlay();

    // Initialize sequence capturer
    SequenceCapturer = NewObject<UGaussianSequenceCapturer>();
    SequenceCapturer->StartFrame = 0;
    SequenceCapturer->EndFrame = 120;  // 4 seconds at 30fps
    SequenceCapturer->FrameInterval = 2;  // Capture every 2nd frame
    SequenceCapturer->TargetFrameRate = 30.0f;
    SequenceCapturer->CaptureActor = CaptureActor;
    SequenceCapturer->OutputDirectory = TEXT("C:/GaussianData/CharacterAnimation");
}

void AMySequenceCaptureController::Tick(float DeltaTime)
{
    Super::Tick(DeltaTime);

    if (!bHasStarted && GetWorld()->GetTimeSeconds() > 1.0f)
    {
        // Start capture after 1 second delay
        SequenceCapturer->StartSequenceCapture(GetWorld());
        bHasStarted = true;
    }

    if (bHasStarted)
    {
        SequenceCapturer->TickCapture(DeltaTime);
    }
}
```

### Configuration
```
Dome Rings: 2
Views Per Ring: 6
Dome Radius: 300.0
Image Width: 1280
Image Height: 720
Total Frames: 60 (every 2nd frame from 0-120)
```

### Output Structure
```
CharacterAnimation/
├── Frame_0000/
│   ├── cameras.txt
│   ├── images.txt
│   ├── points3D.txt
│   └── image_XXXX.png (12 images)
├── Frame_0002/
│   └── ...
└── Frame_0120/
    └── ...
```

---

## Example 4: Small Object Scanning

### Use Case
Capture a small prop or collectible item with high detail.

### Setup
1. Place your prop in the scene
2. Select the prop actor in the Outliner
3. Ensure good lighting (use Sky Light + Directional Light)
4. Add a clean backdrop or use transparent background
5. Create a GaussianCaptureActor

### Configuration
```
Capture Mode: Dome
Target Actor: [Your prop actor]
Use Actor Bounds Center: true  // Automatically centers on the object
Dome Rings: 4
Views Per Ring: 12
Dome Radius: 150.0  // Close proximity for small objects
Dome Height: 75.0
Transparent Background: true
Image Width: 2048
Image Height: 2048
Camera FOV: 60.0  // Narrower FOV for less distortion
Ray Count: 50000  // Dense point cloud for high detail
Max Ray Distance: 5000.0
```

### Tips
- Use **48 total views** for complete coverage (4 × 12)
- Enable **transparent background** for easy compositing
- Consider adding a few **manual ground-level views** for base detail

---

## Example 5: Large Scene with Frustum Culling

### Use Case
Capture an outdoor environment efficiently using volume mode with automatic frustum culling.

### Configuration
```
Capture Mode: Volume
Volume Center: (0, 0, 250)
Volume Size: (2000, 2000, 500)
Volume Subdivisions: 4
Transparent Background: false
Image Width: 1920
Image Height: 1080
Camera FOV: 90.0
```

### Benefits
- Volume mode automatically skips camera positions where no geometry is visible
- 16 viewing directions per cell ensure comprehensive coverage
- Large volume size captures expansive environments

### Optimization
- Adjust `Volume Subdivisions` based on scene density:
  - Open environments: 2-3
  - Medium detail: 4-5
  - Dense/complex: 6-8 (warning: very high view count!)

---

## Example 6: Custom Capture Pipeline

### Use Case
Programmatic control over capture process for specific needs.

### C++ Implementation

```cpp
void UMyCustomCaptureComponent::PerformCustomCapture()
{
    UWorld* World = GetWorld();
    if (!World) return;

    UGaussianCaptureSubsystem* Subsystem = GEngine->GetEngineSubsystem<UGaussianCaptureSubsystem>();
    if (!Subsystem) return;

    // Define custom camera positions
    TArray<FCameraPosition> CustomPositions;

    // Example: Circular path at eye level
    int32 NumViews = 16;
    float Radius = 400.0f;
    float Height = 170.0f;  // Eye level

    for (int32 i = 0; i < NumViews; ++i)
    {
        float Angle = (360.0f * i) / NumViews;
        float Rad = FMath::DegreesToRadians(Angle);

        FVector Position(
            FMath::Cos(Rad) * Radius,
            FMath::Sin(Rad) * Radius,
            Height
        );

        // Look at center
        FVector Direction = (FVector::ZeroVector - Position).GetSafeNormal();
        FRotator Rotation = Direction.Rotation();

        CustomPositions.Add(FCameraPosition(Position, Rotation));
    }

    // Calculate camera intrinsics
    FColmapCamera Camera = Subsystem->CalculateCameraIntrinsics(1920, 1080, 90.0f);

    // Prepare data structures
    TArray<FColmapImage> Images;
    TArray<FColmapPoint3D> AllPoints;
    int32 PointIDOffset = 0;

    FString OutputDir = TEXT("C:/CustomCapture");

    // Capture each custom view
    for (int32 i = 0; i < CustomPositions.Num(); ++i)
    {
        const FCameraPosition& CamPos = CustomPositions[i];

        // Capture view
        Subsystem->CaptureView(World, CamPos, OutputDir, i, 1920, 1080, 90.0f, false);

        // Generate point cloud
        UTextureRenderTarget2D* RT = Subsystem->GetRenderTarget();  // Assume getter exists
        TArray<FColmapPoint3D> ViewPoints = Subsystem->GeneratePointCloud(
            World, CamPos, RT, 10000, 10000.0f
        );

        // Add to collection
        for (FColmapPoint3D& Point : ViewPoints)
        {
            Point.PointID += PointIDOffset;
            AllPoints.Add(Point);
        }
        PointIDOffset += ViewPoints.Num();

        // Create image metadata
        FColmapImage ImageData;
        ImageData.ImageID = i + 1;
        ImageData.CameraID = Camera.CameraID;
        ImageData.ImageName = FString::Printf(TEXT("image_%04d.png"), i);
        Subsystem->UnrealToColmapTransform(
            CamPos.Position, CamPos.Rotation,
            ImageData.Quaternion, ImageData.Translation
        );
        Images.Add(ImageData);
    }

    // Export COLMAP format
    Subsystem->ExportColmapFormat(OutputDir, Camera, Images, AllPoints);

    UE_LOG(LogTemp, Log, TEXT("Custom capture complete: %d views, %d points"),
        Images.Num(), AllPoints.Num());
}
```

---

## Performance Tips

### Memory Management
- Capture large datasets in sessions (e.g., 100 views at a time)
- Lower resolution for preview captures (e.g., 1280×720)
- Close unnecessary editor windows during capture

### Quality vs. Speed
| Priority | Resolution | Ray Count | Views |
|----------|-----------|-----------|-------|
| Fast Preview | 1280×720 | 5,000 | 12-24 |
| Balanced | 1920×1080 | 10,000 | 24-48 |
| High Quality | 2560×1440 | 25,000 | 48-100 |
| Maximum | 3840×2160 | 50,000 | 100+ |

### Best Practices
1. **Test with low settings first** (fewer views, lower resolution)
2. **Optimize scene** (disable post-processing, reduce shadow quality if not needed)
3. **Use transparent background** only when necessary (slightly slower)
4. **Save project** before large captures
5. **Monitor disk space** (high-res captures can be several GB)

---

## Training Integration

### With Gaussian Splatting Original
```bash
# After capture to C:/GaussianData/MyCapture
python train.py -s C:/GaussianData/MyCapture --eval

# View results
python render.py -m C:/GaussianData/MyCapture
python metrics.py -m C:/GaussianData/MyCapture
```

### With 4D Gaussian Splatting
```bash
# After sequence capture
python train_4d.py -s C:/GaussianData/CharacterAnimation --sequence --eval
```

### With InstantSplat (Fast Training)
```bash
# Quick preview training
python train.py -s C:/GaussianData/MyCapture --fast --iterations 7000
```

---

## Common Workflows

### Workflow 1: Quick Object Scan
1. Place object at origin
2. Create Dome capture (3 rings × 8 views = 24)
3. Transparent background ON
4. Resolution: 1920×1080
5. Capture time: 2-3 minutes
6. Train: 15-20 minutes

### Workflow 2: Room Interior
1. Place capture actor at room center
2. Volume capture (3×3×3 grid = 27 cells × 16 views = 432)
3. Transparent background OFF
4. Resolution: 1920×1080
5. Capture time: 15-20 minutes
6. Train: 1-2 hours

### Workflow 3: Character Animation
1. Setup animation timeline
2. Dome capture (2 rings × 6 views = 12)
3. Sequence: 120 frames, every 2nd frame = 60 captures
4. Total views: 720 (60 × 12)
5. Capture time: 30-45 minutes
6. Train: 3-5 hours (4DGS)

---

## Troubleshooting Examples

### Problem: Point cloud is sparse
```
Solution:
- Increase Ray Count: 10000 → 25000
- Decrease Max Ray Distance if scene is small: 10000 → 5000
- Check that geometry has collision enabled
```

### Problem: Images are overexposed
```
Solution:
- Adjust lighting in scene (reduce directional light intensity)
- Use Auto Exposure Off in Post Process Volume
- Set Manual Exposure Compensation
```

### Problem: Capture crashes with out of memory
```
Solution:
- Reduce Image Width/Height: 2560×1440 → 1920×1080
- Capture in multiple sessions (reduce Views Per Ring or Subdivisions)
- Close other applications
- Restart Unreal Editor before large captures
```

---

## Next Steps

After successful capture:
1. Verify output files (cameras.txt, images.txt, points3D.txt, PNGs)
2. Train Gaussian Splatting model
3. Evaluate results
4. Iterate on capture settings if needed
5. Consider capturing additional angles or detail areas

Happy capturing! 🎥✨
