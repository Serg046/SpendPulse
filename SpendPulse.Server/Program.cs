using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SpendPulse.Client.Models;
using SpendPulse.Client.Repositories;
using SpendPulse.Client.Services;
using SpendPulse.Server.Authentication;
using SpendPulse.Server.Components;
using SpendPulse.Server.Models;
using SpendPulse.Server.Repositories;
using SpendPulse.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration["MongoDb:ConnectionString"]));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(builder.Configuration["MongoDb:DatabaseName"]));
builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
    new ConfigureOptions<KeyManagementOptions>(options =>
        options.XmlRepository = new MongoXmlRepository(sp.GetRequiredService<IMongoDatabase>())));
builder.Services.AddDataProtection().SetApplicationName("SpendPulse");
builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
builder.Services.AddSingleton<TransactionRepository>();
builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<TransactionRepository>());
builder.Services.AddSingleton<ISyncStatusRepository, SyncStatusRepository>();
builder.Services.AddSingleton<ISyncHistoryRepository, SyncHistoryRepository>();
builder.Services.AddSingleton<IMerchantMappingRepository, MerchantMappingRepository>();
builder.Services.AddSingleton<IMerchantNameExclusionRepository, MerchantNameExclusionRepository>();
builder.Services.AddSingleton<IMerchantGroupRepository, MerchantGroupRepository>();
builder.Services.AddSingleton<ITrendMonthExclusionRepository, TrendMonthExclusionRepository>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddScoped<SyncStatusState>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = context =>
        {
            var username = context.Principal?.Identity?.Name;
            var stamp = context.Principal?.FindFirst("PasswordStamp")?.Value;
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var user = configuration.GetSection("Auth:Users").Get<List<AuthUser>>()?
                .FirstOrDefault(u => u.Username == username);

            if (user is null || stamp != ComputePasswordStamp(user.Password))
            {
                context.RejectPrincipal();
            }

            return Task.CompletedTask;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null);
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();
    app.Logger.LogInformation("{Method} {Path} -> {StatusCode} ({ElapsedMs}ms)",
        context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/api/transactions", async (DateOnly from, DateOnly to, ITransactionRepository repo) =>
    await repo.Get(from, to));
app.MapGet("/api/merchants", async (ITransactionRepository repo) =>
    await repo.GetDistinctMerchantNames());
app.MapGet("/api/merchant-totals", async (ITransactionRepository repo) =>
    await repo.GetTotalSpentByMerchant());
app.MapGet("/api/monthly-spend", async (DateOnly from, DateOnly to, ITransactionRepository repo) =>
    await repo.GetMonthlySpendByMerchant(from, to));
app.MapGet("/api/monthly-spend/top-merchants", async (DateOnly from, DateOnly to, int topN, ITransactionRepository repo) =>
    await repo.GetTopMerchantsMonthlySpend(from, to, topN));
app.MapGet("/api/monthly-spend/merchant", async (DateOnly from, DateOnly to, string merchant, ITransactionRepository repo) =>
    await repo.GetMonthlySpendForMerchant(from, to, merchant));
app.MapGet("/api/earliest-transaction-date", async (ITransactionRepository repo) =>
    await repo.GetEarliestBookingDate());
app.MapGet("/api/merchant-mappings", async (IMerchantMappingRepository repo) =>
    await repo.GetAll());
app.MapPost("/api/merchant-mappings", async (MerchantMapping mapping, IMerchantMappingRepository repo) =>
{
    try
    {
        await repo.SetMapping(mapping.MappedFrom, mapping.MappedTo);
        return Results.Ok();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapPost("/api/merchant-mappings/remove", async ([FromBody] string mappedFrom, IMerchantMappingRepository repo) =>
{
    await repo.RemoveMapping(mappedFrom);
    return Results.Ok();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapGet("/api/merchant-name-exclusions", async (IMerchantNameExclusionRepository repo) =>
    await repo.GetAll());
app.MapPost("/api/merchant-name-exclusions", async ([FromBody] string word, IMerchantNameExclusionRepository repo) =>
{
    await repo.Add(word);
    return Results.Ok();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapPost("/api/merchant-name-exclusions/remove", async ([FromBody] string word, IMerchantNameExclusionRepository repo) =>
{
    await repo.Remove(word);
    return Results.Ok();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapGet("/api/trend-month-exclusions", async (ITrendMonthExclusionRepository repo) =>
    await repo.GetAll());
app.MapPost("/api/trend-month-exclusions", async ([FromBody] DateOnly month, ITrendMonthExclusionRepository repo) =>
{
    await repo.Add(month);
    return Results.Ok();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapPost("/api/trend-month-exclusions/remove", async ([FromBody] DateOnly month, ITrendMonthExclusionRepository repo) =>
{
    await repo.Remove(month);
    return Results.Ok();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapGet("/api/merchant-groups", async (IMerchantGroupRepository repo) =>
    await repo.GetGroups());
app.MapPost("/api/merchant-groups", async ([FromBody] string name, IMerchantGroupRepository repo) =>
{
    try
    {
        await repo.AddGroup(name);
        return Results.Ok();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapPost("/api/merchant-groups/rename", async (RenameGroupRequest request, IMerchantGroupRepository repo) =>
{
    try
    {
        await repo.RenameGroup(request.OldName, request.NewName);
        return Results.Ok();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapPost("/api/merchant-groups/remove", async ([FromBody] string name, IMerchantGroupRepository repo) =>
{
    await repo.RemoveGroup(name);
    return Results.Ok();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapGet("/api/merchant-group-assignments", async (IMerchantGroupRepository repo) =>
    await repo.GetAssignments());
app.MapPost("/api/merchant-group-assignments", async (MerchantGroupAssignment assignment, IMerchantGroupRepository repo) =>
{
    await repo.SetAssignment(assignment.MerchantName, assignment.GroupName);
    return Results.Ok();
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });
app.MapGet("/api/sync-status/username", async (ISyncStatusRepository repo) =>
    await repo.GetUsername());
app.MapGet("/api/sync-status/is-admin", async (ISyncStatusRepository repo) =>
    await repo.IsAdmin());
app.MapGet("/api/sync-status/last-data-update", async (ISyncStatusRepository repo) =>
    await repo.GetLastDataUpdate());
app.MapGet("/api/sync-status/is-token-expiring-soon", async (ISyncStatusRepository repo) =>
    await repo.IsTokenExpiringSoon());
app.MapGet("/api/sync-status/history", async (int page, int pageSize, ISyncStatusRepository repo) =>
    await repo.GetSyncHistory(page, pageSize));
app.MapPost("/api/sync-status/sync", async (ISyncStatusRepository repo) =>
    await repo.Sync())
    .RequireAuthorization(new AuthorizeAttribute
    {
        Roles = "Admin",
        AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme},Basic"
    });
app.MapPost("/api/sync-status/refresh-token", async (string? code, ISyncStatusRepository repo) =>
    Results.Json(await repo.RefreshToken(code)))
    .RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

app.MapPost("/api/auth/login", async (HttpContext http, IConfiguration configuration) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var user = configuration.GetSection("Auth:Users").Get<List<AuthUser>>()?
        .FirstOrDefault(u => u.Username == username && u.Password == password);

    if (user is null)
    {
        return Results.Redirect("/login?error=1");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
        new("PasswordStamp", ComputePasswordStamp(user.Password))
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });

    return Results.Redirect("/");
}).AllowAnonymous();

app.MapGet("/api/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SpendPulse.Client._Imports).Assembly);

app.Run();

string ComputePasswordStamp(string password) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
