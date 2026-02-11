using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ReadingPdf.Data;
using ReadingPdf.Library;
using ReadingPdf.Services;
using System.Net.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// =======================================================
// 🔹 Kestrel HTTPS Configuration
// =======================================================
// Limpiar cualquier configuración vieja de certificados que Kestrel lea automáticamente
builder.Configuration["Kestrel:Certificates:Default:Path"] = null;
builder.Configuration["Kestrel:Certificates:Default:Password"] = null;

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // HTTP
    serverOptions.Listen(IPAddress.Any, 5000);

    var certPath = Environment.GetEnvironmentVariable("CERT_PATH")
                   ?? builder.Configuration["Kestrel:Certificates:Path"]
                   ?? "devcert.pfx";

    var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD")
                       ?? builder.Configuration["Kestrel:Certificates:Password"]
                       ?? "MiPassword123!";

    if (File.Exists(certPath))
    {
        serverOptions.Listen(IPAddress.Any, 44392, listenOptions =>
        {
            listenOptions.UseHttps(certPath, certPassword);
        });
        Console.WriteLine($"✅ HTTPS habilitado en puerto 44392 con certificado: {Path.GetFullPath(certPath)}");
    }
    else
    {
        Console.WriteLine($"⚠️ Certificado no encontrado en: {Path.GetFullPath(certPath)}");
        Console.WriteLine("HTTPS no estará disponible.");
        Console.WriteLine("Ejecuta: dotnet dev-certs https -ep ./devcert.pfx -p MiPassword123!");
    }
});
//builder.WebHost.ConfigureKestrel(serverOptions =>
//{
//    // HTTP
//    serverOptions.Listen(IPAddress.Any, 5000);

//    // HTTPS — busca certificado por este orden:
//    // 1) Variable de entorno CERT_PATH / CERT_PASSWORD
//    // 2) appsettings.json → Kestrel:Certificates:Path / Password
//    // 3) Fallback a devcert.pfx
//    var certPath = Environment.GetEnvironmentVariable("CERT_PATH")
//                   ?? builder.Configuration["Kestrel:Certificates:Path"]
//                   ?? "devcert.pfx";

//    var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD")
//                       ?? builder.Configuration["Kestrel:Certificates:Password"]
//                       ?? "MiPassword123!";

//    if (File.Exists(certPath))
//    {
//        serverOptions.Listen(IPAddress.Any, 44392, listenOptions =>
//        {
//            listenOptions.UseHttps(certPath, certPassword);
//        });
//        Console.WriteLine($"✅ HTTPS habilitado en puerto 44392 con certificado: {Path.GetFullPath(certPath)}");
//    }
//    else
//    {
//        Console.WriteLine($"⚠️ Certificado no encontrado en: {Path.GetFullPath(certPath)}");
//        Console.WriteLine("HTTPS no estará disponible.");
//        Console.WriteLine("Ejecuta: dotnet dev-certs https -ep ./devcert.pfx -p MiPassword123!");
//    }
//});

// =======================================================
// 🔹 1. Conexión a SQL Server
// =======================================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =======================================================
// 🔹 2. Clearbit (tu servicio HTTP)
// =======================================================
builder.Services.AddHttpClient<IClearbitService, ClearbitService>(client =>
{
    client.BaseAddress = new Uri("https://company.clearbit.com");
});

// =======================================================
// 🔹 3. EPPlus
// =======================================================
ExcelPackage.License.SetNonCommercialPersonal("HugoVargas");

// =======================================================
// 🔹 4. Registrar servicios propios
// =======================================================
builder.Services.AddScoped<LTerceros>();
builder.Services.AddSingleton<IIIFBuilderService, IIFBuilderService>();
builder.Services.AddSingleton<AccountPredictionService>();

// =======================================================
// 🔹 5. CORS Configuration
// =======================================================
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new[]
    {
        "https://localhost:44392",
        "http://localhost:44392",
        "https://localhost:5001",
        "http://localhost:5000"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMvcApp", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// =======================================================
// 🔹 6. PredictionApi HttpClient
// =======================================================
builder.Services.AddHttpClient<IPredictionApiClient, PredictionApiClient>(client =>
{
    var baseUrl = builder.Configuration["PredictionApi:BaseUrl"] ?? "http://localhost:7164/";
    if (!baseUrl.EndsWith("/")) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
});

// =======================================================
// 🔹 6.1 Activation API warm-up hosted service
// =======================================================
var activationBase = builder.Configuration["ActivationApi:BaseUrl"]
                     ?? builder.Configuration["PredictionApi:BaseUrl"]
                     ?? "http://localhost:7164/";

if (!activationBase.Contains("localhost:7165", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient();
    builder.Services.AddHostedService<WarmUpHostedService>();
}

// =======================================================
// 🔹 7. QuickBooks Service
// =======================================================
builder.Services.AddScoped<ReadingPdf.Services.IQuickBooksService, ReadingPdf.Services.QuickBooksService>();

// =======================================================
// 🔹 8. Controllers MVC + API
// =======================================================
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

// =======================================================
// 🔹 9. Build Application
// =======================================================
var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🚀 Aplicación iniciando en: {Urls}", string.Join(", ", app.Urls));
});

// =======================================================
// 🔹 10. Middleware Pipeline
// =======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowMvcApp");
app.UseAuthorization();

// =======================================================
// 🔹 11. Rutas MVC
// =======================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapAreaControllerRoute(
    name: "Terceros",
    areaName: "Terceros",
    pattern: "{controller=Terceros}/{action=Terceros}/{id?}");

// =======================================================
// 🔹 12. Rutas API
// =======================================================
app.MapControllers();

app.Run();