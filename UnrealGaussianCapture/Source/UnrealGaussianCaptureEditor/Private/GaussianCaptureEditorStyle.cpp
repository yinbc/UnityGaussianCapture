#include "GaussianCaptureEditorStyle.h"
#include "Styling/SlateStyleRegistry.h"
#include "Framework/Application/SlateApplication.h"
#include "Slate/SlateGameResources.h"
#include "Interfaces/IPluginManager.h"

TSharedPtr<FSlateStyleSet> FGaussianCaptureEditorStyle::StyleInstance = nullptr;

void FGaussianCaptureEditorStyle::Initialize()
{
	if (!StyleInstance.IsValid())
	{
		StyleInstance = Create();
		FSlateStyleRegistry::RegisterSlateStyle(*StyleInstance);
	}
}

void FGaussianCaptureEditorStyle::Shutdown()
{
	FSlateStyleRegistry::UnRegisterSlateStyle(*StyleInstance);
	ensure(StyleInstance.IsUnique());
	StyleInstance.Reset();
}

FName FGaussianCaptureEditorStyle::GetStyleSetName()
{
	static FName StyleSetName(TEXT("GaussianCaptureEditorStyle"));
	return StyleSetName;
}

const FVector2D Icon16x16(16.0f, 16.0f);
const FVector2D Icon20x20(20.0f, 20.0f);
const FVector2D Icon40x40(40.0f, 40.0f);

TSharedRef<FSlateStyleSet> FGaussianCaptureEditorStyle::Create()
{
	TSharedRef<FSlateStyleSet> Style = MakeShareable(new FSlateStyleSet("GaussianCaptureEditorStyle"));
	Style->SetContentRoot(IPluginManager::Get().FindPlugin("UnrealGaussianCapture")->GetBaseDir() / TEXT("Resources"));

	return Style;
}

void FGaussianCaptureEditorStyle::ReloadTextures()
{
	if (FSlateApplication::IsInitialized())
	{
		FSlateApplication::Get().GetRenderer()->ReloadTextureResources();
	}
}

const ISlateStyle& FGaussianCaptureEditorStyle::Get()
{
	return *StyleInstance;
}
