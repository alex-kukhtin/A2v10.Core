
// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace A2v10.Cli;

internal record JsonError
{
    public String Message { get; init; } = default!;
    public Int32? LineNo { get; init; }
}
internal record JsonResult
{
    public Boolean Success { get; init; } = true;
    public Object? Data { get; init; }
    public JsonError? Error { get; init; }   

    public static void Ok(Object? data)
    {
        Write(new JsonResult()
        {
            Success = true,
            Data = data
        });
    }

    public static void Fail(Exception ex)
    {
        var ex2 = ex.InnerException ?? ex;
        Write(new JsonResult()
        {
            Success = false,
            Error = new JsonError()
            {
                Message = ex2.Message
            }
        });
    }

    private static void Write(JsonResult res)
    {
        var json = JsonConvert.SerializeObject(res, JsonSettings.CamelCaseSerializerSettingsFormat);
        Console.WriteLine(json);
    }

    public static async Task Try(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }
}
