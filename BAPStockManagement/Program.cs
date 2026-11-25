using BAPStockManagement.Data;
using BAPStockManagement.Configuration;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using BAPStockManagement.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SeedSuperAdminOptions>(
    builder.Configuration.GetSection(SeedSuperAdminOptions.SectionName));

builder.Services.Configure<BillingOptions>(
    builder.Configuration.GetSection(BillingOptions.SectionName));

builder.Services.AddScoped<BAPStockManagement.Services.SaleBillService>();
builder.Services.AddScoped<BAPStockManagement.Services.SaleBillExcelExporter>();
builder.Services.AddScoped<BAPStockManagement.Services.StockPredictionService>();
builder.Services.AddScoped<BAPStockManagement.Services.ISmartStockDemoDataProvider, BAPStockManagement.Services.SmartStockDemoDataService>();

builder.Services.AddControllersWithViews();

var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
var connectionStringBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configuredConnectionString);
var connectionPassword = builder.Configuration["ConnectionStrings:DefaultConnectionPassword"];
if (!string.IsNullOrWhiteSpace(connectionPassword))
{
    connectionStringBuilder.Password = connectionPassword;
}

if (string.IsNullOrWhiteSpace(connectionStringBuilder.Password))
{
    throw new InvalidOperationException(
        "Set ConnectionStrings:DefaultConnectionPassword using User Secrets or an environment variable.");
}

builder.Services.AddDbContext<BAPStockContext>(options =>
    options.UseSqlServer(
        connectionStringBuilder.ConnectionString,
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<BAPStockContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("en-GB") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-GB"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (builder.Configuration.GetValue<bool>("Database:RunStartupSeeders"))
{
    using var scope = app.Services.CreateScope();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    await StockDocumentSeeder.EnsureAsync(scope.ServiceProvider);
    await ProductVariantSeeder.SeedAsync(scope.ServiceProvider);
    await StockLedgerRepairSeeder.EnsureAsync(scope.ServiceProvider);
}
else
{
    app.Logger.LogInformation(
        "Startup database seeders are disabled. Set Database:RunStartupSeeders=true to opt in.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
