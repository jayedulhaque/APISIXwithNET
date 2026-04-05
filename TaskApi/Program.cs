using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Features;
using TaskApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<ITaskStore, TaskStore>();

var app = builder.Build();

var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
    ?? Environment.GetEnvironmentVariable("HOSTNAME")
    ?? Environment.MachineName;
app.Logger.LogInformation("TaskApi instance: {InstanceId}", instanceId);

app.UseWebSockets();

app.MapControllers();

app.MapGet("/sse", async (HttpContext context) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    var bodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
    bodyFeature?.DisableBuffering();

    var ct = context.RequestAborted;
    try
    {
        while (!ct.IsCancellationRequested)
        {
            await context.Response.WriteAsync($"data: {DateTimeOffset.UtcNow:O}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }
    catch (OperationCanceledException)
    {
        // client disconnected
    }
});

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[1024 * 4];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(buffer, context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    context.RequestAborted);
                break;
            }

            await webSocket.SendAsync(
                buffer.AsMemory(0, result.Count),
                result.MessageType,
                result.EndOfMessage,
                context.RequestAborted);
        }
    }
    catch (OperationCanceledException)
    {
        // aborted
    }
});

app.Run();
