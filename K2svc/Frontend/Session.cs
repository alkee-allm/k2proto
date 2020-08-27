using Grpc.Core;
using K2B;
using System;
using System.Threading.Tasks;

namespace K2svc.Frontend
{
    static class Session
    {
        // push-server 에 올바르게 붙어 있는 user id 정보를 context 로부터 얻어온다
        internal static async Task<string> GetOnlineUserId(ServerCallContext context, Metadata header)
        { // TODO: https://github.com/alkee-allm/k2proto/issues/12#issuecomment-645863822
            var userId = context.GetHttpContext().User?.Identity?.Name;
            if (string.IsNullOrEmpty(userId)) throw new ApplicationException($"invalid session state of the user : {context.RequestHeaders}");

            var pushBackendAddress = context.GetHttpContext().User.FindFirst(System.Security.Claims.ClaimTypes.System)?.Value ?? "";
            if (pushBackendAddress == "") throw new ApplicationException($"unidentified push service of user : {userId}");

            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(pushBackendAddress);
            var client = new UserSession.UserSessionClient(channel);
            var response = await client.IsOnlineFAsync(new IsOnlineRequest { UserId = userId }, header);
            if (response.Result == IsOnlineResponse.Types.ResultType.Offline) throw new ApplicationException($"offline(not push connected) user : {userId}");
            return userId;
        }
    }
}
