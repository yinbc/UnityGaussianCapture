#include "GaussianCaptureActor.h"
#include "DrawDebugHelpers.h"
#include "Engine/World.h"

AGaussianCaptureActor::AGaussianCaptureActor()
{
	PrimaryActorTick.bCanEverTick = true;

	// Default values
	CaptureMode = ECaptureMode::Dome;
	TargetLocation = FVector::ZeroVector;

	// Dome defaults
	DomeRings = 3;
	ViewsPerRing = 8;
	DomeRadius = 300.0f;
	DomeHeight = 100.0f;

	// Volume defaults
	VolumeCenter = FVector::ZeroVector;
	VolumeSize = FVector(400.0f, 400.0f, 400.0f);
	VolumeSubdivisions = 3;

	// Render defaults
	bTransparentBackground = true;
	ImageWidth = 1920;
	ImageHeight = 1080;
	CameraFOV = 90.0f;

	// Point cloud defaults
	RayCount = 10000;
	MaxRayDistance = 10000.0f;
}

void AGaussianCaptureActor::BeginPlay()
{
	Super::BeginPlay();
}

void AGaussianCaptureActor::Tick(float DeltaTime)
{
	Super::Tick(DeltaTime);

#if WITH_EDITOR
	UWorld* World = GetWorld();
	if (World && World->WorldType == EWorldType::Editor)
	{
		DrawDebugVisualization();
	}
#endif
}

#if WITH_EDITOR
void AGaussianCaptureActor::PostEditChangeProperty(FPropertyChangedEvent& PropertyChangedEvent)
{
	Super::PostEditChangeProperty(PropertyChangedEvent);
	// Force redraw when properties change
}

void AGaussianCaptureActor::DrawDebugVisualization() const
{
	if (!GetWorld())
		return;

	TArray<FCameraPosition> Positions = GetCameraPositions();

	// Draw camera positions
	for (const FCameraPosition& CamPos : Positions)
	{
		// Draw sphere at camera position
		DrawDebugSphere(GetWorld(), CamPos.Position, 10.0f, 8, FColor::Green, false, -1.0f, 0, 2.0f);

		// Draw direction arrow
		FVector Forward = CamPos.Rotation.Vector();
		DrawDebugDirectionalArrow(GetWorld(), CamPos.Position, CamPos.Position + Forward * 50.0f,
			20.0f, FColor::Yellow, false, -1.0f, 0, 2.0f);
	}

	// Draw target or volume bounds
	if (CaptureMode == ECaptureMode::Dome)
	{
		// Draw target sphere
		DrawDebugSphere(GetWorld(), TargetLocation, 20.0f, 12, FColor::Red, false, -1.0f, 0, 3.0f);

		// Draw dome ring
		DrawDebugCircle(GetWorld(), TargetLocation, DomeRadius, 32, FColor::Cyan, false, -1.0f, 0, 2.0f,
			FVector(0, 1, 0), FVector(0, 0, 1), false);
	}
	else if (CaptureMode == ECaptureMode::Volume)
	{
		// Draw volume bounding box
		DrawDebugBox(GetWorld(), VolumeCenter, VolumeSize * 0.5f, FColor::Cyan, false, -1.0f, 0, 2.0f);
	}
}
#endif

TArray<FCameraPosition> AGaussianCaptureActor::GetCameraPositions() const
{
	if (CaptureMode == ECaptureMode::Dome)
	{
		return GenerateDomePositions();
	}
	else
	{
		return GenerateVolumePositions();
	}
}

TArray<FCameraPosition> AGaussianCaptureActor::GenerateDomePositions() const
{
	TArray<FCameraPosition> Positions;

	for (int32 Ring = 0; Ring < DomeRings; ++Ring)
	{
		// Calculate elevation angle for this ring
		float ElevationAngle = (Ring + 1) * 90.0f / (DomeRings + 1);
		float RadAngle = FMath::DegreesToRadians(ElevationAngle);

		// Calculate ring radius and height
		float RingRadius = DomeRadius * FMath::Cos(RadAngle);
		float RingHeight = DomeHeight + DomeRadius * FMath::Sin(RadAngle);

		for (int32 View = 0; View < ViewsPerRing; ++View)
		{
			// Calculate azimuth angle
			float AzimuthAngle = (360.0f * View) / ViewsPerRing;
			float AzimuthRad = FMath::DegreesToRadians(AzimuthAngle);

			// Calculate camera position
			FVector CameraPos;
			CameraPos.X = TargetLocation.X + RingRadius * FMath::Cos(AzimuthRad);
			CameraPos.Y = TargetLocation.Y + RingRadius * FMath::Sin(AzimuthRad);
			CameraPos.Z = TargetLocation.Z + RingHeight;

			// Calculate rotation to look at target
			FVector Direction = (TargetLocation - CameraPos).GetSafeNormal();
			FRotator Rotation = Direction.Rotation();

			Positions.Add(FCameraPosition(CameraPos, Rotation));
		}
	}

	return Positions;
}

TArray<FVector> AGaussianCaptureActor::GenerateVolumeViewDirections() const
{
	TArray<FVector> Directions;

	// 8 horizontal directions (evenly spaced around horizon)
	for (int32 i = 0; i < 8; ++i)
	{
		float Angle = (360.0f * i) / 8.0f;
		float Rad = FMath::DegreesToRadians(Angle);
		Directions.Add(FVector(FMath::Cos(Rad), FMath::Sin(Rad), 0.0f).GetSafeNormal());
	}

	// 4 elevated directions (45 degrees up)
	for (int32 i = 0; i < 4; ++i)
	{
		float Angle = (360.0f * i) / 4.0f;
		float Rad = FMath::DegreesToRadians(Angle);
		float HorizontalScale = FMath::Cos(FMath::DegreesToRadians(45.0f));
		Directions.Add(FVector(
			FMath::Cos(Rad) * HorizontalScale,
			FMath::Sin(Rad) * HorizontalScale,
			FMath::Sin(FMath::DegreesToRadians(45.0f))
		).GetSafeNormal());
	}

	// 4 depressed directions (45 degrees down)
	for (int32 i = 0; i < 4; ++i)
	{
		float Angle = (360.0f * i) / 4.0f;
		float Rad = FMath::DegreesToRadians(Angle);
		float HorizontalScale = FMath::Cos(FMath::DegreesToRadians(45.0f));
		Directions.Add(FVector(
			FMath::Cos(Rad) * HorizontalScale,
			FMath::Sin(Rad) * HorizontalScale,
			-FMath::Sin(FMath::DegreesToRadians(45.0f))
		).GetSafeNormal());
	}

	// Zenith (straight up)
	Directions.Add(FVector(0.0f, 0.0f, 1.0f));

	// Nadir (straight down)
	Directions.Add(FVector(0.0f, 0.0f, -1.0f));

	return Directions;
}

TArray<FCameraPosition> AGaussianCaptureActor::GenerateVolumePositions() const
{
	TArray<FCameraPosition> Positions;
	TArray<FVector> ViewDirections = GenerateVolumeViewDirections();

	// Calculate step size for grid subdivision
	FVector Step = VolumeSize / (VolumeSubdivisions + 1);
	FVector HalfSize = VolumeSize * 0.5f;

	// Generate grid positions
	for (int32 x = 0; x <= VolumeSubdivisions; ++x)
	{
		for (int32 y = 0; y <= VolumeSubdivisions; ++y)
		{
			for (int32 z = 0; z <= VolumeSubdivisions; ++z)
			{
				// Calculate grid cell center
				FVector CellCenter = VolumeCenter - HalfSize + FVector(
					Step.X * (x + 0.5f),
					Step.Y * (y + 0.5f),
					Step.Z * (z + 0.5f)
				);

				// For each direction
				for (const FVector& Direction : ViewDirections)
				{
					FRotator Rotation = Direction.Rotation();
					Positions.Add(FCameraPosition(CellCenter, Rotation));
				}
			}
		}
	}

	return Positions;
}
