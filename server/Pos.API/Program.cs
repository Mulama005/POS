using Pos.Infrastructure;
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
// OpenAPI/Swagger registration is disabled here to avoid runtime assembly
// mismatches during local smoke tests. Enable or replace with explicit
// `AddSwaggerGen()` if you update/OpenAPI packages.
// builder.Services.AddOpenApi();

// Infrastructure (database, storage, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
// OpenAPI mapping disabled to avoid type load issues during local runs.

app.UseSentryTracing();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
