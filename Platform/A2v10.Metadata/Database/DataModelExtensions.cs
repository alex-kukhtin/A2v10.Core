using System.Text;

using Newtonsoft.Json;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;
using A2v10.Services;

namespace A2v10.Metadata;

internal static class DataModelExtensions
{
    internal static IInvokeResult ToInvokeResult(this IDataModel? model)
    {
        var strResult = model != null && model.Root != null ?
            JsonConvert.SerializeObject(model.Root, JsonHelpers.DataSerializerSettings) : "{}";

        return new InvokeResult(
            body: strResult != null ? Encoding.UTF8.GetBytes(strResult) : [],
            contentType: MimeTypes.Application.Json
        );
    }
}
