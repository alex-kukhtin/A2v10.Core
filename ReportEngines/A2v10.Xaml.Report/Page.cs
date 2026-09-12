// Copyright © 2022-2024 Oleksandr Kukhtin. All rights reserved.

using System;
using System.Text.Json.Serialization;
using A2v10.System.Xaml;

namespace A2v10.Xaml.Report;

[ContentProperty("Columns")]
public class Page : XamlElement
{
	public String? Title { get; set; }
	public String? Code { get; init; }

	// Объявление выборки для слоя метаданных, а не разметка: рендерер сюда не смотрит. Живёт
	// на странице, а не на книге, потому что книга бланком не бывает — им бывает Spreadsheet,
	// и он Page. [JsonIgnore] обязателен, а не для красоты: в JSON-бланке "Model" — объект
	// верхнего уровня, иначе System.Text.Json положит его в String и уронит каждую печатную форму
	[JsonIgnore]
	public String? Model { get; init; }

	[JsonIgnore]
	public ColumnCollection Columns { get; init; } = [];
	[JsonIgnore]
	public Column? Header { get; init; }
	[JsonIgnore]
	public Column? Footer { get; set; }

	public String? FontFamily { get; set; }
	public PageOrientation Orientation { get; set; }

	// Имя файла, либо байты через привязку. Геометрия — поворот, прозрачность, размер —
	// живёт внутри SVG: у контейнера QuestPDF нет Opacity, и заводить три свойства,
	// которые всё равно не покрывают знак целиком, дороже, чем не заводить ни одного
	public String? Watermark { get; set; }

	public override void ApplyStyles(String selector, StyleBag styles)
	{
		var sel = "Page";
		_runtimeStyle = styles.GetRuntimeStyle(sel);
		foreach (var col in Columns)
			col.ApplyStyles(sel, styles);
		Header?.ApplyStyles(sel, styles);
		Footer?.ApplyStyles(sel, styles);
		ApplyStylesSelf();
	}
}
