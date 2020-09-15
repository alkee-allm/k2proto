using System;

namespace K2svc
{
    // general configuration
    public class K2Config
    {
        public static string SECTION_NAME = "K2";

        public string ServerId { get; set; } = $"{Environment.MachineName}:{Guid.NewGuid()}";
        public string BackendGroupId { get; set; } = "CB4DCFBD-D209-41EF-92E6-61EE037751C9"; // 같은 groupid 를 갖는 server 끼리만 커뮤니케이션 가능
        public int ListeningPort { get; set; } = 9060;
        public string ServerManagerAddress { get; set; } = ""; // empty 인 경우 ServerManager config 가 존재(not null)해야함

        #region Database
        public Db.AccountDb.Config AccountDb { get; set; } = new Db.AccountDb.Config();
        #endregion

        #region Background
        public Background.ServerManagementBackground.Config ServerManagementBackground { get; set; } = new Background.ServerManagementBackground.Config();
        #endregion

        #region 개별 service 설정
        // appsettings 를 통해 null 로 설정할 수 없어 각 서비스별 bool flag 할당. ; https://github.com/alkee-allm/k2proto/issues/39#issuecomment-690926198
        // backend
        public Backend.ServerManagerBackend.Config ServerManager { get; set; } = new Backend.ServerManagerBackend.Config();
        public bool HasServerManager { get; set; } = true;
        public Backend.SessionManagerBackend.Config SessionManager { get; set; } = new Backend.SessionManagerBackend.Config();
        public bool HasSessionManager { get; set; } = true;

        // frontend
        public Frontend.InitService.Config InitService { get; set; } = new Frontend.InitService.Config();
        public bool HasInitService { get; set; } = true;
        public Frontend.PushService.Config PushService { get; set; } = new Frontend.PushService.Config();
        internal bool HasPushService { get; set; } = true;
        public Frontend.PushSampleService.Config PushSampleService { get; set; } = new Frontend.PushSampleService.Config();
        internal bool HasPushSampleService { get; set; } = true;
        public Frontend.SimpleSampleService.Config SimpleSampleService { get; set; } = new Frontend.SimpleSampleService.Config();
        internal bool HasSimpleSampleService { get; set; } = true;
        #endregion

        #region helper
        internal string ServiceScheme => "http"; // TODO: 설정에 따라.. "https" 지원
        #endregion
    }

    // 외부(ServerManager)로부터 설정되는 값들(ServerManagementService 및 기타 서비스)
    //   멤버 기본값이 null 이어야 문제확인(올바르게 설정되었는지)이 가능
    public class RemoteConfig
    {
        internal bool Registered { get; set; }

        internal string BackendListeningAddress { get; set; }

        internal string SessionManagerAddress { get; set; }
        internal string ServerManagerAddress { get; set; }
    }
}
