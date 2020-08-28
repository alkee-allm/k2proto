using Microsoft.AspNetCore.Authorization;
using K2;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Grpc.Core;

namespace K2svc.Frontend
{
    [Authorize]
    public class PushSampleService : PushSample.PushSampleBase
    {
        private readonly ILogger<PushSampleService> logger;
        private readonly ServiceConfiguration config;
        private readonly Metadata header;

        public PushSampleService(ILogger<PushSampleService> _logger, ServiceConfiguration _config, Metadata _header)
        {
            logger = _logger;
            config = _config;
            header = _header;
        }

        #region rpc
        public override async Task<Null> Broadacast(BroadacastRequest request, ServerCallContext context)
        {
            // 연결된 모든 서버로 메시지를 전송하고 이 서버들에 연결된 모든 유저로 메시지를 전달하는 예시
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.ServerManagementBackendAddress);
            var client = new K2B.ServerManagement.ServerManagementClient(channel);
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
            // 요청을 받은 front-end 서버에서 메시지 전달 대상 유저를 찾고 메시지를 전달하는 예시
            var (userId, pushBackendAddress) = Session.GetUserInfoOrThrow(context);

            // UserSessionService 를 통해 메시지 보내기
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.UserSessionBackendAddress);
            var client = new K2B.UserSession.UserSessionClient(channel);
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
            // 요청을 받은 front-end 서버와 실제 연결되어있는 push-service 가 다른 서버일 수 있다.
            //   따라서, 응답처리가 별도의 push-service 를 통해 이루어져야 하는 경우에 대한 예시

            var (userId, pushBackendAddress) = Session.GetUserInfoOrThrow(context);

            // direct(해당 user가 연결되어있는 push 서버)로 메시지 보내기
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(pushBackendAddress);
            var client = new K2B.UserSession.UserSessionClient(channel);
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
            // UserSessionService 로 보내기
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.UserSessionBackendAddress);
            var client = new K2B.UserSession.UserSessionClient(channel);
            var result = await client.KickUserAsync(new K2B.KickUserRequest { UserId = request.Target }, header);
            logger.LogInformation($"kick result = {result.Result}");

            return new Null();
        }
        #endregion
    }
}
