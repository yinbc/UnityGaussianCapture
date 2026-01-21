#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "GaussianCaptureActor.generated.h"

UENUM(BlueprintType)
enum class ECaptureMode : uint8
{
	Dome UMETA(DisplayName = "Dome"),
	Volume UMETA(DisplayName = "Volume")
};

USTRUCT(BlueprintType)
struct FCameraPosition
{
	GENERATED_BODY()

	UPROPERTY()
	FVector Position;

	UPROPERTY()
	FRotator Rotation;

	FCameraPosition() : Position(FVector::ZeroVector), Rotation(FRotator::ZeroRotator) {}
	FCameraPosition(const FVector& InPos, const FRotator& InRot) : Position(InPos), Rotation(InRot) {}
};

/**
 * Actor that manages camera positions for Gaussian Splatting capture
 * Supports both Dome (spherical) and Volume (grid-based) capture modes
 */
UCLASS()
class UNREALGAUSSIANCAPTURE_API AGaussianCaptureActor : public AActor
{
	GENERATED_BODY()

public:
	AGaussianCaptureActor();

	// Capture mode selection
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Capture Settings")
	ECaptureMode CaptureMode;

	// Target actor for dome mode (cameras will look at this actor's location)
	// If set, this will override TargetLocation
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Capture Settings", meta = (DisplayName = "Target Actor (Optional)"))
	AActor* TargetActor;

	// Manual target location for dome mode (used if TargetActor is not set)
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Capture Settings", meta = (EditCondition = "TargetActor == nullptr", EditConditionHides))
	FVector TargetLocation;

	// Use the actor's bounding box center as target (only if TargetActor is set)
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Capture Settings", meta = (EditCondition = "TargetActor != nullptr", EditConditionHides))
	bool bUseActorBoundsCenter;

	// === Dome Mode Settings ===

	// Number of horizontal rings in dome
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Dome Settings", meta = (ClampMin = "1", ClampMax = "20"))
	int32 DomeRings;

	// Number of views per ring
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Dome Settings", meta = (ClampMin = "3", ClampMax = "36"))
	int32 ViewsPerRing;

	// Radius of the dome
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Dome Settings", meta = (ClampMin = "10"))
	float DomeRadius;

	// Height of the dome above target
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Dome Settings")
	float DomeHeight;

	// === Volume Mode Settings ===

	// Bounding box center for volume capture
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Volume Settings")
	FVector VolumeCenter;

	// Bounding box size
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Volume Settings")
	FVector VolumeSize;

	// Subdivisions for volume grid (higher = more capture points)
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Volume Settings", meta = (ClampMin = "1", ClampMax = "10"))
	int32 VolumeSubdivisions;

	// === Rendering Settings ===

	// Render with transparent background (RGBA)
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Render Settings")
	bool bTransparentBackground;

	// Image resolution width
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Render Settings", meta = (ClampMin = "128"))
	int32 ImageWidth;

	// Image resolution height
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Render Settings", meta = (ClampMin = "128"))
	int32 ImageHeight;

	// Camera field of view
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Render Settings", meta = (ClampMin = "5", ClampMax = "170"))
	float CameraFOV;

	// === Point Cloud Settings ===

	// Number of rays to cast for point cloud generation
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Point Cloud Settings", meta = (ClampMin = "100"))
	int32 RayCount;

	// Maximum ray distance
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Point Cloud Settings", meta = (ClampMin = "100"))
	float MaxRayDistance;

	// === Functions ===

	// Get the effective target location (from TargetActor if set, otherwise TargetLocation)
	UFUNCTION(BlueprintCallable, Category = "Capture")
	FVector GetEffectiveTargetLocation() const;

	// Generate camera positions for dome mode
	UFUNCTION(BlueprintCallable, Category = "Capture")
	TArray<FCameraPosition> GenerateDomePositions() const;

	// Generate camera positions for volume mode
	UFUNCTION(BlueprintCallable, Category = "Capture")
	TArray<FCameraPosition> GenerateVolumePositions() const;

	// Get all camera positions based on current mode
	UFUNCTION(BlueprintCallable, Category = "Capture")
	TArray<FCameraPosition> GetCameraPositions() const;

protected:
	virtual void BeginPlay() override;

#if WITH_EDITOR
	virtual void PostEditChangeProperty(FPropertyChangedEvent& PropertyChangedEvent) override;
#endif

public:
	virtual void Tick(float DeltaTime) override;

#if WITH_EDITORONLY_DATA
	// Draw debug visualization in editor
	virtual void DrawDebugVisualization() const;
#endif

private:
	// Helper function to generate 16 view directions for volume capture
	TArray<FVector> GenerateVolumeViewDirections() const;
};
