﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace K2svc
{
    public class Startup
    {
        public Startup()
        {
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // singleton resource; Config 종속적인 설정은 StartUp 이전(Program.cs : CreateHostBuilder)에
            services.AddSingleton<Net.GrpcClients>();
            services.AddSingleton<RemoteConfig>();

            // database
            services.AddDbContext<Db.AccountDb>();

            // hosted services
            services.AddHostedService<Background.ServerManagementBackground>();

            // grpc
            services.AddGrpc(options =>
            {
                options.Interceptors.Add<Filter.BackendValidator>();
            });

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
                            IssuerSigningKey = Security.SecurityKey
                        };
                });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, K2Config localConfig, RemoteConfig remoteConfig, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // service blocker
            app.Use(async (context, next) =>
            { // https://docs.microsoft.com/ko-kr/aspnet/core/fundamentals/middleware/write?view=aspnetcore-3.1#middleware-class
                if (localConfig.HasServerManager == false && remoteConfig.Registered == false)
                { // 아직 grpc 서비스를 사용가능한 상태가 아님
                    logger.LogWarning($"not registered yet for {context.Request.Path}");
                    return;
                }
                await next();
            });

            // 여기(app.Use~  middleware) 순서 중요 ; https://docs.microsoft.com/ko-kr/aspnet/core/grpc/authn-and-authz?view=aspnetcore-3.1
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // TODO: reflection 으로 endpoints.MapGrpcService 을 자동화

                // backend services(managers)
                if (localConfig.HasServerManager) endpoints.MapGrpcService<Backend.ServerManagerBackend>();
                if (localConfig.HasSessionManager) endpoints.MapGrpcService<Backend.SessionManagerBackend>();

                // hosts(backend client to backend service)
                // backend server 의 명령(push)을 받을 backend host 들 ; disable 될 수 없다.(항상 manager 의 request 에 응답해야 한다)
                endpoints.MapGrpcService<Backend.ServerHostBackend>();
                endpoints.MapGrpcService<Backend.SessionHostBackend>();

                // frontend services
                if (localConfig.HasInitService ) endpoints.MapGrpcService<Frontend.InitService>();
                if (localConfig.HasPushService) endpoints.MapGrpcService<Frontend.PushService>();
                if (localConfig.HasPushSampleService) endpoints.MapGrpcService<Frontend.PushSampleService>();
                if (localConfig.HasSimpleSampleService) endpoints.MapGrpcService<Frontend.SimpleSampleService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("invalid access");
                });
            });
        }
    }
}
