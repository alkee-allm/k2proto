using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;

namespace K2svc
{
    public class Program
    {
        public static class AppExitCode
        {
            public static readonly int OK = 0;
            public static readonly int INVALID_ARGUMENT = 1;
        }

        public static int Main(string[] args)
        {
            string serverManagementBackendAddress = args.Length > 0 ? args[0] : null;
            // 첫번째 parameter 로 ServerManagementService 의 address 입력
            //   없는 경우 ServerManagementService 로 동작
            if (string.IsNullOrEmpty(serverManagementBackendAddress))
            {
                Console.WriteLine("running as ServerManagement backend.");
            }
            else if (Util.HasHttpScheme(serverManagementBackendAddress) == false)
            {
                Console.WriteLine("Invalid argument.");
                Console.WriteLine("The first argument must be a valid URL string.(Server Management Backend Url)");
                Console.WriteLine("  or leave it empty to work as a Server Management Backend.");
                return AppExitCode.INVALID_ARGUMENT;
            }

            // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); // http 허용

            var config = BuildConfig(serverManagementBackendAddress);
            CreateHostBuilder(args, config).Build().Run();
            return AppExitCode.OK;
        }

        private static ServiceConfiguration BuildConfig(string backendAddressFromArg)
        {
            // build configuration ; https://stackoverflow.com/a/58594026
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables() // 환경변수에 "K2:ServerId" 와 같은 형식의 값으로 사용 가능하도록
                                           // commandline 설정은 custom 한 방식을 사용. 즉, AddCommandLine(args) 사용하지 않음.
                .AddJsonFile(DefaultValues.APP_SETTINGS_FILENAME)
                .AddJsonFile(DefaultValues.APP_SETTINGS_OVERRIDE_FILENAME)
                .Build()
                ;

            var config = configuration.GetSection(ServiceConfiguration.SECTION_NAME).Get<ServiceConfiguration>();
            if (config == null) config = new ServiceConfiguration(); // create default

            config.RemoteServerManagerAddress = backendAddressFromArg; // ServerManager 인 경우 null or empty
            return config;
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args, ServiceConfiguration config)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder =>
                {
                    builder
                        .Add(config)
                        ;
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseKestrel(options =>
                        {
                            options.ListenAnyIP(config.ListeningPort);
                        })
                        .UseStartup<Startup>()
                        ;
                });
        }
    }
}
