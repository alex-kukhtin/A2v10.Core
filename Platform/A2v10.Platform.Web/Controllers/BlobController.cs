// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

using Newtonsoft.Json;

using A2v10.Infrastructure;

namespace A2v10.Platform.Web.Controllers;

[ExecutingFilter]
[Authorize]
public class BlobController(IApplicationHost host,
	ILocalizer localizer, ICurrentUser currentUser, IProfiler profiler,
	IBlobService _blobService) : BaseController(host, localizer, currentUser, profiler)
{
	private static readonly FileExtensionContentTypeProvider _contentTypes = new();

	[Route("_blob/{*pathInfo}")]
	[HttpPost]
	public async Task<IActionResult> Upload(String pathInfo)
	{
		try
		{
			var files = Request.Form.Files;
			if (files.Count == 0)
				throw new InvalidRequestException("No files");
			var file = files[0];
			var name = Path.GetFileName(file.FileName);
			// by the name, as a browser does, but by one table for every client
			if (!_contentTypes.TryGetContentType(name, out var mime))
				mime = MimeTypes.Application.OctetStream;
			using var stream = file.OpenReadStream();
			// ?base= means an attachment: linked to ?owner= (in the query - the path is always endpoint/Key)
			var @base = Request.Query["base"].ToString();
			Object result = String.IsNullOrEmpty(@base)
				? await _blobService.UploadAsync(pathInfo, name, mime, stream)
				: await _blobService.UploadLinkAsync(pathInfo, @base, Request.Query["owner"].ToString(), name, mime, stream);
			var json = JsonConvert.SerializeObject(result, JsonHelpers.StandardSerializerSettings);
			return Content(json, MimeTypes.Application.Json);
		}
		catch (Exception ex)
		{
			return WriteExceptionStatus(ex);
		}
	}

	[Route("_blob/{id:long}")]
	[HttpGet]
	public async Task<IActionResult> Load(Int64 id)
	{
		try
		{
			var token = Request.Query["token"].ToString();
			var blob = await _blobService.LoadAsync(id, token, Request.Query.ContainsKey("thumb"));
			// a blob is immutable and the token lives one session: a long cache is safe — on success only,
			// an error is another result and stays uncached ([ResponseCache] would cache it too)
			var ar = new WebBinaryActionResult(blob.Data.ToArray(), blob.Mime)
				.EnableCache()
				.AddHeader("X-Content-Type-Options", "nosniff");
			if (!IsInlineMime(blob.Mime))
				ar.AddHeader("Content-Disposition", new ContentDispositionHeaderValue("attachment") { FileNameStar = blob.Name }.ToString());
			return ar;
		}
		catch (Exception ex)
		{
			if (Request.Headers.Accept.ToString().Trim().StartsWith("image", StringComparison.OrdinalIgnoreCase))
				return WriteImageException(ex);
			return WriteExceptionStatus(ex);
		}
	}
}
