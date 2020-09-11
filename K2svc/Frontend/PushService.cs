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
using System.Threading.Channels;
using System.Threading.Tasks;

namespace K2svc.Frontend
{
    [Authorize]
    public class PushService
        : Push.PushBase
    {
        public class Config
        {
            public int StreamResponseDelayMillisecond { get; set; } = 200;
        }

        private readonly ILogger<PushService> logger;
        private readonly K2Config localConfig;
        private readonly RemoteConfig remoteConfig;
        private readonly Metadata header;
        private readonly Net.GrpcClients clients;

        public PushService(ILogger<PushService> _logger,
            K2Config _localConfig,
            RemoteConfig _remoteConfig,
            Metadata _header,
            Net.GrpcClients _clients)
        {
            logger = _logger;
            localConfig = _localConfig;
            remoteConfig = _remoteConfig;
            header = _header;
            clients = _clients;
        }

        #region rpc
        public override async Task PushBegin(Null request, IServerStreamWriter<PushResponse> responseStream, ServerCallContext context)
        {
            var userId = context.GetHttpContext().User.Identity.Name ?? "";
            if (string.IsNullOrEmpty(userId)) throw new ApplicationException($"invalid session state of the user : {context.RequestHeaders}");

            logger.LogInformation($"begining push service for : {userId}");

            try
            {
                var client = clients.GetClient<K2B.SessionManager.SessionManagerClient>(remoteConfig.SessionManagerAddress);
                var addUserResult = await client.AddUserAsync(new K2B.AddUserRequest
                {
                    Force = true, // 항상 성공
                    BackendListeningAddress = remoteConfig.BackendListeningAddress,
                    UserId = userId,
                }, header);
                logger.LogInformation($"adding user({userId}) {addUserResult.Result} to session backend : {remoteConfig.BackendListeningAddress}");
            }
            catch (RpcException ex)
            {
                logger.LogWarning($"Unable to add user({userId}) to SessionManager : {ex}");
            }

            var streamCanceller = new CancellationTokenSource();
            users.Add(userId, responseStream, context, streamCanceller);

            // thread-safety 문제가 있으므로 responseStream 을 직접 사용하지 않도록(#46)
            await users.SendMessage(userId, new PushResponse
            {
                Type = PushResponse.Types.PushType.Config,
                Message = "jwt",
                Extra = GenerateJwtToken(userId, remoteConfig.BackendListeningAddress) // new JWT
            });

            while (!context.CancellationToken.IsCancellationRequested & !streamCanceller.IsCancellationRequested)
            { // holding up the stream
                await Task
                    .Delay(localConfig.PushService.StreamResponseDelayMillisecond) // 간편한 방법. awaiter 를 추가해 구현하려면 ; https://medium.com/@cilliemalan/how-to-await-a-cancellation-token-in-c-cbfc88f28fa2
                    .ConfigureAwait(false); // 어느 thread 에서나 task 실행 가능
            }
            if (streamCanceller.IsCancellationRequested == false) streamCanceller.Cancel();

            logger.LogInformation($"ending push service for : {userId}");

            // session server 가 변경되었을 수 있기 때문에 다시 할당
            try
            {
                var client = clients.GetClient<K2B.SessionManager.SessionManagerClient>(remoteConfig.SessionManagerAddress);
                var removeUserResult = await client.RemoveUserAsync(new K2B.RemoveUserRequest { BackendListeningAddress = remoteConfig.BackendListeningAddress, UserId = userId }, header);
                logger.LogInformation($"removing user({userId}) to session backend : {removeUserResult}");
            }
            catch (RpcException ex)
            {
                logger.LogWarning($"Unable to remove user({userId}) from SessionManager : {ex}");
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

            void SyncUsersToSessionManager(K2B.SessionManager.SessionManagerClient target, string backendAddress, Metadata headers, ILogger logger);
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
                await user.ResponseQueue.WriteAsync(message, user.Canceller.Token);
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
                    await t.ResponseQueue.WriteAsync(message, t.Canceller.Token);
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
                lock (users)
                    return users.ContainsKey(userId);
            }

            public void SyncUsersToSessionManager(K2B.SessionManager.SessionManagerClient target, string backendAddress, Metadata headers, ILogger logger)
            { // SessionManager 가 연결이 끊겼다가 복구 되었을 경우에만 사용. 이 인터페이스는 마음에 들지 않은데..
                try
                {
                    lock (users) // SessionManager 에 모든 정보가 입력되기 전까지 local 의 users 가 변경되어서는 안된다.
                        foreach (var u in users)
                        {
                            // 동기화를 유지하기 위해 lock 이 걸려있는상태에서 비동기를 사용할 수는 없다.
                            // 해당 channel 이 사용가능한지 미리 응답을 받을 수 있다면 좋을텐데...
                            target.AddUser(new K2B.AddUserRequest { Force = true, UserId = u.Value.Id, BackendListeningAddress = backendAddress }
                            , headers: headers, deadline: DateTime.UtcNow.AddSeconds(0.5));
                        }
                }
                catch (RpcException ex)
                {
                    logger.LogWarning($"failed to sync users : {ex.Message}");
                }
            }
            #endregion

            internal void Add(string userId, IServerStreamWriter<PushResponse> stream, ServerCallContext context, CancellationTokenSource streamCanceller)
            {
                var user = new User
                {
                    Id = userId,
                    ResponseQueue = new GrpcStreamResponseQueue<PushResponse>(stream, streamCanceller.Token),
                    Context = context,
                    Canceller = streamCanceller
                };
                lock (users) users.Add(userId, user);
            }

            internal void Remove(string user)
            {
                lock (users) users.Remove(user);
            }

            private static PushResponse ToResponse(K2B.PushRequest req)
            {
                return new PushResponse
                { // request to response ; Manager 로부터 받은(request) 데이터를 그대로 클라이언트에 전달(response) 
                    Type = (PushResponse.Types.PushType)(req.PushMessage.Type),
                    Message = req.PushMessage.Message,
                    Extra = req.PushMessage.Extra
                };
            }

            private class User
            {
                public string Id { get; set; }
                public GrpcStreamResponseQueue<PushResponse> ResponseQueue { get; set; } // for thread-safety(#46)
                public ServerCallContext Context { get; set; }
                public CancellationTokenSource Canceller { get; set; }
            }

            /// <summary>
            ///     Wraps <see cref="IServerStreamWriter{T}"/> which only supports one writer at a time.
            ///     This class can receive messages from multiple threads, and writes them to the stream
            ///     one at a time.
            /// </summary>
            /// <typeparam name="T">Type of message written to the stream</typeparam>
            private class GrpcStreamResponseQueue<T> // https://github.com/grpc/grpc-dotnet/issues/579#issuecomment-574056565
            {
                private readonly IServerStreamWriter<T> _stream;
                private readonly Task _consumer;

                private readonly Channel<T> _channel = Channel.CreateUnbounded<T>(
                    new UnboundedChannelOptions
                    {
                        SingleWriter = false,
                        SingleReader = true,
                    });

                public GrpcStreamResponseQueue(
                    IServerStreamWriter<T> stream,
                    CancellationToken cancellationToken = default
                )
                {
                    _stream = stream;
                    _consumer = Consume(cancellationToken);
                }

                /// <summary>
                ///     Asynchronously writes an item to the channel.
                /// </summary>
                /// <param name="message">The value to write to the channel.</param>
                /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> used to cancel the write operation.</param>
                /// <returns>A <see cref="T:System.Threading.Tasks.ValueTask" /> that represents the asynchronous write operation.</returns>
                public async ValueTask WriteAsync(T message, CancellationToken cancellationToken = default)
                {
                    await _channel.Writer.WriteAsync(message, cancellationToken);
                }

                /// <summary>
                ///     Marks the writer as completed, and waits for all writes to complete.
                /// </summary>
                public Task CompleteAsync()
                {
                    _channel.Writer.Complete();
                    return _consumer;
                }

                private async Task Consume(CancellationToken cancellationToken)
                {
                    await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        await _stream.WriteAsync(message);
                    }
                }
            }
        }
        #endregion
    }
}
