using A2v10.Web.Identity;
using A2v10.Web.Identity.ApiKey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace A2v10.ApiHost.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.Scheme)]
[Route("[controller]/[action]")]
public class ApiController : ControllerBase
{
	[HttpGet]
	[ActionName("getforecast")]
	public IEnumerable<WeatherForecast> Get()
	{
		var userId = User.Identity.GetUserId<Int64>();
		var tenantId = User.Identity.GetUserTenantId();	
		
		return Enumerable.Range(1, 5).Select(index => new WeatherForecast
		{
			Date = DateTime.Now.AddDays(index),
			TemperatureC = Random.Shared.Next(-20, 55)
		})
		.ToArray();
	}
}
