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

// Conversión de connection string: Render la da en formato URL (postgres://...),
// Npgsql necesita el formato clásico (Host=...;Username=...)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"[DEBUG] Connection string recibida (longitud): {connectionString?.Length ?? 0}");
Console.WriteLine($"[DEBUG] Empieza con postgres://: {connectionString?.StartsWith("postgres://") ?? false}");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings__DefaultConnection no está configurada. Revisá las variables de entorno en Render.");
}

if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    var puerto = uri.Port == -1 ? 5432 : uri.Port;

    connectionString = $"Host={uri.Host};Port={puerto};Database={uri.AbsolutePath.TrimStart('/')};" +
        $"Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpClient<GeminiCategorizadorService>();
builder.Services.AddScoped<ICategorizadorIA, GeminiCategorizadorService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins(
                "http://localhost:5173",                      // desarrollo local
                "https://control-de-finanzas-eight.vercel.app"               // reemplazar con tu URL real de Vercel
              )
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Aplica migraciones pendientes automáticamente al arrancar (necesario en Render,
// donde la base arranca vacía y no podés correr `dotnet ef` a mano)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

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