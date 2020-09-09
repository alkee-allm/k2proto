using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;

namespace K2svc.Net
{
    /// <summary>
    ///     channel 및 Grpc client 재사용을 위한 channel, client 관리자
    /// </summary>
    /// <exception cref="RpcException">입력값(backendAddress)가 공백이나 null 인 경우</exception>
    public class GrpcClients
    { // https://docs.microsoft.com/aspnet/core/grpc/performance?view=aspnetcore-3.1
        public T GetClient<T>(string backendAddress) where T : ClientBase
        {
            if (string.IsNullOrEmpty(backendAddress)) // 개발 편의를 위해 RpcException 사용(사용하는쪽에서 반환된 client 와 함께 try 로 묶어서 사용 가능)
                throw new RpcException(new Status(StatusCode.Unavailable, "empty channel address"));

            var channel = GetChannel(backendAddress);
            lock (clients)
            {
                var key = (typeof(T), channel);
                if (clients.TryGetValue(key, out var client)) return client as T;
                var newClient = Activator.CreateInstance(typeof(T), channel) as T; // 빈번하지 않다(client 마다 최초 한번씩만)
                clients.Add(key, newClient);
                return newClient;
            }
        }

        private GrpcChannel GetChannel(string backendAddress)
        {
            lock (pool)
            {
                if (pool.TryGetValue(backendAddress, out var channel)) return channel;
                var newChannel = GrpcChannel.ForAddress(backendAddress);
                pool.Add(backendAddress, newChannel);
                return newChannel;
            }
        }

        private readonly Dictionary<string, GrpcChannel> pool = new Dictionary<string, GrpcChannel>();
        private readonly Dictionary<(Type, GrpcChannel), ClientBase> clients = new Dictionary<(Type, GrpcChannel), ClientBase>();
    }
}
