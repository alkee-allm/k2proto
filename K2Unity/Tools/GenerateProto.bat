@ECHO OFF
:: TODO : 실행 환경 validation

:: parameters
SET TOOLPATH=windows_x64
SET TARGET=..\Assets\Script\generated
SET SOURCE=..\..\proto

IF NOT EXIST "%TARGET%" (
	ECHO target path not found ; %TARGET%
	EXIT -1 /B
)

ECHO generating .cs from .proto

:: *.proto 를 입력하면 "No such file or directory" 오류
%TOOLPATH%\protoc -I %SOURCE% --csharp_out=%TARGET% --grpc_out=%TARGET% --plugin=protoc-gen-grpc=%TOOLPATH%\grpc_csharp_plugin.exe %SOURCE%\sample.proto

ECHO done.
::PAUSE
