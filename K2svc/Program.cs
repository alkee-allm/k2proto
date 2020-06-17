using Google.Protobuf.WellKnownTypes;
using K2B;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace K2svc
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var serverManagementBackendAddress = DefaultValues.SERVER_MANAGEMENT_BACKEND_ADDRESS;
            // 첫번째 parameter 로 ServerManagementService 의 address 입력
            //   없는 경우 개발환경이라고 가정하고 기본값 사용
            if (args.Length > 0)
            {
                if (Uri.TryCreate(args[0], UriKind.Absolute, out Uri uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    serverManagementBackendAddress = args[0];
                }
                else
                {
                    Console.WriteLine("Invalid argument.");
                    Console.WriteLine("The first argument must be a valid URL string.(Server Management Backend Url)");
                    Console.WriteLine("  or leave it empty to work as a Server Management Backend.");
                    return -1;
                }
            }
            else
            {
                Console.WriteLine("running as ServerManagement backend.");
            }

            // argument 가 없는 경우라면 개발(local) 버전(기본값 사용) 또는 ServerManagement backend 서버.
            var config = args.Length == 0 ? new ServiceConfiguration() : await LoadConfig(serverManagementBackendAddress, args);

            // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); // http 허용

            CreateHostBuilder(args, config).Build().Run();
            return 0;
        }


        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args, ServiceConfiguration config)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Add(config);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }

        private static async Task<ServiceConfiguration> LoadConfig(string serverAddress, string[] args)
        {
            // ServiceManagementService 
            // 서버를 시작하기 전에 ServerManagementService 의 주소를 확인하고
            // 이 서버의 설정들을 로드한다.
            var config = new ServiceConfiguration
            {
                ServerManagementBackendAddress = serverAddress,
            };
            Console.WriteLine("Connecting Server Management Service...");
            while (true)
            {
                using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(serverAddress);
                var client = new ServerManagement.ServerManagementClient(channel);
                var req = new RegisterRequest();
                req.Args.Add(args);
                try
                {
                    var rsp = await client.RegisterAsync(req);
                    if (rsp.Ok)
                    {
                        config.ServerId = rsp.ServerId;
                        config.PushBackendAddress = rsp.PushBackendAddress;
                        config.UserSessionBackendAddress = rsp.UserSessionBackendAddress;

                        //config.EnableServerManagementBackend = false;
                        config.EnableUserSessionBackend = rsp.EnableUserSession;
                        break;
                    }
                    Console.WriteLine("Waiting for Server Management Service ready.");
                }
                catch (Grpc.Core.RpcException e)
                {
                    Console.WriteLine($"ERROR : {e.Status.Detail}");
                }
                await Task.Delay(DefaultValues.SERVER_REGISTER_DELAY_MILLISECONDS);
            }
            return config;
        }
    }
}
