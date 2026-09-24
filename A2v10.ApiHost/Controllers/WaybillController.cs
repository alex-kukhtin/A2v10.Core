using System.Dynamic;
using Microsoft.AspNetCore.Mvc;

using A2v10.Services.Api;

namespace A2v10.ApiHost.Controllers;

[ApiController]
[Route("waybill/[action]/{id}")]
public class WaybillController(EndpointDataService _dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] IndexQuery query, 
            [ModelBinder(typeof(FilterBagBinder))] ExpandoObject filters)
    {
        var dm = await _dataService.At("document/waybillin").IndexAsync(query, filters);
        return Ok(dm);
    }

    [HttpGet]
    public async Task<IActionResult> Load([FromRoute] String id)
    {
        var dr = await _dataService.At("document/waybillin").LoadAsync(id);
        return Ok(dr.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var dr = await _dataService.At("document/waybillin").CreateAsync();
        return Ok(dr.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ExpandoObject data)
    {
        var dr = await _dataService.At("document/waybillin").SaveAsync(data);
        return Ok(dr);
    }
}
