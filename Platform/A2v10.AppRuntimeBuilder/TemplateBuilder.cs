﻿// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A2v10.AppRuntimeBuilder;

internal static class TemplateBuilder
{
	public static String CreateIndexTemplate(EndpointDescriptor endpoint)
	{
		var table = endpoint.BaseTable;

		var template = $$"""

			const template = {
				options:{
					noDirty: true,
					persistSelect: ['{{endpoint.BaseTable.Name}}']
				},
				events: {
					'g.{{table.ItemName().ToLowerInvariant()}}.saved': elemSaved					
				}
			};

			module.exports = template;            

			function elemSaved(r) {
				console.dir(r);
			}
			""";
		return template;
	}

	public static String CreateEditTemplate(EndpointDescriptor endpoint)
	{
		var ui = endpoint.GetEditUI();
		var table = endpoint.BaseTable;

		var rq = ui.Fields.Where(f => f.Required);
		var validators = String.Join(",\n\t\t", rq.Select(f => $"'{table.ItemName()}.{f.Name}': `@[Error.Required]`"));

		String ComputedText(String typeName, UiField f) =>
			$$"""'{{typeName}}.{{f.Name}}'() { return {{f.Computed}}; }""";

		List<String> commands = [];
		StringBuilder functions = new StringBuilder();	
		if (endpoint.EndpointType() == TableType.Document)
		{
			commands.Add("apply");
			commands.Add("unapply");
			functions.Append($$"""
			async function apply() {
				let ctrl = this.$ctrl;
				await ctrl.$invoke('apply', {Id: this.{{table.ItemName()}}.Id}, '{{endpoint.Name}}');
				ctrl.$requery();
			}
			function unapply() {
				alert('unapply');
			}
			""");
		}

		IEnumerable<String> GetComputedFields()
		{
			foreach (var f in ui.Fields.Where(f => !String.IsNullOrEmpty(f.Computed)))
				yield return ComputedText(table.TypeName(), f);
			if (ui.Details != null)
				foreach (var detailsTable in ui.Details)
				{
					var typeName = detailsTable.BaseTable?.TypeName()
						?? throw new InvalidOperationException("BaseTable for Details is null");
					foreach (var f in detailsTable.Fields.Where(f => !String.IsNullOrEmpty(f.Computed)))
						yield return ComputedText(typeName, f);
					foreach (var f in detailsTable.Fields.Where(f => f.Total))
						yield return $$"""'{{typeName}}Array.{{f.Name}}'() {return this.$sum(r => r.{{f.Name}});}""";
				}
		}

		var templateProps = String.Join(",\n\t\t", GetComputedFields());

		var template = $$"""
			const template = {
				options: {
					globalSaveEvent: 'g.{{table.ItemName().ToLowerInvariant()}}.saved'
				},
				properties:{
					{{templateProps}}
				},
				validators: {
					{{validators}}
				},
				commands: {
					{{String.Join(",\n\t\t", commands)}}
				}
			};

			{{functions}}

			module.exports = template;            
			""";
		return template;
	}

}
