// Copyright © 2022-2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using QuestPDF.Infrastructure;

namespace A2v10.ReportEngine.Pdf;

// scope: наверху модель, ниже элемент коллекции. Параметром, а не полем контекста —
// момент вызова замыканий принадлежит QuestPDF
internal abstract class FlowElementComposer
{
	internal abstract void Compose(IContainer container, ExpandoObject scope);
}
