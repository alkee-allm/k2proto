using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;

namespace K2svc
{
    public class Program
    {
        public static int Main(string[] args)
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
            var config = new ServiceConfiguration
            {
                ServerManagementBackendAddress = serverManagementBackendAddress,
                EnableUserSessionBackend = args.Length == 0,
            };

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
    }
}
