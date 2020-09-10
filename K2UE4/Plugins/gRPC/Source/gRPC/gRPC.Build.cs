// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.IO;
using UnrealBuildTool;

public class gRPC : ModuleRules
{
    private string ModulePath
    {
        get { return ModuleDirectory; }
    }

    private string ThirdPartyPath
    {
        get { return Path.GetFullPath(Path.Combine(ModulePath, "../ThirdParty/")); }
    }

    public gRPC(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;
		
		PublicIncludePaths.AddRange(
			new string[] {
				// ... add public include paths required here ...
			}
			);
				
		
		PrivateIncludePaths.AddRange(
			new string[] {
				// ... add other private include paths required here ...
			}
			);
			
		
		PublicDependencyModuleNames.AddRange(
			new string[]
			{
				"Core",
				"Projects"
				// ... add other public dependencies that you statically link with here ...
			}
			);
			
		
		PrivateDependencyModuleNames.AddRange(
			new string[]
			{
				// ... add private dependencies that you statically link with here ...	
			}
			);
		
		
		DynamicallyLoadedModuleNames.AddRange(
			new string[]
			{
				// ... add any modules that your module loads dynamically here ...
			}
			);

		LoadVCPKGThirdPartyLibrary("protobuf", Target, true);
		LoadVCPKGThirdPartyLibrary("grpc", Target);
        PublicDefinitions.Add(string.Format("WITH_GRPC_BINDING=1"));
    }

    public void LoadVCPKGThirdPartyLibrary(string libraryName, ReadOnlyTargetRules Target, bool dynamic = false)
    {
        if (Target.Platform == UnrealTargetPlatform.Win64)
        {
            string PlatformString = "x64";
            string libraryDir = libraryName + "_" + PlatformString + "-windows";

            string LibrariesPath = Path.Combine(ThirdPartyPath, libraryDir, "lib");
            DirectoryInfo d = new DirectoryInfo(LibrariesPath);
            FileInfo[] Files = d.GetFiles("*.lib");
            foreach (FileInfo file in Files)
            {
				// Add the import library
				PublicAdditionalLibraries.Add(Path.Combine(LibrariesPath, file.Name));
            }

			if (dynamic)
            {
                string DLLPaths = Path.Combine(ThirdPartyPath, libraryDir, "bin");
                DirectoryInfo DLLDirectoryInfo = new DirectoryInfo(DLLPaths);
                FileInfo[] DLLFiles = DLLDirectoryInfo.GetFiles("*.dll");
                foreach (FileInfo file in DLLFiles)
                {
					// Delay-load the DLL, so we can load it from the right place first
					PublicDelayLoadDLLs.Add(file.Name);

					// Ensure that the DLL is staged along with the executable
					RuntimeDependencies.Add(Path.Combine(PluginDirectory, "Binaries/ThirdParty/gRPC/Win64", file.Name));
				}
            }

            PublicIncludePaths.Add(Path.Combine(ThirdPartyPath, libraryDir, "include"));
        }
    }
}
