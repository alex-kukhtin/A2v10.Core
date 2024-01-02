using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Platform.Web;
using A2v10.Services.Javascript;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TestServices;

[TestClass]
[TestCategory("Javascript")]
public class TestJsStaticFunctions
{
	[TestMethod]
	public void Static()
	{
		/*
		var sc = new ServiceCollection();
		var cb = new ConfigurationBuilder();
		cb.AddJsonFile("appsettings.json");
		var config  = cb.Build();

		var sp = sc.BuildServiceProvider();
		*/
	}
}