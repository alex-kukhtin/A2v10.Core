
using A2v10.ApiHost;
using A2v10.Data.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine.ClientProtocol;
using Newtonsoft.Json;
using System.Dynamic;
using System.Net;
using System.Net.Http.Headers;

namespace ApiHost.Tests;

public class UnitTest1 : IClassFixture<ApiTestAppFactory>
{
	private readonly ApiTestAppFactory _factory;

	public UnitTest1(ApiTestAppFactory factory)
	{
		_factory = factory;
		var dbContext = _factory.Services.GetService<IDbContext>();
		int z = 44;
	}

	[Fact]
	[Trait("Api", "Simple")]
	public async Task Test1()
	{
		var client = _factory.CreateClient();

		var resp = await client.GetAsync("/api/getforecast/2CDFFA2F-5A04-4C5A-A676-0A1B1434F39D");
		Assert.True(resp.IsSuccessStatusCode);
		Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
		var body = await resp.Content.ReadAsStringAsync();
		var list = JsonConvert.DeserializeObject<List<ExpandoObject>>(body);
		int z = 55;
	}
}