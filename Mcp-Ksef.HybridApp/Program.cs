using System;
using McpKsef.HybridApp.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Shared.Configurations;
using Shared.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<KsefAppSettings>(builder.Configuration, args);

IHost app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();
