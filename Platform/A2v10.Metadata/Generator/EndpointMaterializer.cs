// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using A2v10.Infrastructure;

namespace A2v10.Metadata;

public enum MaterializeWhat
{
    View,
    Template
}

/* One file the platform would have generated, as text. 'Regenerated' says who owns it afterwards:
 * false is a file a human edits from now on and is written once; true is a compile target of the
 * shape (the .d.ts map) and is rewritten on every call - the caller must not refuse over it.
 */
public sealed record MaterializedFile(String Path, String Text, Boolean Regenerated);

/* The files, and what model.json has to say for them to be picked up. The fragment is returned and
 * never written: model.json is the author's file, and merging a key into it would reformat what
 * they wrote. See CLAUDE.md, "Two files in one folder".
 */
public sealed record Materialization(IReadOnlyList<MaterializedFile> Files, Object ModelJson);

/* The default screen of an endpoint, obtained as text - the eject the "whole or nothing" rule owes
 * (ISSUES 3.6). What is written is what the runtime renders: the same XamlBuilder and the same
 * ScriptBuilder, the latter with its types printed, so the first diff after an edit reads.
 *
 * View and template are taken one by one, never as a pair: a hand-written view loses only the
 * load-time check, a hand-written template loses the RULES compiled into it, and one call that
 * hands out both would offer the dear one at the price of the cheap one.
 */
public sealed class EndpointMaterializer(DatabaseMetadataProvider _metadataProvider)
{
    private static readonly String[] _actions =
        [Constants.FormNames.Index, Constants.FormNames.Browse, Constants.FormNames.Edit];

    public async Task<Materialization> MaterializeAsync(String endpointPath, String action, MaterializeWhat what)
    {
        action = action.ToLowerInvariant();
        if (Array.IndexOf(_actions, action) < 0)
            throw new InvalidOperationException(
                $"'{action}': the forms an endpoint has are {String.Join(", ", _actions)}. The transactions dialog and the print page are derived, not forms.");

        var (schema, table) = DatabaseMetadataProvider.ParsePath(endpointPath);
        var endpoint = await _metadataProvider.GetNormalEndpointAsync(null, schema, table);

        var platformUrl = endpoint.PlatformUrl(action);
        var descriptor = new BuilderDescriptor()
        {
            Endpoint = endpoint,
            PlatformUrl = platformUrl,
            // the base the database rests on: an Id is typed by it in the .d.ts
            PlatformId = await _metadataProvider.GetPlatformIdAsync(null)
        };
        // the folder as the code provider names it - a '$module/' prefix included, a slash not
        var folder = endpointPath.NormalizeSlash().Trim('/');
        var isDialog = platformUrl.Kind == UrlKind.Dialog;

        return what switch
        {
            MaterializeWhat.View => View(descriptor, folder, action, isDialog),
            MaterializeWhat.Template => Template(descriptor, folder, action, isDialog),
            _ => throw new InvalidOperationException($"Unknown '{what}'")
        };
    }

    private static Materialization View(BuilderDescriptor descriptor, String folder, String action, Boolean isDialog)
    {
        // 'edit.view' on a page, 'edit.dialog' in a dialog: the name model.json refers to
        var name = $"{action}.{(isDialog ? "dialog" : "view")}";
        var xaml = XamlTextBulder.GetXaml(new XamlBuilder(descriptor).CreateXamlContainer(action));
        return new Materialization(
            [new MaterializedFile($"{folder}/{name}.vxaml", xaml, Regenerated: false)],
            Fragment(action, isDialog, "view", name));
    }

    private static Materialization Template(BuilderDescriptor descriptor, String folder, String action, Boolean isDialog)
    {
        /* The browse dialog runs the index template and its map imports './index': a
         * 'browse.template.ts' would be a second copy of the index one, importing types it does
         * not have beside it. model.json can point at the index one by name.
         */
        if (action == Constants.FormNames.Browse)
            throw new InvalidOperationException(
                $"'{action}' shares the template of '{Constants.FormNames.Index}': materialize that one and write \"template\": \"{Constants.FormNames.Index}.template\" under '{action}'.");

        var ts = new ScriptBuilder(descriptor, isTs: true);
        var (template, map) = action == Constants.FormNames.Index
            ? (ts.CreateIndexTemplate(), ts.CreateIndexMapTS())
            : (ts.CreateEditTemplate(), ts.CreateEditMapTS());

        var name = $"{action}.template";
        return new Materialization(
            [
                new MaterializedFile($"{folder}/{name}.ts", template.Result, Regenerated: false),
                // the types of the model, and the template imports them from './<action>'
                new MaterializedFile($"{folder}/{action}.d.ts", map.Result, Regenerated: true)
            ],
            Fragment(action, isDialog, "template", name));
    }

    // what to put into model.json, as an object: the caller prints it, the author merges it
    private static Dictionary<String, Object> Fragment(String action, Boolean isDialog, String key, String value) =>
        new()
        {
            ["model"] = IModelBase.MetaModel,
            [isDialog ? "dialogs" : "actions"] = new Dictionary<String, Object>()
            {
                [action] = new Dictionary<String, Object>() { [key] = value }
            }
        };
}
