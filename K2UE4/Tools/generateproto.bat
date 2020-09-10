SET TOOLPATH=x64-windows
SET SOURCE=..\..\proto
SET TARGET=..\Source\K2UE4\proto

%TOOLPATH%\protoc.exe -I %SOURCE% --cpp_out=%TARGET% --grpc_out=%TARGET% --plugin=protoc-gen-grpc=%TOOLPATH%\grpc_cpp_plugin.exe %SOURCE%\sample.proto