// Copyright © 2024 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using Newtonsoft.Json;

using A2v10.Services;
using A2v10.Infrastructure;

namespace LicenseBuilder;

internal class CommandProcessor
{
    private readonly String _dir;
    private readonly CommandLineOptions _options;
    public CommandProcessor(String currentDirectory, CommandLineOptions options)
    {
        _dir = currentDirectory;
        _options = options;
    }


    private String PrivateKeyFilePath()
    {
        var privateFileName = _options.PrivateKey ?? "private.key";
        return Path.GetFullPath(Path.Combine(_dir, privateFileName));
    }

    private String PublicKeyFilePath()
    {
        var publicKeyFileName = _options.PublicKey ?? "public.key";
        return Path.GetFullPath(Path.Combine(_dir, publicKeyFileName));
    }

    private String SourceFilePath()
    {
        var sourceFileName = _options.SourceFile ?? "license.json";
        return Path.GetFullPath(Path.Combine(_dir, sourceFileName));
    }

    private String TargetFilePath()
    {
        var targetFileName = _options.TargetFile ?? "license.signed.json";
        return Path.GetFullPath(Path.Combine(_dir, targetFileName));
    }

    public void Process()
    {
        try
        {
            DoProcess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    private void DoProcess()
    {
        switch (_options.Command)
        {
            case Command.genkeys:
                GenerateKeys();
                break;
            case Command.sign:
                SignFile();
                break;
            case Command.verify:
                VerifyFile();
                break;
        }
    }

    void GenerateKeys()
    {
        Console.WriteLine("Generate keys...");

        var keys = SignatureProvider.CreateKeys();
        var privateFilePath = PrivateKeyFilePath();
        var publicFilePath = PublicKeyFilePath();

        CreateDirectoryIfNeeded(privateFilePath);
        CreateDirectoryIfNeeded(publicFilePath);

        File.WriteAllText(privateFilePath, keys.Private);
        Console.WriteLine($"The private key was written to {privateFilePath}");

        File.WriteAllText(publicFilePath, keys.Public);
        Console.WriteLine($"The public key was written to {publicFilePath}");
    }

    void CreateDirectoryIfNeeded(String fullPath)
    {
        var dir = Path.GetDirectoryName(fullPath) ??
            throw new InvalidOperationException("Directory is null");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    String ReadPrivateKey()
    {
        return File.ReadAllText(PrivateKeyFilePath());
    }

    String ReadPublicKey()
    {
        return File.ReadAllText(PublicKeyFilePath());
    }

    void SignFile()
    {
        var sourceFile = SourceFilePath();
        Console.WriteLine($"Signing file {sourceFile}");

        var lic = File.ReadAllText(sourceFile);
        
        var data = JsonConvert.DeserializeObject<ExpandoObject>(lic)
            ?? throw new InvalidOperationException("License is null");

        var privateKey = ReadPrivateKey();

        var target = SignatureProvider.SignData(data, privateKey);

        var targetFileName = TargetFilePath();
        CreateDirectoryIfNeeded(targetFileName);
        File.WriteAllText(targetFileName, target);
        Console.WriteLine($"The signed was written to {targetFileName}");
    }

    private static JsonSerializerSettings NoFormatSettings = new JsonSerializerSettings()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.None
    };

    void VerifyFile()
    {
        var targetFileName = TargetFilePath();
        Console.WriteLine($"Verifind file {targetFileName}");
        var license = File.ReadAllText(targetFileName);
        var publicKey = ReadPublicKey();

        var data = JsonConvert.DeserializeObject<ExpandoObject>(license)
            ?? throw new InvalidOperationException("License is null");

        var verified = SignatureProvider.VerifyData(data, publicKey);

        if (verified)
            Console.WriteLine("Verified: OK");
        else
            Console.WriteLine("Verified: Invalid");
    }
}
