#include "GaussianCaptureSubsystem.h"
#include "Engine/TextureRenderTarget2D.h"
#include "Engine/World.h"
#include "Kismet/GameplayStatics.h"
#include "IImageWrapper.h"
#include "IImageWrapperModule.h"
#include "Modules/ModuleManager.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "HAL/PlatformFileManager.h"
#include "Components/SceneCaptureComponent2D.h"
#include "GameFramework/Actor.h"
#include "Engine/Engine.h"
#include "DrawDebugHelpers.h"
#include "RenderingThread.h"
#include "TextureResource.h"
#include "ImageUtils.h"

void UGaussianCaptureSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	UE_LOG(LogTemp, Log, TEXT("GaussianCaptureSubsystem initialized"));
}

void UGaussianCaptureSubsystem::Deinitialize()
{
	// Clean up resources
	if (RenderTarget)
	{
		RenderTarget->ConditionalBeginDestroy();
		RenderTarget = nullptr;
	}

	if (SceneCaptureComponent)
	{
		SceneCaptureComponent->ConditionalBeginDestroy();
		SceneCaptureComponent = nullptr;
	}

	Super::Deinitialize();
}

FColmapCamera UGaussianCaptureSubsystem::CalculateCameraIntrinsics(int32 Width, int32 Height, float FOV)
{
	FColmapCamera Camera;
	Camera.Width = Width;
	Camera.Height = Height;
	Camera.Model = TEXT("PINHOLE");

	// Calculate focal length from FOV
	float FOVRad = FMath::DegreesToRadians(FOV);
	float FocalLength = (Width / 2.0f) / FMath::Tan(FOVRad / 2.0f);

	// Principal point at image center
	float cx = Width / 2.0f;
	float cy = Height / 2.0f;

	Camera.Params = { FocalLength, FocalLength, cx, cy };

	return Camera;
}

void UGaussianCaptureSubsystem::UnrealToColmapTransform(const FVector& UnrealPos, const FRotator& UnrealRot,
	FQuat& OutQuaternion, FVector& OutTranslation)
{
	// Unreal: Z-up, X-forward, Y-right (left-handed)
	// COLMAP: Y-down, Z-forward, X-right (right-handed)

	// Conversion matrix: flip Y and Z axes
	FMatrix ConversionMatrix = FMatrix(
		FPlane(1, 0, 0, 0),
		FPlane(0, -1, 0, 0),
		FPlane(0, 0, -1, 0),
		FPlane(0, 0, 0, 1)
	);

	// Convert position
	FVector ConvertedPos = ConversionMatrix.TransformPosition(UnrealPos);
	OutTranslation = ConvertedPos;

	// Convert rotation
	FQuat UnrealQuat = UnrealRot.Quaternion();
	FMatrix RotMatrix = FRotationMatrix::Make(UnrealQuat);
	FMatrix ConvertedRotMatrix = ConversionMatrix * RotMatrix * ConversionMatrix.Inverse();
	OutQuaternion = ConvertedRotMatrix.ToQuat();
}

bool UGaussianCaptureSubsystem::CaptureView(UWorld* World, const FCameraPosition& CameraPos, const FString& OutputPath,
	int32 ImageIndex, int32 ImageWidth, int32 ImageHeight, float FOV, bool bTransparent)
{
	if (!World)
	{
		UE_LOG(LogTemp, Error, TEXT("CaptureView: Invalid world"));
		return false;
	}

	UE_LOG(LogTemp, Log, TEXT("CaptureView: Capturing image %d at position %s with rotation %s"),
		ImageIndex, *CameraPos.Position.ToString(), *CameraPos.Rotation.ToString());

	// Create or reuse render target
	if (!RenderTarget || RenderTarget->SizeX != ImageWidth || RenderTarget->SizeY != ImageHeight)
	{
		RenderTarget = NewObject<UTextureRenderTarget2D>();
		RenderTarget->RenderTargetFormat = bTransparent ? RTF_RGBA16f : RTF_RGBA8;
		RenderTarget->InitAutoFormat(ImageWidth, ImageHeight);
		RenderTarget->ClearColor = bTransparent ? FLinearColor(0, 0, 0, 0) : FLinearColor::Black;
		RenderTarget->bAutoGenerateMips = false;
		RenderTarget->UpdateResourceImmediate(true);
	}

	// Create or reuse scene capture component
	if (!SceneCaptureComponent)
	{
		AActor* TempActor = World->SpawnActor<AActor>(AActor::StaticClass(), FVector::ZeroVector, FRotator::ZeroRotator);
		SceneCaptureComponent = NewObject<USceneCaptureComponent2D>(TempActor);
		SceneCaptureComponent->RegisterComponent();
	}

	// Configure scene capture
	SceneCaptureComponent->SetWorldLocationAndRotation(CameraPos.Position, CameraPos.Rotation);
	SceneCaptureComponent->FOVAngle = FOV;
	SceneCaptureComponent->TextureTarget = RenderTarget;
	SceneCaptureComponent->CaptureSource = bTransparent ? ESceneCaptureSource::SCS_SceneColorHDR : ESceneCaptureSource::SCS_FinalColorLDR;
	SceneCaptureComponent->ProjectionType = ECameraProjectionMode::Perspective;
	SceneCaptureComponent->bCaptureEveryFrame = false;
	SceneCaptureComponent->bCaptureOnMovement = false;
	SceneCaptureComponent->bAlwaysPersistRenderingState = true;

	// Configure show flags for proper rendering
	SceneCaptureComponent->ShowFlags.SetAtmosphere(true);
	SceneCaptureComponent->ShowFlags.SetFog(true);
	SceneCaptureComponent->ShowFlags.SetLighting(true);
	SceneCaptureComponent->ShowFlags.SetPostProcessing(true);
	SceneCaptureComponent->ShowFlags.SetSkyLighting(true);
	SceneCaptureComponent->ShowFlags.SetDynamicShadows(true);
	SceneCaptureComponent->ShowFlags.SetStaticMeshes(true);
	SceneCaptureComponent->ShowFlags.SetSkeletalMeshes(true);
	SceneCaptureComponent->ShowFlags.SetLandscape(true);
	SceneCaptureComponent->ShowFlags.SetParticles(true);

	// Set composite mode
	SceneCaptureComponent->CompositeMode = bTransparent ? SCCM_Overwrite : SCCM_Composite;

	// Capture the scene
	SceneCaptureComponent->CaptureScene();

	// Flush rendering commands to ensure capture is complete
	FlushRenderingCommands();

	// Save to file
	FString FileName = FString::Printf(TEXT("%s/image_%04d.png"), *OutputPath, ImageIndex);
	return SaveTextureToPNG(RenderTarget, FileName);
}

bool UGaussianCaptureSubsystem::SaveTextureToPNG(UTextureRenderTarget2D* InRenderTarget, const FString& FilePath)
{
	if (!InRenderTarget)
	{
		UE_LOG(LogTemp, Error, TEXT("SaveTextureToPNG: Invalid render target"));
		return false;
	}

	// Read pixels from render target
	TArray<FColor> OutBMP;
	FTextureRenderTargetResource* RTResource = InRenderTarget->GameThread_GetRenderTargetResource();
	if (!RTResource)
	{
		UE_LOG(LogTemp, Error, TEXT("SaveTextureToPNG: Failed to get render target resource"));
		return false;
	}

	if (!RTResource->ReadPixels(OutBMP))
	{
		UE_LOG(LogTemp, Error, TEXT("SaveTextureToPNG: Failed to read pixels"));
		return false;
	}

	if (OutBMP.Num() == 0)
	{
		UE_LOG(LogTemp, Error, TEXT("SaveTextureToPNG: No pixels read from render target"));
		return false;
	}

	UE_LOG(LogTemp, Log, TEXT("SaveTextureToPNG: Read %d pixels from render target"), OutBMP.Num());

	// Get image wrapper module
	IImageWrapperModule& ImageWrapperModule = FModuleManager::LoadModuleChecked<IImageWrapperModule>(FName("ImageWrapper"));
	TSharedPtr<IImageWrapper> ImageWrapper = ImageWrapperModule.CreateImageWrapper(EImageFormat::PNG);

	if (!ImageWrapper.IsValid())
	{
		return false;
	}

	// Set raw image data
	if (!ImageWrapper->SetRaw(OutBMP.GetData(), OutBMP.Num() * sizeof(FColor),
		InRenderTarget->SizeX, InRenderTarget->SizeY, ERGBFormat::BGRA, 8))
	{
		return false;
	}

	// Get compressed data
	const TArray64<uint8>& CompressedData = ImageWrapper->GetCompressed();

	// Save to file
	return FFileHelper::SaveArrayToFile(CompressedData, *FilePath);
}

TArray<FColmapPoint3D> UGaussianCaptureSubsystem::GeneratePointCloud(UWorld* World, const FCameraPosition& CameraPos,
	UTextureRenderTarget2D* InRenderTarget, int32 RayCount, float MaxDistance)
{
	TArray<FColmapPoint3D> Points;

	if (!World || !InRenderTarget)
	{
		return Points;
	}

	// Calculate grid dimensions
	int32 GridSize = FMath::CeilToInt(FMath::Sqrt(static_cast<float>(RayCount)));

	// Read render target pixels for color sampling
	TArray<FColor> PixelData;
	FTextureRenderTargetResource* RTResource = InRenderTarget->GameThread_GetRenderTargetResource();
	if (!RTResource || !RTResource->ReadPixels(PixelData))
	{
		return Points;
	}

	int32 Width = InRenderTarget->SizeX;
	int32 Height = InRenderTarget->SizeY;

	// Get camera transform
	FVector CameraLocation = CameraPos.Position;
	FRotator CameraRotation = CameraPos.Rotation;
	FTransform CameraTransform(CameraRotation, CameraLocation);

	int32 PointID = 0;

	// Cast rays in a grid pattern
	for (int32 x = 0; x < GridSize; ++x)
	{
		for (int32 y = 0; y < GridSize; ++y)
		{
			// Calculate normalized screen coordinates
			float u = (x + 0.5f) / GridSize;
			float v = (y + 0.5f) / GridSize;

			// Convert to pixel coordinates
			int32 PixelX = FMath::Clamp(FMath::FloorToInt(u * Width), 0, Width - 1);
			int32 PixelY = FMath::Clamp(FMath::FloorToInt(v * Height), 0, Height - 1);

			// Calculate ray direction from screen space
			FVector2D ScreenPos(u * 2.0f - 1.0f, 1.0f - v * 2.0f);

			// Simple perspective projection
			FVector LocalDirection(1.0f, ScreenPos.X, ScreenPos.Y);
			LocalDirection.Normalize();

			FVector WorldDirection = CameraTransform.TransformVector(LocalDirection);

			// Perform raycast
			FHitResult HitResult;
			FVector Start = CameraLocation;
			FVector End = Start + WorldDirection * MaxDistance;

			FCollisionQueryParams QueryParams;
			QueryParams.bTraceComplex = true;

			if (World->LineTraceSingleByChannel(HitResult, Start, End, ECC_Visibility, QueryParams))
			{
				// Get color from render target
				int32 PixelIndex = PixelY * Width + PixelX;
				FColor PixelColor = PixelData[PixelIndex];

				// Skip transparent pixels
				if (PixelColor.A < 10)
				{
					continue;
				}

				// Create point
				FColmapPoint3D Point;
				Point.PointID = PointID++;
				Point.Position = HitResult.Location;
				Point.Color = PixelColor;
				Point.Error = 0.0f;

				Points.Add(Point);
			}
		}
	}

	return Points;
}

bool UGaussianCaptureSubsystem::CaptureAllViews(UWorld* World, AGaussianCaptureActor* CaptureActor, const FString& OutputDirectory)
{
	if (!World || !CaptureActor)
	{
		return false;
	}

	// Create output directory
	IPlatformFile& PlatformFile = FPlatformFileManager::Get().GetPlatformFile();
	if (!PlatformFile.DirectoryExists(*OutputDirectory))
	{
		PlatformFile.CreateDirectoryTree(*OutputDirectory);
	}

	// Get camera positions
	TArray<FCameraPosition> CameraPositions = CaptureActor->GetCameraPositions();

	if (CameraPositions.Num() == 0)
	{
		UE_LOG(LogTemp, Warning, TEXT("No camera positions generated"));
		return false;
	}

	// Calculate camera intrinsics
	FColmapCamera Camera = CalculateCameraIntrinsics(CaptureActor->ImageWidth, CaptureActor->ImageHeight, CaptureActor->CameraFOV);

	// Arrays for COLMAP export
	TArray<FColmapImage> Images;
	TArray<FColmapPoint3D> AllPoints;

	int32 PointIDOffset = 0;

	// Capture each view
	for (int32 i = 0; i < CameraPositions.Num(); ++i)
	{
		const FCameraPosition& CamPos = CameraPositions[i];

		UE_LOG(LogTemp, Log, TEXT("Capturing view %d of %d"), i + 1, CameraPositions.Num());

		// Capture view
		if (!CaptureView(World, CamPos, OutputDirectory, i, CaptureActor->ImageWidth, CaptureActor->ImageHeight,
			CaptureActor->CameraFOV, CaptureActor->bTransparentBackground))
		{
			UE_LOG(LogTemp, Warning, TEXT("Failed to capture view %d"), i);
			continue;
		}

		// Generate point cloud for this view
		TArray<FColmapPoint3D> ViewPoints = GeneratePointCloud(World, CamPos, RenderTarget,
			CaptureActor->RayCount, CaptureActor->MaxRayDistance);

		// Offset point IDs to avoid collisions
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
		UnrealToColmapTransform(CamPos.Position, CamPos.Rotation, ImageData.Quaternion, ImageData.Translation);
		Images.Add(ImageData);

		// Memory management: clean up every 40 images
		if ((i + 1) % 40 == 0)
		{
			if (RenderTarget)
			{
				RenderTarget->UpdateResourceImmediate(true);
			}
			CollectGarbage(GARBAGE_COLLECTION_KEEPFLAGS);
		}
	}

	// Export COLMAP format
	bool bSuccess = ExportColmapFormat(OutputDirectory, Camera, Images, AllPoints);

	UE_LOG(LogTemp, Log, TEXT("Capture complete: %d views, %d points"), Images.Num(), AllPoints.Num());

	return bSuccess;
}

bool UGaussianCaptureSubsystem::ExportColmapFormat(const FString& OutputDirectory, const FColmapCamera& Camera,
	const TArray<FColmapImage>& Images, const TArray<FColmapPoint3D>& Points)
{
	bool bSuccess = true;

	// Write cameras.txt
	FString CamerasPath = OutputDirectory / TEXT("cameras.txt");
	bSuccess &= WriteCamerasFile(CamerasPath, Camera);

	// Write images.txt
	FString ImagesPath = OutputDirectory / TEXT("images.txt");
	bSuccess &= WriteImagesFile(ImagesPath, Images);

	// Write points3D.txt
	FString PointsPath = OutputDirectory / TEXT("points3D.txt");
	bSuccess &= WritePoints3DFile(PointsPath, Points);

	return bSuccess;
}

bool UGaussianCaptureSubsystem::WriteCamerasFile(const FString& FilePath, const FColmapCamera& Camera)
{
	FString Content = TEXT("# Camera list with one line of data per camera:\n");
	Content += TEXT("#   CAMERA_ID, MODEL, WIDTH, HEIGHT, PARAMS[]\n");
	Content += TEXT("# Number of cameras: 1\n");

	Content += FString::Printf(TEXT("%d %s %d %d"),
		Camera.CameraID,
		*Camera.Model,
		Camera.Width,
		Camera.Height);

	for (float Param : Camera.Params)
	{
		Content += FString::Printf(TEXT(" %f"), Param);
	}
	Content += TEXT("\n");

	return FFileHelper::SaveStringToFile(Content, *FilePath);
}

bool UGaussianCaptureSubsystem::WriteImagesFile(const FString& FilePath, const TArray<FColmapImage>& Images)
{
	FString Content = TEXT("# Image list with two lines of data per image:\n");
	Content += TEXT("#   IMAGE_ID, QW, QX, QY, QZ, TX, TY, TZ, CAMERA_ID, NAME\n");
	Content += TEXT("#   POINTS2D[] as (X, Y, POINT3D_ID)\n");
	Content += FString::Printf(TEXT("# Number of images: %d\n"), Images.Num());

	for (const FColmapImage& Image : Images)
	{
		Content += FString::Printf(TEXT("%d %f %f %f %f %f %f %f %d %s\n"),
			Image.ImageID,
			Image.Quaternion.W,
			Image.Quaternion.X,
			Image.Quaternion.Y,
			Image.Quaternion.Z,
			Image.Translation.X,
			Image.Translation.Y,
			Image.Translation.Z,
			Image.CameraID,
			*Image.ImageName);

		// Empty line for 2D points (we don't track 2D-3D correspondences)
		Content += TEXT("\n");
	}

	return FFileHelper::SaveStringToFile(Content, *FilePath);
}

bool UGaussianCaptureSubsystem::WritePoints3DFile(const FString& FilePath, const TArray<FColmapPoint3D>& Points)
{
	FString Content = TEXT("# 3D point list with one line of data per point:\n");
	Content += TEXT("#   POINT3D_ID, X, Y, Z, R, G, B, ERROR, TRACK[] as (IMAGE_ID, POINT2D_IDX)\n");
	Content += FString::Printf(TEXT("# Number of points: %d\n"), Points.Num());

	for (const FColmapPoint3D& Point : Points)
	{
		Content += FString::Printf(TEXT("%d %f %f %f %d %d %d %f\n"),
			Point.PointID,
			Point.Position.X,
			Point.Position.Y,
			Point.Position.Z,
			Point.Color.R,
			Point.Color.G,
			Point.Color.B,
			Point.Error);
	}

	return FFileHelper::SaveStringToFile(Content, *FilePath);
}
