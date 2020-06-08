using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using K2;

namespace K2svc.Frontend
{
    public class InitService : Init.InitBase
    {
        // SecurityKey�� ��� ��� ������ ���� key �� ������ �����ؾ� �� ��.
        public static SymmetricSecurityKey SecurityKey { get; } = new SymmetricSecurityKey(Guid.NewGuid().ToByteArray());

        private readonly ILogger<InitService> logger;

        public InitService(ILogger<InitService> _logger)
        {
            logger = _logger;
        }

        #region rpc
        public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            // (����) �ϳ��� ���ῡ�� ���� ��û�޴� ��� �ź��ϰų�, ��û�� Ƚ���� ���� ���� ���ó���� �ð��� ������ ��

            // �ӽ� - ��й�ȣ�� k �� �����ϸ� ������ ����
            if (request.Pw.Length < 1 || request.Pw[0] != 'k')
            {
                return Task.FromResult(new LoginResponse { Result = LoginResponse.Types.ResultType.Mismatched });
            }

            // TODO: �ߺ� �α��� �˻� �� ó��(��å ��ȹ �ʿ�)

            return Task.FromResult(new LoginResponse
            {
                Result = LoginResponse.Types.ResultType.Ok,
                Jwt = GenerateJwtToken(request.Id)
            });
        }

        public override Task<StateResponse> State(Null request, ServerCallContext context)
        {
            // runtime ȣ���� ���ϰ� �ִ��� static �ϰ�.
            return Task.FromResult(new StateResponse
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
                Gateway = "https://localhost:5001"
            });
        }
        #endregion

        private string GenerateJwtToken(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("invalid id", nameof(id));

            logger.LogInformation($"creating jwt for {id}");

            var claims = new[] { new Claim(ClaimTypes.Name, id) };
            var credentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken("k2server", "k2client", claims, signingCredentials: credentials);

            // �������� �������� ���� �����̱� �ѵ�, https://docs.microsoft.com/en-us/previous-versions/visualstudio/dn464181(v=vs.114)?redirectedfrom=MSDN#thread-safety
            // thread safety �� �����ǹǷ� wtSecurityTokenHandler �� �׻� �� instance ������ ���
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
