// Fill out your copyright notice in the Description page of Project Settings.


#include "KritikaGameInstance.h"

using namespace std;

class AuthCallback : public grpc::ClientContext::GlobalCallbacks
{
public:
	AuthCallback(shared_ptr<grpc::ChannelCredentials> creds)
		: creds(creds)
	{}

	AuthCallback* setJwt(const string& jwt) {

		cout << "setting jtw : " << jwt << endl;
		meta = "Bearer " + jwt;
		return this;
	}

	shared_ptr<grpc::ChannelCredentials> getCreds() const { return creds; }

	virtual void DefaultConstructor(grpc::ClientContext* context) {
		if (meta.empty() == false)
		{
			context->AddMetadata("authorization", meta);
		}
	}
	virtual void Destructor(grpc::ClientContext* context) {}

private:
	string meta;
	shared_ptr<grpc::ChannelCredentials> creds;
};

void UKritikaGameInstance::FinishDestroy()
{
	if (PushStub)
	{
		delete PushStub;
		PushStub = nullptr;
	}

	Super::FinishDestroy();
}

UKritikaGameInstance::UKritikaGameInstance(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
	, Channel(nullptr)
	, PushStub(nullptr)
{
}

bool UKritikaGameInstance::Login(const FString& id, const FString& pw)
{
	std::string CHANNEL_URL("localhost:9060");
	auto creds = grpc::InsecureChannelCredentials();
	auto initChannel = grpc::CreateChannel(CHANNEL_URL, creds);

	K2::Null empty;

	// grpc::ClientContext 는 재사용해 사용될 수 없음. https://github.com/grpc/grpc/issues/486
	AuthCallback auth(creds); // jwt header 를 자동으로 붙여주는 개체

	// INIT service
	K2::Init::Stub initStub(initChannel);

	{ // INIT - state
		K2::StateResponse rsp;
		grpc::ClientContext context;
		auto status = initStub.State(&context, empty, &rsp);

		cout << "service version  = " << rsp.version() << "\n"
			<< "service gateway = " << rsp.gateway() << endl;
	}
	{ // INIT - login
		K2::LoginResponse rsp;
		K2::LoginRequest req;
		req.set_id(TCHAR_TO_UTF8(*id));
		req.set_pw(TCHAR_TO_UTF8(*pw));

		grpc::ClientContext context;
		auto status = initStub.Login(&context, req, &rsp);

		if (rsp.result() != K2::LoginResponse_ResultType::LoginResponse_ResultType_OK)
		{
			return false;
		}

		FString Result(FString(UTF8_TO_TCHAR(K2::LoginResponse::ResultType_Name(rsp.result()).c_str())));
		UE_LOG(LogTemp, Log, TEXT("login result = %s"), *Result);
		
		if (rsp.jwt().empty())
		{ // result 가 OK 가 아니라도 정책에 따라 인증 가능
			UE_LOG(LogTemp, Error, TEXT("NO AUTH TOKEN received"));
			return false;
		}

		Channel = grpc::CreateChannel(CHANNEL_URL, creds);
		PushStub = new K2::PushSample::Stub(Channel);

		grpc::ClientContext::SetGlobalCallbacks(auth.setJwt(rsp.jwt()));
	}

	initChannel = nullptr;

	return true;
}

void UKritikaGameInstance::HelloCommand()
{
	if (PushStub)
	{
		grpc::ClientContext Context;
		K2::Null Empty;
		auto status = PushStub->Hello(&Context, Empty, &Empty);
		if (status.ok())
		{
			UE_LOG(LogTemp, Log, TEXT("Success to call Hello Command"));
		}
		else
		{
			UE_LOG(LogTemp, Log, TEXT("Fail to call Hello Command"));
		}
	}
}
