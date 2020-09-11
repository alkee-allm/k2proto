using Grpc.Core;
using K2B;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace K2svc.Backend
{
    public class SessionManagerBackend
        : SessionManager.SessionManagerBase
    {
        public class Config
        {
        }

        private readonly ILogger<SessionManagerBackend> logger;
        private readonly Metadata header;
        private readonly Net.GrpcClients clients;

        private static Dictionary<string/*userId*/, string/*pushBackendAddress*/> sessions = new Dictionary<string, string>();

        public SessionManagerBackend(ILogger<SessionManagerBackend> _logger, Metadata _header, Net.GrpcClients _clients)
        {
            logger = _logger;
            header = _header;
            clients = _clients;
        }

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

            var client = clients.GetClient<SessionHost.SessionHostClient>(pushBackendAddress);
            return await client.KickUserAsync(request, header);
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
            var client = clients.GetClient<SessionHost.SessionHostClient>(pushBackendAddress);
            return await client.PushAsync(request, header);
        }
    }
}
