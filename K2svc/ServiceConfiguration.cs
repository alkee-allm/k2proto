namespace K2svc
{
    public class ServiceConfiguration
    {
        public string ServerManagementServiceAddress { get; set; } = "http://localhost:5000"; // 최초에 무조건 있어야 하는 값

        // ServerManagementService 로부터 얻어와 설정될 값들
        public string ServerId { get; set; } = "dev";
        public string UserSessionServiceAddress { get; set; } = "http://localhost:5000";
    }
}
