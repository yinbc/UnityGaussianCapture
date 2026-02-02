# Troubleshooting Guide - Unreal Gaussian Capture

This guide helps resolve common issues when using the Unreal Gaussian Capture plugin.

## Issue: Black or Empty Images

If your captured images are completely black, try these solutions in order:

### 1. Check Scene Lighting

**Problem:** No lights in the scene = black images

**Solution:**
- Add a **Directional Light** to your scene (simulates sun)
- Add a **Sky Light** for ambient lighting
- Ensure lights are **not set to Movable** if you want baked lighting
- Check that your meshes are not set to **Hidden in Game**

**Quick Test:**
1. Play in Editor (PIE)
2. If you can see the scene, lighting is OK
3. If it's black in PIE, add lights first

### 2. Verify Target Object is Visible

**Problem:** Camera is looking at nothing or inside geometry

**Solution:**
- In the viewport, you should see:
  - 🔴 Red sphere at the target location
  - 🟢 Green spheres at camera positions
  - 🟡 Yellow arrows showing camera directions
- If arrows point away from the object, adjust **Dome Radius** or **Target Location**
- Ensure the target object is not hidden or disabled

### 3. Check Capture Settings

**Problem:** Wrong capture source or render target format

**Solution - In GaussianCaptureActor Details:**

```
✅ Correct Settings:
- Transparent Background: true (for RGBA) or false (for RGB)
- Image Width: 1920 (or higher)
- Image Height: 1080 (or higher)
- Camera FOV: 60-90 degrees
- Dome Radius: Should be 2-5x the object size
```

**Common Mistakes:**
- ❌ Dome Radius too small (camera inside object)
- ❌ Dome Radius too large (object too far away)
- ❌ Camera FOV too narrow (<30°) or too wide (>120°)

### 4. Ensure Proper World Type

**Problem:** Capturing in wrong world context

**Solution:**
- Must capture in **Editor World** or **PIE (Play In Editor)**
- Do NOT try to capture during game runtime from packaged build
- Use the Gaussian Capture window in the editor

### 5. Check Output Log for Errors

**Problem:** Silent failures

**Solution:**
1. Open **Window → Developer Tools → Output Log**
2. Look for errors with prefix `LogTemp:`
3. Common error messages:
   - `"Failed to read pixels"` → Render target issue
   - `"No pixels read"` → Render target not initialized
   - `"Failed to get render target resource"` → Rendering thread issue

### 6. Disable Post-Processing (Temporary Test)

**Problem:** Post-processing interferes with capture

**Solution:**
1. Add a **Post Process Volume** to your scene
2. Set it to **Unbound**
3. Under **Rendering Features**, disable:
   - Auto Exposure
   - Motion Blur
   - Bloom
4. Try capturing again

### 7. Check Material Settings

**Problem:** Materials not rendering correctly

**Solution:**
- Ensure materials are not set to **Masked** or **Translucent** without proper setup
- Check that textures are loaded (not streaming)
- Verify materials work in PIE

### 8. Verify Render Target Format

**Problem:** Incompatible render target format

**Solution:**
- The plugin uses `RTF_RGBA16f` for transparent backgrounds
- Uses `RTF_RGBA8` for opaque backgrounds
- These should work on all platforms, but check **Project Settings → Rendering → Mobile** if targeting mobile

## Issue: Partial Black Images (Some Views OK, Some Black)

### Cause: Frustum Culling or Occlusion

**Solution:**
- Some camera positions may be occluded or outside the scene
- This is normal for Volume Capture mode
- Check the green spheres - if they're inside walls or far away, they'll capture nothing

## Issue: Images Too Dark

### Cause: Under-exposed lighting

**Solution:**
1. Increase **Directional Light** intensity (default is 10, try 15-20)
2. Increase **Sky Light** intensity (default is 1, try 2-3)
3. Disable **Auto Exposure** in Post Process Volume
4. Set **Exposure Compensation** to +1 or +2 in Post Process Volume

## Issue: Images Have Wrong Colors

### Cause: Color space or tone mapping

**Solution:**
- Disable **Tone Curve** in Post Process Volume
- Set **White Balance** → Temp to 6500
- Check that meshes have proper materials assigned

## Issue: Transparent Background Not Working

### Cause: Composite mode or capture source

**Solution:**
1. Ensure **Transparent Background = true** in GaussianCaptureActor
2. The alpha channel requires `SCS_SceneColorHDR` capture source
3. Check that you're saving as PNG (JPG doesn't support alpha)
4. Verify in image viewer that supports alpha (Photoshop, GIMP, not Windows Photo Viewer)

## Issue: Capture is Very Slow

### Cause: High resolution or ray count

**Solution - Reduce Settings:**
```
Fast Preview Settings:
- Image Width: 1280
- Image Height: 720
- Ray Count: 5000
- Dome Rings: 2
- Views Per Ring: 6

This gives 12 views at 720p - completes in 1-2 minutes
```

## Issue: Out of Memory / Crash

### Cause: Too many high-resolution captures

**Solution:**
1. Capture in batches (the plugin does this automatically every 40 images)
2. Reduce resolution temporarily
3. Close other applications
4. Increase virtual memory in Windows settings
5. For huge captures, break into multiple sessions

## Diagnostic Checklist

Run through this checklist to identify the issue:

- [ ] Can you see the scene when playing in editor? (PIE)
- [ ] Are there lights in the scene? (Directional + Sky Light)
- [ ] Can you see the debug visualization? (red sphere, green cameras, yellow arrows)
- [ ] Are the cameras pointing at the object? (yellow arrows)
- [ ] Is Dome Radius appropriate? (2-5x object size)
- [ ] Have you checked the Output Log for errors?
- [ ] Does a test with 1 ring × 4 views work?
- [ ] Is the target object set correctly?

## Test Capture (Minimal Settings)

To isolate the problem, try this minimal test:

1. Create a new level
2. Add a **Cube** (Static Mesh)
3. Add a **Directional Light**
4. Add a **Sky Light**
5. Create GaussianCaptureActor with:
   ```
   Target Actor: [Your Cube]
   Dome Rings: 1
   Views Per Ring: 4
   Dome Radius: 500
   Image Width: 512
   Image Height: 512
   ```
6. Capture

If this works → Your original scene has an issue
If this fails → Plugin configuration issue

## Advanced: Check Render Target Manually

To manually verify the render target is working:

1. In the Gaussian Capture tool, start a capture
2. While capturing, open **Window → Developer Tools → Widget Reflector**
3. Or add this to test rendering:

```cpp
// Test code - add to your level blueprint
UTextureRenderTarget2D* TestRT = NewObject<UTextureRenderTarget2D>();
TestRT->InitAutoFormat(512, 512);
USceneCaptureComponent2D* TestCapture = NewObject<USceneCaptureComponent2D>(this);
TestCapture->TextureTarget = TestRT;
TestCapture->RegisterComponent();
TestCapture->CaptureScene();

// View TestRT in the content browser to see if it rendered
```

## Still Having Issues?

If none of these solutions work:

1. Check that your Unreal Engine version is supported (5.0+)
2. Verify the plugin is properly installed and enabled
3. Try recompiling the plugin from source
4. Check the GitHub issues page for similar problems
5. Provide the following info when reporting:
   - UE version
   - Output Log errors
   - Example capture settings
   - Screenshot of debug visualization

## Known Limitations

- **VR/XR Scenes:** May require special configuration
- **Mobile Preview:** May not work in mobile preview mode
- **Nanite Meshes:** Should work but may need specific settings
- **Ray Tracing:** Compatible, but increases capture time
- **World Partition:** May require loading all cells first

---

**Quick Fix for Most Issues:**
1. Add lights to scene
2. Set Dome Radius = 3x object size
3. Start with low settings (2 rings, 6 views, 1280x720)
4. Check Output Log for errors
