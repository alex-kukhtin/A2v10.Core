
// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

using Newtonsoft.Json;

using A2v10.Metadata;

namespace A2v10.Cli;

internal record JsonError
{
    public String Message { get; init; } = default!;
    public Int32? LineNo { get; init; }
    // a deploy script: the batch that failed, as a range of lines in the file it was executed from
    public String? File { get; init; }
    public Int32? LineFrom { get; init; }
    public Int32? LineTo { get; init; }
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
        // the coordinates are taken before the unwrapping below: the inner exception has none
        var script = ex as DeployScriptException;
        // message and line come from the same exception - taken from different ones they would lie
        var ex2 = ex.InnerException ?? ex;
        Environment.ExitCode = 1;
        Write(new JsonResult()
        {
            Success = false,
            Error = new JsonError()
            {
                Message = ex2.Message,
                LineNo = LineNo(ex2),
                // relative to the current directory, as every path the CLI reports
                File = script == null ? null : Path.GetRelativePath(Directory.GetCurrentDirectory(), script.File).Replace('\\', '/'),
                LineFrom = script?.LineFrom,
                LineTo = script?.LineTo
            }
        });
    }

    // only malformed markup knows its position: XamlException does not carry one yet
    private static Int32? LineNo(Exception ex) =>
        ex is XmlException xmlEx && xmlEx.LineNumber > 0 ? xmlEx.LineNumber : null;

    private static void Write(JsonResult res)
    {
        var json = JsonConvert.SerializeObject(res, JsonSettings.CamelCaseSerializerSettingsFormat);
        Console.WriteLine(json);
    }

    public static async Task Try(Func<Task<Object>> action)
    {
        try
        {
            JsonResult.Ok(await action());
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }
}
