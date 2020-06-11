namespace K2svc
{
    public class ServiceConfiguration // 기본값은 개발환경(localhost) 설정
    {
        public string ServerManagementServiceAddress { get; set; } = "http://localhost:5000"; // 최초에 무조건 있어야 하는 값

        // ServerManagementService 로부터 얻어와 설정될 값들
        public string ServerId { get; set; } = "dev";
        public string UserSessionServiceAddress { get; set; } = "http://localhost:5000";
        public string PushBackendAddress { get; set; } = "http://localhost:5000";
        public int FrontendListeningPort { get; set; } = 5001; // protocol(http/s) 도 포함해야할 수 있어 address 로 추후에 변경해야할 듯. 
    }
}
