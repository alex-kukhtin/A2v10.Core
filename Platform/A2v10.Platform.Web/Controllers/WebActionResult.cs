// Copyright © 2015-2022 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;

public class WebActionResult : IActionResult
{
	private readonly List<String> _data = new();
	private readonly Dictionary<String, String> _headers = new();
	private readonly String _contentType;

	public WebActionResult(String data, String contentType = MimeTypes.Application.Json)
	{
		if (data != null)
			_data.Add(data);
		_contentType = contentType;
	}

	public void AddHeader(String name, String value)
	{
		_headers.Add(name, value);
	}

	public async Task ExecuteResultAsync(ActionContext context)
	{
		var resp = context.HttpContext.Response;
		resp.ContentType = _contentType;

		foreach (var (k, v) in _headers)
		{
			resp.Headers.Remove(k);
			resp.Headers.Add(k, v);
		}

		for (int i=0; i<_data.Count; i++)
			await resp.WriteAsync(_data[i], Encoding.UTF8);
	}
}

public class WebBinaryActionResult : IActionResult
{
	private readonly List<Byte[]> _data = new();
	private readonly Dictionary<String, String> _headers = new();
	private readonly String _contentType;
	
	Boolean _cache;

	public WebBinaryActionResult(Byte[] data, String contentType = MimeTypes.Application.Json)
	{
		if (data != null)
			_data.Add(data);
		_contentType = contentType;
	}

	public WebBinaryActionResult EnableCache(Boolean bEnable = true)
	{
		_cache = bEnable;
		return this;
	}

	public WebBinaryActionResult AddHeader(String name, String value)
	{
		_headers.Add(name, value);
		return this;
	}

	public async Task ExecuteResultAsync(ActionContext context)
	{
		var resp = context.HttpContext.Response;
		resp.ContentType = _contentType;
		foreach (var (k, v) in _headers)
		{
			resp.Headers.Remove(k);
			resp.Headers.Add(k, v);
		}

		if (_cache)
		{
			resp.GetTypedHeaders().CacheControl =
			new CacheControlHeaderValue()
			{
				Private = true,
				MaxAge = TimeSpan.FromDays(30)
			};
		}

		for (int i = 0; i < _data.Count; i++)
			await resp.BodyWriter.WriteAsync(_data[i]);
	}
}

public class WebExceptionResult : IActionResult
{
	private readonly Int32 _errorCode;
	private readonly String _message;

	public WebExceptionResult(Int32 errorCode, String? message)
	{
		_errorCode = errorCode;
		_message = message ?? String.Empty;
	}

	public Task ExecuteResultAsync(ActionContext context)
	{
		var resp = context.HttpContext.Response;
		resp.ContentType = MimeTypes.Text.Plain;
		resp.StatusCode = _errorCode;
		return resp.WriteAsync(_message, Encoding.UTF8);
	}
}
