using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WebApplication1;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Setup tracing
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: "WebApplication1"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri("http://127.0.0.1:4317");
                    options.Protocol = OtlpExportProtocol.Grpc;
                })
                .AddConsoleExporter());
        
        // Add logging
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.AddConsoleExporter();
        });
        
        // Add services to the container.
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        

        // connect to nats
        var options = new NatsOpts
        {
            Url = "nats://127.0.0.1:4222"
        };
        NatsConnection? natsConnection = null;
        for (int i = 0; i < 100; i++)
        {
            try
            {
                natsConnection = new NatsConnection(options);
            }
            catch (Exception e)
            {
                await Task.Delay(1000);
            }
        }
        if (natsConnection == null)
        {
            Console.WriteLine("Failed to connect to NATS");
            return;
        }
        await using var nats = natsConnection;
        

        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", () =>
            {
                var forecast = Enumerable.Range(1, 5).Select(async (index) =>
                {
                    // Create logger
                    // get random from console app
                    var headers = new NatsHeaders();
                    headers.Add("id", Activity.Current?.Id);
                    var msg = await nats.RequestAsync<int, int>("random", summaries.Length, headers);
                    
                    return new WeatherForecast
                    (
                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        Random.Shared.Next(-20, 55),
                        summaries[msg.Data]
                    );
                });

                Activity.Current?.SetStatus(ActivityStatusCode.Ok);
                return forecast;
            })
            .WithName("GetWeatherForecast")
            .WithOpenApi();

        app.Run();
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public static class DiagnosticsConfig
{
    public const string SourceName = "WebApplication1";
    public static ActivitySource Source = new(SourceName);
}