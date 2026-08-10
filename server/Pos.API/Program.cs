using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddDbContext<PosDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

// Configure the HTTP request pipeline.
// OpenAPI mapping disabled to avoid type load issues during local runs.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
