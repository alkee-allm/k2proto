using K2B;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace K2svc
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // 첫번째 parameter 로 ServerManagementService 의 address 입력
            var rootAddress = DefaultValues.SERVER_MANAGEMENT_SERVICE_ADDRESS;
            if (args.Length > 0)
            {
                if (Uri.TryCreate(args[0], UriKind.Absolute, out Uri uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    rootAddress = args[0];
                }
                else
                {
                    Console.WriteLine("Invalid argument.");
                    Console.WriteLine("The first argument must be a valid URL string.(Server Management Service Url)");
                    Console.WriteLine($"default : {rootAddress}");
                    return -1;
                }
            }

            // argument 가 없는 경우라면 개발(local) 버전이므로 ServerManagementService 가 떠있기 전 상태라 config 를 얻어올 수 없다
            // 따라서 이 경우 기본값 사용
            var config = args.Length == 0 ? new ServiceConfiguration() : await LoadConfig(rootAddress, args);

            // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); // http 허용

            CreateHostBuilder(args, config).Build().Run();
            return 0;
        }


        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args, ServiceConfiguration config) =>

            // TODO: config 를 Startup 에 넘기는 방법.

            Host.CreateDefaultBuilder(args)
                //.ConfigureAppConfiguration(builder =>
                //{
                //})
                //.ConfigureServices(services =>
                //{
                //    services.Configure<ServiceConfiguration>(config);
                //})
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });


        private static async Task<ServiceConfiguration> LoadConfig(string serverAddress, string[] args)
        {
            // ServiceManagementService 
            // 서버를 시작하기 전에 ServerManagementService 의 주소를 확인하고
            // 이 서버의 설정들을 로드한다.
            var config = new ServiceConfiguration
            {
                ServerManagementServiceAddress = serverAddress,
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
                        if (string.Compare(serverAddress.Trim(), rsp.ServerManagementBackendAddress.Trim(), true) != 0)
                        { // 다른 server 로 redirection ?
                            Console.WriteLine($"redirecting {serverAddress} to {rsp.ServerManagementBackendAddress}");
                            serverAddress = rsp.ServerManagementBackendAddress;
                            continue;
                        }
                        config.ServerId = rsp.ServerId;
                        config.FrontendListeningPort = rsp.FrontendListeningPort;
                        config.PushBackendAddress = rsp.PushBackendAddress;

                        config.UserSessionServiceAddress = rsp.UserSessionBackendAddress;
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
