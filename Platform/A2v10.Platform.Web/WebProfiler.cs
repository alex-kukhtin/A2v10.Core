// Copyright © 2020-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.Concurrent;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

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

public sealed class ProfileItem(String msg) : ProfileTimer(), IDisposable
{
    [JsonProperty("text")]
    public String Text { get; set; } = msg;

    public void Dispose()
	{
		Stop();
	}
}

internal class ProfileItems : List<ProfileItem>
{
}

internal class ProfileRequest(String address) : ProfileTimer(), IProfileRequest, IDisposable
{
    public void Dispose()
	{
		Stop();
	}

    [JsonProperty("url")]
    public String Url { get; set; } = address;

    [JsonProperty("items")]
    public IDictionary<ProfileAction, ProfileItems> Items { get; set; } = new Dictionary<ProfileAction, ProfileItems>();

    public IDisposable Start(ProfileAction kind, String description)
	{
		var itm = new ProfileItem(description);
		if (!Items.TryGetValue(kind, out ProfileItems? elems))
		{
			elems = [];
			Items.Add(kind, elems);
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

public sealed class WebProfilerStorage
{
	private readonly ConcurrentDictionary<String, LinkedList<ProfileRequest>> _map = new();

	internal LinkedList<ProfileRequest> Get(String key)
	{
		return _map.GetOrAdd(key, (key) => new LinkedList<ProfileRequest>());
	}
}

public sealed class WebProfiler(IHttpContextAccessor httpContext, WebProfilerStorage storage) : IProfiler, IDataProfiler, IDisposable
{
	const Int32 REQUEST_COUNT = 15;

	private ProfileRequest? _request;
	public Boolean Enabled { get; set; }

	private readonly IHttpContextAccessor _httpContext = httpContext;
	private readonly WebProfilerStorage _storage = storage;

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
		var requestList = _storage.Get(SessionId());
		_request = new ProfileRequest(address);
		lock (requestList)
		{
			requestList.AddFirst(_request);
			while (requestList.Count > REQUEST_COUNT)
				requestList.RemoveLast();
		}
		return _request;
	}

	private String SessionId()
	{
        var sessionKey = _httpContext.HttpContext?.Request.Cookies[CookieNames.Application.Profile];
		if (sessionKey != null)	
			return sessionKey;
		sessionKey = Guid.NewGuid().ToString();
        _httpContext.HttpContext?.Response.Cookies.Append(
            CookieNames.Application.Profile,
            sessionKey,
            new CookieOptions()
            {
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
            }
        );
		return sessionKey;
    }

    public void EndRequest(IProfileRequest? request)
	{
		if (request != _request)
			return;
		_request?.Stop();
	}


	public String? GetJson()
	{
		var requestList = _storage.Get(SessionId());
		if (requestList == null || requestList.Count == 0) 
			return "[]";
        return JsonConvert.SerializeObject(requestList);
	}

	#region IDataProfiler
	IDisposable? IDataProfiler.Start(String command)
	{
		return CurrentRequest.Start(ProfileAction.Sql, command);
	}
	#endregion
}

