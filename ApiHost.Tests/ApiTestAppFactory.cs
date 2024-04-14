using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using A2v10.ApiHost;

namespace ApiHost.Tests;

public class ApiTestAppFactory : WebApplicationFactory<Startup>
{
	protected override IHostBuilder? CreateHostBuilder()
	{
		return base.CreateHostBuilder()!
			.ConfigureAppConfiguration(builder =>
		{
			builder.AddUserSecrets<ApiTestAppFactory>();
		});
	}


	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureServices(services =>
		{
			// replace services
			/*
			var dbContextDescriptor = services.SingleOrDefault(
				d => d.ServiceType == typeof(IDbContext)
			);

			services.Remove(dbContextDescriptor!);

			services.AddSingleton<IDbContext, MockDbContext>();
			*/
		});
		builder.UseEnvironment("Development");
	}
	protected override void ConfigureClient(HttpClient client)
	{
		var config = Services.GetRequiredService<IConfiguration>();
		var apiKey = config.GetValue<String>("TestRun:ApiKey");
		client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
	}
}
