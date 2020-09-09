// Copyright Epic Games, Inc. All Rights Reserved.

#include "gRPC.h"
#include "Core.h"
#include "Modules/ModuleManager.h"
#include "Interfaces/IPluginManager.h"

#define LOCTEXT_NAMESPACE "FgRPCModule"

void FgRPCModule::StartupModule()
{
	// This code will execute after your module is loaded into memory; the exact timing is specified in the .uplugin file per-module

	FString BaseDir = IPluginManager::Get().FindPlugin("gRPC")->GetBaseDir();

	FString LibraryPath;
#if PLATFORM_WINDOWS
	LibraryPath = FPaths::Combine(*BaseDir, TEXT("Binaries/ThirdParty/gRPC/Win64/libprotobuf.dll"));
#endif

	ExampleLibraryHandle = !LibraryPath.IsEmpty() ? FPlatformProcess::GetDllHandle(*LibraryPath) : nullptr;

	if (ExampleLibraryHandle)
	{
		FMessageDialog::Open(EAppMsgType::Ok, LOCTEXT("gRPC Library Success", "Success to load gRPC third party library"));
	}
	else
	{
		FMessageDialog::Open(EAppMsgType::Ok, LOCTEXT("gRPC Library Fail", "Fail to load gRPC third party library"));
	}
}

void FgRPCModule::ShutdownModule()
{
	// This function may be called during shutdown to clean up your module.  For modules that support dynamic reloading,
	// we call this function before unloading the module.

	// Free the dll handle
	FPlatformProcess::FreeDllHandle(ExampleLibraryHandle);
	ExampleLibraryHandle = nullptr;
}

#undef LOCTEXT_NAMESPACE
	
IMPLEMENT_MODULE(FgRPCModule, gRPC)