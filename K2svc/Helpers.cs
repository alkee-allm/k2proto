﻿using Microsoft.IdentityModel.Tokens;
using System;
using System.Security.Cryptography;
using System.Text;

namespace K2svc
{
    public static class DefaultValues
    {
        public static readonly string APP_SETTINGS_FILENAME = "appsettings.json";
        public static readonly string SERVICE_IP = "localhost";
        public static readonly int LISTENING_PORT = 9060;
        public static readonly string SERVER_MANAGEMENT_BACKEND_ADDRESS = $"http://{SERVICE_IP}:{LISTENING_PORT}";
        public static readonly int SERVER_REGISTER_DELAY_MILLISECONDS = 2000;
        public static readonly double SERVER_MANAGEMENT_PING_INTERVAL_SECONDS = 1.0;
    }

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

    // swift defer 와 같이, using 이 끝났을 때 실행되는 함수 지정 ; https://docs.swift.org/swift-book/ReferenceManual/Statements.html#grammar_defer-statement
    public class Defer : IDisposable
    {
        private Action work;
        public Defer(Action work)
        {
            this.work = work;
        }

        void IDisposable.Dispose()
        {
            work();
        }
    }

    public static class Util
    {
        public static bool HasHttpScheme(string uri)
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri x)
                && (x.Scheme == Uri.UriSchemeHttp || x.Scheme == Uri.UriSchemeHttps))
            {
                return true;
            }
            return false;
        }

        public static string GetPublicIp()
        {
            try
            {
                return new System.Net.WebClient().DownloadString("http://ifconfig.me/ip");
            }
            catch
            {
                return null;
            }
        }
    }
}
