using Microsoft.Extensions.Configuration;
using System;

namespace K2svc
{
    public class ServiceConfiguration
        : ConfigurationProvider
        , IConfigurationSource // https://ofpinewood.com/blog/creating-a-custom-configurationprovider-for-a-entity-framework-core-source
    {
        public static string SECTION_NAME = "K2";

        public string ServiceScheme => "http"; // TODO: 설정에 따라.. "https" 지원

        // 기본값은 개발환경(localhost) 설정으로 해 개발편의성을 높일 것.

        // basic configuration
        public string ServerId { get; set; } = $"{Environment.MachineName}:{Guid.NewGuid()}";
        public string BackendGroupId { get; set; } = "CB4DCFBD-D209-41EF-92E6-61EE037751C9"; // 같은 groupid 를 갖는 server 끼리만 커뮤니케이션 가능
        public int ListeningPort { get; set; } = DefaultValues.LISTENING_PORT;

        // as unique backend service
        // ServerManagementService 는 process argument 에 의해 결정되므로 configuration 에 포함하지 않는다.
        public bool EnableUserSessionBackend { get; set; } = true;

        // as frontend
        public bool EnablePushService { get; set; } = true;
        public bool EnableInitService { get; set; } = true;
        public bool EnablePushSampleService { get; set; } = true;
        public bool EnableSimpleSampleService { get; set; } = true;

        // as database
        public string DatabaseFileName { get; set; } = "sample.sqlite3";

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return this;
        }

        public override void Load()
        {
            // ArgumentException ; Build() 를 중복으로 사용한 경우
            Data.Add($"{SECTION_NAME}:{nameof(BackendGroupId)}", $"{BackendGroupId}");
            Data.Add($"{SECTION_NAME}:{nameof(ListeningPort)}", $"{ListeningPort}");

            Data.Add($"{SECTION_NAME}:{nameof(EnableUserSessionBackend)}", $"{EnableUserSessionBackend}");
            Data.Add($"{SECTION_NAME}:{nameof(EnablePushService)}", $"{EnablePushService}");
            Data.Add($"{SECTION_NAME}:{nameof(EnableInitService)}", $"{EnableInitService}");
            Data.Add($"{SECTION_NAME}:{nameof(EnablePushSampleService)}", $"{EnablePushSampleService}");
            Data.Add($"{SECTION_NAME}:{nameof(EnableSimpleSampleService)}", $"{EnableSimpleSampleService}");

            Data.Add($"{SECTION_NAME}:{nameof(DatabaseFileName)}", $"{DatabaseFileName}");
        }

        #region 외부에서 설정되는 값들(ServerManagementService 및 기타 서비스) ; 따라서 기본값이 null 이어야 문제확인(올바르게 설정되었는지)이 가능
        internal string BackendListeningAddress { get; set; }

        // ** Server management service 의 경우, 직접 이 config 값을 사용해서는 안된다. **
        //   이 값들은 이외의 서버에서 Server management service 에 의해(Register) 갱신되는 값들이기 때문에
        //   여러 service 가 중첩되서 기능을 하는 경우에 이 config 가 상황에 따라 변경될 수 있다.

        // specific backend addresses
        internal string UserSessionBackendAddress { get; set; }
        // etc
        internal string RemoteServerManagerAddress { get; set; } // 이 서버가 ServerManager 역할을 갖는 경우 null
        internal bool Registered;
        #endregion

        #region helper
        internal bool IsThisServerManager => string.IsNullOrEmpty(RemoteServerManagerAddress);
        internal string LocalServerManagerAddress => $"{ServiceScheme}://localhost:{ListeningPort}";
        internal string ServerManagerAddress => IsThisServerManager ? LocalServerManagerAddress : RemoteServerManagerAddress;
        #endregion
    }
}
