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
        public class Config
        {
            public double PingIntervalSec { get; set; } = 1.0;
        }

        private readonly ILogger<ServerManagementBackground> logger;
        private readonly K2Config localConfig;
        private readonly RemoteConfig remoteConfig;
        private readonly Metadata header;
        private readonly Net.GrpcClients clients;

        private Timer timer;
        private int workCount = 0;
        private double interval;

        private enum State
        {
            REGISTERING,
            PINGING,
        }
        private State currentState = State.REGISTERING;

        public ServerManagementBackground(ILogger<ServerManagementBackground> _logger,
            K2Config _localConfig,
            RemoteConfig _remoteConfig,
            Metadata _header,
            Net.GrpcClients _clients)
        {
            logger = _logger;
            localConfig = _localConfig;
            remoteConfig = _remoteConfig;
            header = _header;
            clients = _clients;
            interval = localConfig.ServerManagementBackground.PingIntervalSec;
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
            var cnt = Interlocked.Increment(ref workCount);

            if (cnt > 1)
            {
                logger.LogWarning($"ServerManagement timer is busy({cnt - 1})");
                return;
            }

            using (new Defer(() => { Interlocked.Exchange(ref workCount, 0); }))
            {
                if (currentState == State.REGISTERING && await Register())
                {
                    logger.LogInformation($"server registered to ServerManager : {localConfig.ServerManagerAddress}");
                    currentState = State.PINGING;
                }
                else if (currentState == State.PINGING && await Ping())
                {
                }
            }
        }

        private async Task<bool> Register()
        {
            var client = clients.GetClient<ServerManager.ServerManagerClient>(localConfig.ServerManagerAddress);
            var req = new RegisterRequest
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                ServerId = localConfig.ServerId,
                ListeningPort = localConfig.ListeningPort,
                PublicIp = Util.GetPublicIp(), // backend 전용인 경우 null
                ServiceScheme = localConfig.ServiceScheme,

                HasServerManager = localConfig.HasServerManager,

                // backend unique services
                HasSessionManager = localConfig.HasSessionManager,

                // frontend services
                HasPush = localConfig.HasPushService,
                HasInit = localConfig.HasInitService,
                HasPushSample = localConfig.HasPushSampleService,
                HasSimpleSample = localConfig.HasSimpleSampleService,
            };

            try
            {
                var rsp = await client.RegisterAsync(req, header);
                if (rsp.Result == RegisterResponse.Types.ResultType.Ok)
                {
                    remoteConfig.ServerManagerAddress = rsp.ServerManagementAddress;
                    remoteConfig.SessionManagerAddress = rsp.UserSessionAddress;

                    remoteConfig.BackendListeningAddress = rsp.BackendListeningAddress; // Register 에 의해 private IP 가 결정되기때문에 이 이후부터 사용 가능.
                    remoteConfig.Registered = true; // remoteConfig 사용 가능 flag
                    return true;
                }
                logger.LogWarning($"Unable to register this server to ServerManager : {rsp.Result}");
            }
            catch (RpcException e)
            {
                logger.LogWarning($"ERROR on register to {localConfig.ServerManagerAddress} : {e.Status.Detail}");
            }
            return false;
        }

        private async Task<bool> Ping()
        {
            var client = clients.GetClient<ServerManager.ServerManagerClient>(localConfig.ServerManagerAddress);

            var req = new PingRequest
            {
                ServerId = localConfig.ServerId,

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
                logger.LogInformation("Server Manager responded NOT OK(unregistered)");
                // set to reregister
                remoteConfig.Registered = false;
                currentState = State.REGISTERING;
            }
            catch (RpcException e)
            {
                logger.LogWarning($"ERROR on ping : {e.Status.Detail}");
            }
            return false;
        }
    }
}
