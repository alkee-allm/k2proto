using Microsoft.Extensions.Configuration;

namespace K2svc
{
    public class ServiceConfiguration
        : ConfigurationProvider
        , IConfigurationSource // https://ofpinewood.com/blog/creating-a-custom-configurationprovider-for-a-entity-framework-core-source
    {
        public static string SECTION_NAME = "K2";

        // 기본값은 개발환경(localhost) 설정으로 해 개발편의성을 높일 것.
        // 이 값들은 ServerManagementService 에 의해 변경 가능

        // as frontend
        public string ServerId { get; set; } = "dev";
        public string PushBackendAddress { get; set; } = "http://localhost:5000";

        public string ServerManagementBackendAddress { get; set; } = DefaultValues.SERVER_MANAGEMENT_BACKEND_ADDRESS; // argument 로 설정될 값
        public string UserSessionBackendAddress { get; set; } = "http://localhost:5000";

        // as backend
        public bool EnableServerManagementBackend { get; set; } = true;
        public bool EnableUserSessionBackend { get; set; } = true;

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return this;
        }

        public override void Load()
        {
            Data.Add($"{ServiceConfiguration.SECTION_NAME}:{nameof(ServerId)}", $"{ServerId}");
            Data.Add($"{ServiceConfiguration.SECTION_NAME}:{nameof(PushBackendAddress)}", $"{PushBackendAddress}");

            Data.Add($"{ServiceConfiguration.SECTION_NAME}:{nameof(ServerManagementBackendAddress)}", $"{ServerManagementBackendAddress}");
            Data.Add($"{ServiceConfiguration.SECTION_NAME}:{nameof(UserSessionBackendAddress)}", $"{UserSessionBackendAddress}");

            Data.Add($"{ServiceConfiguration.SECTION_NAME}:{nameof(EnableServerManagementBackend)}", $"{EnableServerManagementBackend}");
            Data.Add($"{ServiceConfiguration.SECTION_NAME}:{nameof(EnableUserSessionBackend)}", $"{EnableUserSessionBackend}");
        }
    }
}
