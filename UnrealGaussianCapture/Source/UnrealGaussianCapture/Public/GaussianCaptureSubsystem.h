#pragma once

#include "CoreMinimal.h"
#include "Subsystems/EngineSubsystem.h"
#include "Engine/SceneCapture2D.h"
#include "Components/SceneCaptureComponent2D.h"
#include "GaussianCaptureActor.h"
#include "GaussianCaptureSubsystem.generated.h"

USTRUCT(BlueprintType)
struct FColmapCamera
{
	GENERATED_BODY()

	UPROPERTY()
	int32 CameraID;

	UPROPERTY()
	FString Model; // "PINHOLE"

	UPROPERTY()
	int32 Width;

	UPROPERTY()
	int32 Height;

	UPROPERTY()
	TArray<float> Params; // [fx, fy, cx, cy]

	FColmapCamera() : CameraID(1), Model(TEXT("PINHOLE")), Width(1920), Height(1080)
	{
		Params = { 800.0f, 800.0f, 960.0f, 540.0f };
	}
};

USTRUCT(BlueprintType)
struct FColmapImage
{
	GENERATED_BODY()

	UPROPERTY()
	int32 ImageID;

	UPROPERTY()
	FQuat Quaternion; // qw, qx, qy, qz

	UPROPERTY()
	FVector Translation;

	UPROPERTY()
	int32 CameraID;

	UPROPERTY()
	FString ImageName;

	FColmapImage() : ImageID(0), Quaternion(FQuat::Identity), Translation(FVector::ZeroVector), CameraID(1), ImageName(TEXT("")) {}
};

USTRUCT(BlueprintType)
struct FColmapPoint3D
{
	GENERATED_BODY()

	UPROPERTY()
	int32 PointID;

	UPROPERTY()
	FVector Position;

	UPROPERTY()
	FColor Color;

	UPROPERTY()
	float Error;

	FColmapPoint3D() : PointID(0), Position(FVector::ZeroVector), Color(FColor::White), Error(0.0f) {}
};

/**
 * Subsystem for capturing and exporting Gaussian Splatting datasets
 */
UCLASS()
class UNREALGAUSSIANCAPTURE_API UGaussianCaptureSubsystem : public UEngineSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	// Capture a single view from given camera position
	UFUNCTION(BlueprintCallable, Category = "Gaussian Capture")
	bool CaptureView(UWorld* World, const FCameraPosition& CameraPos, const FString& OutputPath, int32 ImageIndex,
		int32 ImageWidth, int32 ImageHeight, float FOV, bool bTransparent);

	// Capture all views from a GaussianCaptureActor
	UFUNCTION(BlueprintCallable, Category = "Gaussian Capture")
	bool CaptureAllViews(UWorld* World, AGaussianCaptureActor* CaptureActor, const FString& OutputDirectory);

	// Generate point cloud from captured view using raycasting
	UFUNCTION(BlueprintCallable, Category = "Gaussian Capture")
	TArray<FColmapPoint3D> GeneratePointCloud(UWorld* World, const FCameraPosition& CameraPos,
		UTextureRenderTarget2D* RenderTarget, int32 RayCount, float MaxDistance);

	// Export COLMAP format files
	UFUNCTION(BlueprintCallable, Category = "Gaussian Capture")
	bool ExportColmapFormat(const FString& OutputDirectory, const FColmapCamera& Camera,
		const TArray<FColmapImage>& Images, const TArray<FColmapPoint3D>& Points);

	// Convert Unreal transform to COLMAP format (coordinate system conversion)
	UFUNCTION(BlueprintCallable, Category = "Gaussian Capture")
	void UnrealToColmapTransform(const FVector& UnrealPos, const FRotator& UnrealRot,
		FQuat& OutQuaternion, FVector& OutTranslation);

	// Calculate camera intrinsics from FOV and resolution
	UFUNCTION(BlueprintCallable, Category = "Gaussian Capture")
	FColmapCamera CalculateCameraIntrinsics(int32 Width, int32 Height, float FOV);

private:
	// Helper to save texture as PNG
	bool SaveTextureToPNG(UTextureRenderTarget2D* RenderTarget, const FString& FilePath);

	// Helper to write COLMAP cameras.txt
	bool WriteCamerasFile(const FString& FilePath, const FColmapCamera& Camera);

	// Helper to write COLMAP images.txt
	bool WriteImagesFile(const FString& FilePath, const TArray<FColmapImage>& Images);

	// Helper to write COLMAP points3D.txt
	bool WritePoints3DFile(const FString& FilePath, const TArray<FColmapPoint3D>& Points);

	// Scene capture component for rendering
	UPROPERTY(Transient)
	USceneCaptureComponent2D* SceneCaptureComponent;

	// Render target for capturing
	UPROPERTY(Transient)
	UTextureRenderTarget2D* RenderTarget;
};
