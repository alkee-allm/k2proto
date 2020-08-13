## GRPC for Unity 3D

프로젝트를 실행하기 전에, `K2Unity/Toos/GenerateProto.bat` 파일을 실행해, `.proto` 파일을
`.cs` 파일로 생성(위치:`K2Unity/Assets/Script/generated/`)한 후 실행 가능.

구현 내용은 Hierarchy 의 `Canvas/GrpcSampleControler` , `Assets/Script/GrpcSampleControler.cs` 파일을 참고

### Unity 에서 GRPC 사용 구성 방법

1. Unity project 준비

2. [`edd81ac6-e3d1-461a-a263-2b06ae913c3f` 버전](https://packages.grpc.io/archive/2019/12/a02d6b9be81cbadb60eed88b3b44498ba27bcba9-edd81ac6-e3d1-461a-a263-2b06ae913c3f/index.xml)의 grpc 패키지 다운로드
 ; 이 버전이 아니라면, [unity build 오류 발생](https://github.com/alkee-allm/k2proto/issues/23#issuecomment-672580104)
    * `grpc-protoc_windows_x64-1.26.0-dev.zip`
	* `grpc_unity_package.2.26.0-dev.zip`

3. `grpc_unity_package.2.26.0-dev.zip` 파일을 unity project `Assets` 경로에 풀기(plugins directory 생성)

4. 자동 생성되는 파일을 관리할 `Assets/Script/generated` directory 를 생성하고 `.gitignore` 에 이를 포함시킨다

```.gitignore
# Generated code
/[Aa]ssets/Script/[Gg]enerated/
/[Aa]ssets/Script/[Gg]enerated.meta
```

5. Unity project directory(`Assets` directory 가 있는 directory)에 `Tools/windows_x65/` 경로를 만들어 `grpc-protoc_windows_x64-1.26.0-dev.zip` 압축 해제. ; `Assets` 경로를 피하는 이유는 `.meta` 파일들의 생성을 피하기 위함

6. `Tools` directory 에 `GenerateProto.bat` 생성하고, 알맞게 수정한다.

```batch
@ECHO OFF

:: parameters
SET TOOLPATH=windows_x64
SET TARGET=..\Assets\Script\generated
SET SOURCE=..\..\proto

IF NOT EXIST "%TARGET%" (
	ECHO target path not found ; %TARGET%
	EXIT -1 /B
)

ECHO generating .cs from .proto

:: *.proto 를 입력하면 "No such file or directory" 오류. `.proto` 파일 개별로 입력하도록 한다.
%TOOLPATH%\protoc -I %SOURCE% --csharp_out=%TARGET% --grpc_out=%TARGET% --plugin=protoc-gen-grpc=%TOOLPATH%\grpc_csharp_plugin.exe %SOURCE%\sample.proto

ECHO done.
```

7. `GenerateProto.bat` 을 실행하고 출력 경로에 protobuf message 및 grpc stub 파일들(`*.cs` 및 `*Grpc.cs`)이 올바르게 생성되었는지 확인.

8. Unity script 에서 아래와 같은 예시와 같이 grpc 사용(비동기 - 단방향)

``` csharp
public void OnButtonClick()
{
	channel = new Grpc.Core.Channel(Const.SERVER_ADDRESS, Grpc.Core.ChannelCredentials.Insecure); // http
	initClient = new K2.Init.InitClient(channel);
	GrpcRun(
		initClient.StateAsync(Const.NULL),
		(state) => { logOut.text = $"version = {state.Version}"; },
		(error)=> { logOut.text += $"ERROR : {error}\n"; }
	);
}

async void GrpcRun<RSP>(AsyncUnaryCall<RSP> call, Action<RSP> completed, Action<RpcException> error = null)
{
	try
	{
		var r = await call;
		completed(r);
	}
	catch (RpcException e)
	{
		error?.Invoke(e);
	}
}
```

### 요약 및 구성 확인

Unity project 경로 기준

  * `/Assets/Plugins` 경로에 `Google.Protobuf`, `Grpc.Core` 등의 grpc 관련 plugin binary
  * `/Tools/GenerateProyo.bat`
  * `/Tools/windows_x64` 경로에 `protoc.exe`, `grpc_csharp_plugin.exe` 등의 관련 windows commandline tools
  * `.gitignore` 에 protobuf 및 grpc 생성파일 경로 포함

### History

https://github.com/alkee-allm/k2proto/issues/23
