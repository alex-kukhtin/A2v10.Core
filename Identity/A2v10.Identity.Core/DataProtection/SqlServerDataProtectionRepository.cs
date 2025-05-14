// Copyright © 2025 Oleksandr Kukhtin. All rights reserved.

using System.Xml.Linq;
using System.Collections.Generic;
using System.Globalization;

using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

using A2v10.Data.Interfaces;
using A2v10.Web.Identity;

namespace A2v10.Identity.Core;

public sealed class SqlServerDataProtectionRepository<T>(IStaticDbContext _dbContext, IOptions<AppUserStoreOptions<T>> _options)
    : IXmlRepository
    where T : struct
{
    private readonly String? _dataSource = _options.Value?.DataSource;
    private readonly String _dbSchema = _options.Value?.Schema ?? "a2security";

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var elements = new List<XElement>();
        _dbContext.ExecuteReader(_dataSource, $"{_dbSchema}.[KeyVault.Load]", prms => { }, 
            (rdrNo, rdr) =>
            {
                var str = rdr.GetString(0);
                var xelem = XElement.Parse(str);
                elements.Add(xelem);
            }
        );
        return elements.AsReadOnly();
    }

    public void StoreElement(XElement element, String friendlyName)
    {
        DateTime? dtValue = null;
        
        var expirationDate = element.Element("expirationDate")?.Value;
        if (expirationDate != null)
            dtValue = DateTime.Parse(expirationDate, null, DateTimeStyles.RoundtripKind);

        _dbContext.ExecuteNonQuery(_dataSource, $"{_dbSchema}.[KeyVault.Update]", prms =>
        {
            var prmsBuilder = _dbContext.ParameterBuilder(prms);
            prmsBuilder.AddString("@Key", friendlyName)
                .AddString("@Value", element.ToString(), 65535)
                .AddDateTime("@Expired", dtValue);
        });
    }
}
