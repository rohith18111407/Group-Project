using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Repositories;
using WareHouseFileArchiver.Models.Domains;
using System.Text.Json.Serialization;
using Serilog;
using WareHouseFileArchiver.SignalRHub;
using System.Threading.RateLimiting;
using WareHouseFileArchiver.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// Configure Serilog
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/WareHouseFileArchive_log.txt", rollingInterval: RollingInterval.Minute) 
    .MinimumLevel.Information()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

// Database contexts
builder.Services.AddDbContext<WareHouseArchiveAuthDbContext>(opts =>
{
    opts.UseNpgsql(builder.Configuration.GetConnectionString("WareHouseAuthConnectionString"));
});

builder.Services.AddDbContext<WareHouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity configuration
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>("WareHouseFileArchiver")
    .AddEntityFrameworkStores<WareHouseArchiveAuthDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserManager<ApplicationUser>>();
builder.Services.AddScoped<SignInManager<ApplicationUser>>();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Configure JWT for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationhub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"JWT Token validated for user: {context.Principal?.FindFirst(ClaimTypes.Email)?.Value}");
            return Task.CompletedTask;
        }
    };
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .WithOrigins("http://127.0.0.1:5500", "http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true); // Allow any origin for SignalR
    });
});

// SignalR Configuration
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Enable for debugging
    options.KeepAliveInterval = TimeSpan.FromMinutes(1);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
});

// Repository and Service Registration
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IArchiveFileRepository, ArchiveFileRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IStatisticsRepository, StatisticsRepository>();

// Background Services
builder.Services.AddHostedService<AdminArchivalService>();

// JSON Configuration
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PerUserPolicy", context =>
    {
        var username = context.User.Identity?.Name ?? context.Request.Headers["X-UserId"].FirstOrDefault() ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(username, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1000,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromHours(1),
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

// Swagger Configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "WareHouseArchive", Version = "v1" });
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = JwtBearerDefaults.AuthenticationScheme
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                },
                Scheme = "Oauth2",
                Name = JwtBearerDefaults.AuthenticationScheme,
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware order is important!
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseRateLimiter();

// Map SignalR Hub AFTER authentication/authorization
app.MapHub<NotificationsHub>("/notificationhub");

app.MapControllers().RequireRateLimiting("PerUserPolicy");

Console.WriteLine("Starting Warehouse File Archiver API...");
Console.WriteLine($"JWT Issuer: {jwtIssuer}");
Console.WriteLine($"JWT Audience: {jwtAudience}");

app.Run();