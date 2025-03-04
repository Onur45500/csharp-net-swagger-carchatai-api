using DotNetEnv;
using csharp_net_swagger_carchat_api.Services;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

// Load .env file
Env.Load("../.env");

var builder = WebApplication.CreateBuilder(args);

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Debug: Print configuration values
//Console.WriteLine($"Config OPENROUTER_API_KEY: {builder.Configuration["OPENROUTER_API_KEY"]}");
//Console.WriteLine($"Config OPENROUTER_BASE_URL: {builder.Configuration["OPENROUTER_BASE_URL"]}");
//Console.WriteLine($"Config OPENROUTER_SITE_URL: {builder.Configuration["OPENROUTER_SITE_URL"]}");

// Add this near the top of your Program.cs
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

// Register HttpClient and OpenRouterService
builder.Services.AddHttpClient();
//builder.Services.AddScoped<IDeepseekService, DeepseekService>(); // in this version we don't use deepseek
builder.Services.AddScoped<IOpenRouterService, OpenRouterService>();
builder.Services.AddScoped<ILeboncoinService, LeboncoinService>();

// In your service configuration
builder.Services.AddHttpClient("LeboncoinClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
