// Copyright © 2015-2023 Alex Kukhtin. All rights reserved.

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

public class BlobUpdateIdToken
{
    public Object? Id { get; set; }
    public String? Mime { get; set; }
    public String? Name { get; set; }
    public String? Token { get; set; }
}

[ExecutingFilter]
[Authorize]
[ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Client)]
public class ImageController : BaseController
{
	private readonly IDataService _dataService;
	private readonly ITokenProvider _tokenProvider;
	private readonly IAppCodeProvider _appCodeProvider;

	public ImageController(IApplicationHost host,
		ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler, 
		IDataService dataService, ITokenProvider tokenProvider, IAppCodeProvider appCodeProvider)
		: base(host, localizer, currentUser, profiler)
	{
		_dataService = dataService;
		_tokenProvider = tokenProvider;
		_appCodeProvider = appCodeProvider;
	}

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
			var retList = new List<BlobUpdateIdToken>();
			foreach (var f in files)
			{
				Stream stream = f.OpenReadStream();
				var name = Path.GetFileName(f.FileName);
                var saved = await _dataService.SaveBlobAsync(UrlKind.Image, pathInfo, bi =>
				{
					bi.UserId = this.UserId;
                    if (_host.IsMultiTenant)
                        bi.TenantId = this.TenantId;
                    if (_host.IsMultiCompany)
                        bi.CompanyId = this.CompanyId;
                    bi.Name = name;
                    bi.Mime = f.ContentType;
                    bi.Stream = stream;
                });
				stream.Close();
				var res = new BlobUpdateIdToken()
				{
					Id = saved.Id,
					Name = name,
					Mime= f.ContentType,
					Token = saved.Token.HasValue ? _tokenProvider.GenerateToken(saved.Token.Value) : null
				};
				retList.Add(res);
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
