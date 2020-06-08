==== project history by alkee =====

# 시작

## gRPC 학습

solution 생성

ClientSample (.net core console) 추가

K2Svc (.net core grpc service) 추가

[.NET 클라이언트를 사용하여 gRPC 서비스 호출](https://docs.microsoft.com/ko-kr/aspnet/core/grpc/client?view=aspnetcore-3.1)

	ClientSample 에 Grpc.Net.Client nuget package 추가
	

	K2Svc 에 Grpc.Net.Client nuget package 추가 ; 서버도 back-end service 의 client 이므로


protobuf 파일(client-server)들은 공유되어야 하므로 solution 경로에 proto/sample.proto 파일을 먼저 만들고
solution item 으로 추가. (visual studio 에서 솔루션폴더를 추가하면 실제 물리적인 폴더가 생성되지는 않음)

채팅 같은 경우 response 가 발생 안할 수 도 있는데? 이럴땐 service, message 를 어떻게 작성하지?

grpc chat sample https://github.com/chgc/grpc-dotnetcore-3-chat

service ChatRoom {
  rpc join (stream Message) returns (stream Message) {}    
}

와 같이 정의.. 왜 join 에 stream message 입출력이지..

service Chat {
	rpc Message (stream MessageRequest) returns (stream MessageResponse);
}
와 같이 만들었는데... syntax highlight 가 먹지 않는다.


[자습서: ASP.NET Core에서 gRPC 클라이언트 및 서버 만들기](https://docs.microsoft.com/ko-kr/aspnet/core/tutorials/grpc/grpc-start?view=aspnetcore-3.1&tabs=visual-studio)


모든 프로젝트에 Google.Protobuf, Grpc.Tools 패키지 설치

예제와 같이 "프로젝트 파일 편집" 을 이용해 .csproj 수정

   .proto v3 에서는 required/optional 등의 키워드를 지원하지 않음.

예제에서의 `using GrpcGreeter` 에 해당하는 namespace(Login, Chat 등) 가 생성된 것 같지 않다. (intelisense 에 나타나지 않음)

버전 차이 때문인지, 예제에서는 namespace 를 사용하는 듯 나오지만, 실제로는 using 없이 해당 message class 를 사용할 수 있었다.

추후에 추가되는 .proto 파일들을 고려해 <Protobuf Include="..\proto\*.proto" GrpcService="Client" /> 와 같이 .csproj 변경

샘플을 변경한 proto 에 맞추어 수정. stream (chat) 의 경우 chat sample 참고해 작성


gRPC tool 에서 생성되는 class 및 method 들을 보면
   Service : ServiceBase class + method(virtual), Service class (클라이언트용) + Method, MethodAsync method
   Message class


nuget package 도 그렇고.. 외부망에서 개발하는게 좋을 것 같은데..

채팅을 위해 console 에 입출력을 동시에(stdin-stdout)할 수 있을만한 console 용 gui 들
 - https://github.com/migueldeicaza/gui.cs
 - https://github.com/TomaszRewak/C-sharp-console-gui-framework
 - https://github.com/goblinfactory/konsole.
 
 모두, 한글 입력에 문제가 있다. ui 기능을 위해 sample 의 코드가 복잡해지는 것 같기도 하니,
 입출력 동시 문제나 한글(readkey 사용시)입력문제는 무시하고, 기본 console 만으로 sample 을 작성하도록 해볼 것.
 

[인증](https://grpc.io/docs/guides/auth/) 기능을 추가해보자.
[Google credientials 을 사용하면 내장기능](https://grpc.io/docs/guides/auth/#supported-auth-mechanisms)으로
간편하게 사용할 수 있는 모양이지만, 우리 환경에서는 어찌될 지 모르기 때문에, [다른 방식](https://grpc.io/docs/guides/auth/#extending-grpc-to-support-other-authentication-mechanisms)
으로 진행해 볼 것.(Call credentials)

asp.net core 의 auth 이용 방법 : https://docs.microsoft.com/ko-kr/aspnet/core/grpc/authn-and-authz?view=aspnetcore-3.1

https://github.com/grpc/grpc-dotnet/tree/master/examples#ticketer
; [JWT](https://velopert.com/2389) 이용한 [Authorize] attribute 사용

JWT 사용하기 위해 Microsoft.AspNetCore.Authentication.JwtBearer 패키지 설치

client 연결이 종료되는 상황을 알 수 있는 방법이 있나?  serverCallContext.CancellationToken ? https://github.com/cactuaroid/GrpcWpfSample/blob/master/GrpcWpfSample.Server/Grpc/ChatServiceGrpcServer.cs
https://github.com/grpc/grpc/issues/18233


테스트를 위해 두개 프로젝트 모두를 실행해야하므로 하나의 시작프로젝트가 아닌 다중 시작프로젝트로 설정
https://docs.microsoft.com/ko-kr/visualstudio/ide/how-to-set-multiple-startup-projects?view=vs-2019


실행 후 서버에서 

crit: Microsoft.AspNetCore.Hosting.Diagnostics[6]
      Application startup exception
System.InvalidOperationException: Unable to resolve service for type 'Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider' while attempting to activate 'Microsoft.AspNetCore.Authentication.AuthenticationMiddleware'.
   at Microsoft.Extensions.Internal.ActivatorUtilities.ConstructorMatcher.CreateInstance(IServiceProvider provider)
   at Microsoft.Extensions.Internal.ActivatorUtilities.CreateInstance(IServiceProvider provider, Type instanceType, Object[] parameters)
   at Microsoft.AspNetCore.Builder.UseMiddlewareExtensions.<>c__DisplayClass4_0.<UseMiddleware>b__0(RequestDelegate next)
   at Microsoft.AspNetCore.Builder.ApplicationBuilder.Build()
   at Microsoft.AspNetCore.Hosting.GenericWebHostService.StartAsync(CancellationToken cancellationToken)

예외로 크래시. Unable to resolve service for type 'Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider' while attempting to activate 'Microsoft.AspNetCore.Authentication.AuthenticationMiddleware'.
문제인 듯. 임의로 지정한 claim type string 이 문제일 것 같아 [NameIdentifier](https://docs.microsoft.com/ko-kr/dotnet/api/system.security.claims.claimtypes.nameidentifier?view=netcore-3.1) 사용
하지만, 마찬가지. 이 문제가 아니라면..

일단 services.AddAuthorization 부분을 제거하니 
	System.InvalidOperationException: 'Unable to find the required services. Please add all the required services by calling 'IServiceCollection.AddAuthorization' inside the call to 'ConfigureServices(...)' in the application startup code.'
 문제가 발생..
 
 services.AddAuthentication 와 services.AddAuthentication 를 [Ticket sample](https://github.com/dotnet/AspNetCore.Docs/blob/master/aspnetcore/grpc/authn-and-authz/sample/Ticketer/Startup.cs)
 과 같이 설정해 소스코드 실행 성공.

[기본값으로 인증된 사용자만 사용](https://joonasw.net/view/apply-authz-by-default)가능하게 구성하고 예외적으로
[AllowAnonymousAttribute](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authorization.allowanonymousattribute?view=aspnetcore-3.1)이용해
최초 인증(login)받을 수 있도록 구성하고자 함. 하지만 원하는대로 동작하지 않음.
[Authorize] 를 붙여야만 method 의 인증검사. 일단, 기능구현 - sampling 에 먼저 집중하고 이부분은 추후에 다시 보기로.

auth 는 성공. chat 을 chat sample(https://github.com/chgc/grpc-dotnetcore-3-chat) 대로 해보았으나.. join 동작(RequestStream.WriteAsync) 없이 사용하려하니
System.InvalidOperationException: 'Can't write the message because the call is complete.' 발생
``` csharp
	var chatClient = new Message.MessageClient(channel);
		
	using var msg = chatClient.Message();
	_ = Task.Run(async () =>
		{
			while (await msg.ResponseStream.MoveNext(cancellationToken: CancellationToken.None))
			{
				var response = msg.ResponseStream.Current;
				Console.WriteLine($"{response.Id} : {response.Message}");
			}
		});

	string line;
	while ((line = Console.ReadLine()) != null)
	{
		if (line.ToLower() == "/quit") break;
		await msg.RequestStream.WriteAsync(new MessageRequest { Message = line });
	}

	await msg.RequestStream.CompleteAsync();
	await channel.ShutdownAsync();
```

https://github.com/grpc/grpc/issues/8718
push 방법은 현재로서는 없는 모양? 

protobuf 메시지를 두개로 분리
```protobuf
service Message {
//	rpc Message (stream MessageRequest) returns (stream MessageResponse);
	rpc Begin (google.protobuf.Empty) returns (stream MessageResponse); // 메시지를 받기 시작(stream)하기 위해서
	rpc Message (MessageRequest) returns (google.protobuf.Empty);
}
```
`google.protobuf.Empty` 사용을 위해서 `import "google/protobuf/empty.proto";` 필요. `syntax` 구문 앞에 있으면 오류.

[server streaming](https://www.stevejgordon.co.uk/server-streaming-with-grpc-in-asp-dotnet-core) 방식에서,
response stream 의 관리가 필요해보이고, 이는 asp.net 의 singleton management 가 필요해 보인다.(연결 관리)
 - https://stackoverflow.com/questions/45107411/
 - https://github.com/grpc/grpc/blob/5253c8f9a899450397a5e46e4923d01ac9a66a27/examples/csharp/route_guide/RouteGuideServer/RouteGuideImpl.cs#L106

ticketer sample 과 같은 코드인 것 같은데
```csharp
	var user = context.GetHttpContext().User;
	var name = user.Identity.Name;
```	
으로 name 을 얻어올 수 없다.(null)
JwtSecurityToken 에 claims parameter 를 빼먹었었네... 기본값 null 로 동작하는 바람에 문제 찾기가 어려웠음

```csharp
	[Authorize]
	public override async Task Begin(Empty request, IServerStreamWriter<MessageResponse> responseStream, ServerCallContext context)
	{
		var id = context.GetHttpContext().User.Identity.Name!;
		logger.LogInformation($"==== chat begin name : {id}");
		await conns.Add(id, responseStream);
	}
```
와같이 responseStream 을 저장해 사용하려 했으나, 사용시점에 이미 dispose 된 개채라며 exception.

이런 방식으로는 어려워보인다!!!!

simple 하게 client-server 는 Tcp(protobuf) 로 하고, server-server 만 gRPC 사용하는 것이 좋겠다.


[protobuf project 구성](https://dejanstojanovic.net/aspnet/2018/june/generate-c-models-from-protobuf-proto-files-directly-from-visual-studio/)
	; [protobuf-net](https://github.com/mgravell/protobuf-net) 을 이용하는 방식.
	
	
[gRPC 의 설정](https://github.com/grpc/grpc/blob/master/src/csharp/BUILD-INTEGRATION.md)을 이용하는 것이 간편.
 

----------

## C++ protobuf project

client 는 c++ project 여야 테스트가 가능할테니, c++ project 에서 protobuf 사용가능하도록 구성해볼것. 
gRPC 로는 연결관리에 어려움이 많으므로 단일 TCP 연결을 사용해 message 를 주고받도록 해보자.


[c++ tutorial](https://developers.google.com/protocol-buffers/docs/cpptutorial) 에 따라 구성

https://github.com/protocolbuffers/protobuf/releases/tag/v3.12.2 에서 파일 다운로드
 - protobuf-cpp-3.12.2.zip
 - protoc-3.12.2-win64.zip
 

빌드 전 이벤트에 `..\protoc\bin\protoc.exe -I="..\protoc\include" --cpp_out="$(OutDir)\proto" "..\proto\*.proto"` 설정했으나
`..\proto\sample.proto: File does not reside within any path specified using --proto_path (or -I).  You must specify a --proto_path which encompasses this file.  Note that the proto_path must be an exact prefix of the .proto file names -- protoc is too dumb to figure out when two paths (e.g. absolute and relative) are equivalent (it's harder than you think).`
오류. import 경로 문제인 듯..그런데 -I 에 include 경로가 아닌 source(.proto) 파일의 경로를 요구한다.

version 관리될 필요 없는 $(outdir) 을 출력경로로 하려 했으나, include 등의 위치 문제 때문에 고민..

일단, 가이드를 먼저 확인해보자.

https://github.com/protocolbuffers/protobuf/blob/master/src/README.md#c-installation---windows

설치에는 pwoershell 이용해야할 것.
 
[vcpkg](https://github.com/Microsoft/vcpkg#quick-start) 설치.

protobuf 설치. `vcpkg install protobuf protobuf:x64-windows`

protocolbuffer 의 다운로드 는 필요 없었던 듯.

project prebuild event 를 아래와 같이 변경.

```bat
IF NOT EXIST proto MKDIR proto
"$(VcpkgCurrentInstalledDir)tools\protobuf\protoc.exe" -I="$(SolutionDir)proto" -I="$(VcpkgCurrentInstalledDir)include" --cpp_out="proto" "$(SolutionDir)proto\*.proto"
```

생성되는 `/proto/*.pb.h`, `/proto/*.pb.cc` 파일을 빌드에 포함시켜야하는데.. 이를 자동화 하는 방법은 나중에 고민.

빌드시 warning 이 많아 https://stackoverflow.com/a/14995540 고민. 역시 시간문제로 skip

[protobuf-demo](https://github.com/lytsing/protobuf-demo) 너무 low-level socket 예제.
[Brynet](https://github.com/IronsDu/brynet) 이 vcpkg 도 지원하니, 사용해볼만.

`vkpkg install Brynet` 했는데도
`#include <brynet/net/wrapper/AsyncConnector.hpp>` 할 수 없었다. (E1696)

install 후에는 항상 ` .\vcpkg.exe integrate install` (개발환경 재구성) 필요.

컴파일 에러(C2039)발생 `D:\work\git\vcpkg\installed\x64-windows\include\brynet\base\Any.hpp(14,28): error C2039: 'any': 'std'의 멤버가 아닙니다.`
```cpp
#ifdef BRYNET_HAVE_LANG_CXX17
    using BrynetAny = std::any; // C2039
```

[asio](https://think-async.com/Asio/) 를 이용하도록 하는 것이 나을 듯. boost 이용.

```PS
> .\vcpkg.exe install boost:x86-windows
...
> .\vcpkg.exe install boost:x64-windows
...
>  .\vcpkg.exe integrate install`
```
boost 덩치가 워낙 커서, 대략 1시간 쯤 걸린다.. 

https://groups.google.com/forum/#!topic/protobuf/XEg_Na0ThgE
https://stackoverflow.com/questions/675349/deserialize-unknown-type-with-protobuf-net

network handling 하는데, 개발 시간이 너무 걸릴 것 같다.

----

## gRPC c++ client sampling

http://115.92.196.87/kritika2-developers/kritika2-source/issues/1#note_200

c++ gRPC 로 전환. push 용 stream channel 을 최초에 인증과 함께 유지하고
다른 request-response channel 을 이용해 통신하는 방식.

```PS
> .\vcpkg.exe install grpc:x86-windows
...
> .\vcpkg.exe install grpc:x64-windows
...
>  .\vcpkg.exe integrate install`
```

gRPC c++ example 중에 [route_guide](https://github.com/grpc/grpc/tree/master/examples/cpp/route_guide)에서 
[server 가 client 로 stream](https://github.com/grpc/grpc/blob/master/examples/protos/route_guide.proto#L34) 하는 method 가 있다.
이를 참고해 구현해보자.

새로 sample .proto 구성
```protobuf
syntax = "proto3";

package K2;

message Null {}

// Init ///////////////////////////////////////////////////////////////////////
service Init { // authorization 전에 사용할 수 있는 서비스
	rpc State (Null) returns (StateResponse);
	rpc Login (LoginRequest) returns (LoginResponse);
}

message LoginRequest {
	string id = 1;
	string pw = 2;
}

message LoginResponse {
	enum ResultType { // OK 가 아니더라도 jwt 가 공백이 아니면, 인증 된 사용자
		OK = 0;
		DUPLICATED = 1;
		MISMATCHED = 2;
	}
	ResultType result = 1;
	string jwt = 2; // security token ; 항상 header 에 포함되어야 하는 정보. 공백인 경우
	// more info here
}

message StateResponse {
	string version = 1; // service version
	string gateway = 2;
}
/////////////////////////////////////////////////////////////////////// Init //

// Push ///////////////////////////////////////////////////////////////////////
service Push {
	rpc PushBegin (Null) returns (stream PushResponse);
}

message PushResponse {
	enum PushType {
		ERROR = 0;
	    MESSAGE = 1;
		CONFIG = 2;    // 클라이언트의 설정/상태 변경 요구
		COMMAND = 3;   // 클라이언트의 특정 동작 실행 요구
	}
	PushType type = 1;
	string message = 2;
	string extra = 3;
}
/////////////////////////////////////////////////////////////////////// Push //


// Sample /////////////////////////////////////////////////////////////////////
service Sample {
	rpc Broadacast (BroadacastRequest) returns (Null);
}

message BroadacastRequest {
	string message = 1;
}
///////////////////////////////////////////////////////////////////// Sample //

//
// chat, message 등의 경우 별도의 service 를 만들지 않고 해당 기능이 필요한
//   서비스(친구, 길드, 운영 등)에서 각각 rpc 및 message 를 배치해 서비스가
//   추가되는 경우에도 독립적으로(이전의 message 를 변경하지 않고) 동작할 수
//   있도록 하는 것이 좋겠다.
//
```

서버의 stream(push service)은 https://www.stevejgordon.co.uk/server-streaming-with-grpc-in-asp-dotnet-core 참고

grpc c++ client 작성을 위해 https://github.com/grpc/grpc/blob/master/examples/cpp/route_guide/route_guide_client.cc 참고

빌드하니 compile 오류.
```
D:\work\git\vcpkg\installed\x64-windows\include\grpc\impl\codegen\port_platform.h(58,1): fatal error C1189: #error:      "Please compile grpc with _WIN32_WINNT of at least 0x600 (aka Windows Vista)"
```

windows.h 를 include 해주면 되긴 하지만 찝찝하다. 그리고 해주더라도 LNK2019 LNK2001 오류들.
```
1>grpc.lib(parse_address.cc.obj) : error LNK2019: __imp_htons"unsigned short __cdecl grpc_strhtons(char const *)" (?grpc_strhtons@@YAGPEBD@Z) 함수에서 참조되는 확인할 수 없는 외부 기호
1>grpc.lib(socket_utils_windows.cc.obj) : error LNK2001: 확인할 수 없는 외부 기호 __imp_htons
1>grpc.lib(grpc_ares_wrapper.cc.obj) : error LNK2001: 확인할 수 없는 외부 기호 __imp_htons
1>grpc.lib(iomgr_windows.cc.obj) : error LNK2019: __imp_WSAStartup"void __cdecl winsock_init(void)" (?winsock_init@@YAXXZ) 함수에서 참조되는 확인할 수 없는 외부 기호
1>grpc.lib(iomgr_windows.cc.obj) : error LNK2019: __imp_WSACleanup"void __cdecl winsock_shutdown(void)" (?winsock_shutdown@@YAXXZ) 함수에서 참조되는 확인할 수 없는 외부 기호
...
```

winsock 을 사용하기뒤해 패키지 추가 `.\vcpkg.exe install winsock2:x64-windows`

protoc 만 사용해 cpp 를 빌드하는 것이 아니라 추가적인 grpc plugin 이 필요.
  * https://stackoverflow.com/a/40015348
  * https://github.com/plasticbox/grpc-windows/blob/master/grpc_helloworld/test_protoc.bat
  
```batch
IF NOT EXIST proto MKDIR proto
"$(VcpkgCurrentInstalledDir)tools\protobuf\protoc.exe" -I="$(SolutionDir)proto" -I="$(VcpkgCurrentInstalledDir)include" --cpp_out="proto" --grpc_out="proto" "$(SolutionDir)proto\*.proto" --plugin=protoc-gen-grpc="$(VcpkgCurrentInstalledDir)tools\grpc\grpc_cpp_plugin.exe"
```

이를 통해 생성되는 파일은 `*.pb.h`, `*.pb.cc` 가 아닌 `*.grpc.pb.h` 및 `*.grpc.pb.cc` 파일이다.
하지만 grpc 는 protoc 로 만들어진 파일도 요구하고있기 때문에 하나의 proto 에 총 4개의 파일이 필요하다.

이번에는 
```
1>sample.grpc.pb.cc
1>D:\work\git\vcpkg\installed\x64-windows\include\grpc\impl\codegen\port_platform.h(58,1): fatal error C1189: #error:      "Please compile grpc with _WIN32_WINNT of at least 0x600 (aka Windows Vista)"
```

이 파일은 생성되는 파일이기때문에 #include 를 직접 포함하기 어려워보인다.
https://github.com/microsoft/vcpkg/issues/7281
아직 정식적으로 해결되지 않은 것 같다.. 

https://stackoverflow.com/a/58721506 와 같이

  * vcpkg\installed\__x64__-windows\include\grpc\impl\codegen\port_platform.h
  * vcpkg\installed\__x86__-windows\include\grpc\impl\codegen\port_platform.h

두 파일 수동으로 수정


```cpp
	K2::Null empty;
	string CHANNEL_URL("https://localhost:5001");

	grpc::ClientContext context;
	auto insecureChannel = grpc::CreateChannel(CHANNEL_URL, grpc::InsecureChannelCredentials());

	K2::Init::Stub initStub(insecureChannel);

	K2::StateResponse state;
	auto status = initStub.State(&context, empty, &state);
	if (!status.ok())
	{
		cout << "failed rpc to get state" << "\n"
			<< "error coe = " << status.error_code() << "\n"
			<< "error message = " << status.error_message() << "\n"
			<< "error detail = " << status.error_details() << endl;
		return 1;
	}
```
와 같이 작성해 실행했는데, 

```
failed rpc to get state
error coe = 14
error message = DNS resolution failed
error detail =
```

와 같은 오류.. channel 을 `https://127.0.0.1:5001` 로 변경해도 안된다. `127.0.0.1:5001` 로 하니,

```
failed rpc to get state
error coe = 2
error message = Stream removed
error detail =
```

ssl 문제 일 수 있을 것 같아 https://grpc.io/docs/guides/auth/#using-client-side-ssltls 적용

```
E0604 18:36:59.806000000 23132 ssl_utils.cc:497] load_file: {"created":"@1591263419.806000000","description":"Failed to load file","file":"D:\work\git\vcpkg\buildtrees\grpc\src\27a78c3f76-164b627345\src\core\lib\iomgr\load_file.cc","file_line":72,"filename":"/usr/share/grpc/roots.pem","referenced_errors":[{"created":"@1591263419.806000000","description":"No such file or directory","errno":2,"file":"D:\work\git\vcpkg\buildtrees\grpc\src\27a78c3f76-164b627345\src\core\lib\iomgr\load_file.cc","file_line":45,"os_error":"No such file or directory","syscall":"fopen"}]}
E0604 18:36:59.826000000 23132 ssl_security_connector.cc:411] Could not get default pem root certs.
E0604 18:36:59.829000000 23132 secure_channel_create.cc:132] Failed to create secure subchannel for secure name '127.0.0.1:5001'
E0604 18:36:59.834000000 23132 secure_channel_create.cc:50] Failed to create channel args during subchannel creation.
failed rpc to get state
error coe = 14
error message = Empty update
error detail =
```

c# 은 https://docs.microsoft.com/ko-kr/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.1#call-a-grpc-service-with-an-untrustedinvalid-certificate
이렇게 할 수 있는 것 같은데

c++ 에서는 안되는 듯 : https://stackoverflow.com/a/56371822 

c++ 에서는 SSL 적용하는데 애로사항이 많다. 

일단 encryption 사용하지 않고 진행.

```
//auto sslOptions = grpc::SslCredentialsOptions();
//sslOptions.pem_root_certs = "";
//sslOptions.pem_cert_chain = "";
//sslOptions.pem_private_key = "";
//auto channelCreds = grpc::SslCredentials(sslOptions);
//auto initChannel = grpc::CreateChannel(CHANNEL_URL, channelCreds);
```

grpc c++ steam example : https://github.com/perumaalgoog/grpc/blob/b52b3362d6805daf04a587d26d19e906559192fc/examples/cpp/helloworld/greeter_async_bidi_client.cc

CLientContext 를 재사용(stub.Message 함수 사용)하면 내부에서 debug assert 발생.
https://grpc.github.io/grpc/cpp/classgrpc__impl_1_1_client_context.html 의 warning 에 있는내용..
이러면 인증받고 AddMeta 를 항상 해줘야 하는데..

grpc::ClientContext::GlobalCallbacks 이용하면 자동으로 jwt 를 meta 에 입혀 줄 수 있을 것 같다

```cpp
class AuthCallback : public grpc::ClientContext::GlobalCallbacks
{
public:
	AuthCallback* setJwt(string jwt) {
		meta = "Bearer " + jwt;
		return this;
	}
	virtual void DefaultConstructor(grpc::ClientContext* context) {
		context->AddMetadata("Authorization", meta);
	}
	virtual void Destructor(grpc::ClientContext* context) {}
private:
	string meta;
};

```

Push thread 를 분리해 stub 을 넘겨 실행하니 mutex 관련 크래시.
thread 에서 channel 생성부터 모든 동작을 하도록 변경.
하지만.. grpc::internal::CallOpSet::ContinueFillOpsAfterInterception 에서 GRPC_CALL_ERROR_INVALID_METADATA 오류.
```
E0605 13:00:03.587000000 27000 call.cc:907] validate_metadata: {"created":"@1591329603.587000000","description":"Illegal header key","file":"D:\work\git\vcpkg\buildtrees\grpc\src\27a78c3f76-164b627345\src\core\lib\surface\validate_metadata.cc","file_line":44,"offset":0,"raw_bytes":"41 75 74 68 6f 72 69 7a 61 74 69 6f 6e 'Authorization'\u0000"}
E0605 13:00:03.600000000 27000 call_op_set.h:947] assertion failed: false
```


ClientContext 의 metadata 역시 변경하면 안되는 듯.
https://grpc.io/docs/guides/auth/#extending-grpc-to-support-other-authentication-mechanisms 방식으로보았지만,
연결은 되나, header 정보를 얻어오지 못한다. 


```cpp
class CustomAuthenticator : public grpc::MetadataCredentialsPlugin {
public:
	CustomAuthenticator(const string& jwt) {
		meta = "Bearer " + jwt;
	}
	grpc::Status GetMetadata(grpc::string_ref url, grpc::string_ref method, const grpc::AuthContext& channel, multimap<grpc::string, grpc::string>* metadata) override {
		metadata->insert(make_pair("Authorization", meta));
		return grpc::Status::OK;
	}
private:
	string meta;
};

void pushResponseThread(string channelUrl, string jwt) {

	..
	
	grpc::ClientContext context;
	auto callCreds = grpc::MetadataCredentialsFromPlugin(unique_ptr<grpc::MetadataCredentialsPlugin>(new CustomAuthenticator(jwt)));
	context.set_credentials(callCreds);
```

결국.. "Authorization" 이 아닌 "authroization" 을 key 로 하니 Metadata.Add 로 동작.... **대소문자 구별 주의**

동기 stream 함수들을 모두 blocking  (Read 등)
따라서, trhead 를 종료하기 위해서 강제종료밖에.. 아니면 비동기 방식으로 변경하고 sleep 사용 해야할 것.
protable 한 강제 thread 종료 방법이 없다. ; https://stackoverflow.com/questions/12207684

```cpp
::TerminateThread(pushThread->native_handle(), 1);
```




server-server(backend) 디자인

[![](https://mermaid.ink/img/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHBhcnRpY2lwYW50IEMgYXMgQ2xpZW50XG4gIHBhcnRpY2lwYW50IElTIGFzIEluaXQgc2VydmljZVxuICBwYXJ0aWNpcGFudCBQUyBhcyBQdXNoIHNlcnZpY2VcbiAgcGFydGljaXBhbnQgU1MgYXMgU2FtcGxlIHNlcnZpY2VcbiAgcGFydGljaXBhbnQgU0IgYXMgU2Vzc2lvbiBiYWNrZW5kXG5cbiAgcmVjdCByZ2IoMjU1LCAwLCAwLCAuNSlcbiAgICBub3RlIHJpZ2h0IG9mIEM6IG5vLWF1dGggbWVzc2FnZXNcbiAgICBDIC0tPiBJUyA6IFN0YXRlXG4gICAgQyAtLT4gSVMgOiBMb2dpblxuICBlbmRcblxuICBub3RlIG92ZXIgQzogdXNpbmcgand0IGhlYWRlclxuXG4gIEMgLT4-IFBTIDogUHVzaEJlZ2luXG4gIGFjdGl2YXRlIFBTXG4gIFBTIC0-PiBDIDogUHVzaFJlc3BvbnNlIDogY29ubmVjdGVkIHNlcnZlciBhZGRyZXNzXG5cbiAgbm90ZSBvdmVyIEM6IHVwZGF0ZSBqd3QgaGVhZGVyIDxici8-IHdpdGggc2VydmVyIGFkZHJlc3NcblxuICBDIC0tPj4gU1MgOiBCcm9hZGNhc3RcbiAgU1MgLS0-PiBQUyA6IEJyb2FkY2FzdCA8YnIvPiA6IGFsbCBzZXJ2ZXJzICBcbiAgUFMgLT4-IEMgOiBCcm9hZGNhc3RcblxuICBDIC0tPj4gU1MgOiBUYWxrXG4gIGFjdGl2YXRlIFNTXG4gIFNTIC0tPj4gU0IgOiBQdXNoXG4gIGFjdGl2YXRlIFNCXG4gICAgbm90ZSBvdmVyIFNCIDogZmluZCB0aGUgc2VydmVyXG4gICAgU0IgLS0-PiBTUyA6IHJlc3VsdFxuICBkZWFjdGl2YXRlIFNTXG4gICAgU0IgLS0-PiBQUyA6IFB1c2hcbiAgZGVhY3RpdmF0ZSBTQlxuXG4gIG5vdGUgb3ZlciBQUzogZmluZCB0aGUgdXNlclxuICBQUyAtPj4gQyA6IFRhbGtcblxuICBkZWFjdGl2YXRlIFBTXG4iLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCIsInNlcXVlbmNlIjp7Im1pcnJvckFjdG9ycyI6dHJ1ZX19LCJ1cGRhdGVFZGl0b3IiOmZhbHNlfQ)](https://mermaid-js.github.io/mermaid-live-editor/#/edit/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHBhcnRpY2lwYW50IEMgYXMgQ2xpZW50XG4gIHBhcnRpY2lwYW50IElTIGFzIEluaXQgc2VydmljZVxuICBwYXJ0aWNpcGFudCBQUyBhcyBQdXNoIHNlcnZpY2VcbiAgcGFydGljaXBhbnQgU1MgYXMgU2FtcGxlIHNlcnZpY2VcbiAgcGFydGljaXBhbnQgU0IgYXMgU2Vzc2lvbiBiYWNrZW5kXG5cbiAgcmVjdCByZ2IoMjU1LCAwLCAwLCAuNSlcbiAgICBub3RlIHJpZ2h0IG9mIEM6IG5vLWF1dGggbWVzc2FnZXNcbiAgICBDIC0tPiBJUyA6IFN0YXRlXG4gICAgQyAtLT4gSVMgOiBMb2dpblxuICBlbmRcblxuICBub3RlIG92ZXIgQzogdXNpbmcgand0IGhlYWRlclxuXG4gIEMgLT4-IFBTIDogUHVzaEJlZ2luXG4gIGFjdGl2YXRlIFBTXG4gIFBTIC0-PiBDIDogUHVzaFJlc3BvbnNlIDogY29ubmVjdGVkIHNlcnZlciBhZGRyZXNzXG5cbiAgbm90ZSBvdmVyIEM6IHVwZGF0ZSBqd3QgaGVhZGVyIDxici8-IHdpdGggc2VydmVyIGFkZHJlc3NcblxuICBDIC0tPj4gU1MgOiBCcm9hZGNhc3RcbiAgU1MgLS0-PiBQUyA6IEJyb2FkY2FzdCA8YnIvPiA6IGFsbCBzZXJ2ZXJzICBcbiAgUFMgLT4-IEMgOiBCcm9hZGNhc3RcblxuICBDIC0tPj4gU1MgOiBUYWxrXG4gIGFjdGl2YXRlIFNTXG4gIFNTIC0tPj4gU0IgOiBQdXNoXG4gIGFjdGl2YXRlIFNCXG4gICAgbm90ZSBvdmVyIFNCIDogZmluZCB0aGUgc2VydmVyXG4gICAgU0IgLS0-PiBTUyA6IHJlc3VsdFxuICBkZWFjdGl2YXRlIFNTXG4gICAgU0IgLS0-PiBQUyA6IFB1c2hcbiAgZGVhY3RpdmF0ZSBTQlxuXG4gIG5vdGUgb3ZlciBQUzogZmluZCB0aGUgdXNlclxuICBQUyAtPj4gQyA6IFRhbGtcblxuICBkZWFjdGl2YXRlIFBTXG4iLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCIsInNlcXVlbmNlIjp7Im1pcnJvckFjdG9ycyI6dHJ1ZX19LCJ1cGRhdGVFZGl0b3IiOmZhbHNlfQ)

project 파일 편집시에 

```xml
    <Protobuf Include=".\proto\*.proto" GrpcServices="Server" />
    <Protobuf Include=".\proto\*.proto" GrpcServices="Client" />
```

로 하니, Client stub 만 생성되어 [찾아보니](https://chromium.googlesource.com/external/github.com/grpc/grpc/+/HEAD/src/csharp/BUILD-INTEGRATION.md#explicitly-tell-protoc-for-which-files-it-should-use-the-grpc-plugin)

```xml
    <Protobuf Include=".\proto\*.proto" GrpcServices="Both" />
```

와 같이 해야 함.


Startup 에 parameter 추가(DI) ; https://stackoverflow.com/questions/44721426/pass-data-to-startup-cs

하지만 이 방식은 [2.x 까지만 가능](https://github.com/aspnet/Announcements/issues/353)


```
Error starting gRPC call. HttpRequestException: An error occurred while sending the request. IOException: The response ended prematurely.
```

https://github.com/grpc/grpc-dotnet/issues/505
	https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client

서비스 시작 전에 configuration 은 어떻게든 얻어오긴 하겠지만, StartUp 생성 전에(Main 함수?)
configuration 을 만들어내고 이를 StartUp 으로 전달하는 방식은 찾을 수 없었다.

또한, 현재는 frontend 나 backend 가 같은 listening 채널(localhost:5000)을 사용하고 있기 때문에
ssl 을 사용하는 경우 두가지 함께 적용되어야 하고 별도로 정책을 설정할 수 없는 문제.


frontend listener(다른 서버들로부터 호출되는 frontend 의 rpc)의 경우 diagram 에서는 PushService 에서 보내는 것으로
되어있지만 실제 이 listener 함수들의 존재는 backend 에 있다. 이는 diagram 이 instance 와 실제 동작하는 class 를 분리해 보여주기
어렵기 때문이기도 하고, 한 클래스에 .proto 의 frontend 와 .backend 의 service 를 중복해서 정의할 수 없기도 하기 떄문.






