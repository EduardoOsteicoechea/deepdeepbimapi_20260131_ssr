using System.Security.Cryptography.X509Certificates;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using deepdeepbimapi.Database;


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

app.MapPost("/create_user", async (
    deepdeepbimapi.Models.CreateUserRequest input,
    IAmazonDynamoDB dynamoDBClient
) =>
{
    string[] requestErrors = Array.Empty<string>();

    if (string.IsNullOrEmpty(input.FirstName))
    {
        requestErrors.Append("First Name");
    }
    if (string.IsNullOrEmpty(input.LastName))
    {
        requestErrors.Append("Last Name");
    }
    if (string.IsNullOrEmpty(input.Password))
    {
        requestErrors.Append("Password");
    }
    if (string.IsNullOrEmpty(input.Email))
    {
        requestErrors.Append("Email");
    }

    if(requestErrors.Length > 0)
    {
        return Results.BadRequest(new { error = $"Missing fields: {string.Join(", ",requestErrors)}" });
    }

    var emailCheckRequest = new QueryRequest
    {
        TableName = DatabaseTables.UsersTable,
        IndexName = "Email-Index",
        KeyConditionExpression = "Email = :v_email",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            { ":v_email", new AttributeValue { S = input.Email } }
        },
        Limit = 1
    };

    var emailCheckResponse = await dynamoDBClient.QueryAsync(emailCheckRequest);
    
    if (emailCheckResponse.Count > 0)
    {
        // 409 Conflict is the standard HTTP code for "Duplicate Resource"
        return Results.Conflict(new { error = "This email is already registered." });
    }

    string newUserId = Guid.NewGuid().ToString();
    string newUserPasswordHash = BCrypt.Net.BCrypt.HashPassword(input.Password);

    var request = new PutItemRequest
    {
        TableName = DatabaseTables.UsersTable,
        Item = new Dictionary<string, AttributeValue>
        {
            { "UserId", new AttributeValue { S = newUserId } },
            { "FirstName", new AttributeValue { S = input.FirstName } },
            { "LastName", new AttributeValue { S = input.LastName } },
            { "PasswordHash", new AttributeValue { S = newUserPasswordHash } },
            { "Email", new AttributeValue { S = input.Email } },
            { "CreatedAt", new AttributeValue { S = DateTime.UtcNow.ToString("O") } }
        }
    };

    await dynamoDBClient.PutItemAsync(request);

    return Results.Created($"/users/{newUserId}", new { UserId = newUserId });
});












app.Run();
