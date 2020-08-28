using Grpc.Core;
using K2B;
using K2svc.Frontend;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace K2svc.Backend
{
    public class UserSessionBackend : UserSession.UserSessionBase
    {
        private readonly ILogger<UserSessionBackend> logger;
        private readonly PushService.Singleton push;
        private readonly Metadata header;

        private static Dictionary<string/*userId*/, string/*pushBackendAddress*/> sessions = new Dictionary<string, string>();

        public UserSessionBackend(ILogger<UserSessionBackend> _logger, PushService.Singleton _push, Metadata _header)
        {
            logger = _logger;
            push = _push;
            header = _header;
        }

        #region rpc - backend listen
        public override async Task<AddUserResponse> AddUser(AddUserRequest request, ServerCallContext context)
        {
            bool exist = false;
            if (request.Force)
            { // 무조건 kick 
                await KickUser(new KickUserRequest { UserId = request.UserId }, context);
            }

            lock (sessions)
            {
                exist = sessions.ContainsKey(request.UserId);
                if (exist)
                {
                    if (request.Force)
                        logger.LogWarning($"{request.UserId} already exist." + (request.Force ? " EVEN AFTER KICKED" : ""));
                    return new AddUserResponse { Result = AddUserResponse.Types.ResultType.AlreadyConnected };
                }
                sessions.Add(request.UserId, request.BackendListeningAddress);
            }
            return new AddUserResponse
            {
                Result = exist ? AddUserResponse.Types.ResultType.ForceAdded : AddUserResponse.Types.ResultType.Ok
            };
        }

        public override Task<RemoveUserResponse> RemoveUser(RemoveUserRequest request, ServerCallContext context)
        {
            lock (sessions)
            {
                return Task.FromResult(new RemoveUserResponse
                {
                    Result = sessions.Remove(request.UserId) ? RemoveUserResponse.Types.ResultType.Ok : RemoveUserResponse.Types.ResultType.NotExist
                });
            }
        }

        public override async Task<KickUserResponse> KickUser(KickUserRequest request, ServerCallContext context)
        {
            string pushBackendAddress;
            lock (sessions)
            {
                if (sessions.TryGetValue(request.UserId, out pushBackendAddress) == false)
                {
                    return new KickUserResponse { Result = KickUserResponse.Types.ResultType.NotExist };
                }
            }

            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(pushBackendAddress);
            var client = new UserSession.UserSessionClient(channel);
            return await client.KickUserFAsync(request, header);
        }

        public override async Task<PushResponse> Push(PushRequest request, ServerCallContext context)
        {
            string pushBackendAddress;
            lock (sessions)
            {
                if (sessions.TryGetValue(request.TargetUserId, out pushBackendAddress) == false)
                {
                    return new PushResponse { Result = PushResponse.Types.ResultType.NotExist };
                }
            }

            // session 에 기록된 push server 로 메시지 보내기
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(pushBackendAddress);
            var client = new UserSession.UserSessionClient(channel);
            return await client.PushFAsync(request, header);
        }
        #endregion

        #region rpc - frontend listen
        public override Task<KickUserResponse> KickUserF(KickUserRequest request, ServerCallContext context)
        {
            logger.LogInformation($"force to disconnect user : {request.UserId}");
            if (push.Disconnect(request.UserId) == false)
            {
                logger.LogWarning($"user not found to disconnect : {request.UserId}");
                return Task.FromResult(new KickUserResponse { Result = KickUserResponse.Types.ResultType.NotExist });
            }
            return Task.FromResult(new KickUserResponse { Result = KickUserResponse.Types.ResultType.Ok });
        }

        public override async Task<PushResponse> PushF(PushRequest request, ServerCallContext context)
        {
            if (await push.SendMessage(request.TargetUserId, request.ToResponse()))
            {
                return new PushResponse { Result = PushResponse.Types.ResultType.Ok };
            }

            return new PushResponse { Result = PushResponse.Types.ResultType.NotExist };
        }

        public override Task<IsOnlineResponse> IsOnlineF(IsOnlineRequest request, ServerCallContext context)
        {
            return Task.FromResult(
                new IsOnlineResponse
                {
                    Result = push.Exists(request.UserId) ? IsOnlineResponse.Types.ResultType.Online : IsOnlineResponse.Types.ResultType.Offline
                }
            );
        }
        #endregion
    }

    public static class PushResponseExtension
    {
        public static K2.PushResponse ToResponse(this PushRequest request)
        {
            return new K2.PushResponse
            {
                Type = (K2.PushResponse.Types.PushType)(request.PushMessage.Type),
                Message = request.PushMessage.Message,
                Extra = request.PushMessage.Extra
            };
        }
    }
}
