using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace K2svc.Filter
{
    public class BackendValidator : Interceptor
    {
        private readonly ILogger<BackendValidator> logger;
        private readonly K2Config localConfig;

        public BackendValidator(ILogger<BackendValidator> _logger, K2Config _localConfig)
        {
            logger = _logger;
            localConfig = _localConfig;
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        { // Server 의 handler 가 호출될 때
            if (IsBackendMethod(context.Method) == false)
                return base.UnaryServerHandler(request, context, continuation); // not interested

            var backendGroupId = context.RequestHeaders.FirstOrDefault(
                (me) => me.Key.Equals(nameof(localConfig.BackendGroupId), StringComparison.OrdinalIgnoreCase)
                )?.Value;
            if (localConfig.BackendGroupId == backendGroupId)
                return base.UnaryServerHandler(request, context, continuation);

            // unknwon server or status
            //      https://groups.google.com/g/grpc-io/c/OXKLky8p9f8 

            var msg = $"invalid backend request of {context.Method} from {context.Peer}, ({backendGroupId})";
            logger.LogWarning(msg);
            context.Status = new Status(StatusCode.Aborted, msg);
            return base.UnaryServerHandler(request, context, continuation); // not throwing ?
        }

        private static bool IsBackendMethod(string method)
        {
            return method.StartsWith("/K2B.");
        }
    }
}
