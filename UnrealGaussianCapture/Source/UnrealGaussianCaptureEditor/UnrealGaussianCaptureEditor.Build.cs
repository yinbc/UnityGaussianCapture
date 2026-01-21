using UnrealBuildTool;

public class UnrealGaussianCaptureEditor : ModuleRules
{
	public UnrealGaussianCaptureEditor(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicIncludePaths.AddRange(
			new string[] {
			}
		);

		PrivateIncludePaths.AddRange(
			new string[] {
			}
		);

		PublicDependencyModuleNames.AddRange(
			new string[]
			{
				"Core",
				"CoreUObject",
				"Engine",
				"UnrealGaussianCapture"
			}
		);

		PrivateDependencyModuleNames.AddRange(
			new string[]
			{
				"Slate",
				"SlateCore",
				"UnrealEd",
				"EditorStyle",
				"EditorWidgets",
				"PropertyEditor",
				"LevelEditor",
				"InputCore",
				"ImageWrapper",
				"RenderCore",
				"RHI",
				"Json",
				"JsonUtilities"
			}
		);

		DynamicallyLoadedModuleNames.AddRange(
			new string[]
			{
			}
		);
	}
}
