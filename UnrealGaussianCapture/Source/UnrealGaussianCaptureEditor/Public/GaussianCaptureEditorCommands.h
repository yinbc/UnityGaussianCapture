#pragma once

#include "CoreMinimal.h"
#include "Framework/Commands/Commands.h"
#include "GaussianCaptureEditorStyle.h"

class FGaussianCaptureEditorCommands : public TCommands<FGaussianCaptureEditorCommands>
{
public:
	FGaussianCaptureEditorCommands()
		: TCommands<FGaussianCaptureEditorCommands>(TEXT("GaussianCapture"),
			NSLOCTEXT("Contexts", "GaussianCapture", "Gaussian Capture Plugin"),
			NAME_None,
			FGaussianCaptureEditorStyle::GetStyleSetName())
	{
	}

	// TCommands<> interface
	virtual void RegisterCommands() override;

public:
	TSharedPtr<FUICommandInfo> OpenPluginWindow;
};
