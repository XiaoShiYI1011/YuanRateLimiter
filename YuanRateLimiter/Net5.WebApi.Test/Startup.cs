using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using YuanRateLimiter;
using YuanRateLimiter.Config;

namespace Net5.WebApi.Test
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // 注册限流中间件
            //services.AddSingleton(Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>());
            //services.AddRateLimiterSetUp(Configuration["RedisConfig:Defaulr:ConnectionString"]);

            services.AddRateLimiterSetUp(
                config => Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>(),
                Configuration["RedisConfig:Default:ConnectionString"]);

            //services.AddRateLimiterSetUp(
            //    config => Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>());

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Net5.WebApi.Test", Version = "v1" });
            });
        }
        
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Net5.WebApi.Test v1"));
            }
            app.UseRouting();
            // 使用限流中间件
            app.UseRateLimitMiddleware();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
