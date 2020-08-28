using Grpc.Core;
using K2B;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace K2svc.Background
{
    public sealed class ServerManagementBackground
        : IHostedService
        , IDisposable
    {
        private readonly ILogger<ServerManagementBackground> logger;
        private readonly ServiceConfiguration config;
        private readonly Metadata header;

        private Timer timer;
        private int workCount = 0;
        private double interval = DefaultValues.SERVER_MANAGEMENT_PING_INTERVAL_SECONDS;

        private enum State
        {
            REGISTERING,
            PINGING,
        }
        private State currentState = State.REGISTERING;

        public ServerManagementBackground(ILogger<ServerManagementBackground> _logger, ServiceConfiguration _config, Metadata _header)
        {
            logger = _logger;
            config = _config;
            header = _header;
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation($"{GetType().Name} timer is STARTING.");
            timer = new Timer(OnTime, this, TimeSpan.Zero, TimeSpan.FromSeconds(interval));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation($"{GetType().Name} timer is STOPPING.");
            timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        private async void OnTime(object state) // event handler 이므로 async Task 가 아닌 async void
        {
            if (workCount > 0)
            {
                logger.LogWarning($"ServerManagement timer is busy({workCount})");
                var cnt = Interlocked.Increment(ref workCount);
                if (cnt == 1) // timer 끝나자마자 increament 실행된 경우.
                {
                    Interlocked.Exchange(ref workCount, 0);
                }
                return;
            }

            Interlocked.Exchange(ref workCount, 1);
            using (new Defer(() => { Interlocked.Exchange(ref workCount, 0); }))
            {
                if (currentState == State.REGISTERING && await Register())
                {
                    currentState = State.PINGING;
                }
                else if (currentState == State.PINGING && await Ping())
                {
                }
            }
        }

        private async Task<bool> Register()
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.ServerManagementBackendAddress ?? DefaultValues.SERVER_MANAGEMENT_BACKEND_ADDRESS);
            var client = new ServerManagement.ServerManagementClient(channel);
            var req = new RegisterRequest
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                ServerId = config.ServerId,
                ListeningPort = config.ListeningPort,
                PublicIp = Util.GetPublicIp(), // backend 전용인 경우 null
                ServiceScheme = config.ServiceScheme,

                HasServerManagement = config.ServerManagementBackendAddress == null,

                // backend unique services
                HasUserSession = config.EnableUserSessionBackend,

                // frontend services
                HasPush = config.EnablePushSampleService,
                HasInit = config.EnableInitService,
                HasPushSample = config.EnablePushSampleService,
                HasSimpleSample = config.EnableSimpleSampleService,
            };

            try
            {
                var rsp = await client.RegisterAsync(req, header);
                if (rsp.Result == RegisterResponse.Types.ResultType.Ok)
                {
                    config.ServerManagementBackendAddress = rsp.ServerManagementAddress; // 시작환경에 의해 고정되기때문에 의미 없을 것.
                    config.UserSessionBackendAddress = rsp.UserSessionAddress;

                    config.BackendListeningAddress = rsp.BackendListeningAddress; // Register 에 의해 private IP 가 결정되기때문에 이 이후부터 사용 가능.
                    config.Registered = true;
                    return true;
                }
                logger.LogInformation("Server Management backend is not ready");
            }
            catch (Grpc.Core.RpcException e)
            {
                logger.LogWarning($"ERROR on register : {e.Status.Detail}");
            }
            return false;
        }

        private async Task<bool> Ping()
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.ServerManagementBackendAddress ?? DefaultValues.SERVER_MANAGEMENT_BACKEND_ADDRESS);
            var client = new ServerManagement.ServerManagementClient(channel);

            var req = new PingRequest
            {
                ServerId = config.ServerId,

                // TODO: fill this statistics values
                CpuUsagePercent = 0,
                FreeHddBytes = 0,
                MemoryUsage = 0,

                Population = 0,
            };

            try
            {
                var rsp = await client.PingAsync(req, header);
                if (rsp.Ok)
                {
                    return true;
                }
                logger.LogInformation("Server Management backend not OK ping back.");
                // TODO: 다시 register ?
            }
            catch (Grpc.Core.RpcException e)
            {
                logger.LogWarning($"ERROR on ping : {e.Status.Detail}");
            }
            return false;
        }
    }
}
