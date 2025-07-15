using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using WareHouseFileArchiver.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Repositories;
using WareHouseFileArchiver.Models.Domains;
using System.Text.Json.Serialization;
using Serilog;
using Serilog.Exceptions;
using Serilog.Events;
using WareHouseFileArchiver.SignalRHub;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/WareHouseFileArchive_log.txt", rollingInterval: RollingInterval.Minute) 
    .MinimumLevel.Information()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

builder.Services.AddDbContext<WareHouseArchiveAuthDbContext>(opts =>
{
    opts.UseNpgsql(builder.Configuration.GetConnectionString("WareHouseAuthConnectionString"));
});

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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = builder.Configuration["Jwt:Issuer"],
                            ValidAudience = builder.Configuration["Jwt:Audience"],
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
                        });

builder.Services.AddScoped<ITokenRepository, TokenRepository>();

// Add Authorization in Header using Bearer
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

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddDbContext<WareHouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IArchiveFileRepository, ArchiveFileRepository>();

// To prevet enum shown as numbers
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped<IItemRepository, ItemRepository>();

builder.Services.AddSignalR();

#region CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .WithOrigins("http://127.0.0.1:5500", "http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); 
    });
});
#endregion

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

builder.Services.AddScoped<IStatisticsRepository, StatisticsRepository>();

var app = builder.Build();

app.UseCors("AllowAll");
app.MapHub<NotificationsHub>("/notificationhub");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.UseRateLimiter();
app.MapControllers().RequireRateLimiting("PerUserPolicy");
app.Run();
