using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace ConsoleApp;

internal class Program
{
    public static async Task Main(string[] args)
    {
        // Setup tracing
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("ConsoleApp")
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://127.0.0.1:4317");
                options.Protocol = OtlpExportProtocol.Grpc;
            })
            .Build();
        
        
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
        
        // subscribe to a subject
        var cts = new CancellationTokenSource();
        
        // Cancel the cts when ctrl+c is pressed
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            cts.Cancel();
            eventArgs.Cancel = true;
        };

        var subscription = Task.Run(async () =>
        {
            await foreach (var msg in natsConnection.SubscribeAsync<int>(subject: "random").WithCancellation(cts.Token))
            {
                var parentid = msg.Headers!["id"].ToString();
                using var activity = DiagnosticsConfig.Source.StartActivity("Generating random number", ActivityKind.Internal, parentid);
                activity?.SetTag("random", true);

                try
                {
                    await msg.ReplyAsync(Random.Shared.Next(0, msg.Data + 1), cancellationToken: cts.Token);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception e)
                {
                    activity?.RecordException(e);
                    activity?.SetStatus(ActivityStatusCode.Error);
                }
            }
        }, cts.Token);

        
        Console.WriteLine("Hello, World!");
        await subscription.WaitAsync(cts.Token);
        
        tracerProvider.Dispose();
    }
}

public static class DiagnosticsConfig
{
    public const string SourceName = "ConsoleApp";
    public static ActivitySource Source = new(SourceName);
}