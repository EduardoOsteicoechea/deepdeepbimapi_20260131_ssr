using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using deepdeepbimapi.Database;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddAWSService<IAmazonDynamoDB>();

string? jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new Exception("Fatal Error: JWT_SECRET_KEY is missing from environment variables.");
}
var key = Encoding.UTF8.GetBytes(jwtSecret);

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
    var requestErrors = new List<string>();
    if (string.IsNullOrEmpty(input.FirstName))
    {
        requestErrors.Add("First Name");
    }
    if (string.IsNullOrEmpty(input.LastName))
    {
        requestErrors.Add("Last Name");
    }
    if (string.IsNullOrEmpty(input.Password))
    {
        requestErrors.Add("Password");
    }
    if (string.IsNullOrEmpty(input.Email))
    {
        requestErrors.Add("Email");
    }
    if (requestErrors.Any())
    {
        return Results.BadRequest(new { error = $"Missing fields: {string.Join(", ", requestErrors)}" });
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
        return Results.Conflict(new { error = "Already registered email." });
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

app.MapPost("/login", async (
    HttpContext context,
    deepdeepbimapi.Models.LoginRequest input,
    IAmazonDynamoDB dynamoClient
    ) =>
{
    if (context.Request.Cookies.ContainsKey("auth_token"))
    {
        return Results.Ok(new { message = "Already logged in" });
    }

    var requestErrors = new List<string>();
    if (string.IsNullOrEmpty(input.Password)) requestErrors.Add("Password");
    if (string.IsNullOrEmpty(input.Email)) requestErrors.Add("Email");
    if (requestErrors.Any()) return Results.BadRequest(new { error = $"Missing fields: {string.Join(", ", requestErrors)}" });

    var queryRequest = new QueryRequest
    {
        TableName = "deepdeepbim_users",
        IndexName = "Email-Index",
        KeyConditionExpression = "Email = :v_Email",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
            { ":v_Email", new AttributeValue { S = input.Email } }
        },
        Limit = 1
    };

    var queryResponse = await dynamoClient.QueryAsync(queryRequest);
    if (queryResponse.Count == 0)
    {
        return Results.Unauthorized();
    }

    var userItem = queryResponse.Items[0];
    string storedHash = userItem["PasswordHash"].S;
    string userId = userItem["UserId"].S;
    string firstName = userItem.ContainsKey("FirstName") ? userItem["FirstName"].S : "User";

    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(input.Password, storedHash);
    if (!isPasswordValid) return Results.Unauthorized();

    string? jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
    if (string.IsNullOrEmpty(jwtSecret))
    {
        return Results.Problem("Server configuration error: Missing JWT Key");
    }

    var key = Encoding.UTF8.GetBytes(jwtSecret);
    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim("sub", userId),
            new Claim("given_name", firstName)
        }),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    context.Response.Cookies.Append("auth_token", tokenString, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = DateTime.UtcNow.AddHours(1)
    });

    return Results.Ok(new { message = "Logged in successfully", userId = userId });
});

app.MapPost("/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete("auth_token");
    return Results.Ok(new { message = "Logged out successfully" });
});











app.Run();
