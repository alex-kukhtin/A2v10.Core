// Copyright © 2022-2026 Oleksandr Kukhtin. All rights reserved.

using System.Dynamic;

using QuestPDF.Infrastructure;

namespace A2v10.ReportEngine.Pdf;

// scope не бывает пустым: наверху это модель, ниже — элемент коллекции.
// Он приходит параметром, а не хранится в контексте: композеры отдают замыкания
// в QuestPDF, и момент их вызова принадлежит библиотеке, а не нам
internal abstract class FlowElementComposer
{
	internal abstract void Compose(IContainer container, ExpandoObject scope);
}
