# Knapcode.CertificateExtractor (nuget-cert-extractor)

A tool to extract certificate files from NuGet packages.

## Install

```console
dotnet tool install Knapcode.CertificateExtractor --global
```

This will install the `nuget-cert-extractor` command into your PATH.

### Example

Extract the code signing leaf certificate from a .nupkg.

```console
nuget-cert-extractor --file "MyPackage.0.1.0.nupkg" --output . --code-signing --author --leaf
```

If the package has an author signature, a single .cer file will be written to the `--output` directory with the file name `{SHA1 fingerprint}.crt`.

Extract all certificates in PEM format.

```console
nuget-cert-extractor --file "MyPackage.0.1.0.nupkg" --output . --all --format PEM
```

### Help text

```plaintext
Description:
  A tool to extract certificate files from NuGet packages.

  The values that can be specified by the --format option are:
  CER: This is a binary reprenstation of the ASN.1 using DER encoding. The file
  extension will be ".cer".
  PEM: This is a PEM encoding, which is essentially base64 representation of the DER
  encoded ASN.1. The file extension will be ".pem".

Usage:
  nuget-cert-extractor [options]

Options:
  --log-level <level>           The minimum log level to display. Possible values:
                                Verbose, Debug, Information, Warning, Error, Fatal
                                [default: Information]
  --file <file> (REQUIRED)      A file path for an input .nupkg.
  --output <output> (REQUIRED)  A destination directory for writing extracted
                                certificates to.
  --format <CER|PEM>            The format to use for writing certificate files.
                                [default: CER]
  --all                         Extract all certificates.
  --author                      Extract certificates used in the author signature.
  --repository                  Extract certificates used in the repository signature.
  --leaf                        Extract leaf certificates.
  --intermediate                Extract intermediate certificates.
  --root                        Extract root certificates.
  --code-signing                Extract certificates used in the repository signature.
  --timestamping                Extract certificates used in the repository signature.
  -?, -h, --help                Show help and usage information
  --version                     Show version information
```
