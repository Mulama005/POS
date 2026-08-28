using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pos.Api;
using Pos.Api.Authorization;
using Pos.Application.Auth;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;
using Sentry;
using Pos.Infrastructure;

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

// ---------- Swagger UI (needed to test MFA/register endpoints manually) ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Adds the "Authorize" button so a Bearer token can be pasted once and applied
    // to every request for the rest of the Swagger session.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste just the token — no need to type 'Bearer ' yourself."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

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

        options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            {
                context.Fail("Token missing a valid user id.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<PosDbContext>();
            var domainUser = await db.DomainUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (domainUser is null || !domainUser.IsActive)
            {
                context.Fail("This account has been deactivated.");
                return;
            }

            var issuedAt = (context.SecurityToken as System.IdentityModel.Tokens.Jwt.JwtSecurityToken)?.IssuedAt;
            if (domainUser.SessionsRevokedAt is not null &&
                issuedAt is not null &&
                issuedAt.Value.ToUniversalTime() < domainUser.SessionsRevokedAt.Value.UtcDateTime)
            {
                context.Fail("Session was revoked. Please log in again.");
            }
        },
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
            await DevProductSeeder.SeedAsync(scope.ServiceProvider, app.Environment.IsDevelopment());
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

// Hangfire integration temporarily disabled until package/reference is added explicitly.

// ---------- Swagger UI middleware (dev only) ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSentryTracing();
app.UseHttpsRedirection();

app.UseCors("FrontendDev");

app.UseAuthentication(); // must come before UseAuthorization, and now actually does something
app.UseAuthorization();

app.MapControllers();

app.Run();