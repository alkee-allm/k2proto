using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using K2B;
using Microsoft.EntityFrameworkCore.Internal;
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
        private static string serverManagementBackendAddress = null;
        private static string userSessionBackendAddress = null;

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

        private static string GetPeerIp(ServerCallContext context)
        {
            const char SEPARATOR = ':';
            var source = context.Peer; // "ipv4:ip:port" or "ipv6:[ip]:port" ; ipv6 url 주소에서는 [,] 가 쓰임. https://tools.ietf.org/html/rfc2732
            var sep1 = source.IndexOf(SEPARATOR);
            var sep2 = source.LastIndexOf(SEPARATOR);
            //var version = source.Substring(0, sep1);
            var ip = source.Substring(sep1 + 1, sep2 - sep1 - 1);
            return ip;
        }

        #region rpc - backend listen
        public override Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            var server = new Server
            {
                ServerId = request.ServerId,
                ListeningPort = request.ListeningPort,
                PublicIpAddress = request.PublicIp,
                ServiceScheme = request.ServiceScheme,
                PrivateIpAddress = GetPeerIp(context),
                HasServerManagement = request.HasServerManagement,
                HasUserSession = request.HasUserSession,

                HasPush = request.HasPush,
                HasInit = request.HasInit,
                HasPushSample = request.HasPushSample,
                HasSimpleSample = request.HasSimpleSample,

                // statistics
                LastPingTime = DateTime.Now,
                Population = 0,
            };


            var rsp = new RegisterResponse();
            // validation
            if (request.Version != System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString())
            {
                rsp.Result = RegisterResponse.Types.ResultType.InvalidVersion;
                return Task.FromResult(rsp);
            }

            lock (servers)
            {
                if (servers.Any((s) => s.BackendListeningAddress == server.BackendListeningAddress))
                {
                    rsp.Result = RegisterResponse.Types.ResultType.DuplicatedBackendListeningAddress;
                    return Task.FromResult(rsp);
                }
                if (servers.Any((s) => s.FrontendListeningAddress == server.FrontendListeningAddress))
                {
                    rsp.Result = RegisterResponse.Types.ResultType.DuplicatedFrontendListeningAddress;
                    return Task.FromResult(rsp);
                }
                // unique backend services
                if (request.HasServerManagement && servers.Any((s) => s.HasServerManagement))
                {
                    rsp.Result = RegisterResponse.Types.ResultType.AlreadyHasServerManagement;
                    return Task.FromResult(rsp);
                }
                if (request.HasUserSession && servers.Any((s) => s.HasUserSession))
                {
                    rsp.Result = RegisterResponse.Types.ResultType.AlreadyHasUserSeesion;
                    return Task.FromResult(rsp);
                }

                // good to add
                servers.Add(server);
            }

            // unique backend service 가 연결된 경우 관리서버의 설정을 업데이트
            if (server.HasServerManagement) serverManagementBackendAddress = server.BackendListeningAddress;
            if (server.HasUserSession) userSessionBackendAddress = server.BackendListeningAddress;

            // response
            rsp.Result = RegisterResponse.Types.ResultType.Ok;
            rsp.BackendListeningAddress = server.BackendListeningAddress;
            rsp.ServerManagementAddress = serverManagementBackendAddress;
            rsp.UserSessionAddress = userSessionBackendAddress;
            return Task.FromResult(rsp);
        }

        public override Task<Null> Unregister(UnregisterRequest request, ServerCallContext context)
        {
            lock (servers)
            {
                var removed = servers.RemoveAll((s) => (request.ServerId == s.ServerId));
                if (removed != 1) logger.LogWarning($"{removed} server removed by serverId: {request.ServerId}");
            }

            return Task.FromResult(new Null());
        }

        public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
        {
            lock (servers)
            {
                var i = servers.FindIndex((s) => s.ServerId == request.ServerId);
                if (i < 0)
                {
                    logger.LogInformation($"{request.ServerId} server requested ping, but NOT in the list");
                    return Task.FromResult(new PingResponse { Ok = false });
                }
                // ping 순서대로 정렬 유지해 list 뒤쪽에 가장 오래되어 처리가 필요한 server 가 존재할 수 있도록
                var server = servers[i];
                servers.RemoveAt(i);

                // update server statistics
                server.LastPingTime = DateTime.Now;
                server.Population = request.Population;
                server.CpuUsagePercent = request.CpuUsagePercent;
                server.MemoryUsage = request.MemoryUsage;
                server.FreeHddBytes = request.FreeHddBytes;
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
                if (string.IsNullOrEmpty(s.BackendListeningAddress)) continue;
                using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(s.BackendListeningAddress);
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

        private struct Server : IEquatable<Server>
        { // thread safety 를 위해 struct

            #region recent status
            public DateTime LastPingTime { get; set; }
            public uint Population { get; set; }
            public uint CpuUsagePercent { get; set; }
            public ulong MemoryUsage { get; set; }
            public ulong FreeHddBytes { get; set; }
            #endregion

            #region configuration
            public string ServerId { get; set; }
            public string ServiceScheme { get; set; }
            public int ListeningPort { get; set; }
            public string PublicIpAddress { get; set; } // null 인 경우 backend 전용
            public string PrivateIpAddress { get; set; }
            internal string BackendListeningAddress => $"{ServiceScheme}://{PrivateIpAddress}:{ListeningPort}";
            internal string FrontendListeningAddress => string.IsNullOrEmpty(PublicIpAddress) ? null : $"{ServiceScheme}://{PublicIpAddress}:{ListeningPort}";

            // services
            public bool HasServerManagement { get; set; }
            public bool HasUserSession { get; set; }

            public bool HasPush { get; set; }
            public bool HasInit { get; set; }
            public bool HasPushSample { get; set; }
            public bool HasSimpleSample { get; set; }
            #endregion

            public bool Equals([AllowNull] Server other)
            { // List.Remove 에 사용하기 위해 IEquatable
                return ServerId == other.ServerId;
            }

            public static string GetServerAddress(string scheme, string ipAddress, int port)
            {
                return $"{scheme}://{ipAddress}:{port}";
            }
        }
    }
}
