using YuanRateLimiter.Config;
using YuanRateLimiter;

namespace Net6.WebApi.Test
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            // 注册限流中间件
            //builder.Services.AddSingleton(builder.Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>());
            //builder.Services.AddRateLimiterSetUp(builder.Configuration["RedisConfig:Defaulr:ConnectionString"]);

            builder.Services.AddRateLimiterSetUp(
                config => builder.Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>(),
                builder.Configuration["RedisConfig:Default:ConnectionString"]);

            //builder.Services.AddRateLimiterSetUp(
            //    config => builder.Configuration.GetSection("RateLimiter").Get<RateLimiterConfig>());

            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseCors(options =>
            {
                options.AllowAnyOrigin();
                options.AllowAnyMethod();
                options.AllowAnyHeader();
            });
            // 使用限流中间件
            app.UseRateLimitMiddleware();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
