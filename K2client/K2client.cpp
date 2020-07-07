#include <iostream>
#include <fstream>
#include <sstream>
#include <memory>
#include <thread>

#include <grpc/grpc.h>
#include <grpcpp/grpcpp.h>

#include "proto/sample.grpc.pb.h"

using namespace std;


#include "K2helper.hpp"
void pushResponseThread(const string& channelUrl, AuthCallback& auth);
void CommandBroadcast(K2::PushSample::Stub& stub, const string& message)
{
	K2::BroadacastRequest req;
	req.set_message(message);
	grpc::ClientContext context;
	K2::Null empty;
	auto status = stub.Broadacast(&context, req, &empty);
	dumpStatus(status);
}
void CommandHello(K2::PushSample::Stub& stub)
{ //  jwt 에 pushBackend 를 넣고 이를 통해 push 테스트
	grpc::ClientContext context;
	K2::Null empty;
	auto status = stub.Hello(&context, empty, &empty);
	dumpStatus(status);
}
void CommandMessage(K2::PushSample::Stub& stub, const string& msgCommand)
{ // 지정한 target 의 session 을 찾아 push 테스트
	auto subcmd = parse(msgCommand);
	K2::MessageRequest req;
	req.set_target(subcmd.Header);
	req.set_message(subcmd.Body);

	grpc::ClientContext context;
	K2::Null empty;
	auto status = stub.Message(&context, req, &empty);
	dumpStatus(status);
}
void CommandKick(K2::PushSample::Stub& stub, const string& target)
{
	K2::KickRequest req;
	req.set_target(target);

	grpc::ClientContext context;
	K2::Null empty;
	auto status = stub.Kick(&context, req, &empty);
	dumpStatus(status);
}
void CommandCommand(K2::SimpleSample::Stub& stub, const string& param)
{
	K2::SampleCommandRequest req;
	req.set_param1(param);

	grpc::ClientContext context;
	K2::SampleCommandResponse rsp;
	auto status = stub.SampleCommand(&context, req, &rsp);
	cout << "result = " << rsp.result() << ", value = " << rsp.value() << endl;
	dumpStatus(status);
}
void CommandInfo(K2::SimpleSample::Stub& stub, const string& filter)
{
	K2::SampleInfoRequest req;
	req.set_filter(filter);

	grpc::ClientContext context;
	K2::SampleInfoResponse rsp;
	auto status = stub.SampleInfo(&context, req, &rsp);
	cout << "info1 = " << rsp.info1() << ", info2 = [";
	for (auto i = rsp.info2().begin(); i != rsp.info2().end(); ++i)
	{
		cout << *i << ", ";
	}
	cout << "], info3 = { type = " << rsp.info3().SampleType_Name(rsp.info3().type()) << ", nestedValue = "
		<< rsp.info3().nestedvalue() << "}\n";

	dumpStatus(status);
}

int main() {

	// id - password 준비
	string id;
	string pw;

	do {
		cout << "ID : ";
		getline(std::cin, id);

		cout << "PW : ";
		getline(std::cin, pw);
	} while (id.empty() || pw.empty());

	// 연결 준비
	unique_ptr<thread> pushThread;
	K2::Null empty;

	//grpc::SslCredentialsOptions option;
	//option.pem_root_certs = read("localhost.pem"); // 서버의 인증서 필요(certmgr 또는 dotnet dev-cert 명령 이용해 추출)
	//auto creds = grpc::SslCredentials(option);
	auto creds = grpc::InsecureChannelCredentials();

	string CHANNEL_URL("localhost:5000");
	auto initChannel = grpc::CreateChannel(CHANNEL_URL, creds);

	// grpc::ClientContext 는 재사용해 사용될 수 없음. https://github.com/grpc/grpc/issues/486
	AuthCallback auth(creds); // jwt header 를 자동으로 붙여주는 개체

	// INIT service
	K2::Init::Stub initStub(initChannel);

	{ // INIT - state
		K2::StateResponse rsp;
		grpc::ClientContext context;
		auto status = initStub.State(&context, empty, &rsp);
		throwOnError(status);

		cout << "service version  = " << rsp.version() << "\n"
			<< "service gateway = " << rsp.gateway() << endl;
		// TODO: gateway 를 이용해 channel 을 다시 설정
	}
	{ // INIT - login
		K2::LoginResponse rsp;
		K2::LoginRequest req;
		req.set_id(id);
		req.set_pw(pw);

		grpc::ClientContext context;
		auto status = initStub.Login(&context, req, &rsp);
		throwOnError(status);

		cout << "login result = " << K2::LoginResponse::ResultType_Name(rsp.result()) << endl;
		if (rsp.jwt().empty())
		{ // result 가 OK 가 아니라도 정책에 따라 인증 가능
			cout << "NO AUTH TOKEN received" << endl;
			return 1;
		}

		grpc::ClientContext::SetGlobalCallbacks(auth.setJwt(rsp.jwt()));
	}
	// begin PUSH service
	pushThread.reset(new thread(pushResponseThread, ref(CHANNEL_URL), ref(auth)));
	initChannel = nullptr;


	// Sample service test
	auto channel = grpc::CreateChannel(CHANNEL_URL, creds);
	K2::PushSample::Stub pushStub(channel);
	K2::SimpleSample::Stub simpleStub(channel);

	cout << "type 'quit' to terminate\n";
	string line;
	while (line != "quit")
	{
		getline(cin, line);
		auto cmd = parse(line);

		if (cmd.Header == "broadcast") CommandBroadcast(pushStub, cmd.Body);
		else if (cmd.Header == "hello") CommandHello(pushStub);
		else if (cmd.Header == "message") CommandMessage(pushStub, cmd.Body);
		else if (cmd.Header == "kick") CommandKick(pushStub, cmd.Body);
		else if (cmd.Header == "command") CommandCommand(simpleStub, cmd.Body);
		else if (cmd.Header == "info") CommandInfo(simpleStub, cmd.Body);
		else cout << "unknwon command\n";
	}

	::TerminateThread(pushThread->native_handle(), 1); // protable 한 강제 thread 종료 방법이 없다. ; https://stackoverflow.com/questions/12207684
	pushThread->join();
	return 0;
}

void pushResponseThread(const string& channelUrl, AuthCallback& auth) {

	auto channel = grpc::CreateChannel(channelUrl, auth.getCreds());
	K2::Push::Stub pushStub(channel);
	grpc::ClientContext context;
	auto stream = pushStub.PushBegin(&context, K2::Null());
	K2::PushResponse buffer;

	cout << "BEGIN OF PUSH service" << endl;
	while (stream->Read(&buffer)) { // Read 함수는 blocking 함수
		cout << "[PUSH received:" << buffer.PushType_Name(buffer.type()) << "] "
			<< buffer.message();
		if (buffer.extra().empty() == false) {
			cout << "(" << buffer.extra() << ")";
		}
		cout << endl;

		if (buffer.type() == K2::PushResponse_PushType_CONFIG && buffer.message() == "jwt")
		{
			auth.setJwt(buffer.extra());
		}
	}

	cout << "END OF PUSH service" << endl;
	stream->Finish();

	exit(1); // closed by server
}
