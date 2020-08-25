using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Grpc.Core;
using K2B;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K2svc.Backend
{
    public class ServerManagementBackend : ServerManagement.ServerManagementBase
    {
        private readonly ILogger<ServerManagementBackend> logger;
        private readonly ServiceConfiguration config;
        private readonly IHostApplicationLifetime life;
        private readonly Frontend.PushService.Singleton push;
        private readonly Metadata header;

        private static List<Server> servers = new List<Server>(); // server 수가 많지 않고, register/unregister 가 빈번하지 않으므로 별도의 index 는 필요 없을 것

        public ServerManagementBackend(ILogger<ServerManagementBackend> _logger,
            ServiceConfiguration _config,
            IHostApplicationLifetime _life, 
            Frontend.PushService.Singleton _push,
            Metadata _header)
        {
            logger = _logger;
            config = _config;
            life = _life;
            push = _push;
            header = _header;
        }

        #region rpc - backend listen
        public override Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            // TODO: server management here.
            var server = new Server
            {
                Id = "dev",
                PushBackendAddress = "http://localhost:5000",
                HasFrontend = true,
                HasUserSessionBackend = true,

                LastPingTime = DateTime.Now,
            };

            lock (servers) servers.Add(server);
            return Task.FromResult(new RegisterResponse
            {
                Ok = true,
                ServerId = server.Id,

                PushBackendAddress = server.PushBackendAddress,
                EnableUserSession = server.HasUserSessionBackend,
                UserSessionBackendAddress = config.ServerManagementBackendAddress,
            });
        }

        public override Task<Null> Unregister(UnregisterRequest request, ServerCallContext context)
        {
            var server = FindServer(request.ServerId);
            lock (servers) servers.Remove(server);

            return Task.FromResult(new Null());
        }

        public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
        {
            lock (servers)
            {
                var i = servers.FindIndex((s) => s.Id == request.ServerId);
                if (i < 0)
                {
                    logger.LogInformation($"{request.ServerId} server requested ping, but NOT in the list");
                    return Task.FromResult(new PingResponse { Ok = false });
                }
                // ping 순서대로 정렬 유지해 list 뒤쪽에 가장 오래되어 처리가 필요한 server 가 존재할 수 있도록
                var server = servers[i];
                servers.RemoveAt(i);
                server.LastPingTime = DateTime.Now;
                servers.Insert(0, server);
            }

            return Task.FromResult(new PingResponse { Ok = true });
        }

        public override async Task<Null> Broadcast(PushRequest request, ServerCallContext context)
        {
            List<Server> all;
            lock (servers) all = new List<Server>(servers);
            foreach (var s in all)
            {
                if (string.IsNullOrEmpty(s.PushBackendAddress)) continue;
                using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(s.PushBackendAddress);
                var client = new ServerManagement.ServerManagementClient(channel);
                await client.BroadcastFAsync(request, header);
            }
            return new Null();
        }
        #endregion

        #region rpc - frontend listen
        public override Task<Null> StopF(Null request, ServerCallContext context)
        {
            // TODO: 리소스 정리

            life.StopApplication();
            return Task.FromResult(new Null());
        }

        public override async Task<Null> BroadcastF(PushRequest request, ServerCallContext context)
        {
            var message = request.ToResponse();
            var count = await push.SendMessageToAll(message);
            logger.LogInformation($"{count} broadcasted message : ", message.Message);
            return new Null();
        }
        #endregion

        private static Server FindServer(string id)
        {
            lock (servers)
            {
                return servers.Find((s) => (id == s.Id));
            }
        }

        private struct Server : IEquatable<Server>
        { // thread safety 를 위해 struct
            public string Id { get; set; }
            public string PushBackendAddress { get; set; }
            public DateTime LastPingTime { get; set; }
            public int Population { get; set; }

            // service types
            public bool HasFrontend { get; set; }
            //public bool HasServerManagementBackend { get; set; } // servermanagement 여부는 servermanagement 에서 결정할 수 없다
            public bool HasUserSessionBackend { get; set; }

            public bool Equals([AllowNull] Server other)
            { // List.Remove 에 사용하기 위해 IEquatable
                return this.Id == other.Id;
            }
        }
    }
}
