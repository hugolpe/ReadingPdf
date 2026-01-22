using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReadingPdf.Data;
using ReadingPdf.Services;

var builder = WebApplication.CreateBuilder(args);

// ======================================
// CORS
// ======================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        policy.WithOrigins("https://localhost:44392")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ======================================
// MVC Controllers
// ======================================
builder.Services.AddControllers();

// ======================================
// 🔹 Registrar ApplicationDbContext
// ======================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// ======================================
// 🔹 Registrar AccountPredictionService
// ======================================
builder.Services.AddSingleton<AccountPredictionService>(sp =>
{
    return new AccountPredictionService(seed: 123);
});

// register client in DI (add near other service registrations)
builder.Services.AddHttpClient<IPredictionApiClient, PredictionApiClient>(client =>
{
    var baseUrl = builder.Configuration["PredictionApi:BaseUrl"] ?? "http://localhost:7283/";
    client.BaseAddress = new Uri(baseUrl);
});

// 🔹 Registrar IIF builder service para QuickBooks
builder.Services.AddSingleton<IIIFBuilderService, IIFBuilderService>();

// Ensure IQuickBooksService is registered so the controller can use it
builder.Services.AddScoped<IQuickBooksService, QuickBooksService>();

var app = builder.Build();

// ======================================
// PIPELINE
// ======================================
app.UseRouting();
app.UseCors("LocalDev");
app.UseAuthorization();

app.MapControllers();

app.Run();
