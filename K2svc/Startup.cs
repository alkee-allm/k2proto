using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.Tasks;

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
            // 공통 resource(singleton)
            var cfg = config.GetSection(ServiceConfiguration.SECTION_NAME).Get<ServiceConfiguration>();
            services.AddSingleton(cfg); // 쉽게 접근해 사용할 수 있도록
            services.AddSingleton<Frontend.PushService.Singleton>();
            var backendHeader = new Grpc.Core.Metadata();
            backendHeader.Add(nameof(cfg.BackendGroupId), cfg.BackendGroupId); // key 는 소문자로 변환되어 들어간다
            services.AddSingleton(backendHeader);

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
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime life, ServiceConfiguration cfg)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // service blocker
            app.Use(async (context, next) =>
            { // https://docs.microsoft.com/ko-kr/aspnet/core/fundamentals/middleware/write?view=aspnetcore-3.1#middleware-class
                if (cfg.Registered == false && cfg.EnableServerManagementBackend == false)
                { // 아직 register 되지 않은 서버는 사용할 수 없음
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
                // backend services
                endpoints.MapGrpcService<Backend.ServerManagementBackend>();
                endpoints.MapGrpcService<Backend.UserSessionBackend>();

                // frontend services
                endpoints.MapGrpcService<Frontend.InitService>();
                endpoints.MapGrpcService<Frontend.PushService>();
                endpoints.MapGrpcService<Frontend.PushSampleService>();
                endpoints.MapGrpcService<Frontend.SimpleSampleService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("invalid access");
                });
            });
        }
    }
}
