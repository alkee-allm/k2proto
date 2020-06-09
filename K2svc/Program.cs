using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace K2svc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // TODO: configuration from arguments
            // ServiceManagementService 
            // 서버를 시작하기 전에 ServerManagementService 의 주소를 확인하고
            // 이 서버의 설정들을 로드한다.
            var config = new ServiceConfiguration // development configuration
            {
                ServerManagementServiceAddress = "http://localhost:5000", // must be set first

                ServerId = "dev",
                UserSessionServiceAddress = "http://localhost:5000"
            };


            // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            System.AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            CreateHostBuilder(args).Build().Run();
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
