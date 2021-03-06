## PROJECT k2svc 

  K2 서비스를 위한 서버들(out-game)의 구성 및 작동방식을 단순화해 구현한 prototype
	

### TERMS

| 용어 | 설명 |
| ---- | ---- |
| backend | 서버끼리의 통신을 위한 연결점 |
| backend manager | server-server communication 에서의 server 역할(backend service 내에 존재) |
| backend host | server-server communication 에서의 client 역할(frontend service 내에 존재) |
| forntend | 서버와 외부(클라이언트) 통신을 위한 연결점 |
| push | 서버에서 클라이언트로 전달하는 이벤트 |
| server management service / 관리서버 | 서버들을 관리하는 중앙집중형 backend 서비스 |
| session service / 세션서버 | push channel 을 통해 연결되어있는 유저들을 관리하는 중앙집중형 backend 서비스 |
| game session | 실제 멀티 게임 플레이가 이루어지는 하나의 방 / 스테이지 |
| in-game | [unreal dedicated server](https://docs.unrealengine.com/ko/Gameplay/Networking/HowTo/DedicatedServers/index.html) 에 의해 동작하는 직접적인 게임 플레이(game session 내) 관련 요소 |
| out-game | game session 내 게임 플레이 이외의 요소. 로그인, 로비, 채팅, 상점 등등 |
| local config([K2Config](https://github.com/alkee-allm/k2proto/blob/393ccf640b89bc748dfd77ddee2d01d45cc145df/K2svc/ServiceConfiguration.cs#L6)) | environment variables, commandline argument, [appsettings](https://github.com/alkee-allm/k2proto/blob/feature/61-doc-update/K2svc/appsettings.json) 등을 통해 process 가 실행 될 때 얻게되는 설정 |
| remote config([RemoteConfig](https://github.com/alkee-allm/k2proto/blob/393ccf640b89bc748dfd77ddee2d01d45cc145df/K2svc/ServiceConfiguration.cs#L49)) | [서비스가 시작될 때 SeverManager 를 통해 Register](https://github.com/alkee-allm/k2proto/tree/feature/61-doc-update#server-management-service-server-manager) 과정에서 얻게되는 - ServerManager 가 주입하는 - 설정 |


### CONCEPT

  frontend service 의 경우 scale-out 및 병렬처리가 용이하도록 stateless protocol 을 사용. 또한, 여러 서비스에서 검증된 asp.net core 가 적합할 것. 이에 맞추어 [grpc-dotnet](https://github.com/grpc/grpc-dotnet)을 이용해 HTTP protocol 과 [protobuf](https://developers.google.com/protocol-buffers)를 사용.
  
  in-game 은 [unreal dedicated server](https://docs.unrealengine.com/ko/Gameplay/Networking/HowTo/DedicatedServers/index.html)를 벗어날 수 없기 때문에, 이 프로젝트에서 직접적으로 다루지 않음.

  stateless 서비스가 기본이기 때문에 server 에서 client 로 이벤트를 전달하기 위해서 **항상 연결되어있는 stream channel 을 유지하고 이를 이용해 이벤트를 push**하는 방식을 택함.

  AWS ECS(docker container), scale-out, load balancing, failover 등의 사용성을 고려함

### DESIGN

  * 서비스가 시작될 때 필요한 설정(local configuration) 얻기; 다양한 배포 및 실행환경 고려
  * 서비스가 관리서버에의해 등록되면 동적으로 변할 수 있는 설정(Remote configuration)들을 업데이트
  * 주된 통신은 항상 sessionless 연결(HTTP)을 통해 커뮤니케이션 ; 손쉬운 scale out 및 load balancing 을 위함
  * client 는 [init service](./Frontend/InitService.cs)를 통해 인증을 시작. 이후 모든 요청은 [JWT](https://jwt.io/introduction/) header 를 통해 인증유지
  * client 는 항상 하나의 push service 에 TCP 연결된 상태로 유지함으로써 session 을 유지. ; [Push service](./Frontend/PushService.cs)
  * client 가 일반적인 요청(request)하는 경우 stateless 요청(http)을 사용해 어느 서버(frontend)에서든지 서비스 가능
  * service 가 시작될 때 관리서버(server management service)에 등록되어 시작되고 지속적인 ping(by [BackgroundService](./Background/ServerManagementBackground.cs))을 통해 서비스가 동작중인지 확인
  * service 들 끼리는 공통된 [group id 를 request header 에 삽입](https://github.com/alkee-allm/k2proto/issues/15#issuecomment-679490397)해 내부 통신(backend communication)을 인증
  * server 가 다른 server 로 요청이 필요할 때 backend service 를 이용


### BACKEND service

  backend service 의 경우 server - client 관계의 연결점이 각각 존재하는데 이를 `Manager` 와 `Host` 로 명명. 전체적인 매커니즘 구조는 아래와 같다.

[![](https://mermaid.ink/img/eyJjb2RlIjoiZ3JhcGggQlRcblxuICAgIGNsaWVudFtcImNsaWVudFwiXVxuICAgIHN0eWxlIGNsaWVudCBmaWxsOiNiYmYsc3Ryb2tlOiNmNjYsc3Ryb2tlLXdpZHRoOjJweCxjb2xvcjojZmZmLHN0cm9rZS1kYXNoYXJyYXk6IDUgNVxuXG4gICAgc3ViZ3JhcGggZnJvbnRlbmRbXCJmcm9udGVuZCBzZXJ2aWNlXCJdXG4gICAgICAgIGZzdmNbXCJmcm9udGVuZDxicj5zZXJ2aWNlXCJdXG4gICAgICAgIHBzdmNbXCJwdXNoPGJyPnNlcnZpY2U8YnI+KEZyb250ZW5kLlB1c2hTZXJ2aWNlLlB1c2hlcilcIl1cbiAgICAgICAgc3R5bGUgcHN2YyBmaWxsOnllbGxvd1xuICAgICAgICBob3N0W1wiYmFja2VuZDxicj5IT1NUXCJdXG4gICAgICAgIHN0eWxlIGhvc3QgZmlsbDojZjlmLHN0cm9rZTojMzMzLHN0cm9rZS13aWR0aDo0cHhcbiAgICBlbmRcblxuICAgIHN1YmdyYXBoIGJhY2tlbmRcbiAgICAgICAgbWFuYWdlcltcImJhY2tlbmQ8YnI+TUFOQUdFUlwiXVxuICAgICAgICBzdHlsZSBtYW5hZ2VyIGZpbGw6I2Y5ZixzdHJva2U6IzMzMyxzdHJva2Utd2lkdGg6NHB4XG4gICAgZW5kXG5cbiAgICBjbGllbnQgLS4tPnxcImFueSBpbnN0YW5jZVwifCBmc3ZjXG4gICAgZnN2YyAtLi0+IG1hbmFnZXJcbiAgICBtYW5hZ2VyIC0uLT58XCJ0YXJnZXQgcHVzaCBiYWNrZW5kXCJ8IGhvc3RcbiAgICBob3N0IC0tPiBwc3ZjXG4gICAgcHN2YyAtLi0+fFwiY29ubmVjdGVkIGNsaWVudFwifCBjbGllbnRcblxuICAgIHN1YmdyYXBoIElOREVYXG4gICAgICAgIGNbXCJjb25uZWN0b3JcIl0gLS4tPiB8UlBDfCBzW1wibGlzdGVuZXJcIl1cbiAgICAgICAgbTFbXCJjYWxsZXJcIl0gLS0+IHxmdW5jdGlvbnwgbTJbXCJjYWxsZWVcIl1cbiAgICBlbmRcbiAgICAiLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCJ9fQ)](https://mermaid-js.github.io/mermaid-live-editor/#/edit/eyJjb2RlIjoiZ3JhcGggQlRcblxuICAgIGNsaWVudFtcImNsaWVudFwiXVxuICAgIHN0eWxlIGNsaWVudCBmaWxsOiNiYmYsc3Ryb2tlOiNmNjYsc3Ryb2tlLXdpZHRoOjJweCxjb2xvcjojZmZmLHN0cm9rZS1kYXNoYXJyYXk6IDUgNVxuXG4gICAgc3ViZ3JhcGggZnJvbnRlbmRbXCJmcm9udGVuZCBzZXJ2aWNlXCJdXG4gICAgICAgIGZzdmNbXCJmcm9udGVuZDxicj5zZXJ2aWNlXCJdXG4gICAgICAgIHBzdmNbXCJwdXNoPGJyPnNlcnZpY2U8YnI+KEZyb250ZW5kLlB1c2hTZXJ2aWNlLlB1c2hlcilcIl1cbiAgICAgICAgc3R5bGUgcHN2YyBmaWxsOnllbGxvd1xuICAgICAgICBob3N0W1wiYmFja2VuZDxicj5IT1NUXCJdXG4gICAgICAgIHN0eWxlIGhvc3QgZmlsbDojZjlmLHN0cm9rZTojMzMzLHN0cm9rZS13aWR0aDo0cHhcbiAgICBlbmRcblxuICAgIHN1YmdyYXBoIGJhY2tlbmRcbiAgICAgICAgbWFuYWdlcltcImJhY2tlbmQ8YnI+TUFOQUdFUlwiXVxuICAgICAgICBzdHlsZSBtYW5hZ2VyIGZpbGw6I2Y5ZixzdHJva2U6IzMzMyxzdHJva2Utd2lkdGg6NHB4XG4gICAgZW5kXG5cbiAgICBjbGllbnQgLS4tPnxcImFueSBpbnN0YW5jZVwifCBmc3ZjXG4gICAgZnN2YyAtLi0+IG1hbmFnZXJcbiAgICBtYW5hZ2VyIC0uLT58XCJ0YXJnZXQgcHVzaCBiYWNrZW5kXCJ8IGhvc3RcbiAgICBob3N0IC0tPiBwc3ZjXG4gICAgcHN2YyAtLi0+fFwiY29ubmVjdGVkIGNsaWVudFwifCBjbGllbnRcblxuICAgIHN1YmdyYXBoIElOREVYXG4gICAgICAgIGNbXCJjb25uZWN0b3JcIl0gLS4tPiB8UlBDfCBzW1wibGlzdGVuZXJcIl1cbiAgICAgICAgbTFbXCJjYWxsZXJcIl0gLS0+IHxmdW5jdGlvbnwgbTJbXCJjYWxsZWVcIl1cbiAgICBlbmRcbiAgICAiLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCJ9fQ)

### WORK FLOW

 1. [LoadConfig](https://github.com/alkee-allm/k2proto/blob/393ccf640b89bc748dfd77ddee2d01d45cc145df/K2svc/Program.cs#L28) ; 서비스 시작 전 [local configuration(K2Config)](./ServiceConfiguration.cs) 로드; [환경변수(environment variable](https://en.wikipedia.org/wiki/Environment_variable), [설정파일](./appsettings.json) 및 실행인수(commandline argument) 순서대로 값을 읽고 중복된 경우 overwrite (참고:[#39](https://github.com/alkee-allm/k2proto/issues/39))
 
 2. [build web service](https://github.com/alkee-allm/k2proto/blob/393ccf640b89bc748dfd77ddee2d01d45cc145df/K2svc/Program.cs#L54) ; `StartUp` 에서 할 수 없는(local config 접근 등) [서비스 설정](https://github.com/alkee-allm/k2proto/blob/393ccf640b89bc748dfd77ddee2d01d45cc145df/K2svc/Program.cs#L64)
 
 3. `ConfigureService` in [Startup](./Startup.cs).
    * 공용자원(singleton) 추가
    * 각종 서비스(background, database, grpc 등) 추가 ;[서비스 수명](https://docs.microsoft.com/ko-kr/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-3.1#service-lifetimes) 참고
    * 정책설정 ; backend grpc(`BackendValidator`), 클라이언트 인증
 
 4. `Configure` in [Startup](./Startup.cs) ; 추가된 service 들과 연결(client) 사이의 middleware 설정
    * 관리서버에 등록되기 전에는 서비스를 사용할 수 없도록(service blocker)
    * 각종 서비스들을 사용할 수 있도록
    * grpc 의 서비스 연결
   
 4. [ServerManagementBackground](./Background/ServerManagementBackground.cs)의 timer 를 통해 관리서버(ServerManager)에 등록(Register)하고, 관리서버로부터 필요한 설정(RemoteConfig)들을 얻어와 저장 ; https://github.com/alkee-allm/k2proto/blob/393ccf640b89bc748dfd77ddee2d01d45cc145df/K2svc/Background/ServerManagementBackground.cs#L93-L135
 
 5. client 가 요청 하면(Init service)되면 인증과정을 거쳐 JWT 발급 ; https://github.com/alkee-allm/k2proto/blob/3300dbab79e27ed48dee4f4718fa1dd6964f27e9/K2svc/Frontend/InitService.cs#L25-L45

 6. client 는 push 용 연결(stream)을 만들고 JWT 갱신 ; https://github.com/alkee-allm/k2proto/blob/3300dbab79e27ed48dee4f4718fa1dd6964f27e9/K2svc/Frontend/PushService.cs#L31-L73

 7. 이후 client 는 필요한 다양한 요청을 시작 ; 각 서비스의 [work flow](../README.md#design--process-flow) 참고.


### EXAMPLE SCENARIO

#### forntend service 의 추가

 `MyService` 라는 이름의 예.

 1. 서비스를 디자인 하고 공용(client-server) [`.proto` 파일](../proto/sample.proto)을 추가 또는 수정 후 빌드(message 및 stub class 를 사용할 수 있는 상태로 만듦)
 
 2. `Frontend` 경로에 stub service class 를 추가 ; 이 클래스는 [AuthorizeAttribute](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authorization.authorizeattribute?view=aspnetcore-3.1)를 가지고 있어(인증 필요 지정)야 하며, stub base class 로부터 상속받아 사용해야 한다. 또한 Config subclass 를 포함해 필요한 설정이 있는 경우 get-set property 로 구성(초기값은 기본값)한다.
```csharp
[Authorize]
public class MyService : MyService.MyServiceBase
{
    public class Config
    {
        public string Example { get; set; } = "default value";
    }
}
```

 3. [ServiceConfiguration](./ServiceConfiguration.cs)에 해당 서비스의 설정을 추가한다. ; 이 예시에서는 항상 disable 할 수 없는 service 로 가정
```csharp
public class K2Config
{
//...
    // frontend
    public Frontend.MyService.Config MyService { get; set; } = new Frontend.MyService.Config();
//...
}
```

 4. [Startup](./Startup.cs)`.Configure` endpoint 에 해당 service 를 추가한다.
```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime life, ServiceConfiguration cfg)
{
//...
	app.UseEndpoints(endpoints =>
	{
//...
		// frontend services
		endpoints.MapGrpcService<Frontend.MyService>(); // 이 서비스는 client 요청이 발생하면 생성되고 요청이 끝나면 제거된다.
```
 
 5. 생성자에 필요한 resource들을 parameter 로 받고 이를 private readonly 멤버에 할당(참조)한다. ; [Dependency injection](https://sahansera.dev/dotnet-core-ioc-container/)에 의해 이 class 가 자동으로 생성되고 알맞은 생성자가 호출될 것
```csharp
[Authorize]
public class MyService : MyService.MyServiceBase
{
//...
	private readonly ILogger<MyService> logger; // generic type `MyService` 는 logging 의 category 로 사용됨
	private readonly Metadata header; // server-server(backend) communication 에 반드시 필요한 header.
	public MyService(ILogger<MyService> _logger, Metadata _header) // dependency-injection
	{
		logger = _logger; // 대입해 사용
		header = _header;
	}
}
```

 6. `.proto` 에 정의한 rpc 함수들을 [override](https://docs.microsoft.com/ko-kr/dotnet/csharp/language-reference/keywords/override)해 구현한다. 이 때 `Session.GetUserInfoOrThrow` 함수나 `Session.GetOnlineUserInfoOrThrow` 함수를 이용해 유저를 확인한다.
 ```csharp
 public override async Task<MyServiceFunctionResponse> MyServiceFunction(MyServiceFunctionRequest request, ServerCallContext context)
 {
	var (userId, pushBackendAddress) = Session.GetUserInfoOrThrow(context);
	logger.LogInformation($"Service method called from {userId} who is connected {pushBackendAddress}");
 }
 ```
 
 * **주의 ; 이 서비스 class 는 매 요청마다 생성**되므로, 공용 자원이 필요한 경우 생성자에서 참조를 얻어(Dependency injection) 사용해야한다.
 
