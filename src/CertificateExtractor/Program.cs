using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using Knapcode.CertificateExtractor;
using Microsoft.Extensions.Hosting;
using NuGet.Protocol.Core.Types;
using Serilog;

var builder = new UserAgentStringBuilder("Knapcode.CertificateExtractor")
    .WithOSDescription(string.Join("; ", new object[]
    {
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture,
        RuntimeInformation.FrameworkDescription,
        RuntimeInformation.RuntimeIdentifier,
    }));
UserAgent.SetUserAgentString(builder);

var runner = new CommandLineBuilder(new CertificateExtractorCommand())
    .UseExceptionHandler()
    .UseHost(_ => Host.CreateDefaultBuilder(args), (builder) => builder
        .UseSerilog()
        .ConfigureServices((hostContext, services) =>
        {
            services.AddCustomSerilog();
        })
        .UseCommandHandler<CertificateExtractorCommand, CertificateExtractorCommand.Handler>())
    .UseHelp()
    .UseVersionOption()
    .UseParseErrorReporting()
    .Build();

if (args.Length == 0 || args.All(string.IsNullOrWhiteSpace))
{
    args = ["--help"];
}

return await runner.Parse(args).InvokeAsync();
