using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Pos.Api;
using Pos.Api.Authorization;
using Pos.Application.Auth;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;
using Sentry;

var builder = WebApplication.CreateBuilder(args);


// --- Sentry (Step 7) ---
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Configuration["Sentry:Environment"];
    options.TracesSampleRate = double.Parse(
        builder.Configuration["Sentry:TracesSampleRate"] ?? "0.1");

    // Only verbose in local dev — don't want Sentry's own debug logging in production logs.
    options.Debug = builder.Environment.IsDevelopment();
});

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<PosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Identity ----------
// AddIdentityCore (not AddIdentity) — AddIdentity also wires up cookie-based
// authentication as a side effect, which would fight with the JWT bearer scheme
// registered below over which one is "the" default. AddIdentityCore + explicitly
// adding SignInManager gives just what's needed: UserManager, RoleManager,
// SignInManager, password hashing/lockout — without a competing auth scheme.
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<PosDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// ---------- JWT Bearer authentication ----------
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it via 'dotnet user-secrets' locally " +
        "or an environment variable in production — see Step 8 README.");

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
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PosApi",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PosClient",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30), // small tolerance, not the 5-minute default — access tokens are short-lived by design
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.RegisterScoped, policy =>
        policy.Requirements.Add(new RegisterAccessRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, RegisterAccessHandler>();

// ---------- App services ----------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton<IMemoryCache, MemoryCache>();
builder.Services.AddSingleton<IMfaChallengeStore, MemoryCacheMfaChallengeStore>();
builder.Services.AddSingleton<IMfaService, MfaService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // required so the browser will send the httpOnly refresh cookie cross-origin
    });
});

var app = builder.Build();



using (var scope = app.Services.CreateScope())
{
    var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        try
        {
            await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration, app.Environment.IsDevelopment());
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Identity seeding skipped because the database is unavailable or misconfigured.");
        }
    }
    else
    {
        app.Logger.LogWarning("Identity seeding skipped because no database connection string was configured.");
    }
}

app.UseSentryTracing();
app.UseHttpsRedirection();

app.UseCors("FrontendDev");

app.UseAuthentication(); // must come before UseAuthorization, and now actually does something
app.UseAuthorization();

app.MapControllers();

app.Run();