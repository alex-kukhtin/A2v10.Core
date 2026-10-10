# 05 — ViewEngines (Xaml, Html) и ReportEngines

ВЫСОКАЯ
1. WorkbookHelper.cs:176–216 GetRealRows: при null-коллекции continue без r++ → бесконечный цикл (C#, бюджет Jint не спасает). GetTableRealRows :227 безопасен (foreach).
2. Нет кодирования HTML/Vue-шаблона: TagBuilder.cs:228–240 атрибуты, :202 InnerText (SetInnerText принимает сырой HTML — Dialog.cs:221, InlineDialog.cs:86 пишут &#x2715;); UIElementBase.RenderContent :91 как есть; SheetGeneratorReport.cs берёт Title полей/фильтров из DataModel (:42, 170–172, 219, 280, 305) в статический SheetCell.Content → RenderContent / SheetCell.cs:100–105. HTML-инъекция и инъекция в Vue-шаблон ({{…}} из БД). Высокая, если конфигурацию отчёта правит пользователь.
3. RenderContext.cs:100 IsDebugConfiguration = true (TODO). Render="Debug" в проде (UIElementBase.cs:250–251, Control.cs:163–164); test-id в проде: Control.cs:40, Panel.cs:52, Dialog.cs:82, Hyperlink.cs:117, PropertyGrid.cs:44.
4. Bind.cs:70 хвостовая запятая у format: + ToJsonObject (Infrastructure/Helpers/CollectionHelpers.cs:26) → "format: x,,mask:" при Format={Bind} + Mask/HideZeros → SyntaxError в браузере.

СРЕДНЯЯ
5. JS-литералы в ' без экранирования: XamlElement.cs:147 (GetBindingString → Confirm/Toast), BindCmd.cs:291,294,462,489,548,560,580, FilterDescription.cs:26, StringExtensions.cs:14, TabButton.cs:25; а SortDescription.cs:20, GroupDescription.cs:26/30 используют EncodeJs. LocalizeCheckApostrophe (RenderContext.cs:290–298) снимает \' — словари хранят JS-экранированное. Апостроф («об'єкт») ломает @click. Bind.cs:73/78 экранирует только ' в обратных кавычках и до Localize.
6. TagBuilder.MergeAttribute (:170–179) склеивает через пробел и для :attr-выражений: SheetCell.cs:55 + :122; UIElement.cs:24/37 + Span.cs:41. Точечные исключения UIElement.cs:25–26, Span.cs:38–39.
7. LSP: RenderElement абстрактный (UIElementBase.cs:28), бросают NotImplemented: TabButton.cs:19, TreeView.cs:40 (TreeViewItem), TreeGridColumn.cs:35, AutoPages/DataColumn.cs:24. TagBuilder.SetInnerText(Object) :33–36 всегда бросает; XamlPartProvider.GetXamlPartAsync :90.
8. NotImplementedException вместо ошибок автора: BindCmd.cs:195, :559, :573, :578; FilterDescription.cs:39, 54; Sheets/Sheet.cs:220 — рядом есть XamlException.
9. IDisposable: ExcelConvertor.cs:185 SpreadsheetDocument.Open не освобождается; StimulsoftReportEngine.cs:29 поток.
10. ExcelConvertor: :336–340 Descendants<CellFormat> захватывает cellStyleXfs и cellXfs; :137 _fills[id-1] при fillId=0 underflow; :190 мёртвый ??; :197 обязательный SharedStringTablePart.
11. PageComposer.cs:63–99 недостижимая ветка Spreadsheet (ReportDocument.cs:22–25), пустой блок :67–70; дубль настроек PageComposer.cs:30–61 vs SpeadsheetComposer.cs:30–63, разошлись (Verdana vs Calibri); ComposeContent :125–137 container.Column на один контейнер (не проверено запуском).
12. ExcelReportEngine.cs:28 всегда бросает, README рекламирует под xlsx; PDF тянет OpenXml транзитивно; PdfReportEngine.ExportAsync игнорирует format; копия разбора {{…}}: PdfReportEngine.cs:45,71–76 vs ExcelReportEngine.cs:18,23–26.
13. Stimulsoft: net6.0 при net8+ Infrastructure — не собирается, нет в sln, пакеты устарели (Data.Interfaces 10.1.7330); StimulsoftController.cs:59 мёртвая queryString, :70 Int64.Parse без проверки, :82 текст исключения клиенту.
14. ReportEngines/ReportEngineExcel — песочница с C:\Projects\NovaEra.2023\… (Program.cs:12), в sln (:125). Test.PdfReportEngine — настоящий MSTest (~66 тестов), имя вводит в заблуждение.
15. XamlViewEngineTests: 3 теста, WritePage.cs:46 без Assert; ни один тест не вызывает RenderElement; ExcelConvertor без тестов.

НИЗКАЯ
16. RenderContext.cs:106, 212–213, 225–226, 260–270 закомментированный type checker; typeCode в GetTypedNormalizedPath :218 игнорируется.
17. UIElementBase.cs:269 в сообщении rmResult вместо rmString.
18. Дубли: Layouts/Card.cs:72–107,109–131 ≈ KanbanCard.cs:43–64,66–87; Dialog.cs:255–273 ≈ InlineDialog.cs:59–102; UIElementBase.cs:133–193 три метода одной логики; TestId&&IsDebug пять раз.
19. Без Localize: ContentControl.cs:18, Drawing/Card.cs:50,68, StateCard.cs:87,101,116; ListItem.cs:66 Localize вместо LocalizeCheckApostrophe.
20. PdfReportEngine ctor :26–31 пишет статические QuestPDF.Settings — ambient.
21. Перф: TagBuilder аллокации (:186), GridRowCol.GetGridAttributes List на тег; Diagram.cs:50–57 ресурс на каждый рендер; мёртвые #else Bind.cs:91–93, Grid.cs:260–262; XamlRenderer.cs:62 парсинг без кеша (видимо сознательно).
22. XamlPartProvider :55–63 общий кеш для OrNull и бросающего — закешированный null; FileSystemWatcher :37–44 не освобождается.
23. StringExtensions.ParseUrlQuery:26 Split('=')[1] IndexOutOfRange; ToDictionary на дубле ключа.
24. Длинные: SheetGeneratorReport.Generate 207 строк (63–269), ExcelConvertor.ParseFile 178 (176–353), BindCmd.GetCommand 171 (185–355), Selector.RenderElement 138, DataGridColumn.RenderColumn 128; BindCmd.cs 746, Enums.cs 509; BindCmd.Execute «not used» :119.
25. Глубина 6 уровней; XamlElement.SetParent :77–81 скрыто вызывает OnEndInit; A2v10.Xaml.Report/Base — параллельные Bind/BindImpl/Thickness/Length/XamlElement, два RenderContext; TemplateReader.Extensions публичный изменяемый массив :16.

Общая: отчётный код моложе и дисциплинирован; риски в WorkbookHelper, импорте Excel и XAML-движке. Системно: нет единого правила кодирования HTML/JS; нет теста, рендерящего HTML.
