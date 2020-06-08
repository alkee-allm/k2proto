using Microsoft.AspNetCore.Authorization;
using K2;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Grpc.Core;
using System;
using System.Security.Claims;

namespace K2svc.Frontend
{
    [Authorize]
    public class PushSampleService : PushSample.PushSampleBase
    {
        private readonly ILogger<PushSampleService> logger;
        private readonly ServiceConfiguration config;

        public PushSampleService(ILogger<PushSampleService> _logger, ServiceConfiguration _config)
        {
            logger = _logger;
            config = _config;
        }

        #region rpc
        public override async Task<Null> Broadacast(BroadacastRequest request, ServerCallContext context)
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.ServerManagementServiceAddress);
            var client = new K2B.ServerManagement.ServerManagementClient(channel);
            await client.BroadcastAsync(new K2B.PushRequest
            {
                PushMessage = new K2B.PushRequest.Types.PushResponse
                {
                    Type = K2B.PushRequest.Types.PushResponse.Types.PushType.Message,
                    Message = request.Message
                }
            });
            return new Null();
        }

        public override async Task<Null> Message(MessageRequest request, ServerCallContext context)
        {
            var userId = context.GetHttpContext().User.Identity.Name ?? "";
            if (string.IsNullOrEmpty(userId)) throw new ApplicationException($"invalid session state of the user : {context.RequestHeaders}");

            // UserSessionService 로 보내기
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.UserSessionServiceAddress);
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
            });
            logger.LogInformation($"kick result = {result.Result}");

            return new Null();
        }

        public override async Task<Null> Hello(Null request, ServerCallContext context)
        {
            var userId = context.GetHttpContext().User.Identity.Name ?? "";
            var pushBackendAddress = context.GetHttpContext().User.FindFirst(ClaimTypes.System)?.Value ?? "";
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(pushBackendAddress))
            {
                logger.LogWarning($"invalid user information : {userId} / {pushBackendAddress}");
                return new Null();
            }

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
            });
            return new Null();
        }

        public override async Task<Null> Kick(KickRequest request, ServerCallContext context)
        {
            // UserSessionService 로 보내기
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.UserSessionServiceAddress);
            var client = new K2B.UserSession.UserSessionClient(channel);
            var result = await client.KickUserAsync(new K2B.KickUserRequest { UserId = request.Target });
            logger.LogInformation($"kick result = {result.Result}");

            return new Null();
        }
        #endregion
    }
}
