using Microsoft.AspNetCore.Authorization;
using K2;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Grpc.Core;
using K2svc.Backend;

namespace K2svc.Frontend
{
    [Authorize]
    public class SimpleSampleService : SimpleSample.SimpleSampleBase
    {
        private readonly ILogger<SimpleSampleService> logger;
        private readonly ServiceConfiguration config;

        public SimpleSampleService(ILogger<SimpleSampleService> _logger, ServiceConfiguration _config)
        {
            logger = _logger;
            config = _config;
        }

        #region rpc
        public override async Task<SampleCommandResponse> SampleCommand(SampleCommandRequest request, ServerCallContext context)
        {
            // TODO: 매번 반복해야하는 userId 얻는 코드를 없앨 방법?
            var userId = await UserSessionBackend.GetOnlineUserId(context, config.BackendListeningAddress);

            return new SampleCommandResponse
            {
                Result = userId,
                Value = userId.Length
            };
        }

        public override async Task<SampleInfoResponse> SampleInfo(SampleInfoRequest request, ServerCallContext context)
        {
            // TODO: 매번 반복해야하는 userId 얻는 코드를 없앨 방법?
            var userId = await UserSessionBackend.GetOnlineUserId(context, config.BackendListeningAddress);

            var rsp = new SampleInfoResponse
            {
                Info1 = userId,
                Info2 = { "value1", "value2", "value3" }, // repeated member
                Info3 = new SampleInfoResponse.Types.NestedMessage // nested member
                {
                    NestedValue = "nested",
                    Type = SampleInfoResponse.Types.NestedMessage.Types.SampleType.Type1
                }
            };
            return rsp;
        }
        #endregion
    }
}
