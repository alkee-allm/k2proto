using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
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
            var server = new Server
            {
                BackendListeningAddress = request.BackendListeningAddress,
                FrontendListeningAddress = request.FrontendListeningAddress,
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
                if (servers.Any((s) => s.BackendListeningAddress == request.BackendListeningAddress))
                {
                    rsp.Result = RegisterResponse.Types.ResultType.DuplicatedBackendListeningAddress;
                    return Task.FromResult(rsp);
                }
                if (servers.Any((s) => s.FrontendListeningAddress == request.FrontendListeningAddress))
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
            if (server.HasServerManagement) config.ServerManagementBackendAddress = server.BackendListeningAddress; // ServerManagementBackendAddress 는 고정이긴 할텐데..
            if (server.HasUserSession) config.UserSessionBackendAddress = server.BackendListeningAddress; // update backend service

            // response
            rsp.Result = RegisterResponse.Types.ResultType.Ok;
            rsp.ServerManagementAddress = config.ServerManagementBackendAddress;
            rsp.UserSessionAddress = config.UserSessionBackendAddress;
            return Task.FromResult(rsp);
        }

        public override Task<Null> Unregister(UnregisterRequest request, ServerCallContext context)
        {
            lock (servers)
            {
                var removed = servers.RemoveAll((s) => (request.BackendListeningAddress == s.BackendListeningAddress));
                if (removed != 1) logger.LogWarning($"{removed} server removed. BackendListeningAddress: {request.BackendListeningAddress}");
            }

            return Task.FromResult(new Null());
        }

        public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
        {
            lock (servers)
            {
                var i = servers.FindIndex((s) => s.BackendListeningAddress == request.BackendListeningAddress);
                if (i < 0)
                {
                    logger.LogInformation($"{request.BackendListeningAddress} server requested ping, but NOT in the list");
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
            public string BackendListeningAddress { get; set; } // 고유해야할 것(Id 처럼 사용)
            public string FrontendListeningAddress { get; set; } // null 인 경우 backend 전용

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
                return BackendListeningAddress == BackendListeningAddress;
            }
        }
    }
}
