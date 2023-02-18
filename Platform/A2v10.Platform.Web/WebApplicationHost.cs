﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Dynamic;
using System.Text;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using A2v10.Infrastructure;
using Microsoft.Extensions.Options;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace A2v10.Platform.Web;

public class WebApplicationHost : IApplicationHost
{
    private readonly IConfiguration _appSettings;
    private readonly AppOptions _appOptions;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _hostEnvironment;
    private IApplicationReader _reader;

    public WebApplicationHost(IConfiguration config, IOptions<AppOptions> appOptions, ICurrentUser currentUser, IWebHostEnvironment hostEnvironment)
    {
        _appOptions = appOptions.Value;

        _appSettings = config.GetSection(key: "application");

        _currentUser = currentUser;
        _hostEnvironment = hostEnvironment;

        StartApplication(isAdmin: false);
    }

    public Boolean IsProductionEnvironment
    {
        get
        {
            return _hostEnvironment.IsProduction();
        }
    }

    public Boolean IsMultiTenant => _appOptions.MultiTenant;
    public Boolean IsMultiCompany => _appOptions.MultiCompany;

    public Boolean IsDebugConfiguration => _appOptions.Environment.IsDebug;

    public Boolean IsUsePeriodAndCompanies => _appSettings.GetValue<Boolean>("custom");
    public Boolean IsRegistrationEnabled => _appSettings.GetValue<Boolean>("registration");
    public Boolean IsDTCEnabled => _appSettings.GetValue<Boolean>("enableDTC");

    public Boolean Mobile { get; private set; }

    public Boolean IsAdminMode { get; set; } = false;

    //public String AppDescription => throw new NotImplementedException();

    //public String AppHost => throw new NotImplementedException();
    //public String UserAppHost => throw new NotImplementedException();

    //public String SupportEmail => throw new NotImplementedException();

    //public String HelpUrl => throw new NotImplementedException();

    //public String HostingPath => throw new NotImplementedException();
    //public String SmtpConfig => throw new NotImplementedException();

    //public String ScriptEngine => throw new NotImplementedException();

    public Boolean IsAdminAppPresent => true /*TODO:*/;

    public String? CatalogDataSource => IsMultiTenant ? "Catalog" : null;
    public String? TenantDataSource => String.IsNullOrEmpty(_currentUser.Identity.Segment) ? null : _currentUser.Identity.Segment;    

    public IApplicationReader ApplicationReader
    {
        get
        {
            return _reader == null ? 
                throw new InvalidProgramException(message: "ApplicationReader is not configured") : 
                _reader;
        }
    }

    public String AppPath
    {
        get
        {
            String relativePathToAppDir = _appSettings.GetValue<String>(key: "path")!;
            String fullAppPath = Path.Combine(_hostEnvironment.ContentRootPath, relativePathToAppDir);

            return fullAppPath;
        }
    }

    public String? ZipApplicationFile
    {
        get
        {
            var path = Path.Combine(AppPath, AppKey ?? String.Empty);
            path = Path.ChangeExtension(path, extension: ".app");
            if (File.Exists(path))
            {
                return path;
            }
            return null;
        }
    }

    public String AppKey => _appSettings.GetValue<String>(key: "appName") ?? String.Empty;

    /*
	public string MakeRelativePath(string path, string fileName)
	{
		throw new NotImplementedException();
	}
	*/

    public void CheckIsMobile(string host)
    {
        throw new NotImplementedException();
    }

    public String? GetAppSettings(String? source)
    {
        if (source == null)
        {
            return null;
        }

        if (!source.Contains("@{AppSettings.", StringComparison.InvariantCulture))
        {
            return source;
        }

        Int32 xpos = 0;
        var sb = new StringBuilder();
        do
        {
            Int32 start = source.IndexOf("@{AppSettings.", xpos);
            if (start == -1)
            {
                break;
            }

            Int32 end = source.IndexOf("}", start + 14);
            if (end == -1)
            {
                break;
            }

            var key = source.Substring(start + 14, end - start - 14);
            var value = _appSettings.GetValue<String>(key) ?? String.Empty;
            sb.Append(source[xpos..start]);
            sb.Append(value);
            xpos = end + 1;
        } while (true);
        sb.Append(source[xpos..]);
        return sb.ToString();
    }

    public ExpandoObject GetEnvironmentObject(String key)
    {
        var val = _appSettings.GetValue<String>(key);
        if (val != null)
        {
            return JsonConvert.DeserializeObject<ExpandoObject>(val, new ExpandoObjectConverter())
                ?? new ExpandoObject();
        }

        IConfigurationSection valObj = _appSettings.GetSection(key);
        if (valObj != null)
        {
            ExpandoObject eo = new();
            foreach (IConfigurationSection v in valObj.GetChildren())
            {
                eo.Add(v.Key, v.Value);
            }

            return eo;
        }
        throw new InvalidOperationException($"Configuration parameter 'appSettings/{key}' not defined");
    }

    public void StartApplication(Boolean isAdmin)
    {
        if (AppKey.StartsWith(value: "clr-type:"))
        {
            _reader = new ClrApplicationReader(AppPath, AppKey);
        }
        else
        {
            String key = isAdmin ? "admin" : AppKey;
            var file = ZipApplicationFile;
            if (file != null)
            {
                _reader = new ZipApplicationReader(AppPath, key);
            }
            else if (AppPath.StartsWith(value: "db:"))
            {
                throw new NotImplementedException(message: "DbApplicationReader");
            }
            else
            {
                _reader = new FileApplicationReader(AppPath, key)
                {
                    EmulateBox = _appSettings.GetValue<Boolean>(key: "emulateBox")
                };
            }
        }
    }
}

