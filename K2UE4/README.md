## GRPC for Unreal Engine 4

Unreal Engine 4 에서 [gRPC](https://grpc.io)를 이용해 [서버](../K2svc/README.md)와 통신하는 동작을 하는 sample project

1. 엔진 버전  
Unreal Engine 4 **4.25.3**

### Build and run
 1. UE4 protobuf, grpc 빌드를 위한 라이브러리 추가

    1) 프로젝트 루트에 ThirdParty 폴더 생성  
    2) vcpkg/packages 안에 앞서 설치한 protobuf / grpc 폴더 복사하여 ThirdParty 안에 붙여넣기