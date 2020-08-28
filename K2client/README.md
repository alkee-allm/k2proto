## GRPC for C++ console client

C++ 에서 [gRPC](https://grpc.io)를 이용해 [서버](../K2svc/README.md)와 통신하는 동작을 하는 sample project

### C++ project(visual studio) 에서 gRPC 사용 구성 방법

 1. package(dependency) 추가 ; [Buld & run](../README.md#build-and-run) section 참고

 2. C++ project 의 빌드전 이벤트에서 `.proto` 파일로부터 message 및 stub 코드를 생성하도록 구성
```batch
	IF NOT EXIST proto MKDIR proto
	"$(VcpkgCurrentInstalledDir)tools\protobuf\protoc.exe" -I="$(SolutionDir)proto" -I="$(VcpkgCurrentInstalledDir)include" --cpp_out="proto" --grpc_out="proto" "$(SolutionDir)proto\*.proto" --plugin=protoc-gen-grpc="$(VcpkgCurrentInstalledDir)tools\grpc\grpc_cpp_plugin.exe"
```

 3. `.gitignore` 에 자동 생성되는 파일들(경로) 제외
```.gitignore
	# Generated code
	K2client/proto/
```

 4. 연결을 위한 channel 생성 및 해당 채널에 
```cpp
	//grpc::SslCredentialsOptions option;
	//option.pem_root_certs = read("localhost.pem"); // 서버의 인증서 필요(certmgr 또는 dotnet dev-cert 명령 이용해 추출)
	//auto creds = grpc::SslCredentials(option);
	auto creds = grpc::InsecureChannelCredentials();
	auto initChannel = grpc::CreateChannel(CHANNEL_URL, creds);
```

 5. 해당 전역적으로 작동(header 삽입 등)할 callback 설정
```cpp
	// class AuthCallback : public grpc::ClientContext::GlobalCallbacks
	AuthCallback auth(creds); // jwt header 를 자동으로 붙여주는 개체
```

 6. 사용할 서비스 stub 생성
```cpp
	K2::Init::Stub initStub(initChannel);
```

 7. rpc 호출 및 응답처리(동기 방식)
```cpp
	K2::StateResponse rsp;
	grpc::ClientContext context; // 재사용할 수 없음 주의
	auto status = initStub.State(&context, empty, &rsp);
	throwOnError(status);
```
