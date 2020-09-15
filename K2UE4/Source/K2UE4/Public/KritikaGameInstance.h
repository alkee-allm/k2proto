// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "Engine/GameInstance.h"

#pragma warning (push)
#pragma warning(disable : 4582)
#pragma warning(disable : 4583)
#include <grpc/grpc.h>
#include <grpcpp/grpcpp.h>
#include "sample.grpc.pb.h"
#pragma warning( pop )

#include "KritikaGameInstance.generated.h"

/**
 * 
 */
UCLASS()
class K2UE4_API UKritikaGameInstance : public UGameInstance
{
	GENERATED_UCLASS_BODY()
	
public:
	virtual void FinishDestroy() override;

	UFUNCTION(BlueprintCallable)
	bool Login(const FString& id, const FString& pw);

	UFUNCTION(BlueprintCallable)
	void HelloCommand();

private:
	std::shared_ptr<grpc_impl::Channel> Channel;
	K2::PushSample::Stub* PushStub;
};
