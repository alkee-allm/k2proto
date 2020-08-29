using Grpc.Core;
using K2B;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace K2svc.Backend
{
    public class SessionHostBackend : SessionHost.SessionHostBase
    {
        private readonly ILogger<SessionHostBackend> logger;
        private readonly Frontend.PushService.IPushable pusher;

        public SessionHostBackend(ILogger<SessionHostBackend> _logger)
        {
            logger = _logger;
            pusher = Frontend.PushService.Pusher;
        }

        public override Task<KickUserResponse> KickUser(KickUserRequest request, ServerCallContext context)
        {
            logger.LogInformation($"force to disconnect user : {request.UserId}");
            if (pusher.Disconnect(request.UserId) == false)
            {
                logger.LogWarning($"user not found to disconnect : {request.UserId}");
                return Task.FromResult(new KickUserResponse { Result = KickUserResponse.Types.ResultType.NotExist });
            }
            return Task.FromResult(new KickUserResponse { Result = KickUserResponse.Types.ResultType.Ok });
        }

        public override async Task<PushResponse> Push(PushRequest request, ServerCallContext context)
        {
            if (await pusher.SendMessage(request.TargetUserId, request))
            {
                return new PushResponse { Result = PushResponse.Types.ResultType.Ok };
            }

            return new PushResponse { Result = PushResponse.Types.ResultType.NotExist };
        }

        public override Task<IsOnlineResponse> IsOnline(IsOnlineRequest request, ServerCallContext context)
        {
            return Task.FromResult(
                new IsOnlineResponse
                {
                    Result = pusher.IsConnected(request.UserId) ? IsOnlineResponse.Types.ResultType.Online : IsOnlineResponse.Types.ResultType.Offline
                }
            );
        }
    }
}
