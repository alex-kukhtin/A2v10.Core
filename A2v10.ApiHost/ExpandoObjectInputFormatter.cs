using System.Dynamic;
using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;

namespace A2v10.ApiHost;

internal sealed class ExpandoObjectInputFormatter : TextInputFormatter
{
    public ExpandoObjectInputFormatter()
    {
        SupportedMediaTypes.Add("application/json");
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    protected override Boolean CanReadType(Type type) => type == typeof(ExpandoObject);

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context, Encoding encoding)
    {
        using var reader = context.ReaderFactory(context.HttpContext.Request.Body, encoding);
        var json = await reader.ReadToEndAsync();
        try
        {
            var model = JsonConvert.DeserializeObject<ExpandoObject>(json);
            return model is null
                ? await InputFormatterResult.NoValueAsync()
                : await InputFormatterResult.SuccessAsync(model);
        }
        catch (JsonException ex)
        {
            context.ModelState.AddModelError(context.ModelName, ex.Message);
            return await InputFormatterResult.FailureAsync();
        }
    }
}
