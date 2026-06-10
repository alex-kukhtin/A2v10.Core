// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;

using A2v10.Data.Interfaces;
using A2v10.Infrastructure;

namespace A2v10.Cli;

internal class ResolveEndpointCommand(IServiceProvider services)
{
    private readonly IDataService _dataService = services.GetRequiredService<IDataService>();

    internal IList<Command> Register(Command root)
    {
        root.Subcommands.Add(BuildAction());
        root.Subcommands.Add(BuildDialog());
        root.Subcommands.Add(BuildPopup());
        return root.Subcommands;
    }
    internal Command BuildAction()
    {
        var cmd = new Command("resolve-action", 
            "Resolve an action (page) route to its runtime contract: bound procedures, view/template, and model type tree.");
        var routeArg = new Argument<String>("route")
        {
            Description = "Endpoint route (with id)",
        };
        cmd.Arguments.Add(routeArg);

        cmd.SetAction(r => JsonResult.Try(() => ResolveAction("_page", r.GetValue(routeArg)!)));
        return cmd;
    }
    internal Command BuildPopup()
    {
        var cmd = new Command("resolve-popup",
            "Resolve an action (popup) route to its runtime contract: bound procedures, view/template, and model type tree.");
        var routeArg = new Argument<String>("route")
        {
            Description = "Endpoint route (with id)",
        };
        cmd.Arguments.Add(routeArg);

        cmd.SetAction(r => JsonResult.Try(() => ResolveAction("_popup", r.GetValue(routeArg)!)));
        return cmd;
    }

    internal Command BuildDialog()
    {
        var cmd = new Command("resolve-dialog",
            "Resolve an dialog (model) route to its runtime contract: bound procedures, view/template, and model type tree.");
        var routeArg = new Argument<String>("route")
        {
            Description = "Endpoint route (with id)",
        };
        cmd.Arguments.Add(routeArg);

        cmd.SetAction(r => JsonResult.Try(() => ResolveAction("_dialog", r.GetValue(routeArg)!)));
        return cmd;
    }

    private async Task<Object> ResolveAction(String prefix, String route)
    {
        var path = Path.Combine(prefix, route, "0").Replace('\\', '/');
        var dr = await _dataService.LoadAsync(path, p =>
        {
            p.Add("UserId", 99);
        });
        if (dr.View == null)
            throw new InvalidOperationException("Can't resolve action or dialog");    

        //var view = dr.View.GetRawView(false);
        return new ExpandoObject()
        {
            { "route", route },
            { "model", dr.View.CurrentModel },
            { "view", BuildResolvedPath(route, dr.View?.GetRawView(false), "xaml") },
            { "template", BuildResolvedPath(route, dr.View?.Template, "ts") },
            { "sqlProcedures", BuildSqlProcedures(dr.View!) },
            { "dataModel", BuildDataModelMeta(dr.Model) }
        };
    }

    private static ExpandoObject BuildResolvedPath(String route, String? file, String ext)
    {
        return new ExpandoObject()
        {
            { "dir", route.NormalizePath() },
            { "file",  $"{file}.{ext}" }
        };

    }

    private static ExpandoObject BuildDataModelMeta(IDataModel? model)
    {
        if (model == null)
            return [];

        ExpandoObject buildProps(IDataMetadata metadata)
        {
            var props = new ExpandoObject();
            foreach (var p in metadata.Fields)
            {
                props.TryAdd(p.Key, new ExpandoObject() {
                    { "type", p.Value.TypeScriptName },
                    { "len", p.Value.Length == 0 ? null : p.Value.Length }
                });
            }
            return props;
        }

        ExpandoObject buildTypes()
        {
            var types = new ExpandoObject();
            foreach (var t in model.Metadata)
            {
                types.Add(t.Key, new ExpandoObject()
                {
                    {"props", buildProps(t.Value)  },
                    {"id", t.Value.Id },
                    {"name", t.Value.Name },
                });
            }
            return types;
        }

        return new ExpandoObject()
        {
            {"types", buildTypes() }
        };
    }
    private static ExpandoObject BuildSqlProcedures(IModelView view)
    {
        static String? canonical(String? name)
        {
            if (name == null)
                return null;
            var ix = name.IndexOf("].[");
            if (ix == -1)
                return name;
            return $"{name[1..ix]}{name[(ix + 1)..]}";
        }

        var res = new ExpandoObject() { 
            {"load",  canonical(view.LoadProcedure()) },
            {"update", view.IsIndex ? null : canonical(view.UpdateProcedure()) }
        };
        return res;
    }
}
