using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using System.CommandLine.Parsing;

namespace Knapcode.CertificateExtractor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCustomSerilog(this IServiceCollection services)
    {
        Log.Logger = CreateLogger(services);
        return services;
    }

    private static Serilog.Core.Logger CreateLogger(IServiceCollection services)
    {
        var scope = services.BuildServiceProvider();
        var parseResult = scope.GetRequiredService<ParseResult>();
        var logLevelOption = parseResult.RootCommandResult.Command.Options.Single(x => x.Name == "log-level");
        var logEventLevel = (LogEventLevel)parseResult.GetValueForOption(logLevelOption)!;

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(logEventLevel)
            .MinimumLevel.Override("System", Max(logEventLevel, LogEventLevel.Warning))
            .MinimumLevel.Override("Microsoft", Max(logEventLevel, LogEventLevel.Information))
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Max(logEventLevel, LogEventLevel.Warning))
            .Enrich.WithExceptionDetails()
            .WriteTo.Console();

        return loggerConfiguration.CreateLogger();
    }

    private static LogEventLevel Max(LogEventLevel a, LogEventLevel b)
    {
        return a > b ? a : b;
    }
}