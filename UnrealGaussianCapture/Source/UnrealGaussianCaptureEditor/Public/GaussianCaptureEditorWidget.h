#pragma once

#include "CoreMinimal.h"
#include "Widgets/SCompoundWidget.h"
#include "Widgets/DeclarativeSyntaxSupport.h"
#include "GaussianCaptureActor.h"

class SEditableTextBox;
class SCheckBox;
class SSpinBox;
class SComboBox;

/**
 * Main editor widget for Gaussian Capture
 */
class SGaussianCaptureEditorWidget : public SCompoundWidget
{
public:
	SLATE_BEGIN_ARGS(SGaussianCaptureEditorWidget) {}
	SLATE_END_ARGS()

	void Construct(const FArguments& InArgs);

private:
	// UI Callbacks
	FReply OnCaptureButtonClicked();
	FReply OnCreateCaptureActorClicked();
	FReply OnSelectCaptureActorClicked();

	// Helper functions
	AGaussianCaptureActor* GetSelectedCaptureActor() const;
	FString GetDefaultOutputPath() const;

	// UI State
	TSharedPtr<SEditableTextBox> OutputPathTextBox;
	TWeakObjectPtr<AGaussianCaptureActor> SelectedCaptureActor;

	// Status text
	FText StatusText;
	TSharedPtr<STextBlock> StatusTextBlock;

	void UpdateStatusText(const FText& NewText);
};
