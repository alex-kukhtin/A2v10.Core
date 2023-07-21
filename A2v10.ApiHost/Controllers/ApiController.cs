using A2v10.Web.Identity;
using A2v10.Web.Identity.ApiKey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Dynamic;

namespace A2v10.ApiHost.Controllers;

public class ResponseFail
{
	public Boolean status;
}

public class ResponseSuccess
{
#pragma warning disable IDE1006 // Naming Styles
    public Boolean success { get; set; }
    public ExpandoObject? data { get; set; }
#pragma warning restore IDE1006 // Naming Styles
}

[ApiController]
//[Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.Scheme)]
[Authorize]
[Route("api/[action]")]
[Produces("application/json")]
[ProducesResponseType(typeof(ResponseSuccess), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
public class ApiController : ControllerBase
{
	/// <summary>
	/// Повертає список варіантів
	/// </summary>
	/// <param name="id">Id або UID параметру</param>
	/// <returns></returns>
	/// <remarks>
	/// Sample request:
	///
	///	POST /Todo
	///		{
	///			"id": 1,
	///			"name": "Item #1",
	///			"isComplete": true
	///     }
	///     
	/// </remarks>
	/// <response code="200">Returns the newly created item</response>
	/// <response code="401">If the item is null</response>	
	//[HttpGet("{id:guid}")]
	[HttpGet("{id}")]
	[ActionName("getforecast")]
	public IEnumerable<WeatherForecast> Get([FromRoute] String id)
	{
		// constraints: Microsoft.AspNetCore.Routing.RouteOptions
		var userId = User.Identity.GetUserId<Int64>();
		var tenantId = User.Identity.GetUserTenant<Int32>();	
		
		return Enumerable.Range(1, 5).Select(index => new WeatherForecast
		{
			Date = DateTime.Now.AddDays(index),
			TemperatureC = Random.Shared.Next(-20, 55)
		})
		.ToArray();
	}
}
