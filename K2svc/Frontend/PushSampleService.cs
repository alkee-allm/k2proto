using Grpc.Core;
using K2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace K2svc.Frontend
{
    [Authorize]
    public class PushSampleService : PushSample.PushSampleBase
    {
        private readonly ILogger<PushSampleService> logger;
        private readonly ServiceConfiguration config;
        private readonly Metadata header;
        private readonly Net.GrpcClients clients;

        public PushSampleService(ILogger<PushSampleService> _logger, ServiceConfiguration _config, Metadata _header, Net.GrpcClients _clients)
        {
            logger = _logger;
            config = _config;
            header = _header;
            clients = _clients;
        }

        #region rpc
        public override async Task<Null> Broadacast(BroadacastRequest request, ServerCallContext context)
        {
            // 연결된 모든 서버로 메시지를 전송하고 이 서버들에 연결된 모든 유저로 메시지를 전달하는 예시

            var client = clients.GetClient<K2B.ServerManager.ServerManagerClient>(config.ServerManagerAddress);
            await client.BroadcastAsync(new K2B.PushRequest
            {
                PushMessage = new K2B.PushRequest.Types.PushResponse
                {
                    Type = K2B.PushRequest.Types.PushResponse.Types.PushType.Message,
                    Message = request.Message
                }
            }, header);
            return new Null();
        }

        public override async Task<Null> Message(MessageRequest request, ServerCallContext context)
        {
            // 메시지를 전달할 대상(client)이 어느 서버에 연결되어있는지 알 수 없는 경우
            //    UserSessionBackend 를 통해 메시지를 전달(push) 하는 예시

            var (userId, pushBackendAddress) = Session.GetUserInfoOrThrow(context);

            // UserSessionService 를 통해 메시지 보내기
            var client = clients.GetClient<K2B.SessionManager.SessionManagerClient>(config.UserSessionBackendAddress);
            var result = await client.PushAsync(new K2B.PushRequest
            {
                TargetUserId = request.Target,
                PushMessage = new K2B.PushRequest.Types.PushResponse
                {
                    Type = K2B.PushRequest.Types.PushResponse.Types.PushType.Message,
                    Message = request.Message,
                    Extra = userId
                }
            }, header);
            logger.LogInformation($"kick result = {result.Result}");

            return new Null();
        }

        public override async Task<Null> Hello(Null request, ServerCallContext context)
        {
            // 요청받은 user 에게 지연된 결과(matching 등의 결과)를 push 를 통해 전달하는 방법 예시
            //  (요청을 받은 front-end 서버와 실제 연결되어있는 push-service 가 다른 서버)
            //  pushBackendAddress 를 별도 저장해 사용

            var (userId, pushBackendAddress) = Session.GetUserInfoOrThrow(context);

            // direct(해당 user가 연결되어있는 push 서버)로 메시지 보내기
            var client = clients.GetClient<K2B.SessionHost.SessionHostClient>(pushBackendAddress);
            var result = await client.PushAsync(new K2B.PushRequest
            {
                TargetUserId = userId,
                PushMessage = new K2B.PushRequest.Types.PushResponse
                {
                    Type = K2B.PushRequest.Types.PushResponse.Types.PushType.Message,
                    Message = userId,
                    Extra = "HELLO"
                }
            }, header);
            return new Null();
        }

        public override async Task<Null> Kick(KickRequest request, ServerCallContext context)
        {
            // 다른 user 에게 명령을 수행하는 예시. 이 경우 연결이 끊어지도록 하는 명령 예시

            var client = clients.GetClient<K2B.SessionManager.SessionManagerClient>(config.UserSessionBackendAddress);
            var result = await client.KickUserAsync(new K2B.KickUserRequest { UserId = request.Target }, header);
            logger.LogInformation($"kick result = {result.Result}");

            return new Null();
        }
        #endregion
    }
}
