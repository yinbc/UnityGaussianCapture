#include "GaussianCaptureEditorWidget.h"
#include "Widgets/Input/SButton.h"
#include "Widgets/Input/SEditableTextBox.h"
#include "Widgets/Text/STextBlock.h"
#include "Widgets/Layout/SScrollBox.h"
#include "Widgets/Layout/SBorder.h"
#include "Styling/AppStyle.h"
#include "Styling/CoreStyle.h"
#include "Editor.h"
#include "EngineUtils.h"
#include "GaussianCaptureSubsystem.h"
#include "Misc/Paths.h"
#include "Misc/MessageDialog.h"
#include "Selection.h"
#include "EditorViewportClient.h"
#include "LevelEditorViewport.h"

#define LOCTEXT_NAMESPACE "SGaussianCaptureEditorWidget"

void SGaussianCaptureEditorWidget::Construct(const FArguments& InArgs)
{
	StatusText = LOCTEXT("StatusReady", "Ready");

	ChildSlot
	[
		SNew(SScrollBox)
		+ SScrollBox::Slot()
		.Padding(10)
		[
			SNew(SVerticalBox)

			// Title
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(STextBlock)
				.Text(LOCTEXT("Title", "Gaussian Splatting Capture Tool"))
				.Font(FCoreStyle::GetDefaultFontStyle("Bold", 16))
			]

			// Description
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(STextBlock)
				.Text(LOCTEXT("Description", "Capture multi-view scenes for Gaussian Splatting neural rendering with COLMAP export support."))
				.AutoWrapText(true)
			]

			// Separator
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(SBorder)
				.BorderImage(FAppStyle::GetBrush("Menu.Separator"))
				.Padding(0)
			]

			// Capture Actor Section
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(STextBlock)
				.Text(LOCTEXT("CaptureActorHeader", "1. Capture Actor Setup"))
				.Font(FCoreStyle::GetDefaultFontStyle("Bold", 12))
			]

			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 5)
			[
				SNew(STextBlock)
				.Text(LOCTEXT("CaptureActorInfo", "Create or select a GaussianCaptureActor in your level to define camera positions."))
				.AutoWrapText(true)
			]

			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(SHorizontalBox)

				+ SHorizontalBox::Slot()
				.AutoWidth()
				.Padding(0, 0, 5, 0)
				[
					SNew(SButton)
					.Text(LOCTEXT("CreateCaptureActor", "Create Capture Actor"))
					.OnClicked(this, &SGaussianCaptureEditorWidget::OnCreateCaptureActorClicked)
				]

				+ SHorizontalBox::Slot()
				.AutoWidth()
				[
					SNew(SButton)
					.Text(LOCTEXT("SelectCaptureActor", "Use Selected Actor"))
					.OnClicked(this, &SGaussianCaptureEditorWidget::OnSelectCaptureActorClicked)
				]
			]

			// Separator
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(SBorder)
				.BorderImage(FAppStyle::GetBrush("Menu.Separator"))
				.Padding(0)
			]

			// Output Settings Section
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(STextBlock)
				.Text(LOCTEXT("OutputHeader", "2. Output Settings"))
				.Font(FCoreStyle::GetDefaultFontStyle("Bold", 12))
			]

			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 5)
			[
				SNew(SHorizontalBox)

				+ SHorizontalBox::Slot()
				.AutoWidth()
				.Padding(0, 5, 10, 0)
				[
					SNew(STextBlock)
					.Text(LOCTEXT("OutputPath", "Output Directory:"))
					.MinDesiredWidth(120)
				]

				+ SHorizontalBox::Slot()
				.FillWidth(1.0f)
				[
					SAssignNew(OutputPathTextBox, SEditableTextBox)
					.Text(FText::FromString(GetDefaultOutputPath()))
				]
			]

			// Separator
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 10, 0, 10)
			[
				SNew(SBorder)
				.BorderImage(FAppStyle::GetBrush("Menu.Separator"))
				.Padding(0)
			]

			// Capture Section
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(STextBlock)
				.Text(LOCTEXT("CaptureHeader", "3. Start Capture"))
				.Font(FCoreStyle::GetDefaultFontStyle("Bold", 12))
			]

			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 5)
			[
				SNew(STextBlock)
				.Text(LOCTEXT("CaptureInfo", "Captures all camera views defined by the Capture Actor and exports COLMAP format data."))
				.AutoWrapText(true)
			]

			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(SButton)
				.Text(LOCTEXT("StartCapture", "Start Capture"))
				.OnClicked(this, &SGaussianCaptureEditorWidget::OnCaptureButtonClicked)
				.HAlign(HAlign_Center)
			]

			// Separator
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 0, 0, 10)
			[
				SNew(SBorder)
				.BorderImage(FAppStyle::GetBrush("Menu.Separator"))
				.Padding(0)
			]

			// Status Section
			+ SVerticalBox::Slot()
			.AutoHeight()
			[
				SNew(SVerticalBox)

				+ SVerticalBox::Slot()
				.AutoHeight()
				.Padding(0, 0, 0, 5)
				[
					SNew(STextBlock)
					.Text(LOCTEXT("StatusHeader", "Status"))
					.Font(FCoreStyle::GetDefaultFontStyle("Bold", 12))
				]

				+ SVerticalBox::Slot()
				.AutoHeight()
				[
					SAssignNew(StatusTextBlock, STextBlock)
					.Text(StatusText)
				]
			]

			// Info Section
			+ SVerticalBox::Slot()
			.AutoHeight()
			.Padding(0, 20, 0, 0)
			[
				SNew(SBorder)
				.BorderImage(FAppStyle::GetBrush("ToolPanel.GroupBorder"))
				.Padding(10)
				[
					SNew(SVerticalBox)

					+ SVerticalBox::Slot()
					.AutoHeight()
					.Padding(0, 0, 0, 5)
					[
						SNew(STextBlock)
						.Text(LOCTEXT("InfoHeader", "How to Use"))
						.Font(FCoreStyle::GetDefaultFontStyle("Bold", 10))
					]

					+ SVerticalBox::Slot()
					.AutoHeight()
					[
						SNew(STextBlock)
						.Text(LOCTEXT("InfoText",
							"1. Create a GaussianCaptureActor and configure capture settings (Dome/Volume mode)\n"
							"2. Adjust camera count, resolution, FOV, and point cloud settings in the Details panel\n"
							"3. Set output directory path\n"
							"4. Click 'Start Capture' to generate multi-view dataset\n"
							"5. Use exported COLMAP data for Gaussian Splatting training"))
						.AutoWrapText(true)
					]
				]
			]
		]
	];
}

AGaussianCaptureActor* SGaussianCaptureEditorWidget::GetSelectedCaptureActor() const
{
	if (SelectedCaptureActor.IsValid())
	{
		return SelectedCaptureActor.Get();
	}
	return nullptr;
}

FString SGaussianCaptureEditorWidget::GetDefaultOutputPath() const
{
	return FPaths::ProjectSavedDir() / TEXT("GaussianCapture");
}

void SGaussianCaptureEditorWidget::UpdateStatusText(const FText& NewText)
{
	StatusText = NewText;
	if (StatusTextBlock.IsValid())
	{
		StatusTextBlock->SetText(StatusText);
	}
}

FReply SGaussianCaptureEditorWidget::OnCreateCaptureActorClicked()
{
	UWorld* World = GEditor->GetEditorWorldContext().World();
	if (!World)
	{
		UpdateStatusText(LOCTEXT("ErrorNoWorld", "Error: No valid world"));
		return FReply::Handled();
	}

	// Spawn a new GaussianCaptureActor
	FVector SpawnLocation = FVector::ZeroVector;
	FRotator SpawnRotation = FRotator::ZeroRotator;

	// Try to spawn at editor camera location
	if (GEditor->GetActiveViewport())
	{
		FViewport* Viewport = GEditor->GetActiveViewport();
		FEditorViewportClient* ViewportClient = static_cast<FEditorViewportClient*>(Viewport->GetClient());
		if (ViewportClient)
		{
			SpawnLocation = ViewportClient->GetViewLocation();
		}
	}

	AGaussianCaptureActor* NewActor = World->SpawnActor<AGaussianCaptureActor>(AGaussianCaptureActor::StaticClass(), SpawnLocation, SpawnRotation);
	if (NewActor)
	{
		SelectedCaptureActor = NewActor;
		GEditor->SelectNone(false, true);
		GEditor->SelectActor(NewActor, true, true);
		UpdateStatusText(LOCTEXT("CaptureActorCreated", "Created new GaussianCaptureActor"));
	}
	else
	{
		UpdateStatusText(LOCTEXT("ErrorCreatingActor", "Error: Failed to create capture actor"));
	}

	return FReply::Handled();
}

FReply SGaussianCaptureEditorWidget::OnSelectCaptureActorClicked()
{
	USelection* Selection = GEditor->GetSelectedActors();
	if (Selection && Selection->Num() > 0)
	{
		for (FSelectionIterator It(*Selection); It; ++It)
		{
			AGaussianCaptureActor* Actor = Cast<AGaussianCaptureActor>(*It);
			if (Actor)
			{
				SelectedCaptureActor = Actor;
				UpdateStatusText(FText::Format(LOCTEXT("CaptureActorSelected", "Selected: {0}"),
					FText::FromString(Actor->GetName())));
				return FReply::Handled();
			}
		}

		UpdateStatusText(LOCTEXT("ErrorNotCaptureActor", "Error: Selected actor is not a GaussianCaptureActor"));
	}
	else
	{
		UpdateStatusText(LOCTEXT("ErrorNoSelection", "Error: No actor selected"));
	}

	return FReply::Handled();
}

FReply SGaussianCaptureEditorWidget::OnCaptureButtonClicked()
{
	// Validate capture actor
	AGaussianCaptureActor* CaptureActor = GetSelectedCaptureActor();
	if (!CaptureActor)
	{
		FMessageDialog::Open(EAppMsgType::Ok,
			LOCTEXT("ErrorNoCaptureActor", "Please create or select a GaussianCaptureActor first."));
		UpdateStatusText(LOCTEXT("ErrorNoCaptureActor", "Error: No capture actor selected"));
		return FReply::Handled();
	}

	// Validate world
	UWorld* World = CaptureActor->GetWorld();
	if (!World)
	{
		UpdateStatusText(LOCTEXT("ErrorNoWorld", "Error: No valid world"));
		return FReply::Handled();
	}

	// Get output path
	FString OutputPath = OutputPathTextBox->GetText().ToString();
	if (OutputPath.IsEmpty())
	{
		OutputPath = GetDefaultOutputPath();
	}

	// Confirm with user
	FText ConfirmMessage = FText::Format(
		LOCTEXT("ConfirmCapture", "Start capture with the following settings?\n\nCapture Mode: {0}\nOutput Path: {1}\n\nThis may take several minutes depending on the number of views."),
		CaptureActor->CaptureMode == ECaptureMode::Dome ? LOCTEXT("ModeDome", "Dome") : LOCTEXT("ModeVolume", "Volume"),
		FText::FromString(OutputPath)
	);

	EAppReturnType::Type Result = FMessageDialog::Open(EAppMsgType::YesNo, ConfirmMessage);
	if (Result != EAppReturnType::Yes)
	{
		return FReply::Handled();
	}

	// Start capture
	UpdateStatusText(LOCTEXT("StatusCapturing", "Capturing... Please wait."));

	UGaussianCaptureSubsystem* Subsystem = GEngine->GetEngineSubsystem<UGaussianCaptureSubsystem>();
	if (!Subsystem)
	{
		UpdateStatusText(LOCTEXT("ErrorNoSubsystem", "Error: Capture subsystem not available"));
		return FReply::Handled();
	}

	bool bSuccess = Subsystem->CaptureAllViews(World, CaptureActor, OutputPath);

	if (bSuccess)
	{
		UpdateStatusText(FText::Format(LOCTEXT("StatusComplete", "Capture complete! Output: {0}"),
			FText::FromString(OutputPath)));

		FMessageDialog::Open(EAppMsgType::Ok,
			FText::Format(LOCTEXT("CaptureSuccess", "Capture completed successfully!\n\nOutput directory: {0}"),
				FText::FromString(OutputPath)));
	}
	else
	{
		UpdateStatusText(LOCTEXT("StatusFailed", "Capture failed. Check log for details."));

		FMessageDialog::Open(EAppMsgType::Ok,
			LOCTEXT("CaptureFailed", "Capture failed. Please check the Output Log for error details."));
	}

	return FReply::Handled();
}

#undef LOCTEXT_NAMESPACE
