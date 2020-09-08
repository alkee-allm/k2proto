// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.IO;
using UnrealBuildTool;

public class K2UE4 : ModuleRules
{
	private string ModulePath
	{
		get { return ModuleDirectory; } 
	}
	
	private string ThirdPartyPath
	{ 
		get { return Path.GetFullPath(Path.Combine(ModulePath, "../../ThirdParty/")); } 
	}
	
	public K2UE4(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
	
		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "InputCore" });

		PrivateDependencyModuleNames.AddRange(new string[] {  });

		LoadThirdPartyLibrary("protobuf", Target);
		LoadThirdPartyLibrary("grpc", Target);
		PublicDefinitions.Add(string.Format("WITH_GRPC_BINDING=1"));
		
		// Uncomment if you are using Slate UI
		// PrivateDependencyModuleNames.AddRange(new string[] { "Slate", "SlateCore" });
		
		// Uncomment if you are using online features
		// PrivateDependencyModuleNames.Add("OnlineSubsystem");

		// To include OnlineSubsystemSteam, add it to the plugins section in your uproject file with the Enabled attribute set to true
	}
	
	public void LoadThirdPartyLibrary(string libraryName, ReadOnlyTargetRules Target, bool dynamic = false) {
		bool isLibrarySupported = false;
		if (Target.Platform == UnrealTargetPlatform.Win64) {
			isLibrarySupported = true;
			string PlatformString = "x64";
			string libraryDir = libraryName + "_" + PlatformString + "-windows";
			
			if (!dynamic) {
				string LibrariesPath = Path.Combine(ThirdPartyPath, libraryDir, "lib");
				DirectoryInfo d = new DirectoryInfo(LibrariesPath);
				FileInfo[] Files = d.GetFiles("*.lib");
                foreach (FileInfo file in Files)
                {
                    PublicAdditionalLibraries.Add(Path.Combine(LibrariesPath, file.Name));
				}
			}

			PublicIncludePaths.Add(Path.Combine(ThirdPartyPath, libraryDir, "include"));
		}
	}
}
