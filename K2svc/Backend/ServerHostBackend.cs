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

        public ServerHostBackend(ILogger<ServerHostBackend> _logger,
            IHostApplicationLifetime _life)
        {
            logger = _logger;
            life = _life;
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
        #endregion
    }
}
