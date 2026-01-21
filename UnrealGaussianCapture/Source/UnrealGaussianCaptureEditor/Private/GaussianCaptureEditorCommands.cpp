#include "GaussianCaptureEditorCommands.h"

#define LOCTEXT_NAMESPACE "FGaussianCaptureEditorModule"

void FGaussianCaptureEditorCommands::RegisterCommands()
{
	UI_COMMAND(OpenPluginWindow, "Gaussian Capture", "Open Gaussian Capture window for multi-view scene capture", EUserInterfaceActionType::Button, FInputChord());
}

#undef LOCTEXT_NAMESPACE
