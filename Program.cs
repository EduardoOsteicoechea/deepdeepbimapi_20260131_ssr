using System.Security. Cryptography.X509Certificates;
using Amazon.DynamoDBv2;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddAWSService<IAmazonDynamoDB>();

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

app.MapGet("/", async (HttpContext context, IAmazonDynamoDB dynamoDBClient) =>
{
    context.Response.Headers.ContentType = "text/html; charset=utf-8";
    
    await Page.Print(context.Response.BodyWriter, "Eduardo. We're preparing for bd queries");
});

app.Run();
