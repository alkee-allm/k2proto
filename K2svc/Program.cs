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
            // ������ �����ϱ� ���� ServerManagementService �� �ּҸ� Ȯ���ϰ�
            // �� ������ �������� �ε��Ѵ�.
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

            // TODO: config �� Startup �� �ѱ�� ���.

            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
