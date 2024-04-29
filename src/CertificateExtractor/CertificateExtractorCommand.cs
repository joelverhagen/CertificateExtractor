using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using Serilog.Events;
using IOFile = System.IO.File;

namespace Knapcode.CertificateExtractor;

public enum CertificateFormat
{
    CER,
    PEM,
}

public class CertificateExtractorCommand : RootCommand
{
    public const string FormatOption = "--format";
    private const string AllOption = "--all";

    public CertificateExtractorCommand() : base(
        $"""
        This is a CLI tool to extract certificate files from NuGet packages.

        Use a combination of options to filter in (include) categories of certificates contained in a package that should be extracted. If no options are provided, no certificates will be extracted.
        
        The values that can be specified by the {FormatOption} option are:
        CER: This is a binary reprenstation of the ASN.1 using DER encoding. The file extension will be ".cer".
        PEM: This is a PEM encoding, which is essentially base64 representation of the DER encoded ASN.1. The file extension will be ".pem".
        """)
    {
        Name = "nuget-cert-extractor";

        AddOption(new Option<FileInfo?>(
            name: "--file",
            description: "A file path for an input .nupkg.")
        {
            IsRequired = true,
        });

        AddOption(new Option<DirectoryInfo?>(
            name: "--output",
            description: "A destination directory for writing extracted certificates to")
        {
            IsRequired = true,
        });

        AddOption(new Option<CertificateFormat>(
            name: FormatOption,
            () => CertificateFormat.CER,
            description: $"The format to use for writing certificate files"));

        AddOption(new Option<bool>(
            name: AllOption,
            description: "Extract all certificates"));

        AddOption(new Option<bool>(
            name: "--author",
            description: "Extract certificates used in the author signature"));

        AddOption(new Option<bool>(
            name: "--repository",
            description: "Extract certificates used in the repository signature"));

        AddOption(new Option<bool>(
            name: "--leaf",
            description: "Extract leaf certificates"));

        AddOption(new Option<bool>(
            name: "--intermediate",
            description: "Extract intermediate certificates"));

        AddOption(new Option<bool>(
            name: "--root",
            description: "Extract root certificates"));

        AddOption(new Option<bool>(
            name: "--code-signing",
            description: "Extract certificates used in code sign signatures"));

        AddOption(new Option<bool>(
            name: "--timestamping",
            description: "Extract certificates used in timestamp signatures"));

        AddGlobalOption(new Option<LogEventLevel>(
            "--log-level",
            () => LogEventLevel.Information,
            "The minimum log level to display. Possible values: " + string.Join(", ", Enum.GetNames<LogEventLevel>()))
        {
            ArgumentHelpName = "level",
        });
    }

    public new class Handler : ICommandHandler
    {
        private readonly ILogger<Handler> _logger;
        // private readonly NuGet.Common.ILogger _nuGetLogger;

        public FileInfo? File { get; set; }
        public DirectoryInfo? Output { get; set; }
        public CertificateFormat Format { get; set; }
        public bool All { get; set; }
        public bool Author { get; set; }
        public bool Repository { get; set; }
        public bool Leaf { get; set; }
        public bool Intermediate { get; set; }
        public bool Root { get; set; }
        public bool CodeSigning { get; set; }
        public bool Timestamping { get; set; }

        private List<X509Certificate2> _extraCertificates = new();
        private List<string> _writtenPaths = new();

        public Handler(ILogger<Handler> logger)
        {
            _logger = logger;
            // _nuGetLogger = logger.ToNuGetLogger(mapInformationToDebug: true);
        }

        public int Invoke(InvocationContext context)
        {
            throw new NotImplementedException();
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var token = context.GetCancellationToken();
            return await InvokeAsync(token);
        }

        private async Task<int> InvokeAsync(CancellationToken token)
        {
            if (All)
            {
                Author = true;
                Repository = true;
                Leaf = true;
                Intermediate = true;
                Root = true;
                CodeSigning = true;
                Timestamping = true;
            }

            var reader = new PackageArchiveReader(File!.FullName);
            if (!await reader.IsSignedAsync(token))
            {
                _logger.LogWarning("The provided package is not signed. No certificates will be extracted.");
                return 1;
            }

            var primary = await reader.GetPrimarySignatureAsync(token);
            AddExtraCertificates(primary.SignedCms);
            ExtractSignature(primary);

            var counterSignature = RepositoryCountersignature.GetRepositoryCountersignature(primary);
            if (counterSignature != null)
            {
                ExtractSignature(counterSignature);
            }

            if (_writtenPaths.Count == 0)
            {
                _logger.LogWarning($"No certificates were written. Consider using the {AllOption} or a combination of the other more specific options.");
            }

            return 0;
        }

        private void AddExtraCertificates(SignedCms signedCms)
        {
            _extraCertificates.AddRange(signedCms.Certificates);
        }

        private void ExtractSignature(Signature signature)
        {
            string type;

            if (signature.Type == SignatureType.Author)
            {
                if (!Author)
                {
                    return;
                }

                type = "author";
                _logger.LogDebug("Reading the author signature.");
            }
            else if (signature.Type == SignatureType.Repository)
            {
                if (!Repository)
                {
                    return;
                }

                type = "repository";
                _logger.LogDebug("Reading the repository signature.");
            }
            else
            {
                _logger.LogWarning("Ignoring package signature with an unknown type.");
                return;
            }

            if (CodeSigning)
            {
                _logger.LogDebug("Reading a code signing signature.");
                ExtractSignerInfo(type + " code signing", signature.SignerInfo);
            }

            if (Timestamping)
            {
                foreach (var timestamp in signature.Timestamps)
                {
                    AddExtraCertificates(timestamp.SignedCms);
                    _logger.LogDebug("Reading a timestamper signature.");
                    ExtractSignerInfo(type + " timestamper", timestamp.SignerInfo);
                }
            }
        }

        private void ExtractSignerInfo(string type, SignerInfo signerInfo)
        {
            var leaf = signerInfo.Certificate!;

            if (Leaf)
            {
                SaveCertificate(type + " leaf", leaf);
            }

            using var chain = new X509Chain(useMachineContext: false);

            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;

            foreach (var certificate in _extraCertificates)
            {
                chain.ChainPolicy.CustomTrustStore.Add(certificate);
                chain.ChainPolicy.ExtraStore.Add(certificate);
            }

            if (!chain.Build(leaf))
            {
                _logger.LogWarning(
                    "The chain for leaf certificate {Fingerprint} could not be build. No intermediate or root certificates in this certificate's chain will be checked.",
                    leaf.Thumbprint);
                return;
            }

            var root = chain.ChainElements.Last().Certificate;

            foreach (var element in chain.ChainElements)
            {
                if (element.Certificate.Equals(leaf))
                {
                    continue;
                }

                if (element.Certificate.Equals(root))
                {
                    if (Root)
                    {
                        SaveCertificate(type + " root", element.Certificate);
                        continue;
                    }
                }

                if (Intermediate)
                {
                    SaveCertificate(type + " intermediate", element.Certificate);
                    continue;
                }
            }
        }

        private void SaveCertificate(string type, X509Certificate2 certificate)
        {
            _logger.LogInformation("Saving {Type} certificate with fingerprint {Fingerprint}.", type, certificate.Thumbprint);

            byte[] bytes;
            string extension;
            switch (Format)
            {
                case CertificateFormat.CER:
                    bytes = certificate.Export(X509ContentType.Cert);
                    extension = "cer";
                    break;
                case CertificateFormat.PEM:
                    bytes = Encoding.UTF8.GetBytes(certificate.ExportCertificatePem());
                    extension = "pem";
                    break;
                default:
                    return;
            }

            var path = Path.Combine(Output!.FullName, $"{certificate.Thumbprint}.{extension}");
            if (_writtenPaths.Contains(path))
            {
                return;
            }

            if (!Output!.Exists)
            {
                Output.Create();
            }

            _writtenPaths.Add(path);

            if (!IOFile.Exists(path))
            {
                IOFile.WriteAllBytes(path, bytes);
            }
            else
            {
                _logger.LogDebug("Skipping the write to {Path} because the file exists.", path);
            }
        }
    }
}
