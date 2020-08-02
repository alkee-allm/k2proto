using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using K2;
using System.Linq;

namespace K2svc.Frontend
{
    public class InitService : Init.InitBase
    {
        private readonly ILogger<InitService> logger;
        private readonly Db.AccountDb accountDb;

        public InitService(ILogger<InitService> _logger, Db.AccountDb _accountDb)
        {
            logger = _logger;
            accountDb = _accountDb;
        }

        #region rpc
        public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            // (보안) 하나의 연결에서 자주 요청받는 경우 거부하거나, 요청의 횟수에 따라 점점 결과처리의 시간을 지연할 것


            var account = from a in accountDb.Accounts
                          where a.AccountName == request.Id && a.Password == request.Pw
                          select a;
            if (account.Count() == 0)
            {
                return Task.FromResult(new LoginResponse { Result = LoginResponse.Types.ResultType.Mismatched });
            }

            // TODO: 중복 로그인 검사 및 처리(정책 기획 필요; 이전 연걸끊기? 새연결 거부?)

            return Task.FromResult(new LoginResponse
            {
                Result = LoginResponse.Types.ResultType.Ok,
                Jwt = GenerateJwtToken(request.Id)
            });
        }

        public override Task<StateResponse> State(Null request, ServerCallContext context)
        {
            // runtime 호출을 피하고 최대한 static 하게.
            return Task.FromResult(new StateResponse
            {
                Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
                // TODO: server management service 로부터 알맞은(가장 한산한?) 서버 전송
                Gateway = "https://localhost:5001"
            });
        }
        #endregion

        private string GenerateJwtToken(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("invalid id", nameof(id));

            logger.LogInformation($"creating jwt for {id}");

            var claims = new[] { new Claim(ClaimTypes.Name, id) };
            var credentials = new SigningCredentials(Security.SecurityKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken("k2server", "k2client", claims, signingCredentials: credentials);

            // 새버전의 문서에는 없는 내용이긴 한데, https://docs.microsoft.com/en-us/previous-versions/visualstudio/dn464181(v=vs.114)?redirectedfrom=MSDN#thread-safety
            // thread safety 가 걱정되므로 wtSecurityTokenHandler 를 항상 새 instance 생성해 사용
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
