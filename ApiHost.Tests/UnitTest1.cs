

namespace ApiHost.Tests;

public class UnitTest1(ApiTestAppFactory factory) : IClassFixture<ApiTestAppFactory>
{
	private readonly ApiTestAppFactory _factory = factory;

    [Fact]
	[Trait("Api", "Simple")]
	public async Task Test1()
	{
		var client = _factory.CreateClient();

		//var resp = 
		await client.GetAsync("/api/getforecast/2CDFFA2F-5A04-4C5A-A676-0A1B1434F39D", CancellationToken.None);

        // Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        /*
		Assert.True(resp.IsSuccessStatusCode);
		var body = await resp.Content.ReadAsStringAsync();
		var list = JsonConvert.DeserializeObject<List<ExpandoObject>>(body);
		*/
    }
}