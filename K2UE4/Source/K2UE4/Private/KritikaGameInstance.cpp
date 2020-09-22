// Fill out your copyright notice in the Description page of Project Settings.


#include "KritikaGameInstance.h"

using namespace std;

AuthCallback gRPCGlobalAuth;

UKritikaGameInstance::UKritikaGameInstance(const FObjectInitializer& ObjectInitializer)
	: Super(ObjectInitializer)
	, Channel(nullptr)
	, PushStub(nullptr)
{
}

void UKritikaGameInstance::Init()
{
	const std::string CHANNEL_URL("localhost:9060");

	// Ư������ ���� ���, Creds�� Channel�� �ѹ��� call �ϸ� �� ��
	if (!Creds)
	{
		Creds = grpc::InsecureChannelCredentials();
	}

	if (!Channel)
	{
		Channel = grpc::CreateChannel(CHANNEL_URL, Creds);
	}

	static bool AlreadySetted = false;

	if (!AlreadySetted)
	{
		grpc::ClientContext::SetGlobalCallbacks(&gRPCGlobalAuth);
		AlreadySetted = true;
	}
}

void UKritikaGameInstance::FinishDestroy()
{
	if (PushStub)
	{
		delete PushStub;
		PushStub = nullptr;
	}
	if (SimpleStub)
	{
		delete SimpleStub;
		SimpleStub = nullptr;
	}

	Creds = nullptr;
	Channel = nullptr;

	FPushResponseThread::Shutdown();

	// Editor �� Stop ��ư�� ���� FinishDestroy()�� ȣ��Ǿ ���μ����� �������°� �ƴϱ⿡,
	// client_context.cc �� g_client_callbacks �� �⺻���� �ʱ�ȭ �Ǿ����� ����.
	// �����Ϳ��� �׽�Ʈ�� ���Ǹ� ���� FinishDestroy�� g_client_callbacks �� default �� �缳���ʿ�.
	// * �ƴ� �ٵ� g_default_client_callbacks �� ������ .cc �� ���ݾ�~
	// grpc::ClientContext::SetGlobalCallbacks(grpc::ClientContext::)

	Super::FinishDestroy();
}

bool UKritikaGameInstance::Login(const FString& id, const FString& pw)
{	
	K2::Null empty;

	// INIT service
	K2::Init::Stub initStub(Channel);

	{ // INIT - state
		K2::StateResponse rsp;
		grpc::ClientContext context;
		auto status = initStub.State(&context, empty, &rsp);

		UE_LOG(LogTemp, Warning, TEXT("service version = %s"), UTF8_TO_TCHAR(rsp.version().c_str()));
		UE_LOG(LogTemp, Warning, TEXT("service gateway = %s"), UTF8_TO_TCHAR(rsp.gateway().c_str()));
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

		FString Result(UTF8_TO_TCHAR(K2::LoginResponse::ResultType_Name(rsp.result()).c_str()));
		UE_LOG(LogTemp, Log, TEXT("login result = %s"), *Result);
		
		if (rsp.jwt().empty())
		{ // result �� OK �� �ƴ϶� ��å�� ���� ���� ����
			UE_LOG(LogTemp, Error, TEXT("NO AUTH TOKEN received"));
			return false;
		}

		gRPCGlobalAuth.setJwt(rsp.jwt());
	}
	FPushResponseThread::ThreadInit(Channel);

	PushStub = new K2::PushSample::Stub(Channel);
	SimpleStub = new K2::SimpleSample::Stub(Channel);

	return true;
}

void UKritikaGameInstance::CommandHello()
{
	if (PushStub)
	{
		grpc::ClientContext Context;
		K2::Null Empty;
		auto Status = PushStub->Hello(&Context, Empty, &Empty);
		if (Status.ok())
		{
			UE_LOG(LogTemp, Log, TEXT("Success to call Hello Command"));
		}
		else
		{
			UE_LOG(LogTemp, Log, TEXT("Fail to call Hello Command"));
		}
	}
}

/////////////////////////////////////////////////////////
// FPushResponseThread Implementation
/////////////////////////////////////////////////////////

FPushResponseThread* FPushResponseThread::Runnable = nullptr;

FPushResponseThread::FPushResponseThread(const std::shared_ptr<grpc::Channel>& InAuthedChannel)
	: AuthedChannel(InAuthedChannel)
{
	Thread = FRunnableThread::Create(this, TEXT("PushResponseThread"), 0);
}

FPushResponseThread::~FPushResponseThread()
{
	delete Thread;
	Thread = nullptr;
}

bool FPushResponseThread::Init()
{
	UE_LOG(LogTemp, Warning, TEXT("BEGIN OF PUSH service"));

	return true;
}

uint32 FPushResponseThread::Run()
{
	// Channels are thread safe! �׷��� �Ƚ��ϰ� ��� ����~
	// https://stackoverflow.com/questions/33197669/are-channel-stubs-in-grpc-thread-safe
	K2::Push::Stub PushStub(AuthedChannel);
	grpc::ClientContext context;
	auto Stream = PushStub.PushBegin(&context, K2::Null());
	K2::PushResponse Buffer;

	while (Stream->Read(&Buffer)) // Read �Լ��� blocking �Լ�
	{
		FString Message(UTF8_TO_TCHAR(Buffer.message().c_str()));
		if (Buffer.extra().empty() == false)
		{
			Message += FString::Printf(TEXT("(%s)"), UTF8_TO_TCHAR(Buffer.extra().c_str()));
		}
		UE_LOG(LogTemp, Warning, TEXT("[PUSH received:%s] %s"), UTF8_TO_TCHAR(Buffer.PushType_Name(Buffer.type()).c_str()), *Message);

		if (Buffer.type() == K2::PushResponse_PushType_CONFIG && Buffer.message() == "jwt")
		{
			gRPCGlobalAuth.setJwt(Buffer.extra());
		}
	}

	Stream->Finish();

	return 0;
}

void FPushResponseThread::Stop()
{
	UE_LOG(LogTemp, Warning, TEXT("END OF PUSH service"));
}

FPushResponseThread* FPushResponseThread::ThreadInit(const std::shared_ptr<grpc::Channel>& InAuthedChannel)
{
	if (!Runnable && FPlatformProcess::SupportsMultithreading())
	{
		Runnable = new FPushResponseThread(InAuthedChannel);
	}
	// TODO : Unless supported multi-threading...?
	return Runnable;
}

void FPushResponseThread::Shutdown()
{
	if (Runnable)
	{
		// Stream->Read�� blocking �Լ����� �����尡 ���ϴ� ������ ���ᰡ �ٷ� �ȵ�.
		// ���� ����� �ƴ�����.. ������ thread�� ������!
		// https://docs.unrealengine.com/en-US/API/Runtime/Core/HAL/FRunnableThread/Kill/index.html
		//Runnable->Stop();
		//Runnable->Thread->WaitForCompletion();
		Runnable->Thread->Kill(false);

		delete Runnable;
		Runnable = nullptr;
	}
}

bool FPushResponseThread::IsThreadFinished()
{
	if (Runnable) return Runnable->IsThreadFinished();
	return true;
}