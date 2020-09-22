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

	// 특별하지 않은 경우, Creds와 Channel은 한번만 call 하면 될 듯
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

	// Editor 는 Stop 버튼을 눌러 FinishDestroy()가 호출되어도 프로세스가 꺼진상태가 아니기에,
	// client_context.cc 의 g_client_callbacks 가 기본으로 초기화 되어있지 않음.
	// 에디터에서 테스트의 용의를 위해 FinishDestroy시 g_client_callbacks 를 default 로 재설정필요.
	// * 아니 근데 g_default_client_callbacks 의 선언이 .cc 에 있잖아~
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
		{ // result 가 OK 가 아니라도 정책에 따라 인증 가능
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
	// Channels are thread safe! 그러니 안심하고 사용 가능~
	// https://stackoverflow.com/questions/33197669/are-channel-stubs-in-grpc-thread-safe
	K2::Push::Stub PushStub(AuthedChannel);
	grpc::ClientContext context;
	auto Stream = PushStub.PushBegin(&context, K2::Null());
	K2::PushResponse Buffer;

	while (Stream->Read(&Buffer)) // Read 함수는 blocking 함수
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
		// Stream->Read가 blocking 함수여서 스래드가 원하는 시점에 종료가 바로 안됨.
		// 좋은 방법은 아니지만.. 강제로 thread를 죽이자!
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