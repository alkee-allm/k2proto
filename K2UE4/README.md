## GRPC for Unreal Engine 4

Unreal Engine 4 에서 [gRPC](https://grpc.io)를 이용해 [서버](../K2svc/README.md)와 통신하는 동작을 하는 sample project

1. 엔진 버전  
Unreal Engine 4 **4.25.3**

### Build and run
 1. UE4 protobuf, grpc 빌드를 위한 라이브러리 추가

    1) Plugins/gRPC/Source 안에 ThirdParty 폴더 생성  
    2) vcpkg/packages 안에 앞서 설치한 abseil/c-ares/openssl/upb/winsock2/zlib/protobuf/grpc 라이브러리들을 ThirdParty 폴더 안에 복사  
    3) Plugins/gRPC/Binaries 안에 ThirdParty/gRPC/Win64 폴더 생성  
    4) ThirdParty/gRPC/Source/ThirdParty/protobuf../bin 안에 있는 dll 들을 복사하여 Win64 안에 붙여넣기  