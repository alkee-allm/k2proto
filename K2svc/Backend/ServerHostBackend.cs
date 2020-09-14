using Grpc.Core;
using K2B;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace K2svc.Backend
{
    public class ServerHostBackend
        : ServerHost.ServerHostBase
    {
        private readonly ILogger<ServerHostBackend> logger;
        private readonly IHostApplicationLifetime life;
        private readonly ServiceConfiguration config;
        private readonly Net.GrpcClients clients;
        private readonly Metadata headers;

        public ServerHostBackend(ILogger<ServerHostBackend> _logger,
            IHostApplicationLifetime _life,
            ServiceConfiguration _config,
            Net.GrpcClients _clients,
            Metadata _headers)
        {
            logger = _logger;
            life = _life;
            config = _config;
            clients = _clients;
            headers = _headers;
        }

        #region RPC
        public override Task<Null> Stop(StopRequest request, ServerCallContext context)
        {
            logger.LogWarning($"STOP rpc is called from ServerManager. reason : {request.Reason}");

            // TODO: 리소스 정리

            life.StopApplication();
            return Task.FromResult(new Null());
        }

        public override async Task<Null> Broadcast(PushRequest request, ServerCallContext context)
        {
            var count = await Frontend.PushService.Pusher.SendMessageToAll(request);
            logger.LogInformation($"{count} broadcasted message : ", request.PushMessage);
            return new Null();
        }

        public override Task<Null> UpdateBackend(UpdateBackendRequest request, ServerCallContext context)
        {
            logger.LogInformation($"Updating SessionManager {config.SessionManagerAddress} to {request.SessionManagerAddress} ");
            if (config.SessionManagerAddress == request.SessionManagerAddress) // ServerManager 가 복구된 경우 ServerManager 가 SessionServer 의 정보를 알수가 없어 UpdateBackend 를 보낼 수 있다.
                return Task.FromResult(new Null()); 

            config.SessionManagerAddress = request.SessionManagerAddress;

            if (string.IsNullOrEmpty(request.SessionManagerAddress) == false // SessionManager 복구된 경우
                && config.EnablePushService) // Push service 에서
            { // SessionManager 와 user 동기화
                var client = clients.GetClient<SessionManager.SessionManagerClient>(request.SessionManagerAddress);
                Frontend.PushService.Pusher.SyncUsersToSessionManager(client, config.BackendListeningAddress, headers, logger);
            }

            return Task.FromResult(new Null());
        }
        #endregion
    }
}
