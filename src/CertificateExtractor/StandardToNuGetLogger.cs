using NuGet.Common;
using StandardLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Knapcode.CertificateExtractor;

public class StandardToNuGetLogger : LoggerBase
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly bool _mapInformationToDebug;

    public StandardToNuGetLogger(Microsoft.Extensions.Logging.ILogger logger) : this(logger, mapInformationToDebug: false)
    {
    }

    public StandardToNuGetLogger(Microsoft.Extensions.Logging.ILogger logger, bool mapInformationToDebug) : base(LogLevel.Debug)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mapInformationToDebug = mapInformationToDebug;
    }

    public override void Log(ILogMessage message)
    {
        var level = message.Level;
        if (_mapInformationToDebug && message.Level == LogLevel.Information)
        {
            level = LogLevel.Verbose;
        }

        if ((int)level >= (int)VerbosityLevel)
        {
            _logger.Log(
                logLevel: GetLogLevel(level),
                eventId: 0,
                state: message,
                exception: null,
                formatter: (s, e) => s.Message);
        }
    }

    public override Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    private static StandardLogLevel GetLogLevel(LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Debug:
                return StandardLogLevel.Trace;
            case LogLevel.Verbose:
                return StandardLogLevel.Debug;
            case LogLevel.Information:
                return StandardLogLevel.Information;
            case LogLevel.Minimal:
                return StandardLogLevel.Information;
            case LogLevel.Warning:
                return StandardLogLevel.Warning;
            case LogLevel.Error:
                return StandardLogLevel.Error;
            default:
                return StandardLogLevel.Trace;
        }
    }
}
