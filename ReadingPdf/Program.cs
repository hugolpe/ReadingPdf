using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using ReadingPdf.Data;
using ReadingPdf.Library;
using ReadingPdf.Services;

var builder = WebApplication.CreateBuilder(args);

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

// IIF builder
builder.Services.AddSingleton<IIIFBuilderService, IIFBuilderService>();

// ⭐⭐ REGISTRO CRÍTICO — ESTE FALTABA ⭐⭐
builder.Services.AddSingleton<AccountPredictionService>();

// Read allowed origins from configuration (appsettings / environment)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new []
    {
        "https://localhost:44392",
        "http://localhost:44392"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMvcApp", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // .AllowCredentials() // habilita solo si usas cookies/credenciales y orígenes específicos
            ;
    });
});

// Ensure PredictionApi base URL comes from config / env
builder.Services.AddHttpClient<IPredictionApiClient, PredictionApiClient>(client =>
{
    var baseUrl = builder.Configuration["PredictionApi:BaseUrl"] ?? "https://localhost:7283/";
    if (!baseUrl.EndsWith("/")) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddScoped<ReadingPdf.Services.IQuickBooksService, ReadingPdf.Services.QuickBooksService>();



// =======================================================
// 🔹 5. Controllers MVC + Controllers API
// =======================================================
builder.Services.AddControllersWithViews();
builder.Services.AddControllers(); // <-- necesario para MapControllers()

var app = builder.Build();

// =======================================================
// 🔹 6. Pipeline
// =======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowMvcApp");

// mantén el orden: UseRouting, UseCors, UseAuthorization
app.UseAuthorization();


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}



// =======================================================
// 🔹 7. Rutas MVC
// =======================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapAreaControllerRoute(
    name: "Terceros",
    areaName: "Terceros",
    pattern: "{controller=Terceros}/{action=Terceros}/{id?}");

// =======================================================
// 🔹 8. Rutas API (ESTO FALTABA COMPLETAMENTE)
// =======================================================
app.MapControllers();  // <-- necesario para api/Training/... etc.

app.Run();
