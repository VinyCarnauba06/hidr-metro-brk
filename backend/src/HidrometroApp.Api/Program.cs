using System.Text;
using System.Threading.RateLimiting;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Services;
using HidrometroApp.Infrastructure.Services;
using HidrometroApp.Infrastructure.Data;
using HidrometroApp.Infrastructure.Security;
using HidrometroApp.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// Carrega .env em Development (sem biblioteca externa).
// Variáveis já presentes no ambiente têm precedência — produção não é afetada.
LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// Serilog
var appLevel = Enum.Parse<Serilog.Events.LogEventLevel>(
    builder.Configuration["LOG_LEVEL"] ?? "Information");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        builder.Configuration["LOG_PATH"] ?? "storage/logs/app.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .MinimumLevel.Is(appLevel)
    // Suprime ruído do pipeline ASP.NET Core e queries EF Core no console
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Infrastructure", Serilog.Events.LogEventLevel.Warning)
    // Mantém mensagem "Now listening on:" visível
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();

// Database
var rawConn = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DATABASE_URL não configurada");

var connStr = NpgsqlConnectionString(rawConn);

builder.Services.AddDbContext<HidrometroDbContext>(opts =>
    opts.UseNpgsql(connStr));

// JWT
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT_SECRET não configurada");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "HidrometroApp",
            ValidAudience = "HidrometroApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILeituraService, LeituraService>();
builder.Services.AddScoped<IRelatorioService, RelatorioService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IGeminiVisionService, GeminiVisionService>();
builder.Services.AddScoped<AnomaliaService>();

// Storage de fotos: GCS quando GCS_BUCKET_NAME está setado; senão filesystem local
var gcsBucket = builder.Configuration["GCS_BUCKET_NAME"];
if (!string.IsNullOrWhiteSpace(gcsBucket))
    builder.Services.AddSingleton<IFotoStorage, GcsFotoStorage>();
else
    builder.Services.AddSingleton<IFotoStorage, LocalFotoStorage>();
builder.Services.AddScoped<ITokenGenerator, JwtTokenGeneratorAdapter>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

// Controllers + JSON
builder.Services.AddControllers()
    .AddNewtonsoftJson(opts =>
        opts.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hidrômetro BRK API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Informe: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// CORS — restrito em produção via ALLOWED_ORIGINS (vírgula-separado)
var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
{
    if (builder.Environment.IsDevelopment() || allowedOrigins.Length == 0)
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    else
        p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
}));

// Rate limiting — 3 tentativas de login por minuto por IP
builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy("login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    opts.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Muitas tentativas de login. Aguarde 1 minuto e tente novamente." }, token);
    };
});

var app = builder.Build();

// Migrations automáticas + Seed em desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hidrômetro BRK API v1"));

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HidrometroDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lê .env da pasta do projeto (sobe até encontrar o arquivo ou atingir a raiz).
// Ignora linhas vazias e comentários (#). Não sobrescreve variáveis já definidas no ambiente.
static void LoadDotEnv()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, ".env");
        if (File.Exists(candidate))
        {
            foreach (var line in File.ReadAllLines(candidate))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

                var sep = trimmed.IndexOf('=');
                if (sep <= 0) continue;

                var key = trimmed[..sep].Trim();
                var val = trimmed[(sep + 1)..].Trim();

                // Variável já definida no ambiente real tem precedência
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, val);
            }
            return;
        }
        dir = dir.Parent;
    }
}

// Converte "postgresql://user:pass@host:port/db" → "Host=...;Port=...;Database=...;Username=...;Password=..."
// Aceita também "postgres://" (alias Railway/Heroku). Retorna a string inalterada se não for URI.
static string NpgsqlConnectionString(string raw)
{
    if (!raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        return raw;

    var uri = new Uri(raw);
    var host = uri.Host;
    var port = uri.IsDefaultPort ? 5432 : uri.Port;
    var database = uri.AbsolutePath.TrimStart('/');
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

    var sb = new StringBuilder();
    sb.Append($"Host={host};Port={port};Database={database};Username={username};Password={password}");

    // Suporte a parâmetros extras na query string (ex: ?sslmode=require)
    if (!string.IsNullOrEmpty(uri.Query))
    {
        foreach (var part in uri.Query.TrimStart('?').Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
                sb.Append($";{Uri.UnescapeDataString(kv[0])}={Uri.UnescapeDataString(kv[1])}");
        }
    }

    return sb.ToString();
}
