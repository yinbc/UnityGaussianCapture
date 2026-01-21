using UnrealBuildTool;

public class UnrealGaussianCapture : ModuleRules
{
	public UnrealGaussianCapture(ReadOnlyTargetRules Target) : base(Target)
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
				"RenderCore",
				"RHI"
			}
		);

		PrivateDependencyModuleNames.AddRange(
			new string[]
			{
				"ImageWrapper",
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
