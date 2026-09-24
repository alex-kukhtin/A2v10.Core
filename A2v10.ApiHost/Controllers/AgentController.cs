using System.Dynamic;
using Microsoft.AspNetCore.Mvc;

using A2v10.Services.Api;

namespace A2v10.ApiHost.Controllers;

[ApiController]
[Route("agent/[action]/{id}")]
public class AgentController(EndpointDataService _dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] IndexQuery query, 
            [ModelBinder(typeof(FilterBagBinder))] ExpandoObject filters)
    {
        var dm = await _dataService.At("catalog/agent").IndexAsync(query, filters);
        return Ok(dm);
    }

    [HttpGet]
    public async Task<IActionResult> Load([FromRoute] String id)
    {
        var dr = await _dataService.At("catalog/agent").LoadAsync(id);
        return Ok(dr.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var dr = await _dataService.At("catalog/agent").CreateAsync();
        return Ok(dr.Data);
    }


    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ExpandoObject data)
    {
        var dr = await _dataService.At("catalog/agent").SaveAsync(data);
        return Ok(dr);
    }
}
