// Copyright © 2025-2026 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using A2v10.Infrastructure;


namespace A2v10.Metadata;

internal partial class ScriptBuilder
{
    internal Task<String> CreatePrintTemplate()
    {
        var form = PrintRequest.FormOf(Endpoint, _descr.PlatformUrl);
        var title = String.IsNullOrEmpty(form.Header)
            ? null
            : PrintTitle.Parse(form.Header, Table);

        IEnumerable<String> functions()
        {
            yield return $$"""
            function reportUrl() {
                return `/report/show/${this.Id}?base={{Endpoint.Path}}&rep={{form.Name}}`;
            }
            """;
            /* The tab caption, evaluated here rather than on the server: the values live in the
             * model the page already holds, and the paths were checked when that model was built.
             */
            if (title != null)
                yield return $$"""
                function pageTitle() {
                    return `{{title.Js}}`;
                }
                """;
        }

        IEnumerable<String> properties()
        {
            yield return $"'{Table.TypeName}.$ReportUrl': reportUrl";
            if (title != null)
                yield return $"'{Table.TypeName}.$Title': pageTitle";
        }

        const String jsDivider = ",\n\t\t";

        /* Emitted only when a title actually formats a date - a require nobody uses is noise in a
         * generated file, and this one is the only helper the page needs.
         */
        var requires = title is { UsesDate: true }
            ? "const du = require('std:utils').date;" + Environment.NewLine + Environment.NewLine
            : String.Empty;

        var templ = $$"""
        {{requires}}{{TemplateDecl}} {
            properties: {
                {{String.Join(jsDivider, properties())}}
            }
        };

        {{TemplateExport}}

        {{String.Join("\n", functions())}}
        """;
        return Task.FromResult<String>(templ);
    }
}
