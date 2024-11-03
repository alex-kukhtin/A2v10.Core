// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using CommandLine;

namespace LicenseBuilder;

internal enum Command
{
    genkeys,
    sign,
    verify
}

internal class CommandLineOptions
{
    [Value(index: 0, HelpText = "Command")]
    public Command Command { get; set; }

    [Option(shortName: 'o', longName: "output", HelpText = "Output directory")]
    public String? Output { get; set; }

    [Option(shortName: 's', longName: "source", HelpText = "Source file name")]
    public String? SourceFile { get; set; }

    [Option(shortName: 't', longName: "target", HelpText = "Target file name")]
    public String? TargetFile { get; set; }

    [Option(longName: "public", HelpText = "Public key file name")]
    public String? PublicKey { get; set; }

    [Option(longName: "private", HelpText = "Private key file name")]
    public String? PrivateKey { get; set; }
}
