
[gRPC](https://grpc.io/) 를 이용한 서비스 sample

## Getting started

### Build and run

아래 과정을 거쳐 project 를 설정하고 실행 가능. 이미 설치 되어있는 component 나 package 가 있다면
skip 가능.

  1. 개발환경
    - 64 bit Windows 10
	- Visual studio 2019 16.6.1
	- Windows SDK 10.0.18362.0
	- .NET core 3.1 SDK
	- git 2.22.0

  2. [vcpkg](https://docs.microsoft.com/ko-kr/cpp/build/vcpkg?view=vs-2019) 설치
    a. vcpkg clone `git clone https://github.com/microsoft/vcpkg.git`
	b. powershell 또는 cmd 에서 `bootstrap-vcpkg.bat` 실행

  3. [protobuf](https://developers.google.com/protocol-buffers) pakcage 설치 `.\vcpkg.exe install protobuf:x64-windows`
  
  4. grpc package 설치 `.\vcpkg.exe install grpc:x64-windows`
  
  5. winsock2 pakcage 설치 `.\vcpkg.exe install winsock2:x64-windows`
	
  6. 관리자 권한으로 pwoershell 혹은 cmd 에서 `.\vcpkg.exe integrate install` 실행해 visual studio 에서 사용가능하도록 등록
  
  7. visual studio 에서 `K2.sln` 파일 열기
  
  8. solution 속성 설정(properies)에서 `시작 프로젝트`에서 여러개의 시작프로젝트로 설정하고 포함되어있는 모든 프로젝트를 시작으로 설정.
  
  9. Run debug

### design

[![](https://mermaid.ink/img/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHBhcnRpY2lwYW50IEMgYXMgQ2xpZW50XG4gIHBhcnRpY2lwYW50IElTIGFzIEluaXQgc2VydmljZVxuICBwYXJ0aWNpcGFudCBQUyBhcyBQdXNoIHNlcnZpY2VcbiAgcGFydGljaXBhbnQgU1MgYXMgU2FtcGxlIHNlcnZpY2VcblxuICByZWN0IHJnYigyNTUsIDAsIDAsIC41KVxuICAgIG5vdGUgcmlnaHQgb2YgQzogbm8tYXV0aCBtZXNzYWdlc1xuICAgIEMgLS0-IElTIDogU3RhdGVcbiAgICBDIC0tPiBJUyA6IExvZ2luXG4gIGVuZFxuICBDIC0-PiBQUyA6IFB1c2hCZWdpblxuICBhY3RpdmF0ZSBQU1xuICBDIC0tPj4gU1MgOiBCcm9hZGNhc3RcbiAgU1MgLS0-PiBQUyA6IEJyb2FkY2FzdFxuICBQUyAtPj4gQyA6IEJyb2FkY2FzdFxuICBkZWFjdGl2YXRlIFBTIiwibWVybWFpZCI6eyJ0aGVtZSI6ImRlZmF1bHQifSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)](https://mermaid-js.github.io/mermaid-live-editor/#/edit/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHBhcnRpY2lwYW50IEMgYXMgQ2xpZW50XG4gIHBhcnRpY2lwYW50IElTIGFzIEluaXQgc2VydmljZVxuICBwYXJ0aWNpcGFudCBQUyBhcyBQdXNoIHNlcnZpY2VcbiAgcGFydGljaXBhbnQgU1MgYXMgU2FtcGxlIHNlcnZpY2VcblxuICByZWN0IHJnYigyNTUsIDAsIDAsIC41KVxuICAgIG5vdGUgcmlnaHQgb2YgQzogbm8tYXV0aCBtZXNzYWdlc1xuICAgIEMgLS0-IElTIDogU3RhdGVcbiAgICBDIC0tPiBJUyA6IExvZ2luXG4gIGVuZFxuICBDIC0-PiBQUyA6IFB1c2hCZWdpblxuICBhY3RpdmF0ZSBQU1xuICBDIC0tPj4gU1MgOiBCcm9hZGNhc3RcbiAgU1MgLS0-PiBQUyA6IEJyb2FkY2FzdFxuICBQUyAtPj4gQyA6IEJyb2FkY2FzdFxuICBkZWFjdGl2YXRlIFBTIiwibWVybWFpZCI6eyJ0aGVtZSI6ImRlZmF1bHQifSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)


### Test

  client 실행창에서 최초에 id 와 password 를 묻게 되는데, password 의 경우 k 로 시작하면 항상 성공하고 그렇지 않으면 실패하도록 되어있음.
  

### Todo

 * Improvement client
   - 비동기 방식의 c++ gRPC

 * Backend services
   - independent channel for frontend & backend
   - strong typed configuration from Main to Startup --> ServerManagement service flow

 * Design, Documents
   - Diagram 에서 Push Service 에 send 하는 것처럼 되어있지만 실제로는 Push service instance 를 가지고있는 서버의 해당 backend 에서 처리(singleton 호출)되는 괴리 문제
   
