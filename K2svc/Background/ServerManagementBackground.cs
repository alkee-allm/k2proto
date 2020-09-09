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
        private readonly Net.GrpcClients clients;

        private Timer timer;
        private int workCount = 0;
        private double interval = DefaultValues.SERVER_MANAGEMENT_PING_INTERVAL_SECONDS;

        private enum State
        {
            REGISTERING,
            PINGING,
        }
        private State currentState = State.REGISTERING;

        public ServerManagementBackground(ILogger<ServerManagementBackground> _logger, ServiceConfiguration _config, Metadata _header, Net.GrpcClients _clients)
        {
            logger = _logger;
            config = _config;
            header = _header;
            clients = _clients;
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
                    logger.LogInformation($"server registered to ServerManager : {config.ServerManagerAddress}");
                    currentState = State.PINGING;
                }
                else if (currentState == State.PINGING && await Ping())
                {
                }
            }
        }

        private async Task<bool> Register()
        {
            var client = clients.GetClient<ServerManager.ServerManagerClient>(config.ServerManagerAddress);
            var req = new RegisterRequest
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
                ServerId = config.ServerId,
                ListeningPort = config.ListeningPort,
                PublicIp = Util.GetPublicIp(), // backend 전용인 경우 null
                ServiceScheme = config.ServiceScheme,

                HasServerManagement = config.RemoteServerManagerAddress == null,

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
                    config.RemoteServerManagerAddress = rsp.ServerManagementAddress; // 시작환경에 의해 고정되기때문에 의미 없을 것.
                    config.UserSessionBackendAddress = rsp.UserSessionAddress;

                    config.BackendListeningAddress = rsp.BackendListeningAddress; // Register 에 의해 private IP 가 결정되기때문에 이 이후부터 사용 가능.
                    config.Registered = true;
                    return true;
                }
                logger.LogInformation("Server Management backend is not ready");
            }
            catch (RpcException e)
            {
                logger.LogWarning($"ERROR on register to {config.ServerManagerAddress} : {e.Status.Detail}");
            }
            return false;
        }

        private async Task<bool> Ping()
        {
            var client = clients.GetClient<ServerManager.ServerManagerClient>(config.ServerManagerAddress);

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
                logger.LogInformation("Server Manager responded NOT OK(unregistered)");
                // set to reregister
                config.Registered = false;
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
