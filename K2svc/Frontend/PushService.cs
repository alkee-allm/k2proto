using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using K2;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace K2svc.Frontend
{
    [Authorize]
    public class PushService : Push.PushBase
    {
        private readonly ILogger<PushService> logger;
        private readonly Singleton users;
        private readonly ServiceConfiguration config;

        public PushService(ILogger<PushService> _logger, ServiceConfiguration _config, Singleton _users)
        {
            logger = _logger;
            config = _config;
            users = _users;
        }

        #region rpc
        public override async Task PushBegin(Null request, IServerStreamWriter<PushResponse> responseStream, ServerCallContext context)
        {
            var userId = context.GetHttpContext().User.Identity.Name ?? "";
            if (string.IsNullOrEmpty(userId)) throw new ApplicationException($"invalid session state of the user : {context.RequestHeaders}");

            logger.LogInformation($"begining push service for : {userId}");
            var user = users.Add(userId, responseStream);

            var newJwt = GenerateJwtToken(userId);
            await responseStream.WriteAsync(new PushResponse
            {
                Type = PushResponse.Types.PushType.Config,
                Message = "jwt",
                Extra = newJwt
            });

            while (!context.CancellationToken.IsCancellationRequested & user.Available)
            { // holding up the stream
                await Task.Delay(1);
            }

            logger.LogInformation($"ending push service for : {userId}");
            users.Remove(userId);
        }
        #endregion

        private string GenerateJwtToken(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("invalid id", nameof(id));

            logger.LogInformation($"creating jwt for {id}");

            var claims = new[] { new Claim(ClaimTypes.Name, id), new Claim(ClaimTypes.System, config.UserSessionServiceAddress) };
            var credentials = new SigningCredentials(InitService.SecurityKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken("k2server", "k2client", claims, signingCredentials: credentials);

            // �������� �������� ���� �����̱� �ѵ�, https://docs.microsoft.com/en-us/previous-versions/visualstudio/dn464181(v=vs.114)?redirectedfrom=MSDN#thread-safety
            // thread safety �� �����ǹǷ� wtSecurityTokenHandler �� �׻� �� instance ������ ���
            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        public class Singleton // ���� naming ������..
        {
            private Dictionary<string, User> users = new Dictionary<string, User>();
            public bool IsAvailable { get { lock (users) return users.Count > 0; } }
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

            public bool Disconnect(string userId)
            {
                // available �� �������ָ�, PushBegin �� async loop ���� �������� �� remove �� ������ ��.

                lock (users)
                {
                    if (users.TryGetValue(userId, out User user))
                    {
                        user.Available = false;
                        return true;
                    }
                }
                return false;
            }

            public bool Exists(string user)
            {
                return users.ContainsKey(user);
            }

            public User Add(string userId, IServerStreamWriter<PushResponse> stream)
            {
                var user = new User
                {
                    Id = userId,
                    Stream = stream,
                    Available = true,
                };
                lock (users) users.Add(userId, user);
                return user;
            }

            public void Remove(string user)
            {
                lock (users) users.Remove(user);
            }

            public class User
            {
                public string Id { get; set; }
                public IServerStreamWriter<PushResponse> Stream { get; set; }
                public bool Available { get; set; }
            }
        }
    }

}
