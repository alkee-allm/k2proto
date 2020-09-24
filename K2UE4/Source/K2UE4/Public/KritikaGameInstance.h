// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/GameInstance.h"
#include "Templates/UniquePtr.h"
#include "HAL/Thread.h"

#pragma warning (push)
#pragma warning(disable : 4582)
#pragma warning(disable : 4583)
#include <grpc/grpc.h>
#include <grpcpp/grpcpp.h>
#include "sample.grpc.pb.h"
#pragma warning( pop )

#include "KritikaGameInstance.generated.h"

class FPushListener;

class AuthCallback : public grpc::ClientContext::GlobalCallbacks
{
public:
	AuthCallback()
	{}

	AuthCallback* setJwt(const std::string& jwt)
	{
		UE_LOG(LogTemp, Warning, TEXT("setting jtw : %s"), *FString(UTF8_TO_TCHAR(jwt.c_str())));
		meta = "Bearer " + jwt;
		return this;
	}

	virtual void DefaultConstructor(grpc::ClientContext* context) {
		if (meta.empty() == false)
		{
			context->AddMetadata("authorization", meta);
		}
	}
	virtual void Destructor(grpc::ClientContext* context) {}

private:
	std::string meta;
	std::shared_ptr<grpc::ChannelCredentials> creds;
};
extern AuthCallback gRPCGlobalAuth;

/**
 * 
 */
UCLASS()
class K2UE4_API UKritikaGameInstance : public UGameInstance
{
	GENERATED_UCLASS_BODY()
	
public:
	virtual void Init() override;
	virtual void FinishDestroy() override;

	UFUNCTION(BlueprintCallable)
	bool Login(const FString& id, const FString& pw);

	UFUNCTION(BlueprintCallable)
	void CommandHello();

private:
	std::shared_ptr<grpc::ChannelCredentials> Creds;
	std::shared_ptr<grpc::Channel> Channel;

	TSharedPtr<FPushListener> PushThread;

	K2::PushSample::Stub* PushStub;
	K2::SimpleSample::Stub* SimpleStub;
};

class FPushListener : public FRunnable
{
public:
	FPushListener(const std::shared_ptr<grpc::Channel>& InAuthChannel);
	~FPushListener();

	virtual void Stop() override;

	bool IsRunning() const;

	uint32 GetThreadId() const
	{
		return Thread ? Thread->GetThreadID() : FThread::InvalidThreadId;
	}

protected:
	virtual uint32 Run() override;

private:
	FRunnableThread* Thread;
	FThreadSafeBool	bExitRequested;

	std::shared_ptr<grpc::Channel> AuthChannel;
};