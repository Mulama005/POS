using Pos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
