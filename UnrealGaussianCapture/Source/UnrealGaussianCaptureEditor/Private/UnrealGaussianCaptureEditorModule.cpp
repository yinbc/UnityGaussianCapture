#include "UnrealGaussianCaptureEditorModule.h"
#include "GaussianCaptureEditorStyle.h"
#include "GaussianCaptureEditorCommands.h"
#include "LevelEditor.h"
#include "Widgets/Docking/SDockTab.h"
#include "Widgets/Input/SButton.h"
#include "Widgets/Text/STextBlock.h"
#include "ToolMenus.h"
#include "GaussianCaptureEditorWidget.h"

static const FName GaussianCaptureTabName("GaussianCapture");

#define LOCTEXT_NAMESPACE "FUnrealGaussianCaptureEditorModule"

void FUnrealGaussianCaptureEditorModule::StartupModule()
{
	// Register styles
	FGaussianCaptureEditorStyle::Initialize();
	FGaussianCaptureEditorStyle::ReloadTextures();

	// Register commands
	FGaussianCaptureEditorCommands::Register();

	PluginCommands = MakeShareable(new FUICommandList);

	PluginCommands->MapAction(
		FGaussianCaptureEditorCommands::Get().OpenPluginWindow,
		FExecuteAction::CreateRaw(this, &FUnrealGaussianCaptureEditorModule::PluginButtonClicked),
		FCanExecuteAction());

	// Register menus
	UToolMenus::RegisterStartupCallback(FSimpleMulticastDelegate::FDelegate::CreateRaw(this, &FUnrealGaussianCaptureEditorModule::RegisterMenus));

	// Register tab spawner
	FGlobalTabmanager::Get()->RegisterNomadTabSpawner(GaussianCaptureTabName, FOnSpawnTab::CreateLambda([](const FSpawnTabArgs& Args)
	{
		return SNew(SDockTab)
			.TabRole(ETabRole::NomadTab)
			[
				SNew(SGaussianCaptureEditorWidget)
			];
	}))
	.SetDisplayName(LOCTEXT("GaussianCaptureTabTitle", "Gaussian Capture"))
	.SetMenuType(ETabSpawnerMenuType::Hidden);
}

void FUnrealGaussianCaptureEditorModule::ShutdownModule()
{
	UToolMenus::UnRegisterStartupCallback(this);
	UToolMenus::UnregisterOwner(this);

	FGaussianCaptureEditorStyle::Shutdown();
	FGaussianCaptureEditorCommands::Unregister();

	FGlobalTabmanager::Get()->UnregisterNomadTabSpawner(GaussianCaptureTabName);
}

void FUnrealGaussianCaptureEditorModule::PluginButtonClicked()
{
	FGlobalTabmanager::Get()->TryInvokeTab(GaussianCaptureTabName);
}

void FUnrealGaussianCaptureEditorModule::RegisterMenus()
{
	// Owner will be used for cleanup in call to UToolMenus::UnregisterOwner
	FToolMenuOwnerScoped OwnerScoped(this);

	{
		UToolMenu* Menu = UToolMenus::Get()->ExtendMenu("LevelEditor.MainMenu.Window");
		{
			FToolMenuSection& Section = Menu->FindOrAddSection("WindowLayout");
			Section.AddMenuEntryWithCommandList(FGaussianCaptureEditorCommands::Get().OpenPluginWindow, PluginCommands);
		}
	}

	{
		UToolMenu* ToolbarMenu = UToolMenus::Get()->ExtendMenu("LevelEditor.LevelEditorToolBar");
		{
			FToolMenuSection& Section = ToolbarMenu->FindOrAddSection("Settings");
			{
				FToolMenuEntry& Entry = Section.AddEntry(FToolMenuEntry::InitToolBarButton(FGaussianCaptureEditorCommands::Get().OpenPluginWindow));
				Entry.SetCommandList(PluginCommands);
			}
		}
	}
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FUnrealGaussianCaptureEditorModule, UnrealGaussianCaptureEditor)
