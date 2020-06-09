using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace K2svc
{
    public class Startup
    {
        private readonly IConfiguration config;
        public Startup(IConfiguration _config)
        {
            config = _config;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // TODO: main 에서부터 온 config 를 사용해야하는데...
            var settings = new ServiceConfiguration();
            config.GetSection("K2").Bind(settings);

            // 공통 resource(singleton)
            services.AddSingleton(settings);
            services.AddSingleton<Frontend.PushService.Singleton>();

            services.AddGrpc();

            // https://github.com/dotnet/AspNetCore.Docs/blob/master/aspnetcore/grpc/authn-and-authz.md
            // https://github.com/dotnet/AspNetCore.Docs/blob/master/aspnetcore/grpc/authn-and-authz/sample/Ticketer/Startup.cs
            services.AddAuthorization(option =>
            {
                option.AddPolicy(JwtBearerDefaults.AuthenticationScheme, policy =>
                {
                    policy.RequireAuthenticatedUser(); // https://joonasw.net/view/apply-authz-by-default
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireClaim(System.Security.Claims.ClaimTypes.Name);
                });
            });
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateAudience = false,
                            ValidateIssuer = false,
                            ValidateActor = false,
                            ValidateLifetime = false,
                            IssuerSigningKey = Frontend.InitService.SecurityKey
                        };
                });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime life)
        {
            // 임시 ///////////////////////////////////////////////////////////////////////////////////////
            // StartUp 이 만들어지기 전에(Main 함수?) register 를 호출해 설정을 업데이트하고 서비스를 시작해야한다.
            ServiceConfiguration conf = new ServiceConfiguration();
            config.Bind("K2", conf);
            life.ApplicationStarted.Register(() =>
            {
                using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(conf.ServerManagementServiceAddress);
                var client = new K2B.ServerManagement.ServerManagementClient(channel);
                var result = client.Register(new K2B.RegisterRequest());
            });
            /////////////////////////////////////////////////////////////////////////////////////// 임시 //


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // 여기(app.Use~  middleware) 순서 중요 ; https://docs.microsoft.com/ko-kr/aspnet/core/grpc/authn-and-authz?view=aspnetcore-3.1
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // TODO: reflection 으로 endpoints.MapGrpcService 을 자동화
                // backend services
                endpoints.MapGrpcService<Backend.ServerManagementService>();
                endpoints.MapGrpcService<Backend.UserSessionService>();

                // frontend services
                endpoints.MapGrpcService<Frontend.InitService>();
                endpoints.MapGrpcService<Frontend.PushService>();
                endpoints.MapGrpcService<Frontend.PushSampleService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("invalid access");
                });
            });
        }
    }
}
