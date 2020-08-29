using Grpc.Core;
using K2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace K2svc.Frontend
{
    [Authorize]
    public class SimpleSampleService : SimpleSample.SimpleSampleBase
    {
        private readonly ILogger<SimpleSampleService> logger;
        private readonly ServiceConfiguration config;
        private readonly Metadata header;

        public SimpleSampleService(ILogger<SimpleSampleService> _logger, ServiceConfiguration _config, Metadata _header)
        {
            logger = _logger;
            config = _config;
            header = _header;
        }

        #region rpc
        public override async Task<SampleCommandResponse> SampleCommand(SampleCommandRequest request, ServerCallContext context)
        {
            // 보내온 요청(request)에 대한 단순한 응답(response) 예시

            // TODO: 매번 반복해야하는 userId 얻는 코드를 없앨 방법?
            var (userId, pushBackendAddress) = await Session.GetOnlineUserInfoOrThrow(context, header);

            return new SampleCommandResponse
            {
                Result = userId,
                Value = userId.Length
            };
        }

        public override async Task<SampleInfoResponse> SampleInfo(SampleInfoRequest request, ServerCallContext context)
        {
            // 보내온 요청(request)에 대한 단순한 응답(response) 예시 ; nested message 사용 예시

            var (userId, pushBackendAddress) = await Session.GetOnlineUserInfoOrThrow(context, header);

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
