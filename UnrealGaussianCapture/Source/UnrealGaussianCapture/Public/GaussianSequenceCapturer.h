#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "GaussianCaptureActor.h"
#include "GaussianSequenceCapturer.generated.h"

/**
 * Helper class for capturing animation sequences over time
 * Used for 4D Gaussian Splatting (temporal data)
 */
UCLASS(BlueprintType)
class UNREALGAUSSIANCAPTURE_API UGaussianSequenceCapturer : public UObject
{
	GENERATED_BODY()

public:
	// Start frame for sequence capture
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Sequence Capture")
	int32 StartFrame;

	// End frame for sequence capture
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Sequence Capture")
	int32 EndFrame;

	// Frame interval (capture every N frames)
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Sequence Capture")
	int32 FrameInterval;

	// Target framerate for playback
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Sequence Capture")
	float TargetFrameRate;

	// Capture actor to use for each frame
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Sequence Capture")
	AGaussianCaptureActor* CaptureActor;

	// Output directory for sequence
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Sequence Capture")
	FString OutputDirectory;

	// Is currently capturing
	UPROPERTY(BlueprintReadOnly, Category = "Sequence Capture")
	bool bIsCapturing;

	// Current frame being captured
	UPROPERTY(BlueprintReadOnly, Category = "Sequence Capture")
	int32 CurrentFrame;

	UGaussianSequenceCapturer();

	// Start sequence capture
	UFUNCTION(BlueprintCallable, Category = "Sequence Capture")
	bool StartSequenceCapture(UWorld* World);

	// Stop sequence capture
	UFUNCTION(BlueprintCallable, Category = "Sequence Capture")
	void StopSequenceCapture();

	// Tick function (call every frame during capture)
	UFUNCTION(BlueprintCallable, Category = "Sequence Capture")
	void TickCapture(float DeltaTime);

private:
	float AccumulatedTime;
	int32 CapturedFrameCount;
	UWorld* WorldContext;

	void CaptureCurrentFrame();
};
