#pragma once

string StringFrom(const string& filePath) {
	ifstream fs(filePath);
	stringstream buffer;
	buffer << fs.rdbuf();
	fs.close();
	return buffer.str();
}

std::string dumpStatus(const grpc::Status& status)
{
	if (status.ok()) return "";

	stringstream ss;
	ss << "failed rpc to get state" << "\n"
		<< "error coe = " << status.error_code() << "\n"
		<< "error message = " << status.error_message() << "\n"
		<< "error detail = " << status.error_details() << endl;
	auto msg = ss.str();
	cout << msg;
	return msg;
}

void throwOnError(const grpc::Status& status)
{
	if (!status.ok()) {
		throw dumpStatus(status);
	}
}

struct Command
{
	string Header; // lower case
	string Body;
};

Command parse(string line)
{
	Command cmd;
	auto pos = line.find_first_of(' ', 0);
	if (pos == string::npos) {
		cmd.Header = line;
		return cmd;
	}
	cmd.Header = line.substr(0, pos);
	transform(cmd.Header.begin(), cmd.Header.end(), cmd.Header.begin(), [](unsigned char c) { return tolower(c); });
	cmd.Body = line.substr(pos + 1);
	return cmd;
}

//class CustomAuthenticator : public grpc::MetadataCredentialsPlugin {
//public:
//	CustomAuthenticator(const string& jwt) {
//		meta = "Bearer " + jwt;
//	}
//	grpc::Status GetMetadata(grpc::string_ref url, grpc::string_ref method, const grpc::AuthContext& channel, multimap<grpc::string, grpc::string>* metadata) override {
//		metadata->insert(make_pair("Authorization", meta));
//		return grpc::Status::OK;
//	}
//private:
//	string meta;
//};

class AuthCallback : public grpc::ClientContext::GlobalCallbacks
{
public:
	AuthCallback* setJwt(const string& jwt) {
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
	string meta;
};


//class ProtobufHandle
//{
//public:
//	// Verify that the version of the library that we linked against is
//	// compatible with the version of the headers we compiled against.
//	ProtobufHandle() { GOOGLE_PROTOBUF_VERIFY_VERSION; }
//
//	// Delete all global objects allocated by libprotobuf when this program is terminated.
//	~ProtobufHandle() { google::protobuf::ShutdownProtobufLibrary(); }
//};
