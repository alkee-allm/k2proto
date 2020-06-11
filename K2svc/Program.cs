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
                    Console.WriteLine($"default : {rootAddress}" );
                    return -1;
                }
            }

            //using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(rootAddress);
            //var client = new ServerManagement.ServerManagementClient(channel);
            //var rsp = client.Register(new RegisterRequest { });


            // ServiceManagementService 
            // 서버를 시작하기 전에 ServerManagementService 의 주소를 확인하고
            // 이 서버의 설정들을 로드한다.
            var config = new ServiceConfiguration // development configuration
            {
                ServerManagementServiceAddress = rootAddress,
            };


            // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            System.AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            CreateHostBuilder(args).Build().Run();
            return 0;
        }


        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>

            // TODO: config 를 Startup 에 넘기는 방법.

            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
