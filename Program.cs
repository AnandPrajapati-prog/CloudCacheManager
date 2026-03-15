using CloudCacheManager.Models;
using CloudCacheManager.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var basePath = builder.Configuration["AppSettings:BasePath"];

if (string.IsNullOrEmpty(basePath))
{
    basePath = OperatingSystem.IsWindows()
        ? @"D:\CloudCache\UserAppSettings"
        : "/app/data";
}

// Ensure directory exists
if (!Directory.Exists(basePath))
{
    Directory.CreateDirectory(basePath);
}
// ── Bind AppSettings section ─────────────────────────────
builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── Swagger Configuration ───────────────────────────────
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Cloud Cache Manager API",
        Version = "v1",
        Description = "API documentation for Cloud Cache Manager"
    });

    // JWT Authentication support in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT token as: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddSingleton<FileValidationService>();
builder.Services.AddSingleton<FileManagerService>();
builder.Services.AddSingleton<AuthService>();

// ── JWT Authentication ──────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing in appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

// ── Authorization Policies ──────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.CanDownload, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Viewer));

    options.AddPolicy(Policies.CanEdit, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager));
});

// ── CORS ────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// ── Swagger enabled for ALL environments ───────────────
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Cloud Cache Manager API v1");
    options.RoutePrefix = "swagger"; // access via /swagger
});

app.UseStaticFiles();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();