using GestorGastos.Api.Data;
using GestorGastos.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<GeminiCategorizadorService>();
builder.Services.AddScoped<ICategorizadorIA, GeminiCategorizadorService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:5173") // puerto típico de Vite
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseMiddleware<GestorGastos.Api.Middleware.ManejadorExcepcionesMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendDev");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();