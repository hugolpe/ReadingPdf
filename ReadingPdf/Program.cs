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

// Prefer environment variables for certificate configuration. Supported env var names (in order):
// 1) CERT_PATH / CERT_PASSWORD (simple custom names)
// 2) Kestrel__Certificates__Default__Path / Kestrel__Certificates__Default__Password
// 3) ASPNETCORE_Kestrel__Certificates__Default__Path / ASPNETCORE_Kestrel__Certificates__Default__Password
string GetEnv(params string[] names)
{
    foreach (var n in names)
    {
        var v = Environment.GetEnvironmentVariable(n);
        if (!string.IsNullOrEmpty(v)) return v;
    }
    return null;
}

var envCertPath = GetEnv("CERT_PATH", "Kestrel__Certificates__Default__Path", "ASPNETCORE_Kestrel__Certificates__Default__Path");
var envCertPassword = GetEnv("CERT_PASSWORD", "Kestrel__Certificates__Default__Password", "ASPNETCORE_Kestrel__Certificates__Default__Password");

// Fallback to configuration (appsettings.*.json, user secrets, etc.)
var configCertPath = builder.Configuration["Kestrel:Certificates:Default:Path"];
var configCertPassword = builder.Configuration["Kestrel:Certificates:Default:Password"];

var certPath = !string.IsNullOrEmpty(envCertPath) ? envCertPath : configCertPath;
var certPassword = !string.IsNullOrEmpty(envCertPassword) ? envCertPassword : configCertPassword;

// Bind Kestrel explicitly to HTTPS on localhost:44392.
// In Production you must configure a PFX via configuration (Kestrel:Certificates:Default:Path / Password).
//// In Development the default dev certificate will be used automatically.
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Loopback, 44392, listenOptions =>
    {
        if (!string.IsNullOrEmpty(certPath))
        {
            var cert = new X509Certificate2(certPath, certPassword ?? string.Empty);
            listenOptions.UseHttps(cert);
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Use the development certificate (requires dotnet dev-certs trusted)
            listenOptions.UseHttps();
        }
        else
        {
            // Fail fast so you see the problem in logs when published
            throw new InvalidOperationException("HTTPS certificate not configured for port 44392. Set CERT_PATH/CERT_PASSWORD or Kestrel:Certificates:Default:Path and Password in configuration.");
        }
    });
});

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
//ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
ExcelPackage.License.SetNonCommercialPersonal("HugoVargas");

// =======================================================
// 🔹 4. Registrar servicios propios
// =======================================================
builder.Services.AddScoped<LTerceros>();

// IIF builder
builder.Services.AddSingleton<IIIFBuilderService, IIFBuilderService>();

// ⭐️⭐️ REGISTRO CRÍTICO ⭐️⭐️
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
        // .AllowCredentials(); // Descomenta si usas cookies/autenticación
    });
});

// =======================================================
// 🔹 6. PredictionApi HttpClient
// =======================================================
// NOTE: default fallback changed to the requested API host (localhost:7164)
builder.Services.AddHttpClient<IPredictionApiClient, PredictionApiClient>(client =>
{
    var baseUrl = builder.Configuration["PredictionApi:BaseUrl"] ?? "http://localhost:7164/";
    if (!baseUrl.EndsWith("/")) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
});

// =======================================================
// 🔹 6.1 Activation API warm-up hosted service (disabled for localhost:7165)
// =======================================================
var activationBase = builder.Configuration["ActivationApi:BaseUrl"]
                     ?? builder.Configuration["PredictionApi:BaseUrl"]
                     ?? "http://localhost:7164/";

if (!activationBase.Contains("localhost:7165", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient(); // ensures IHttpClientFactory is available
    builder.Services.AddHostedService<WarmUpHostedService>();
}
else
{
    // Warm-up skipped because target is localhost:7165
}

// =======================================================
// 🔹 6.2 PredictionApi warm-up hosted service (activate automatically)
// =======================================================
//builder.Services.AddHostedService<PredictionApiWarmUpHostedService>();

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