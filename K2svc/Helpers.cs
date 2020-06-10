using Microsoft.IdentityModel.Tokens;
using System;
using System.Security.Cryptography;
using System.Text;

namespace K2svc
{
    public static class Security
    {
        private static string VERSION = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(2); // major.minor
        private static string VERSION_HASH = HashSha256(VERSION); // ArgumentOutOfRangeException 을 피하기 위해 최소한의 길이를 가져야 한다.
        // ** 주의 **  ; https://github.com/alkee-allm/k2proto/issues/2#issuecomment-641868442
        //   모든 실행되는 서버들은 Major+minor 버전이 일치해야한다.
        public static SymmetricSecurityKey SecurityKey = new SymmetricSecurityKey(System.Text.Encoding.Default.GetBytes("hash salt +" + VERSION_HASH));

        public static String HashSha256(String value) // https://stackoverflow.com/a/17001289
        {
            var Sb = new StringBuilder();
            using (var hash = SHA256.Create())
            {
                var result = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                foreach (Byte b in result)
                    Sb.Append(b.ToString("x2"));
            }
            return Sb.ToString();
        }
    }
}
