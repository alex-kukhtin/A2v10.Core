// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

public class LicenseInfo : ILicenseInfo
{
    public LicenseState LicenseState { get; set; } = LicenseState.InvalidFile;
    public String ApplicationName => "A2v10.Platform";
    public String Name { get; set; } = String.Empty;
    public DateTime IssuedOn => Data.Get<DateTime>("IssuedOn");
    public DateTime ExpiresOn => Data.Get<DateTime>("ExpiresOn");
    public String? Message => GetLicenseMessage();
    public String? Title => GetLicenseTitle();
    public ExpandoObject Data { get; set; } = [];

    private String? GetLicenseTitle()
    {
        return LicenseState switch
        {
            LicenseState.Ok => null,
            LicenseState.NotFound => LICENSE_NOT_FOUND_TITLE,
            _ => throw new InvalidOperationException($"Unsupported License State: {LicenseState}")
        };
    }

    private String? GetLicenseMessage()
    {
        return LicenseState switch
        {
            LicenseState.Ok => null,
            LicenseState.NotFound => LICENSE_NOT_FOUND_MSG,
            _ => throw new InvalidOperationException($"Unsupported License State: {LicenseState}")
        };
    }

    private const String LICENSE_NOT_FOUND_TITLE = "Ліцензію не знайдено";
    private const String LICENSE_NOT_FOUND_MSG = """
        <p>Не знайдено файл ліцензії (license.json). 
        <br>
        Зверніться до вашого постачальника.
        </p>
        """;
}

public class LicenseManager(ILogger<LicenseManager> _logger) : ILicenseManager
{
    public async Task<LicenseState> VerifyLicensesAsync(String? dataSource, Int32? tenantId, IEnumerable<Guid> modules)
    {
        try
        {
            var licInfo = await LoadLicenseAsync();
            return licInfo.LicenseState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return LicenseState.InvalidFile;
        }
    }

    public async Task<ILicenseInfo> GetLicenseInfoAsync(String? dataSource, Int32? tenantId)
    {
        try
        {
            return await LoadLicenseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return new LicenseInfo()
            {
                LicenseState = LicenseState.InvalidFile,
            };
        }
    }

    private async Task<ILicenseInfo> LoadLicenseAsync()
    {
        var ass = Assembly.GetExecutingAssembly();
        var assemblyName = ass.Location;
        var path = Path.GetDirectoryName(assemblyName)!;
        var licFileName = Path.Combine(path, "license.json");
        var licenseInfo = new LicenseInfo();

        if (!File.Exists(licFileName))
        {
            licenseInfo.LicenseState = LicenseState.NotFound;
            return licenseInfo;
        }
        var text = await File.ReadAllTextAsync(licFileName);
        var licData = JsonConvert.DeserializeObject<ExpandoObject>(text)
            ?? throw new InvalidOperationException("Invalid Json");

        licenseInfo.Data = licData.Get<ExpandoObject>("Data")
            ?? throw new InvalidOperationException("Data Is null");

        var keyVersion = Convert.ToInt32(licData.Get<Object>("KeyVersion"));
        if (keyVersion == 0)
            throw new InvalidOperationException("Key version is null");


        String publicKey = FindKey(keyVersion)
            ?? throw new InvalidOperationException("Public key is null");

        if (!SignatureProvider.VerifyData(licData, publicKey))
            return licenseInfo;

        // License file is OK.

        if (licenseInfo.ExpiresOn >= DateTime.Now)
        {
            // expired
            // якщо дата поточної версії меньша за ExpiresOn, повертаємо OK
            var issueDate = ass.GetCustomAttribute<IssueDateAttribute>()!;
            if (licenseInfo.ExpiresOn < issueDate.IssueDate)
                licenseInfo.LicenseState = LicenseState.Ok;
            licenseInfo.LicenseState = LicenseState.Expired;
        }

        licenseInfo.LicenseState = LicenseState.Ok;
        return licenseInfo;
    }

    String? FindKey(Int32 keyVersion)
    {
        if (_publicKeys.TryGetValue(keyVersion, out var result))
            return result;
        return null;

    }

    private static readonly IReadOnlyDictionary<Int32, String> _publicKeys = new Dictionary<Int32, String>()
    {
        [1] = "MIIBCgKCAQEA3iYUlXQ3NnoFE5aK8prwj3x7hy0tDClCF3UeB+LKRL9uJ1dI5MPpIpH3PLfy3hQTvKoyCfmvIW1ceHEsO4xUy0cQ5/TQtX+dPgoJieV01vbLlsmXzkFe9UFszmNr5OK7m0kHDa0H5TxEd7fIFqQz52hNBFpUO/OJNLoZqynPuf+7Bq+zx6ecaAVNKvJwp/qooSOGmTLykh3YX3x7yZ+13U/SrF9V5R5/b1mwjPOu+YmxxllxTW2Qzt6g2gB1EHYLAxzCwcqCsBu7pBsXicd8/LadJXNP86jvTgki5NzEpPvqu34B4a4/XuMKAWrp4fc2CONRHbhTv3PFX6SKWqXcKQIDAQAB"
    };
}
