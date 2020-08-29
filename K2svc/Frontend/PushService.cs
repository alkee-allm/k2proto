using Grpc.Core;
using K2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace K2svc.Frontend
{
    [Authorize]
    public class PushService
        : Push.PushBase
    {
        private readonly ILogger<PushService> logger;
        private readonly ServiceConfiguration config;
        private readonly Metadata header;

        public PushService(ILogger<PushService> _logger, ServiceConfiguration _config, Metadata _header)
        {
            logger = _logger;
            config = _config;
            header = _header;
        }

        #region rpc
        public override async Task PushBegin(Null request, IServerStreamWriter<PushResponse> responseStream, ServerCallContext context)
        {
            var userId = context.GetHttpContext().User.Identity.Name ?? "";
            if (string.IsNullOrEmpty(userId)) throw new ApplicationException($"invalid session state of the user : {context.RequestHeaders}");

            logger.LogInformation($"begining push service for : {userId}");
            using (var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.UserSessionBackendAddress))
            {
                var client = new K2B.SessionManager.SessionManagerClient(channel);
                var result = await client.AddUserAsync(new K2B.AddUserRequest
                {
                    Force = true, // 항상 성공
                    BackendListeningAddress = config.BackendListeningAddress,
                    UserId = userId,
                }, header);
                logger.LogInformation($"adding user({userId}) {result.Result} to session backend : {config.BackendListeningAddress}");
            }

            var streamCanceller = new CancellationTokenSource();
            users.Add(userId, responseStream, context, streamCanceller);

            var newJwt = GenerateJwtToken(userId, config.BackendListeningAddress);
            await responseStream.WriteAsync(new PushResponse
            {
                Type = PushResponse.Types.PushType.Config,
                Message = "jwt",
                Extra = newJwt
            });

            while (!context.CancellationToken.IsCancellationRequested & !streamCanceller.IsCancellationRequested)
            { // holding up the stream
                await Task
                    .Delay(DefaultValues.STREAM_RESPONSE_TIME_MILLISECOND)
                    .ConfigureAwait(false); // 어느 thread 에서나 task 실행 가능
            }

            logger.LogInformation($"ending push service for : {userId}");

            using (var channel = Grpc.Net.Client.GrpcChannel.ForAddress(config.UserSessionBackendAddress))
            {
                var client = new K2B.SessionManager.SessionManagerClient(channel);
                var result = await client.RemoveUserAsync(new K2B.RemoveUserRequest { BackendListeningAddress = config.BackendListeningAddress, UserId = userId }, header);
                logger.LogInformation($"removing user({userId}) to session backend : {result}");
            }
            users.Remove(userId);
        }
        #endregion

        private static string GenerateJwtToken(string id, string pushBackendAddress, ILogger logger = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("invalid id", nameof(id));

            var claims = new[] { new Claim(ClaimTypes.Name, id), new Claim(ClaimTypes.System, pushBackendAddress) };
            var credentials = new SigningCredentials(Security.SecurityKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken("k2server", "k2client", claims, signingCredentials: credentials);

            // 새버전의 문서에는 없는 내용이긴 한데, https://docs.microsoft.com/en-us/previous-versions/visualstudio/dn464181(v=vs.114)?redirectedfrom=MSDN#thread-safety
            // thread safety 가 걱정되므로 wtSecurityTokenHandler 를 항상 새 instance 생성해 사용
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            logger?.LogInformation($"creating jwt for {id} ; {tokenString}");
            return tokenString;
        }

        #region external push handler
        private static readonly PushStreamDb users = new PushStreamDb();
        internal static IPushable Pusher => users;
        internal interface IPushable
        { // PushService 외부 서비스에서 사용가능하도록 노출
            Task<bool> SendMessage(string targetUserId, PushResponse message);
            Task<bool> SendMessage(string targetUserId, K2B.PushRequest message); // backend 요청을 client 응답(K2.PushResponse)으로 내부변환
            Task<int> SendMessageToAll(PushResponse message);
            Task<int> SendMessageToAll(K2B.PushRequest message); // backend 요청을 client 응답(K2.PushResponse)으로 내부변환
            bool Disconnect(string targetUserId);
            bool IsConnected(string targetUserId);
        }
        internal class PushStreamDb
            : IPushable
        {
            private readonly Dictionary<string, User> users = new Dictionary<string, User>();

            #region IPushable 구현
            public async Task<bool> SendMessage(string targetUserId, PushResponse message)
            {
                User user;
                lock (users)
                {
                    if (users.TryGetValue(targetUserId, out user) == false)
                        return false;
                }
                await user.Stream.WriteAsync(message);
                return true;
            }
            public Task<bool> SendMessage(string targetUserId, K2B.PushRequest message) => SendMessage(targetUserId, ToResponse(message));

            public async Task<int> SendMessageToAll(PushResponse message)
            {
                List<User> targets;
                lock (users)
                {
                    targets = new List<User>(users.Values);
                }
                foreach (var t in targets)
                    await t.Stream.WriteAsync(message);
                return targets.Count;
            }
            public Task<int> SendMessageToAll(K2B.PushRequest message) => SendMessageToAll(ToResponse(message));

            public bool Disconnect(string userId)
            {
                lock (users)
                {
                    if (users.TryGetValue(userId, out User user))
                    {
                        user.Canceller.Cancel();
                        return true;
                    }
                }
                return false;
            }

            public bool IsConnected(string userId)
            {
                return users.ContainsKey(userId);
            }
            #endregion

            internal void Add(string userId, IServerStreamWriter<PushResponse> stream, ServerCallContext context, CancellationTokenSource streamCanceller)
            {
                var user = new User
                {
                    Id = userId,
                    Stream = stream,
                    Context = context,
                    Canceller = streamCanceller
                };
                lock (users) users.Add(userId, user);
            }

            internal void Remove(string user)
            {
                lock (users) users.Remove(user);
            }

            private static K2.PushResponse ToResponse(K2B.PushRequest req)
            {
                return new K2.PushResponse
                { // request to response ; Manager 로부터 받은(request) 데이터를 그대로 클라이언트에 전달(response) 
                    Type = (K2.PushResponse.Types.PushType)(req.PushMessage.Type),
                    Message = req.PushMessage.Message,
                    Extra = req.PushMessage.Extra
                };
            }


            private class User
            {
                public string Id { get; set; }
                public IServerStreamWriter<PushResponse> Stream { get; set; }
                public ServerCallContext Context { get; set; }
                public CancellationTokenSource Canceller { get; set; }
            }
        }
        #endregion
    }
}
