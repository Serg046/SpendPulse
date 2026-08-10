using System.Globalization;
using SpendPulse.Client.Repositories;
using SpendPulse.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ITransactionRepository, TransactionApiClient>();
builder.Services.AddScoped<ISyncStatusRepository, SyncStatusApiClient>();
builder.Services.AddScoped<IMerchantMappingRepository, MerchantMappingApiClient>();
builder.Services.AddScoped<IMerchantNameExclusionRepository, MerchantNameExclusionApiClient>();
builder.Services.AddScoped<IMerchantGroupRepository, MerchantGroupApiClient>();
builder.Services.AddScoped<ITrendMonthExclusionRepository, TrendMonthExclusionApiClient>();
builder.Services.AddScoped<SyncStatusState>();

await builder.Build().RunAsync();
