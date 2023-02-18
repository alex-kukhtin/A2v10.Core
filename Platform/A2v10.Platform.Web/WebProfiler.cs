// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace A2v10.Platform.Web;

public class ProfileTimer
{
    private readonly Stopwatch _timer;

    [JsonProperty("elapsed")]
    public Int64 Elapsed { get; set; }

    protected ProfileTimer()
    {
        _timer = new Stopwatch();
        _timer.Start();
    }
    public void Stop()
    {
        if (_timer.IsRunning)
        {
            _timer.Stop();
            Elapsed = _timer.ElapsedMilliseconds;
        }
    }
}

public sealed class ProfileItem : ProfileTimer, IDisposable
{
    [JsonProperty("text")]
    public String Text { get; set; }

    public ProfileItem(String msg)
        : base()
    {
        Text = msg;
    }

    public void Dispose()
    {
        Stop();
    }
}

internal class ProfileItems : List<ProfileItem>
{
}

internal class ProfileRequest : ProfileTimer, IProfileRequest, IDisposable
{
    private readonly ILogger<ProfileRequest> _logger;

    public ProfileRequest(String address, ILogger<ProfileRequest> logger)
        : base()
    {
        Url = address;
        _logger = logger;

        Items = new Dictionary<ProfileAction, ProfileItems>();
    }

    public void Dispose()
    {
        Stop();
    }

    [JsonProperty("url")]
    public String Url { get; set; }

    [JsonProperty("items")]
    public IDictionary<ProfileAction, ProfileItems> Items { get; set; }

    public IDisposable Start(ProfileAction kind, String description)
    {
        var itm = new ProfileItem(description);
        if (!Items.TryGetValue(kind, out ProfileItems? elems))
        {
            elems = new ProfileItems();
            Items.Add(kind, elems);
        }

        if (kind == ProfileAction.Exception)
        {
            _logger.LogError(description);
        }

        elems.Add(itm);
        return itm;
    }
}

internal class DummyRequest : IProfileRequest
{
    public IDisposable? Start(ProfileAction kind, String description)
    {
        return null;
    }

    public void Stop()
    {
    }
}

public sealed class WebProfiler : IProfiler, IDataProfiler, IDisposable
{
    // TODO: ???? COOCKIE SIZE!!!!
    const Int32 REQUEST_COUNT = 10;

    private LinkedList<ProfileRequest> _requestList = new();
    private ProfileRequest? _request;

    public Boolean Enabled { get; set; }

    private readonly IHttpContextAccessor _httpContext;
    private readonly IDataProtector _protector;

    public WebProfiler(IHttpContextAccessor httpContext, IDataProtectionProvider protectionProvider)
    {
        _httpContext = httpContext;
        _protector = protectionProvider.CreateProtector("Session");
    }

    public void Dispose()
    {
        var rq = _request;
        if (rq != null)
        {
            _request = null;
            rq.Dispose();
        }
    }

    public IProfileRequest CurrentRequest => _request ?? new DummyRequest() as IProfileRequest;

    public IProfileRequest? BeginRequest(String address, String? session)
    {
        if (!Enabled)
            return null;
        if (address.ToLowerInvariant().EndsWith("/_shell/trace"))
            return null;
        LoadSession();

        var logger = ServiceLocator.Current.GetService<ILogger<ProfileRequest>>();
        _request = new ProfileRequest(address, logger);

        _requestList.AddFirst(_request);
        while (_requestList.Count > REQUEST_COUNT)
            _requestList.RemoveLast();
        return _request;
    }

    public void EndRequest(IProfileRequest? request)
    {
        if (request != _request)
            return;
        _request?.Stop();
        SaveSession();
    }

    void LoadSession()
    {
        var protectedData = _httpContext.HttpContext?.Request.Cookies[CookieNames.Application.Profile];
        if (!String.IsNullOrEmpty(protectedData))
            _requestList = JsonConvert.DeserializeObject<LinkedList<ProfileRequest>>(_protector.Unprotect(protectedData))
                ?? throw new InvalidProgramException("Invalid RequestList");
    }

    void SaveSession()
    {
        String json = JsonConvert.SerializeObject(_requestList);
        _httpContext.HttpContext?.Response.Cookies.Append(
            CookieNames.Application.Profile,
            _protector.Protect(json),
            new CookieOptions()
            {
                SameSite = SameSiteMode.Strict,
                Secure = true,
                HttpOnly = true,
            }
        );
    }

    public String? GetJson()
    {
        var protectedData = _httpContext.HttpContext?.Request.Cookies[CookieNames.Application.Profile];
        if (protectedData == null)
            return null;
        return _protector.Unprotect(protectedData);
    }

    #region IDataProfiler
    IDisposable? IDataProfiler.Start(String command)
    {
        return CurrentRequest.Start(ProfileAction.Sql, command);
    }
    #endregion
}

