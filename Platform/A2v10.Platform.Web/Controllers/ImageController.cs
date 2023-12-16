// Copyright © 2015-2023 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Dynamic;
using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Platform.Web.Controllers;

[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
public class ImageController(IApplicationHost host,
    ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler,
    IDataService dataService, ITokenProvider tokenProvider, IAppCodeProvider appCodeProvider) : BaseController(host, localizer, currentUser, profiler)
{
	private readonly IDataService _dataService = dataService;
	private readonly ITokenProvider _tokenProvider = tokenProvider;
	private readonly IAppCodeProvider _appCodeProvider = appCodeProvider;

    [Route("_image/{*pathInfo}")]
	[HttpGet]
	public async Task<IActionResult> Image(String pathInfo)
	{
		try
		{
			var token = Request.Query["token"];

			if (token.Count == 0)
				throw new InvalidReqestExecption("Invalid image token");
			var strToken = token[0] ??
				throw new InvalidReqestExecption("Invalid image token");

			var blob = await _dataService.LoadBlobAsync(UrlKind.Image, pathInfo, SetSqlQueryParams);
			if (blob == null || blob.Stream == null)
				throw new InvalidReqestExecption($"Image not found. ({pathInfo})");
			if (blob.Mime is null)
				throw new InvalidReqestExecption($"Invalid mime type for image. ({pathInfo})");
			if (!IsTokenValid(blob.Token, strToken))
				throw new InvalidReqestExecption("Invalid image token");
			return new WebBinaryActionResult(blob.Stream, blob.Mime);
		}
		catch (Exception ex)
		{
			return WriteImageException(ex);
		}
	}


	[Route("_image/{*pathInfo}")]
	[HttpPost]
    public async Task<IActionResult> ImagePost(String pathInfo)
	{
		try
		{
			var files = Request.Form.Files;
			var retList = new List<ExpandoObject>();
			foreach (var f in files)
			{
				Stream stream = f.OpenReadStream();
				var name = Path.GetFileName(f.FileName);
				var saved = await _dataService.SaveBlobAsync(pathInfo, blob =>
				{
					blob.Name = name;
					blob.Mime = f.ContentType;
					blob.Stream = stream;
					blob.TenantId = _host.IsMultiTenant ? TenantId : null;
				}, 
				SetSqlQueryParams, UrlKind.Image);

				stream.Close();
				saved.ReplaceValue("Token", (v) =>
				{
					if (v is Guid guid)
						return _tokenProvider.GenerateToken(guid);
					return v;
				});

				/*
				var res = new BlobUpdateIdToken()
				{
					Id = saved.Id,
					Name = name,
					Mime= f.ContentType,
					Token = saved.Token.HasValue ? _tokenProvider.GenerateToken(saved.Token.Value) : null
				};
				*/
				retList.Add(saved);
            }
            var rval = new ExpandoObject();
            rval.Set("status", "OK");
            rval.Set("elems", retList);
            String result = JsonConvert.SerializeObject(rval, JsonHelpers.StandardSerializerSettings);
			return Content(result, MimeTypes.Application.Json);
        }
        catch (Exception ex)
		{
            return WriteExceptionStatus(ex);
        }
    }

    [Route("_static_image/{*pathInfo}")]
	[HttpGet]
	public IActionResult StaticImage(String pathInfo)
	{
		try
		{
			if (String.IsNullOrEmpty(pathInfo))
				throw new ArgumentOutOfRangeException(nameof(pathInfo), nameof(StaticImage));
			pathInfo = pathInfo.Replace('-', '.');
			// without using! The FileStreamResult will close stream
			var stream = _appCodeProvider.FileStreamRO(pathInfo)
                ?? throw new FileNotFoundException($"File not found '{pathInfo}'");
            var ext = PathHelpers.GetExtension(pathInfo);
			return new FileStreamResult(stream, MimeTypes.GetMimeMapping(ext));
		} 
		catch (Exception ex)
		{
			return WriteImageException(ex);
		}
	}

	Boolean IsTokenValid(Guid dbToken, String token)
	{
		var generated = _tokenProvider.GenerateToken(dbToken);
		if (generated == token)
			return true;
		Response.ContentType = MimeTypes.Text.Plain;
		Response.StatusCode = 403;
		return false;
	}

}
