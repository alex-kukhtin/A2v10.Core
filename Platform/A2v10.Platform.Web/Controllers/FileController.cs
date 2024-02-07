// Copyright © 2015-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;
using System.Dynamic;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

using Newtonsoft.Json;

using A2v10.Infrastructure;
using A2v10.Data.Interfaces;

namespace A2v10.Platform.Web.Controllers;

[ExecutingFilter]
[Authorize]
public class FileController(IApplicationHost host,
    ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler,
    IDataService _dataService, ITokenProvider _tokenProvider, IAppCodeProvider _appCodeProvider) : BaseController(host, localizer, currentUser, profiler)
{
	// always Cached!
	[ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
	[Route("_file/{*pathInfo}")]
	[HttpGet]
	public async Task<IActionResult> DefaultGet(String pathInfo)
	{
		try
		{
			var blob = await _dataService.LoadBlobAsync(UrlKind.File, pathInfo, (prms) =>
			{
				SetSqlQueryParams(prms);
				SetRequestQueryParams(prms);
			}) ?? throw new InvalidOperationException("Blob not found");

            if (blob.CheckToken)
                ValidateToken(blob.Token);

            if (blob.Stream == null)
				throw new InvalidReqestExecption($"Blob not found. ({pathInfo})");

			var ar = new WebBinaryActionResult(blob.Stream, blob.Mime ?? MimeTypes.Text.Plain);
			if (Request.Query["export"].Count > 0)
			{
				var cdh = new ContentDispositionHeaderValue("attachment")
				{
					FileNameStar = _localizer.Localize(null, blob.Name)
				};
				ar.AddHeader("Content-Disposition", cdh.ToString());
			}
			return ar;
		}
		catch (Exception ex)
		{
			var accept = Request.Headers.Accept.ToString();
			if (accept != null && accept.Trim().StartsWith("image", StringComparison.OrdinalIgnoreCase))
				return WriteImageException(ex);
			else if (Request.Query["export"].Count > 0)
			{
				var bytes = Encoding.UTF8.GetBytes(ex.ToString());
				var ar = new WebBinaryActionResult(bytes, MimeTypes.Text.Plain);
				var cdh = new ContentDispositionHeaderValue("attachment")
				{
					FileNameStar = "error.txt"
				};
				ar.AddHeader("Content-Disposition", cdh.ToString());
				return ar;
			}
			else
				return WriteExceptionStatus(ex);
		}
	}

	[Route("_file/{*pathInfo}")]
	[HttpPost]
	public async Task<IActionResult> DefaultPost(String pathInfo)
	{
		try
		{
			var files = Request.Form.Files;
			if (files.Count == 0)
				throw new InvalidOperationException("No files");
			var file = files[0];

			using var fileStream = file.OpenReadStream();
            var name = Path.GetFileName(file.FileName);

            var result = await _dataService.SaveBlobAsync(pathInfo, (blob) =>
				{
					blob.Stream = fileStream;
					blob.Name = name;
					blob.Mime = file.ContentType;
					blob.TenantId = _host.IsMultiTenant ? TenantId : null;
				}, 
				(prms) => {
					SetSqlQueryParams(prms);
					SetRequestQueryParams(prms);
				}
			);
			result.ReplaceValue("Token", (v) =>
			{
				if (v is Guid guid)
					return _tokenProvider.GenerateToken(guid);
				return v;
			});
            String json = JsonConvert.SerializeObject(result, JsonHelpers.StandardSerializerSettings);
            return Content(json, MimeTypes.Application.Json);
        }
        catch (Exception ex)
		{
			return WriteExceptionStatus(ex);
		}
	}

	[Route("_file/_delete/{*pathInfo}")]
	[HttpGet]
	public async Task<IActionResult> DeleteFile(String pathInfo)
	{
		try
		{
			var result = await _dataService.DeleteBlobAsync(pathInfo, SetSqlQueryParams);
			String json = JsonConvert.SerializeObject(result, JsonHelpers.StandardSerializerSettings);
			return Content(json, MimeTypes.Application.Json);
		}
		catch (Exception ex)
		{
			return WriteExceptionStatus(ex);
		}
	}

	void SetRequestQueryParams(ExpandoObject prms)
	{
		foreach (var qkey in Request.Query.Keys)
		{
			if (qkey == "export" || qkey == "token")
				continue;
			var val = Request.Query[qkey].ToString();
			if (!String.IsNullOrEmpty(val))
				prms.Set(qkey, val);
		}
	}

	void ValidateToken(Guid dbToken)
	{
		var token = Request.Query["token"];
		if (token.Count == 0)
			throw new InvalidReqestExecption("Invalid token");
		var strToken = token[0] ??
			throw new InvalidReqestExecption("Invalid token");
		ValidateToken(dbToken, strToken);
	}

    void ValidateToken(Guid dbToken, String token)
	{
		var generated = _tokenProvider.GenerateToken(dbToken);
		if (generated == token)
			return;
		throw new InvalidReqestExecption("Invalid image token");
	}

    [ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
    [Route("file/{*pathInfo}")]
	[HttpGet]
	public IActionResult LoadFile(String pathInfo)
	{
		try
		{
			Int32 ix = pathInfo.LastIndexOf('-');
			if (ix != -1)
				pathInfo = pathInfo[..ix] + "." + pathInfo[(ix + 1)..];
			if (!new FileExtensionContentTypeProvider().TryGetContentType(pathInfo, out String? contentType))
				contentType = MimeTypes.Application.OctetStream;
            // without using! The FileStreamResult will close stream
            var stream = _appCodeProvider.FileStreamResource(_appCodeProvider.MakePath("_files/", pathInfo))
                ?? throw new FileNotFoundException($"File not found '{pathInfo}'");
			return new FileStreamResult(stream, contentType)
			{
				FileDownloadName = Path.GetFileName(pathInfo)
			};
		}
		catch (Exception ex)
		{
			return WriteHtmlException(ex);
		}
	}
}

