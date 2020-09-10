using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            string serverManagerAddress = args.Length > 0 ? args[0] : null;
            // 첫번째 parameter 로 ServerManagementService 의 address 입력
            //   없는 경우 ServerManagementService 로 동작
            if (string.IsNullOrEmpty(serverManagerAddress))
            {
                Console.WriteLine("running as ServerManager backend.");
            }
            else if (Util.HasHttpScheme(serverManagerAddress) == false)
            {
                Console.WriteLine("Invalid argument.");
                Console.WriteLine("The first argument must be a valid URL string.(Server Management Backend Url)");
                Console.WriteLine("  or leave it empty to work as a Server Management Backend.");
                return AppExitCode.INVALID_ARGUMENT;
            }
            else
            {
                Console.WriteLine($"ServerManager is set to {serverManagerAddress}");
            }

            // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); // http 허용

            var config = BuildConfig(serverManagerAddress);
            CreateHostBuilder(args, config).Build().Run();
            return AppExitCode.OK;
        }

        private static ServiceConfiguration BuildConfig(string backendAddressFromArg)
        {
            // build configuration ; https://stackoverflow.com/a/58594026
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables() // 환경변수에 "K2:ServerId" 와 같은 형식의 값으로 사용 가능하도록
                                           // commandline 설정은 custom 한 방식을 사용. 즉, AddCommandLine(args) 사용하지 않음.
                .AddJsonFile(DefaultValues.APP_SETTINGS_FILENAME);
            if (System.IO.File.Exists(DefaultValues.APP_SETTINGS_OVERRIDE_FILENAME)) builder.AddJsonFile(DefaultValues.APP_SETTINGS_OVERRIDE_FILENAME);

            var configuration = builder.Build();
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
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseKestrel(options =>
                        {
                            options.ListenAnyIP(config.ListeningPort);
                        })
                        .ConfigureServices(sc =>
                        {
                            var backendHeader = new Grpc.Core.Metadata();
                            backendHeader.Add(nameof(config.BackendGroupId), config.BackendGroupId); // key 는 소문자로 변환되어 들어간다

                            // ServiceConfiguration 종속적인 resource 들 ; asp.net core 3.0 이후 직접 StartUp 에 parameter 를 전달할 수 없음 (https://andrewlock.net/avoiding-startup-service-injection-in-asp-net-core-3/)
                            sc.AddSingleton(config);
                            sc.AddSingleton(backendHeader);
                        })
                        .UseStartup<Startup>()
                        ;
                });
        }
    }
}
