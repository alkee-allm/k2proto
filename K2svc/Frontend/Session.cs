using Grpc.Core;
using K2B;
using System;
using System.Threading.Tasks;

namespace K2svc.Frontend
{
    // gRPC ServerCallContext 를 이용해 user 정보를 얻어오는 도구 함수 모음
    static class Session
    {
        // invalid 한 user id 또는 pushBackendAddress 가 아니라면 throws
        internal static (string userId, string pushBackendAddress)
            GetUserInfoOrThrow(ServerCallContext context)
        {
            var user = GetUserInfo(context);
            if (string.IsNullOrEmpty(user.userId))
                throw new ApplicationException($"invalid session state of the user : {context.RequestHeaders}");
            if (string.IsNullOrEmpty(user.pushBackendAddress))
                throw new ApplicationException($"unidentified push service of user : {user.userId}");
            return user;
        }

        // push-server 에 올바르게 붙어 있는 user id 정보를 context 로부터 얻어온다. 그렇지 못하면 throws
        internal static async Task<(string userId, string pushBackendAddress)>
            GetOnlineUserInfoOrThrow(ServerCallContext context, Metadata backendHeader)
        {
            var user = GetUserInfoOrThrow(context);
            if (await IsOnline(user.userId, user.pushBackendAddress, backendHeader) == false)
                throw new ApplicationException($"offline(not push connected) user : {user.userId} on {user.pushBackendAddress}");
            return user;
        }

        internal static (string userId, string pushBackendAddress) GetUserInfo(ServerCallContext context)
        {
            var userId = context.GetHttpContext().User?.Identity?.Name;
            var pushBackendAddress = context.GetHttpContext().User.FindFirst(System.Security.Claims.ClaimTypes.System)?.Value;
            return (userId, pushBackendAddress);
        }

        internal static async Task<bool> IsOnline(string userId, string pushBackendAddress, Metadata backendHeader)
        {
            using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(pushBackendAddress);
            var client = new SessionHost.SessionHostClient(channel); // 연결되어있는 서버(Host)에 직접 호출
            var response = await client.IsOnlineAsync(new IsOnlineRequest { UserId = userId }, backendHeader);
            return response.Result == IsOnlineResponse.Types.ResultType.Online;
        }
    }
}
