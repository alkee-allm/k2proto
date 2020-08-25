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

        // as service
        public string ServerId { get; set; } = "dev";
        public string BackendGroupId { get; set; } = "CB4DCFBD-D209-41EF-92E6-61EE037751C9"; // 같은 groupid 를 갖는 server 끼리만 커뮤니케이션 가능
        public string BackendListeningAddress { get; set; } = "http://localhost:5000";
        public string FrontendListeningAddress { get; set; } = "http://localhost:5000"; // NullOrEmpty 인 경우 frontend service disable

        // specific backend addresses
        public string ServerManagementBackendAddress { get; set; } = DefaultValues.SERVER_MANAGEMENT_BACKEND_ADDRESS; // argument 로 설정될 값
        public string UserSessionBackendAddress { get; set; } = "http://localhost:5000";

        // as backend
        public bool EnableServerManagementBackend { get; set; } = true;
        public bool EnableUserSessionBackend { get; set; } = true;

        // as database
        public string DatabaseFileName { get; set; } = "sample.sqlite3";

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return this;
        }

        public override void Load()
        {
            // ArgumentException ; Build() 를 중복으로 사용한 경우
            Data.Add($"{SECTION_NAME}:{nameof(ServerId)}", $"{ServerId}");
            Data.Add($"{SECTION_NAME}:{nameof(BackendGroupId)}", $"{BackendGroupId}");
            Data.Add($"{SECTION_NAME}:{nameof(BackendListeningAddress)}", $"{BackendListeningAddress}");
            Data.Add($"{SECTION_NAME}:{nameof(FrontendListeningAddress)}", $"{FrontendListeningAddress}");

            Data.Add($"{SECTION_NAME}:{nameof(ServerManagementBackendAddress)}", $"{ServerManagementBackendAddress}");
            Data.Add($"{SECTION_NAME}:{nameof(UserSessionBackendAddress)}", $"{UserSessionBackendAddress}");

            Data.Add($"{SECTION_NAME}:{nameof(EnableServerManagementBackend)}", $"{EnableServerManagementBackend}");
            Data.Add($"{SECTION_NAME}:{nameof(EnableUserSessionBackend)}", $"{EnableUserSessionBackend}");

            Data.Add($"{SECTION_NAME}:{nameof(DatabaseFileName)}", $"{DatabaseFileName}");
        }

        // internal valuse to communicate
        internal bool Registered = false;
    }
}
