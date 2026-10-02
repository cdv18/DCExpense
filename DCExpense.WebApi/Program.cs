using System.Text;
using DCExpense.Application.Interfaces;
using DCExpense.Application.Services;
using DCExpense.Infrastructure;
using DCExpense.Infrastructure.Data;
using DCExpense.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllLocal", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5002",
                "http://localhost:5001",
                "https://localhost:7001",
                "http://localhost:3000",
                "http://127.0.0.1:5002",
                "http://127.0.0.1:5001",
                "http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddDbContext<DCExpenseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DCExpenseDbContext>());

// Application & Infrastructure Services
builder.Services.AddScoped<ITransactionStateMachine, TransactionStateMachine>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<IBankStatementParser, BankStatementParser>();
builder.Services.AddScoped<IAiVoiceExpenseParser, AiVoiceExpenseParser>();
builder.Services.AddScoped<IHouseholdService, HouseholdService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

// Telegram Bot Service with HttpClient
builder.Services.AddHttpClient<ITelegramBotService, TelegramBotService>();

// JWT Authentication synced with DCManager Secret
var jwtConfig = builder.Configuration.GetSection("Jwt");
var secretKey = jwtConfig["Secret"] ?? "DCManager-Super-Secret-Key-That-Should-Be-At-Least-32-Characters-Long-2024!";
var issuer = jwtConfig["Issuer"] ?? "DCManager";
var audience = jwtConfig["Audience"] ?? "DCManagerClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = false,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

var app = builder.Build();

app.UseCors("AllowAllLocal");

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DCExpenseDbContext>();
        db.Database.EnsureCreated();
        await DCExpenseSeedData.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization warning: {ex.Message}");
    }
}

app.MapControllers();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
