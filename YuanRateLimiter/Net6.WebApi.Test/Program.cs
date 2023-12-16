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
            // ע�������м��
            //builder.Services.AddSingleton(builder.Configuration.GetSection("RateLimiting").Get<RateLimitingConfig>());
            //builder.Services.AddRateLimiterSetUp(builder.Configuration["RedisConfig:Defaulr:ConnectionString"]);
            builder.Services.AddRateLimiterSetUp(
                builder.Configuration["RedisConfig:Defaulr:ConnectionString"], 
                config => builder.Configuration.GetSection("RateLimiting").Get<RateLimitingConfig>());

            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            // ʹ�������м��
            app.UseRateLimitMiddleware();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
