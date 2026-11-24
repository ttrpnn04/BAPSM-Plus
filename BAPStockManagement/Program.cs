using BAPStockManagement.Data;
using BAPStockManagement.Configuration;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using BAPStockManagement.Models;

var builder = WebApplication.CreateBuilder(args);
var dashboardPreviewEnabled =
    BAPStockManagement.Services.Dashboard.DashboardPreviewMode.IsEnabled(
        builder.Configuration,
        builder.Environment);

builder.Services.Configure<SeedSuperAdminOptions>(
    builder.Configuration.GetSection(SeedSuperAdminOptions.SectionName));

builder.Services.Configure<BillingOptions>(
    builder.Configuration.GetSection(BillingOptions.SectionName));

// ===== Dashboard feature (BEGIN) =====
builder.Services
    .AddOptions<BAPStockManagement.Services.Dashboard.DashboardOptions>()
    .Bind(builder.Configuration.GetSection(
        BAPStockManagement.Services.Dashboard.DashboardOptions.SectionName))
    .Validate(
        options => Enum.IsDefined(options.DataSource),
        "Dashboard:DataSource must be Mock or Ef.")
    .Validate(
        options => options.SimulatedLatencyMs >= 0,
        "Dashboard:SimulatedLatencyMs must be zero or greater.")
    .ValidateOnStart();

builder.Services.AddScoped<BAPStockManagement.Services.Dashboard.MockDashboardService>();
builder.Services.AddScoped<BAPStockManagement.Services.Dashboard.EfDashboardService>();
builder.Services.AddScoped<BAPStockManagement.Services.Dashboard.IDashboardService>(services =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<
            BAPStockManagement.Services.Dashboard.DashboardOptions>>()
        .Value;

    return options.DataSource switch
    {
        BAPStockManagement.Services.Dashboard.DashboardDataSource.Mock =>
            services.GetRequiredService<BAPStockManagement.Services.Dashboard.MockDashboardService>(),
        BAPStockManagement.Services.Dashboard.DashboardDataSource.Ef =>
            services.GetRequiredService<BAPStockManagement.Services.Dashboard.EfDashboardService>(),
        _ => throw new InvalidOperationException(
            $"Unsupported dashboard data source: {options.DataSource}.")
    };
});
builder.Services.AddSingleton<
    BAPStockManagement.Services.Dashboard.IPurchaseRequestService,
    BAPStockManagement.Services.Dashboard.MockPurchaseRequestService>();

builder.Services.AddAntiforgery(options =>
    options.HeaderName = "RequestVerificationToken");
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(
            new BAPStockManagement.Services.Dashboard.UpperSnakeCaseNamingPolicy(),
            allowIntegerValues: false)));
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
            context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        var result = new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});
// ===== Dashboard feature (END) =====

builder.Services.AddScoped<BAPStockManagement.Services.SaleBillService>();
builder.Services.AddScoped<BAPStockManagement.Services.SaleBillExcelExporter>();

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<BAPStockContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// ===== Dashboard API cookie behavior (BEGIN) =====
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
// ===== Dashboard API cookie behavior (END) =====

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// ===== Dashboard API middleware (BEGIN) =====
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    api =>
    {
        api.UseExceptionHandler();
        api.UseStatusCodePages(async statusContext =>
        {
            var httpContext = statusContext.HttpContext;
            var statusCode = httpContext.Response.StatusCode;
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = statusCode,
                Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases
                    .GetReasonPhrase(statusCode),
                Instance = httpContext.Request.Path
            };
            await httpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>()
                .TryWriteAsync(new Microsoft.AspNetCore.Http.ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = problem
                });
        });
    });
// ===== Dashboard API middleware (END) =====

var supportedCultures = new[] { new CultureInfo("en-GB") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-GB"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (!dashboardPreviewEnabled)
{
    using var scope = app.Services.CreateScope();
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    await StockDocumentSeeder.EnsureAsync(scope.ServiceProvider);
    await ProductVariantSeeder.SeedAsync(scope.ServiceProvider);
    await StockLedgerRepairSeeder.EnsureAsync(scope.ServiceProvider);
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

// ===== Dashboard API endpoints (BEGIN) =====
app.MapControllers();
// ===== Dashboard API endpoints (END) =====

app.Run();
