// Copyright © 2020-2021 Alex Kukhtin. All rights reserved.

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Newtonsoft.Json;

namespace A2v10.Services.Javascript;
public static class FetchCommand
{
	static void SetHeaders(HttpRequestMessage wr, ExpandoObject? headers)
	{
		if (headers == null)
			return;
		var d = headers as IDictionary<String, Object>;
		foreach (var hp in d)
			wr.Headers.Add(hp.Key, hp.Value.ToString());
	}

	static void AddAuthorization(HttpRequestMessage wr, ExpandoObject? auth)
	{
		if (auth == null)
			return;
		var type = auth.Get<String>("type");
		switch (type)
		{
			case "apiKey":
				var apiKey = auth.Get<String>("apiKey");
				wr.Headers.Add("Authorization", $"ApiKey {apiKey}");
				break;
			case "basic":
				var name = auth.Get<String>("name");
				var password = auth.Get<String>("password");
				var encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("UTF-8").GetBytes(name + ":" + password));
				wr.Headers.Add("Authorization", $"Basic {encoded}");
				break;
			case "bearer":
				var token = auth.Get<String>("token");
				wr.Headers.Add("Authorization", $"Bearer {token}");
				break;
			default:
				throw new InvalidOperationException($"Invalid Authorization type ({type})");
		}
	}

	static String CreateQueryString(ExpandoObject? query)
	{
		if (query == null || query.IsEmpty())
			return String.Empty;
		var elems = (query as IDictionary<String, Object>)
			.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value.ToString()!)}");
		var ts = String.Join("&", elems);
		if (String.IsNullOrEmpty(ts))
			return String.Empty;
		return "?" + ts;
	}

	static ExpandoObject? GetResponseHeaders(HttpResponseHeaders? headers)
	{
		if (headers == null)
			return null;
		var eo = new ExpandoObject();
		foreach (var (key, value) in headers)
			eo.Set(key, value);
		return eo;
	}

	public static FetchResponse Execute(IHttpClientFactory factory, String url, ExpandoObject? prms)
	{
		using var client = factory.CreateClient();

		String mtdString = prms?.Get<String>("method")?.ToUpperInvariant() ?? "get";

		HttpMethod mtd = new(mtdString);
		String requestUrl = url + CreateQueryString(prms?.Get<ExpandoObject>("query"));
		var requestMessage = new HttpRequestMessage(mtd, requestUrl);

		AddAuthorization(requestMessage, prms?.Get<ExpandoObject>("authorization"));
		SetHeaders(requestMessage, prms?.Get<ExpandoObject>("headers"));

		if (mtd == HttpMethod.Post)
		{
			requestMessage.Method = mtd;
			var bodyObj = prms?.Get<Object>("body");

			switch (bodyObj)
			{
				case String strObj:
					requestMessage.Content = new StringContent(strObj);
					break;
				case ExpandoObject eoObj:
					var bodyStr = JsonConvert.SerializeObject(eoObj, new JsonDoubleConverter());
					requestMessage.Content = JsonContent.Create(bodyStr);
					break;
			}
		}

		using HttpResponseMessage resp = client.Send(requestMessage);
		var contentType = resp.Content.Headers.ContentType?.ToString();
		using var rs = resp.Content.ReadAsStream();
		using var ms = new StreamReader(rs);
		return new FetchResponse
		(
			resp.StatusCode,
			contentType,
			ms.ReadToEnd(),
			GetResponseHeaders(resp.Headers)
		);
	}
}

