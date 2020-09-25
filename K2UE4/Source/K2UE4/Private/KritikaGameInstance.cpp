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

	Creds.reset();
	Channel.reset();

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
	PushThread = MakeShareable(new FPushListener(Channel));

	PushStub = new K2::PushSample::Stub(Channel);
	SimpleStub = new K2::SimpleSample::Stub(Channel);

	return true;
}

void UKritikaGameInstance::DumpStatus(const grpc::Status& Status)
{
	if (Status.ok()) return;

	UE_LOG(LogTemp, Error,
		TEXT("******************************\nFailed rpc to get state\nerror code = %d\nerror message = %s\n error detail = %s\n******************************"),
		Status.error_code(),
		UTF8_TO_TCHAR(Status.error_message().c_str()),
		UTF8_TO_TCHAR(Status.error_details().c_str()));
}

void UKritikaGameInstance::CommandBroadcast(const FString& Message)
{
	if (PushStub)
	{
		K2::BroadacastRequest Req;
		Req.set_message(TCHAR_TO_UTF8(*Message));
		grpc::ClientContext Context;
		K2::Null Empty;
		auto Status = PushStub->Broadacast(&Context, Req, &Empty);
		DumpStatus(Status);
	}
}

void UKritikaGameInstance::CommandHello()
{
	if (PushStub)
	{
		grpc::ClientContext Context;
		K2::Null Empty;
		auto Status = PushStub->Hello(&Context, Empty, &Empty);
		DumpStatus(Status);
	}
}

void UKritikaGameInstance::CommandMessage(const FString& Target, const FString& Message)
{
	if (PushStub)
	{
		K2::MessageRequest Req;
		Req.set_target(TCHAR_TO_UTF8(*Target));
		Req.set_message(TCHAR_TO_UTF8(*Message));

		grpc::ClientContext Context;
		K2::Null Empty;
		auto Status = PushStub->Message(&Context, Req, &Empty);
		DumpStatus(Status);
	}
}

void UKritikaGameInstance::CommandKick(const FString& Target)
{
	if (PushStub)
	{
		K2::KickRequest Req;
		Req.set_target(TCHAR_TO_UTF8(*Target));

		grpc::ClientContext Context;
		K2::Null Empty;
		auto Status = PushStub->Kick(&Context, Req, &Empty);
		DumpStatus(Status);
	}
}

void UKritikaGameInstance::CommandCommand(const FString& Param)
{
	if (SimpleStub)
	{
		K2::SampleCommandRequest Req;
		Req.set_param1(TCHAR_TO_UTF8(*Param));

		grpc::ClientContext Context;
		K2::SampleCommandResponse Rsp;
		auto Status = SimpleStub->SampleCommand(&Context, Req, &Rsp);
		UE_LOG(LogTemp, Warning, TEXT("result = %s, value = %d"), UTF8_TO_TCHAR(Rsp.result().c_str()), Rsp.value());
		DumpStatus(Status);
	}
}

void UKritikaGameInstance::CommandInfo(const FString& Filter)
{
	if (SimpleStub)
	{
		K2::SampleInfoRequest Req;
		Req.set_filter(TCHAR_TO_UTF8(*Filter));

		grpc::ClientContext Context;
		K2::SampleInfoResponse Rsp;
		auto Status = SimpleStub->SampleInfo(&Context, Req, &Rsp);

		FString Output;
		Output += FString::Printf(TEXT("info1 = %s, info2 = ["), UTF8_TO_TCHAR(Rsp.info1().c_str()));
		for (auto i = Rsp.info2().begin(); i != Rsp.info2().end(); ++i)
		{
			Output += FString::Printf(TEXT("%s, "), UTF8_TO_TCHAR(i->c_str()));
		}
		Output += FString::Printf(TEXT("], info3 = { type = %s, NestedValue = %s}"), UTF8_TO_TCHAR(Rsp.info3().SampleType_Name(Rsp.info3().type()).c_str()), UTF8_TO_TCHAR(Rsp.info3().nestedvalue().c_str()));
		UE_LOG(LogTemp, Warning, TEXT("%s"), *Output);
		DumpStatus(Status);
	}
}

FPushListener::FPushListener(const std::shared_ptr<grpc::Channel>& InAuthChannel)
	: AuthChannel(InAuthChannel)
	, bExitRequested(false)
{
	UE_LOG(LogTemp, Warning, TEXT("Consjasjlalkj"));

	Thread = FRunnableThread::Create(this, TEXT("PushListener"));
}

FPushListener::~FPushListener()
{
	if (Thread)
	{
		Thread->Kill();
		delete Thread;
		Thread = nullptr;
	}
}

void FPushListener::Stop()
{
	bExitRequested = true;
}

bool FPushListener::IsRunning() const
{
	return !bExitRequested;
}

uint32 FPushListener::Run()
{
	UE_LOG(LogTemp, Warning, TEXT("BEGIN OF PUSH service"));

	// Channels are thread safe! 그러니 안심하고 사용 가능~
	// https://stackoverflow.com/questions/33197669/are-channel-stubs-in-grpc-thread-safe
	K2::Push::Stub PushStub(AuthChannel);
	grpc::ClientContext Context;
	grpc::CompletionQueue cq;
	auto AsyncStream = PushStub.PrepareAsyncPushBegin(&Context, K2::Null(), &cq);
	K2::PushResponse Buffer;

	AsyncStream->StartCall(reinterpret_cast<void*>(1));
	
	while (!bExitRequested)
	{
		void* got_tag;
		bool ok = false;

		while (true)
		{
			const std::chrono::milliseconds Interval(1500);
			auto Deadline = std::chrono::system_clock::now() + Interval;

			const auto NextStatus = cq.AsyncNext(&got_tag, &ok, Deadline);
			if (NextStatus == grpc::CompletionQueue::SHUTDOWN || bExitRequested)
			{
				break;
			}
			else if (NextStatus == grpc::CompletionQueue::TIMEOUT)
				continue;

			if (!ok)
			{
				continue;
			}
			else
			{
				if (got_tag != nullptr)
				{
					AsyncStream->Read(&Buffer, reinterpret_cast<void*>(2));

					FString Message(UTF8_TO_TCHAR(Buffer.message().c_str()));
					if (Buffer.extra().empty() == false)
					{
						Message += FString::Printf(TEXT("(%s)"), UTF8_TO_TCHAR(Buffer.extra().c_str()));
					}
					UE_LOG(LogTemp, Warning, TEXT("{T_ID:%d}[PUSH received:%s] %s"), GetThreadId(), UTF8_TO_TCHAR(Buffer.PushType_Name(Buffer.type()).c_str()), *Message);

					if (Buffer.type() == K2::PushResponse_PushType_CONFIG && Buffer.message() == "jwt")
					{
						gRPCGlobalAuth.setJwt(Buffer.extra());
					}
				}
			}
		}
	}

	Context.TryCancel();

	grpc::Status Status;
	AsyncStream->Finish(&Status, reinterpret_cast<void*>(1));
	
	cq.Shutdown();

	UE_LOG(LogTemp, Warning, TEXT("END OF PUSH service"));

	return 0;
}