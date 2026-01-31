using System.Security. Cryptography.X509Certificates;

var builder = WebApplication.CreateSlimBuilder(args);

var certPath = "/etc/letsencrypt/live/deepdeepbim.com/fullchain.pem";
var keyPath = "/etc/letsencrypt/live/deepdeepbim.com/privkey.pem";


builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80); 

    if (File.Exists(certPath))
    {
        options.ListenAnyIP(443, listenOptions =>
        {
            var cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            listenOptions.UseHttps(cert);
        });
    }
    else
    {
        Console.WriteLine("Warning: SSL Certificates not found. Serving on Port 80 only.");
    }
});

var app = builder.Build();

app.MapGet("/", async (HttpContext context) =>
{
    context.Response.Headers.ContentType = "text/html; charset=utf-8";
    
    // Get the raw pipe writer (skips the string converter overhead)
    var writer = context.Response.BodyWriter;

    // Write raw bytes directly to the network stream
    // "u8" creates a ReadOnlySpan<byte> at compile time (Zero Allocation)
    await writer.WriteAsync("<!DOCTYPE html><html><body>"u8.ToArray());
    await writer.WriteAsync("<h1>DeepDeepBIM is Online</h1>"u8.ToArray());
    await writer.WriteAsync("<p>Served via Kestrel + Let's Encrypt on AWS Graviton.</p>"u8.ToArray());
    
    // Simulate a massive report streaming
    await writer.WriteAsync("<ul>"u8.ToArray());
    for (int i = 0; i < 1000; i++)
    {
        // For dynamic numbers, we still need a tiny allocation, 
        // but the static HTML parts are free.
        var line = $"<li>Row Data {i} - <strong>Streamed instantly</strong></li>";
        await writer.WriteAsync(System.Text.Encoding.UTF8.GetBytes(line));
    }
    await writer.WriteAsync("</ul></body></html>"u8.ToArray());
});

app.Run();
