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
                Interlocked.Increment(ref workCount);
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
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.ServerManagementBackendAddress);
            var client = new ServerManagement.ServerManagementClient(channel);
            var req = new RegisterRequest();
            req.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
            try
            {
                var rsp = await client.RegisterAsync(req, header);
                if (rsp.Ok)
                {
                    config.ServerId = rsp.ServerId;
                    config.BackendListeningAddress = rsp.PushBackendAddress;
                    config.UserSessionBackendAddress = rsp.UserSessionBackendAddress;
                    config.EnableUserSessionBackend = rsp.EnableUserSession;
                    //config.EnableServerManagementBackend 는 시작환경(args)과 함께 고정

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
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.ServerManagementBackendAddress);
            var client = new ServerManagement.ServerManagementClient(channel);

            // TODO: fill this values
            var req = new PingRequest
            {
                ServerId = config.ServerId,

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
