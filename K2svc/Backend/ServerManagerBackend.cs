using Grpc.Core;
using K2B;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace K2svc.Backend
{
    public class ServerManagerBackend
        : ServerManager.ServerManagerBase
    {
        private readonly ILogger<ServerManagerBackend> logger;
        private readonly Metadata header;
        private readonly Net.GrpcClients clients;

        private static List<Server> servers = new List<Server>(); // server 수가 많지 않고, register/unregister 가 빈번하지 않으므로 별도의 index 는 필요 없을 것

        #region internal states for ServerManager
        private static string SERVER_NOT_EXIST = ""; // null 의 의미이지만 gRPC message 에 string null 을 사용할 수 없음('System.ArgumentNullException'(Google.Protobuf.dll))
        private static object internalStateGuard = new object();
        private static string serverManagementBackendAddress = SERVER_NOT_EXIST; 
        private static string userSessionBackendAddress = SERVER_NOT_EXIST;
        #endregion

        public ServerManagerBackend(ILogger<ServerManagerBackend> _logger,
            Metadata _header,
            Net.GrpcClients _clients)
        {
            // ** ServiceConfiguration 사용 금지 **
            //   ServerManagement 가 다른 서비스들과 함께 서비스 될 수 있기 때문에 ServiceConfiguration 을 사용하게되면
            //   단독으로 사용되는 경우와 설정이 달라지는 상황(singleton)에 대한 디버깅이나 구현이 어렵다.

            logger = _logger;
            header = _header;
            clients = _clients;
        }

        #region RPC
        public override Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            var server = new Server
            {
                ServerId = request.ServerId,
                ListeningPort = request.ListeningPort,
                PublicIpAddress = request.PublicIp,
                ServiceScheme = request.ServiceScheme,
                PrivateIpAddress = Util.GetGrpcPeerIp(context),
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
                // 서비스 주소는 netwrok 내에서 중복되지 않아야 함
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

                // unique backend services 는 network 내에서 유일해야 함
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
            OnServerAdd(server);

            // response
            rsp.Result = RegisterResponse.Types.ResultType.Ok;
            rsp.BackendListeningAddress = server.BackendListeningAddress;
            rsp.ServerManagementAddress = serverManagementBackendAddress;
            rsp.UserSessionAddress = userSessionBackendAddress;
            return Task.FromResult(rsp);
        }

        public override Task<Null> Unregister(UnregisterRequest request, ServerCallContext context)
        {
            Server removed;
            lock (servers)
            {
                var i = servers.FindIndex((s) => (request.ServerId == s.ServerId));
                if (i < 0)
                {
                    logger.LogWarning($"the server is not registered. serverId: {request.ServerId}");
                    return Task.FromResult(new Null());
                }
                removed = servers[i];
                servers.RemoveAt(i);
            }
            OnServerRemove(removed);

            return Task.FromResult(new Null());
        }

        public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
        {
            var timedOutServers = new List<Server>();
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

                // find timed out servers
                var expried = DateTime.Now.Subtract(TimeSpan.FromSeconds(DefaultValues.SERVER_MANAGEMENT_PING_TIMEOUT_SECONDS));
                while (servers.Count > 0 && servers.Last().LastPingTime < expried)
                {
                    timedOutServers.Add(servers.Last());
                    servers.RemoveAt(servers.Count - 1); // remove last(oldest)
                }
            }

            // timeed out server process
            foreach (var timedOutServer in timedOutServers)
            {
                OnServerRemove(timedOutServer);
                OnServerPingTimeOut(timedOutServer);
            }

            return Task.FromResult(new PingResponse { Ok = true });
        }

        public override async Task<Null> Broadcast(PushRequest request, ServerCallContext context)
        {
            List<Server> all;
            lock (servers) all = new List<Server>(servers);
            foreach (var s in all)
            {
                if (s.HasPush == false) continue;
                var client = clients.GetClient<ServerHost.ServerHostClient>(s.BackendListeningAddress);
                await client.BroadcastAsync(request, header);
            }
            return new Null();
        }
        #endregion

        private void OnServerAdd(Server server)
        {
            // unique backend service 가 연결된 경우 관리서버의 설정을 업데이트
            lock (internalStateGuard)
            {
                if (server.HasServerManagement) serverManagementBackendAddress = server.BackendListeningAddress;
                if (server.HasUserSession) userSessionBackendAddress = server.BackendListeningAddress;
            }
        }

        private void OnServerRemove(Server server)
        {
            // backend unique server 인 경우 상태 정보 업데이트
            lock (internalStateGuard)
            {
                if (server.HasServerManagement) serverManagementBackendAddress = SERVER_NOT_EXIST;
                if (server.HasUserSession) userSessionBackendAddress = SERVER_NOT_EXIST;
            }
        }

        private void OnServerPingTimeOut(Server server)
        {
            logger.LogWarning($"PING TIMEDOUT server {server.ServerId}\n\tfrontend: {server.FrontendListeningAddress}\n\tbackend: {server.BackendListeningAddress}");

            // ping timoue 이더라도 backed 가 살아있을 수 있으므로 강제 종료시켜, 사용자 및 서버의 데이터가 일치하지 않아
            //  발생할 수 있는 예측불가능한 문제를 차단
            var client = clients.GetClient<ServerHost.ServerHostClient>(server.BackendListeningAddress);
            try
            {
                client.Stop(new StopRequest { Reason = "PING TIMEOUT" }, deadline: DateTime.UtcNow.AddSeconds(1));
                logger.LogWarning($"server was alive. STOP rpc called. {server.ServerId}/{server.BackendListeningAddress}");
            }
            catch (RpcException) { }
        }

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
            // backend services
            public bool HasServerManagement { get; set; }
            public bool HasUserSession { get; set; }

            // frontend services
            public bool HasPush { get; set; }
            public bool HasInit { get; set; }
            public bool HasPushSample { get; set; }
            public bool HasSimpleSample { get; set; }
            #endregion

            internal string BackendListeningAddress => $"{ServiceScheme}://{PrivateIpAddress}:{ListeningPort}";
            internal string FrontendListeningAddress => string.IsNullOrEmpty(PublicIpAddress) ? null : $"{ServiceScheme}://{PublicIpAddress}:{ListeningPort}";

            public bool Equals([System.Diagnostics.CodeAnalysis.AllowNull] Server other)
            { // List.Remove 에 사용하기 위해 IEquatable
                return ServerId == other.ServerId;
            }
        }



    }
}
