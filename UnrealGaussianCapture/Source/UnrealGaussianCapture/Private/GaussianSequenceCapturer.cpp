#include "GaussianSequenceCapturer.h"
#include "GaussianCaptureSubsystem.h"
#include "Engine/Engine.h"
#include "Misc/Paths.h"

UGaussianSequenceCapturer::UGaussianSequenceCapturer()
{
	StartFrame = 0;
	EndFrame = 100;
	FrameInterval = 1;
	TargetFrameRate = 30.0f;
	CaptureActor = nullptr;
	OutputDirectory = TEXT("");
	bIsCapturing = false;
	CurrentFrame = 0;
	AccumulatedTime = 0.0f;
	CapturedFrameCount = 0;
	WorldContext = nullptr;
}

bool UGaussianSequenceCapturer::StartSequenceCapture(UWorld* World)
{
	if (!World)
	{
		UE_LOG(LogTemp, Error, TEXT("Invalid world for sequence capture"));
		return false;
	}

	if (!CaptureActor)
	{
		UE_LOG(LogTemp, Error, TEXT("No capture actor specified"));
		return false;
	}

	if (OutputDirectory.IsEmpty())
	{
		OutputDirectory = FPaths::ProjectSavedDir() / TEXT("GaussianSequence");
	}

	WorldContext = World;
	bIsCapturing = true;
	CurrentFrame = StartFrame;
	AccumulatedTime = 0.0f;
	CapturedFrameCount = 0;

	UE_LOG(LogTemp, Log, TEXT("Started sequence capture: Frames %d-%d, Interval %d"), StartFrame, EndFrame, FrameInterval);

	return true;
}

void UGaussianSequenceCapturer::StopSequenceCapture()
{
	bIsCapturing = false;
	WorldContext = nullptr;

	UE_LOG(LogTemp, Log, TEXT("Stopped sequence capture. Captured %d frames."), CapturedFrameCount);
}

void UGaussianSequenceCapturer::TickCapture(float DeltaTime)
{
	if (!bIsCapturing || !WorldContext || !CaptureActor)
	{
		return;
	}

	AccumulatedTime += DeltaTime;

	// Check if we should capture this frame
	float FrameTime = 1.0f / TargetFrameRate;
	if (AccumulatedTime >= FrameTime)
	{
		AccumulatedTime = 0.0f;

		// Check if we're at a capture interval
		if ((CurrentFrame - StartFrame) % FrameInterval == 0)
		{
			CaptureCurrentFrame();
			CapturedFrameCount++;
		}

		CurrentFrame++;

		// Check if we've reached the end
		if (CurrentFrame > EndFrame)
		{
			StopSequenceCapture();
		}
	}
}

void UGaussianSequenceCapturer::CaptureCurrentFrame()
{
	UGaussianCaptureSubsystem* Subsystem = GEngine->GetEngineSubsystem<UGaussianCaptureSubsystem>();
	if (!Subsystem)
	{
		UE_LOG(LogTemp, Error, TEXT("Capture subsystem not available"));
		return;
	}

	// Create frame directory
	FString FrameDirectory = FPaths::Combine(OutputDirectory, FString::Printf(TEXT("Frame_%04d"), CurrentFrame));

	UE_LOG(LogTemp, Log, TEXT("Capturing frame %d to %s"), CurrentFrame, *FrameDirectory);

	// Capture all views for this frame
	Subsystem->CaptureAllViews(WorldContext, CaptureActor, FrameDirectory);
}
