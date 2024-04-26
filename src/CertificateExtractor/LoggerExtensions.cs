using Microsoft.Extensions.Logging;

namespace Knapcode.CertificateExtractor;

public static class LoggerExtensions
{
    public static NuGet.Common.ILogger ToNuGetLogger(this ILogger logger, bool mapInformationToDebug)
    {
        return new StandardToNuGetLogger(logger, mapInformationToDebug);
    }

    public static NuGet.Common.ILogger ToNuGetLogger(this ILogger logger)
    {
        return new StandardToNuGetLogger(logger);
    }
}
