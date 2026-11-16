// Copyright © 2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace A2v10.Metadata;

/* The exact search of an 'equal' channel (TableMetadata.Lookup), for both edit templates: what is
 * typed into '<Ref>.<channel>' is looked up at the target's address - found, the element is that
 * record; empty or not found, the element is emptied, and 'required' catches it as any empty
 * reference. Driven from the FORM: a channel the form does not show is typed into by nobody.
 */
internal partial class ScriptBuilder
{
    private sealed record EqualChannel(String Path, MemberDescriptor Member, Dictionary<String, InheritDescriptor[]> Inherits);

    private IEnumerable<EqualChannel> EqualChannels()
    {
        IEnumerable<EqualChannel> walk(FormElement el, RowSetDeclaration? rows)
        {
            if (el.RowSet != null)
                rows = RowSets.First(rs => rs.Collection == el.RowSet);
            foreach (var m in el.Members.Where(m => m.Channel?.Match == LookupMatch.Equal))
                yield return rows == null
                    ? new($"{Table.Model}.{m.ModelName}", m, Endpoint.Declaration.Inherits)
                    : new($"{Table.Model}.{rows.Collection}[].{m.ModelName}", m, rows.Inherits);
            foreach (var x in el.Elements.SelectMany(e => walk(e, rows)))
                yield return x;
        }
        var form = Endpoint.Form(Constants.FormNames.Edit);
        return form.Body.Append(form.Taskpad)
            .SelectMany(el => walk(el, null))
            .DistinctBy(x => x.Path);
    }

    // the element types the handlers are annotated with
    private IEnumerable<String> LookupTypes() =>
        EqualChannels().Select(x => x.Member.ColumnCheck.RefTableCheck.Storage.RefTypeName).Distinct();

    /* The answer is the fetch's element (SqlBuilder.ChoiceSqlText), with the columns the picking
     * row inherits, named to the target as the selector names them (XamlBuilder.SelectorUrl) - so
     * the element merged here is the one the selector would have handed over.
     */
    private IEnumerable<String> LookupEvents()
    {
        foreach (var (path, member, inherits) in EqualChannels())
        {
            var reference = member.ColumnCheck;
            var target = reference.RefTableCheck;
            var inherit = inherits.TryGetValue(reference.Name, out var inh) && inh.Length > 0
                ? $", inherit: '{String.Join(',', inh.Select(x => x.Source))}'"
                : String.Empty;
            yield return $$"""
                async '{{path}}.change'({{(IsTs ? "this: TRoot, " : "")}}item{{Ann(target.Storage.RefTypeName)}}, val{{Ann("string")}}) {
                			if (!val) { item.$empty(); return; }
                			const r = await this.$ctrl.$invoke('lookup', { Channel: '{{member.ChannelCheck.Column.Name}}', Text: val{{inherit}} }, '{{target.Path}}');
                			const found = r?.{{target.Storage.CollectionName}};
                			found?.length ? item.$merge(found[0]) : item.$empty();
                		}
                """;
        }
    }
}
