// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.IO;
using System.Linq;
using UnrealBuildTool;

public class gRPC : ModuleRules
{
	private string ModulePath
	{
		get { return ModuleDirectory; }
	}

    private string SourcePath
    {
        get { return Path.GetFullPath(Path.Combine(ModulePath, "../")); }
    }

    private string ThirdPartyPath
	{
		get { return Path.GetFullPath(Path.Combine(ModulePath, "../ThirdParty/")); }
	}

	private string GetUProjectPath
	{
		get { return Path.Combine(PluginDirectory, "../.."); }
	}

	public string GetVCPKGLibraryDirectoryName(string LibraryName)
    {
		if (Target.Platform == UnrealTargetPlatform.Win64)
		{
			string PlatformString = "x64";

			return (LibraryName + "_" + PlatformString + "-windows");
		}

		return "";
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

        // Generate gRPC code
        string ProtobufToolPath = Path.Combine(ThirdPartyPath, GetVCPKGLibraryDirectoryName("protobuf"), "tools/protobuf");
        string gRPCToolPath = Path.Combine(ThirdPartyPath, GetVCPKGLibraryDirectoryName("gRPC"), "tools/grpc");
        string ProtobufSourcePath = Path.Combine(GetUProjectPath, "../proto");
        string SaveTargetPath = Path.Combine(GetUProjectPath, "Source/K2UE4/proto");

        string Arguments = String.Format("-I {0} --cpp_out={1} --grpc_out={1} --plugin=protoc-gen-grpc={2} {0}/sample.proto", ProtobufSourcePath, SaveTargetPath, Path.Combine(gRPCToolPath, "grpc_cpp_plugin.exe"));
        System.Diagnostics.Process.Start(Path.Combine(ProtobufToolPath, "protoc.exe"), Arguments);

        // Merge template codes to gRPC generated code
        string[] BeginTemplateCode = File.ReadAllLines(Path.Combine(SourcePath, "BeginTemplate.txt"));
        string[] EndTemplateCode = File.ReadAllLines(Path.Combine(SourcePath, "EndTemplate.txt"));

        DirectoryInfo TargetDirectoryInfo = new DirectoryInfo(SaveTargetPath);
        FileInfo[] gRPCGeneratedCodeFiles = TargetDirectoryInfo.GetFiles("*.cc");
        foreach (FileInfo file in gRPCGeneratedCodeFiles)
        {
            string[] RawList = File.ReadAllLines(file.FullName);

			System.IO.StreamWriter OutputSW = new System.IO.StreamWriter(file.FullName, false);
			OutputSW.Write(BeginTemplateCode);
            OutputSW.Write(RawList);
            OutputSW.Write(EndTemplateCode);
            OutputSW.Close();
        }
		Console.WriteLine("gRPC complete");

        // Register Third-Party libraries
        PublicDefinitions.Add("GOOGLE_PROTOBUF_NO_RTTI");
        PublicDefinitions.Add("GPR_FORBID_UNREACHABLE_CODE");
        PublicDefinitions.Add("GRPC_ALLOW_EXCEPTIONS=0");
        LoadVCPKGThirdPartyLibrary("protobuf", Target, true);
		LoadVCPKGThirdPartyLibrary("grpc", Target);

		PublicDefinitions.Add("WITH_GRPC_BINDING=1");

        AddEngineThirdPartyPrivateStaticDependencies(Target, "OpenSSL");
        AddEngineThirdPartyPrivateStaticDependencies(Target, "zlib");
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
					RuntimeDependencies.Add("$(TargetOutputDir)/" + file.Name, Path.Combine(PluginDirectory, "Source/ThirdParty", libraryDir, "bin", file.Name));

					File.Copy(Path.Combine(PluginDirectory, "Source/ThirdParty", libraryDir, "bin", file.Name), Path.Combine(GetUProjectPath, "Binaries/Win64/", file.Name), true);
				}
            }

            PublicIncludePaths.Add(Path.Combine(ThirdPartyPath, libraryDir, "include"));
        }
    }
}
