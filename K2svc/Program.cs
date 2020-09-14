using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        private const string APP_SETTINGS_FILENAME = "appsettings.json";

        private static readonly ILogger<Program> logger = LoggerFactory.Create(builder =>
        { // https://docs.microsoft.com/ko-kr/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1#logging-providers
            builder.AddConsole();
        }).CreateLogger<Program>();

        public static int Main(string[] args)
        {
            // https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-3.0#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); // http 허용

            CreateHostBuilder(args, LoadConfig(args))
                .Build()
                .Run();
            return AppExitCode.OK;
        }

        private static string CurrentProcessPath
            => System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName);

        private static string GetBasePath()
        { // https://github.com/alkee-allm/k2proto/issues/55
            // 설정 파일은 작업경로 및 실행파일 경로 어느쪽에 있든 동작 가능하도록
            var cd = System.IO.Directory.GetCurrentDirectory();
            var pd = CurrentProcessPath;
            if (cd == pd) return cd;

            logger.LogInformation($"WORKING DIRECTORY : {cd}");
            logger.LogInformation($"PROCESS DIRECTORY : {pd}");

            bool configInCurrentDir = System.IO.File.Exists(System.IO.Path.Combine(cd, APP_SETTINGS_FILENAME));
            bool configInProcessDir = System.IO.File.Exists(System.IO.Path.Combine(pd, APP_SETTINGS_FILENAME));

            if (configInCurrentDir && configInProcessDir)
            { // 어느쪽 파일을 사용하는지 직관적이지 않으므로, logging 에라도 표현
                logger.LogWarning($"{APP_SETTINGS_FILENAME} EXISTS in BOTH of working and process directoroes. Using the file in WORKING DIRECTORY.");
                return cd;
            }

            return configInCurrentDir ? cd : pd;
        }

        private static K2Config LoadConfig(string[] args)
        {
            string APP_SETTINGS_OVERRIDE_FILENAME = $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json";

            // build configuration ; https://stackoverflow.com/a/58594026
            var builder = new ConfigurationBuilder() // configuration 순서 중요
                .SetBasePath(GetBasePath()) // 파일을 읽기 전에 설정
                .AddEnvironmentVariables() // 환경변수에 "K2:ServerId" 와 같은 형식의 값으로 사용 가능하도록
                .AddJsonFile(APP_SETTINGS_FILENAME); // 환경변수 값을 json 으로 override 가능하도록
            if (System.IO.File.Exists(APP_SETTINGS_OVERRIDE_FILENAME)) builder.AddJsonFile(APP_SETTINGS_OVERRIDE_FILENAME); // 환경에 따른 json override
            builder.AddCommandLine(args); // 모든 설정은 다시 commandline argument 로 override 가능. ex) K2:ListeningPort=1111

            var configuration = builder.Build();
            var config = configuration.GetSection(K2Config.SECTION_NAME).Get<K2Config>();
            if (config == null) config = new K2Config(); // create default (development environment)

            if (config.HasServerManager)
            { // ServerManager 로 동작하는 경우 ServerManagerAddress 는 항상 자기 자신이어야 한다. 잘못설정한 경우 crash 가 맞나?
                config.ServerManagerAddress = $"{config.ServiceScheme}://localhost:{config.ListeningPort}";
            }

            return config;
        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args, K2Config config)
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

                            // K2Config 종속적인 resource 들 ; asp.net core 3.0 이후 직접 StartUp 에 parameter 를 전달할 수 없음 (https://andrewlock.net/avoiding-startup-service-injection-in-asp-net-core-3/)
                            sc.AddSingleton(config);
                            sc.AddSingleton(backendHeader);
                        })
                        .UseStartup<Startup>()
                        ;
                });
        }
    }
}
