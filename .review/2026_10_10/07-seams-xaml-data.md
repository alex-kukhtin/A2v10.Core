# 07 — Швы: A2v10.System.Xaml 10.1.8057 и A2v10.Data 7625

Только чтение. Каждая находка подтверждена чтением кода (dotnet не запускался).
Не повторяется: экранирование HTML/JS во ViewEngine.Xaml, Bind.cs:70, записи ISSUES.md.

## System.Xaml

### X1. Место ISSUES 2.21: писатель не отличает литерал от разметки
- Репозиторий: a2v10.system.xaml.
- Где: `A2v10.System.Xaml/XamlWriter.cs:52` и `:54` — строковое свойство пишется `WriteAttributeString` как есть, без проверки ведущей `{`. Корень на уровень выше: `XamlWriteNode.cs:97` (обычная строка) и `XamlWriteNode.cs:128` (`ParseBinding`, результат `IBindWriter.CreateMarkup`) кладут значение в один и тот же `XamlWriteProp` с `Value: String`, так что на `XamlWriter.cs:49-55` литерал `{x: 1}` и разметка `{Bind X}` неразличимы. То же для `IXamlConverter.ToXamlString()` (`XamlWriteNode.cs:112`) и прикреплённых свойств (`attVal.ToString()`, `:247`).
- Ридер: `XamlNode.cs:144` срезает префикс `{}`, `:146` берёт `{...}` как расширение. Значит симметричны два дефекта, а не один: литерал, начинающийся с `{` и кончающийся `}`, читается как markup extension; литерал, начинающийся с `{}`, теряет два символа при перечитывании. Текстовое содержимое элемента (`:59`, `:75`) не затронуто — ридер не разбирает расширения в тексте (`XamlReader.cs:115-119`).
- Принцип: round-trip (запись — обратная операция чтения); тип значения теряет различие «литерал/разметка» (примитивная одержимость строкой).
- Последствие: рукописный view после материализации меняется или не грузится; beam зелёный только потому, что генератор таких литералов не пишет.
- Серьёзность: средняя (латентно для генерации, реально для общего контракта пакета).

### X2. Запасной путь писателя для неизвестного типа значения даёт нечитаемый текст
- Репозиторий: a2v10.system.xaml.
- Где: `XamlWriteNode.cs:95-101` обрабатывает только `String/Boolean/Int32/UInt32` (и enum); всё остальное без `IXamlConverter` и без `Add` уходит в `XamlWriteNode.Create(value, parent)` (`:117`) — элемент с именем CLR-типа и собственным `xmlns="clr-namespace:...;assembly=..."` (`:29`, `XamlWriter.cs:39`). Для `Double` (например `ZoomValue.Value`, `ViewEngine.Xaml/Sheets/PrintPage.cs:14`) это `<Double xmlns="clr-namespace:System;assembly=System.Private.CoreLib"/>` — значение потеряно в любом случае. Ридер же переопределение префикса не принимает: `NodeBuilder.cs:101-102` — пространства имён одно на документ, «первый выигрывает», XML-область видимости `xmlns` игнорируется. Поэтому и элемент из `A2v10.Xaml.Drawing` (14 файлов в ViewEngine.Xaml), записанный писателем внутри корня `A2v10.Xaml`, будет искаться в `A2v10.Xaml` → `Class ... not found`.
- Принцип: OCP/LSP писателя — «любой объект пишется» обещано сигнатурой `GetXaml(Object)`, но выполнено для узкого набора; ридер и писатель расходятся в модели пространств имён.
- Последствие: тихая потеря значений Double/Decimal/Int64/DateTime/Guid и нечитаемый вывод для элементов другого CLR-namespace; сейчас Metadata таких не генерирует, beam этого не видит.
- Серьёзность: средняя (латентная).

### X3. Кеш дескрипторов типов ключуется не всем, от чего зависит дескриптор
- Репозиторий: a2v10.system.xaml.
- Где: ключ `ClassNamePair(Prefix, Namespace, ClassName, IsCamelCase)` (`NodeBuilder.cs:55`, `:329-334`), но `BuildTypeDescriptor` зависит ещё от (а) сборки из `xmlns` текущего документа (`:184`, сборки в ключе нет), (б) наличия `IAttachedPropertyManager` у текущего ридера (`:253-254`: при менеджере `AttachedProperties = null`), то есть от `XamlServicesOptions.UseInternalAttached`. Статический общий кеш `XamlServices._typeDescriptorCache` (`XamlServices.cs:10`) делится между вызовами с любыми опциями. Если дескриптор первым построил ридер с менеджером, то ридер с `UseInternalAttached = true` идёт в ветку `XamlNode.cs:171-180` и `TypeDescriptor.SetAttachedPropertyValue` (`TypeDescriptor.cs:143-144`) молча выходит — прикреплённые свойства пропадают без ошибки.
- Принцип: кеш должен ключеваться всеми входами вычисления; статическое разделяемое состояние с неявной зависимостью от опций.
- Последствие: порядко-зависимая тихая потеря `Grid.Row`-подобных значений в процессе, где XAML читают с разными опциями. В Core `UseInternalAttached` не используется (только тесты пакета) — латентно.
- Серьёзность: низкая–средняя.

### X4. Кешированный дескриптор держит первый NodeBuilder (мёртвое поле)
- Репозиторий: a2v10.system.xaml.
- Где: `TypeDescriptor.cs:9`, `:14` — `Func<XamlNode, Object?> BuildNode`, передаётся `BuildNode` экземпляра первого `NodeBuilder` (`NodeBuilder.cs:237`). Свойство нигде не вызывается (все вызовы идут через `builder.BuildNode`). Через делегат кеш навсегда удерживает первый `NodeBuilder` → его `XamlServiceProvider` → `RootObject` первого прочитанного документа, внедрённые сервисы (`IXamlPartProvider`), очередь отложенных замыканий с целями.
- В Core кеш живёт столько же, сколько `XamlPartProvider` (`ViewEngine.Xaml/XamlPartProvider.cs:28` → `AppXamlReaderService` → новый `TypeDescriptorCache` на экземпляр); сброс `_cache` по watcher-у (`:39`) эти графы не освобождает.
- Принцип: мёртвый код; кеш захватывает состояние одного вызова.
- Последствие: удержание памяти (дерево первого view на каждый тип), и ловушка: тот, кто начнёт вызывать `descriptor.BuildNode`, получит чужие namespaces и чужой корень.
- Серьёзность: низкая.

### X5. «Нет catch» соблюдено, «XamlException — единственный тип ошибки» — нет
- Репозиторий: a2v10.system.xaml.
- Проверено: `catch` в пакете нет ни одного — обещание CLAUDE.md выполнено.
- Но ридер бросает и другие типы: `InvalidOperationException` (`XamlServices.cs:41,47,53`; `Extensions/ServiceProviderExtensions.cs:12`), `InvalidProgramException` (`PropertyDescriptor.cs:29,63,75`), `NotImplementedException` (`PropertyDescriptor.cs:81` — вложенное свойство-коллекция без конструктора), `ArgumentNullException` (`XamlReader.cs:130`), а также непереведённые исключения BCL из путей конвертации: `Convert.ChangeType` (`NodeBuilder.cs:363`, `PropertyConvertor.cs:156`, `TypeDescriptor.cs:154`), `Enum.Parse` (`TypeDescriptor.cs:148` — для прикреплённых свойств, тогда как обычные дают понятное `Invalid enum value`, `PropertyConvertor.cs:145-148`), `Assembly.Load` (`NodeBuilder.cs:97`), `ArgumentException` от `Dictionary.Add`, когда одно свойство задано и атрибутом, и вложенным элементом `<T.Prop>` (`XamlNode.cs:125` против `:145/:149`).
- Принцип: единый контракт ошибок (CLAUDE.md пакета: «One type to catch, one type to enrich»); LSP для вызывающего, который ловит `XamlException`.
- Последствие: план LINE-INFO.md («catch (XamlException) в двух местах») не накроет эти пути — `Grid.Row="abc"`, неверное значение enum в прикреплённом свойстве, свойство, заданное дважды (атрибутом и элементом), останутся без позиции и с сообщением BCL (`Input string was not in a correct format`), то есть ровно тот класс ошибок, который LINE-INFO.md хочет устранить.
- Серьёзность: средняя.

### X6. Хвост после закрывающей `}` в расширении отбрасывается молча
- Репозиторий: a2v10.system.xaml.
- Где: `Markup/ExtensionParser.cs:73-79` — `Read()` возвращается на первой `RightCurly`, остаток текста не проверяется; ридер отдаёт в парсер любое значение, которое начинается с `{` и кончается `}` (`XamlNode.cs:146`). Атрибут `Content="{Bind Name} - {Bind Code}"` даёт привязку к `Name`, «` - {Bind Code}`» исчезает без ошибки. Аналогично `{Bind Path=}`: состояние `Value` (`:122-130`) пропускает не-значение и уходит в `Continue`, расширение строится без пути.
- Принцип: fail-fast; тот же класс дефекта, что уже исправлен в `Unquoted()` (комментарий `:142-152`), — тихо принятое некорректное.
- Последствие: правдоподобная ошибка LLM (склейка двух привязок в одной строке) проходит `a2 view validate` и видна только как неполный текст на экране.
- Серьёзность: средняя (бьёт по петле обратной связи — главной цели платформы).

### X7. Прикреплённые свойства в режиме менеджера не проверяются при чтении вовсе
- Репозиторий: a2v10.system.xaml (механизм) + A2v10.Core (режим по умолчанию).
- Где: `XamlServiceProvider.cs:19-20` — менеджер регистрируется всегда, кроме `UseInternalAttached`; Core этот флаг не ставит. Тогда `XamlNode.cs:166-170` кладёт любое `Owner.Prop="..."` в `XamlAttachedPropertyManager` сырой строкой (`Services/XamlAttachedPropertyManager.cs:11-14`), не спрашивая, существует ли владелец и его свойство; конвертация отложена до `GetProperty<T>` (`:16-20`), то есть до рендера (`ViewEngine.Xaml/Layouts/Grid.cs:40-60`).
- `a2 view validate` только загружает (`Tools/A2v10.Cli/View/ViewValidateCommand.cs:43`), поэтому `Grid.Rwo="1"` и `Foo.Bar="1"` молча игнорируются навсегда, а `Grid.Row="abc"` или `Grid.VAlign="Middel"` проходят валидацию и падают только на рендере (первое — `FormatException` из `Convert.ChangeType`, без позиции).
- Принцип: проверка на шве сборки (CLAUDE.md Core: «beam verifies the assembled result»); одна и та же ошибка значения проверяется для обычного свойства при загрузке и для прикреплённого — нет.
- Последствие: дыра в петле обратной связи именно для разметки раскладки (Grid.*), которую LLM пишет чаще всего; «validate зелёный» не значит «view рендерится».
- Серьёзность: средняя.

### X8. Писатель не пишет get-only коллекции, которые ридер умеет заполнять
- Репозиторий: a2v10.system.xaml.
- Где: `XamlWriteNode.cs:70` — `if (!prop.CanWrite) return;` стоит до обработки коллекций (`AddCollectionProp`, `:115`), поэтому свойство `{ get; } = []` не пишется никогда, даже если это `ContentProperty`. Ридер такие свойства поддерживает: `TypeDescriptor.cs:51-59` копирует элементы в существующую коллекцию.
- Пример в Core: `SheetColumnGroup.Columns` и `SheetCellGroup.Cells` (`ViewEngine.Xaml/Sheets/SheetColumnGroup.cs:5,9`, `SheetCellGroup.cs:6,9`) — content-свойства get-only; `ChessboardReportBuilder.cs:99-121` их заполняет. Сейчас отчёты не материализуются через `XamlTextBulder`, поэтому латентно.
- Принцип: симметрия writer/reader; порядок проверок в `ParseOneProperty` противоречит модели ридера.
- Последствие: дети групп молча исчезают из записанного текста; beam (см. С5) этого не увидит.
- Серьёзность: низкая–средняя.

## Data.Core

### D1. MetadataCache — синглтон на обычных Dictionary, и пишется даже при выключенном кеше
- Репозиторий: a2v10.data.core.
- Где: `A2v10.Data/MetadataCache.cs:8-9` — два `Dictionary`, без блокировок; чтение `:14`, `:45`, запись `:22-25`, `:61`. Регистрация синглтоном: `A2v10.Data/Extensions/DependencyInjection.cs:13`, в Core — `Platform/A2v10.Platform/ServicesExtensions.cs:39`. `SqlDbContext.GetWriterMetadata` (`SqlDbContext.cs:439-462`) читает кеш только при `IsWriteMetadataCacheEnabled`, но `AddWriterMetadata` (`:460`) вызывает безусловно — при выключенном кеше каждый save перезаписывает общий словарь.
- Принцип: потокобезопасность разделяемого состояния; флаг, который не отключает то, что обещает отключить.
- Последствие: параллельные сохранения/`SaveList`/`DeriveParameters` разных процедур из разных запросов пишут в один `Dictionary` — возможны порча внутренней структуры (зависание в цикле, `IndexOutOfRange`, «same key already added») под нагрузкой; при `MetadataCache: false` (так в тестовых appsettings Core) гонка не исчезает, а происходит на каждом сохранении. Кеш включён по умолчанию (`Extensions/DataConfiguration.cs:10`).
- Серьёзность: высокая.

### D2. ClassFactory: мутация словаря под разделяемой блокировкой чтения
- Репозиторий: a2v10.data.core.
- Где: `A2v10.Data/DynamicObject/DynamicType.cs:127-134` — `_classes.Add(signature, type)` выполняется под `AcquireReaderLock`, то есть одновременно с другими читателями; `CreateDynamicClass` (`:146-158`) берёт writer-lock только на генерацию типа и после `DowngradeFromWriterLock` проверку не повторяет. Два потока с одинаковой новой сигнатурой оба создают тип, второй `Add` бросает `ArgumentException`; параллельный `TryGetValue` видит словарь в момент роста. Сборка `AssemblyBuilderAccess.Run` (`:119`) не выгружается — каждая новая сигнатура навсегда.
- Потребитель: `DynamicDataModel.GetDynamic()` (`DynamicDataModel.cs:122-125`) → `StimulsoftReportEngine.cs:60` в Core — параллельные отчёты.
- Принцип: потокобезопасность статического кеша (double-checked без повторной проверки).
- Последствие: редкие падения генерации отчёта при первом параллельном обращении к новой форме данных.
- Серьёзность: средняя.

### D3. Синхронный ввод-вывод внутри async-путей сохранения
- Репозиторий: a2v10.data.core.
- Где: `SqlDbContext.SaveModelAsync` (`:506-550`) и `SaveModelBatchAsync` (`:553-610`) вызывают `GetWriterMetadata`, который делает `cmd.ExecuteReader()` синхронно (`:449-458`); `MetadataCache.DeriveParameters` (`MetadataCache.cs:42`, `:57`) — синхронный `SqlCommandBuilder.DeriveParameters`, круг до сервера, — вызывается из `SaveModelAsync` (`:535`), `SaveListAsync` (`:433`), `SaveModelBatchAsync` (`:582`, `:591`).
- Принцип: async всю дорогу; блокирующий I/O в async-методе.
- Последствие: на холодном (или выключенном) кеше каждый save блокирует поток пула на 1-2 лишних round-trip; под нагрузкой — голодание пула потоков ASP.NET.
- Серьёзность: средняя.

### D4. Перегрузки с одним именем ведут себя по-разному
- Репозиторий: a2v10.data.core.
- Где: `LoadModelSqlAsync(source, sql, Object? prms)` разрешает источник `{{key ?? default}}` (`SqlDbContext.cs:379`), а `LoadModelSqlAsync(source, sql, Action<DbParameterCollection>)` (`:398-414`) — нет, источник уходит в `_config.ConnectionString` как есть. `ResolveSource` для не-`ExpandoObject` бросает `NotImplementedException` (`:282`). `SaveModelBatchAsync` принимает `commandTimeout`, но не применяет его ни к итоговой команде (`:596-600`), ни при делегировании (`:556` — параметр теряется), тогда как `SaveModel/SaveModelAsync` применяют (`:475-476`, `:533-534`).
- Принцип: LSP/принцип наименьшего удивления для перегрузок одного контракта.
- Последствие: Metadata почти везде пользуется Action-перегрузкой — объявленный в ней источник вида `{{...}}` (латентно: Metadata таких сейчас не строит) ушёл бы в строку подключения как есть; долгий batch-save обрывается по общему таймауту вопреки переданному.
- Серьёзность: низкая.

### D5. Утечка открытого соединения при ошибке установки тенанта
- Репозиторий: a2v10.data.core.
- Где: `SqlDbContext.GetConnection` (`:169-176`) и `GetConnectionAsync` (`:183-190`) открывают `SqlConnection` и затем вызывают `SetTenantId[Async]` (`:911-936`); если процедура тенанта бросает, открытое соединение не освобождается — `using` у вызывающего ещё не взял его во владение.
- Принцип: владение ресурсом при частичной инициализации.
- Последствие: при сбое процедуры тенанта (неверный тенант, права) соединения уходят из пула до сборки мусора; в мультитенантной конфигурации — исчерпание пула.
- Серьёзность: низкая–средняя.

### D6. Ширина IDbContext
- Репозиторий: a2v10.data.core.
- Где: `A2v10.Data.Interfaces/IDbContext.cs:9-61` — 32 члена: синхронные/асинхронные пары, типизированные, Raw, Stream, Expando, `ParameterBuilder`, плюс `GetDbConnection[Async]`, отдающий сырой `IDbConnection`. Metadata использует 6: `LoadModelSqlAsync` (31 вызов), `LoadAsync`, `LoadListAsync`, `ExecuteAsync`, `GetDbConnectionAsync`, `ConnectionString`; при этом приводит соединение к `SqlCommand` (`DatabaseMetadataProvider.cs:70`, `SqlDbGenerator.cs:867`).
- Принцип: ISP; абстракция, которая всё равно протекает до `SqlCommand`.
- Последствие: любая реализация (тестовая, будущая из MULTIDB-DESIGN.md) обязана реализовать 32 метода, хотя потребитель-ядро опирается на один; синхронные дубли поощряют sync-путь.
- Серьёзность: низкая.

## Швы с Core

### С1. Сохранение Metadata — многооператорный пакет без транзакции
- Репозиторий: A2v10.Core (Metadata) поверх a2v10.data.core.
- Где: `Platform/A2v10.Metadata/Database/SqlBuilderPlain.cs:669-714` — пакет save: `set xact_abort on` без `begin tran`, затем merge главной таблицы, `exec` процедуры автономера и `update` номера (`:630-641`), merge каждой коллекции деталей (`:444-463`), merge тегов (`:545-550`), всё отправляется через `IDbContext.LoadModelSqlAsync` (`:746`), который транзакции не открывает (`a2v10.data.core/A2v10.Data/SqlDbContext.cs:780-822` и синхронный `:824-843`, `CreateCommandText`). `xact_abort` прерывает пакет на ошибке, но не откатывает уже автокоммиченные операторы.
- Контраст: Data.Core свой batch оборачивает в `begin tran`/`commit` (`BatchCommandBuilder.cs:84-87`), сама Metadata делает это для post/unpost (`SqlBuilderPost.cs:53`, `:63`) и для пользователей (`Admin/SqlBuilderUsers.cs:160`, `:175`) — то есть правило в платформе есть, а у самого частого пути записи оно потеряно. В ISSUES.md не записано.
- Принцип: атомарность единицы работы; API чтения (`LoadModel*`) использован для записи и не несёт её гарантий.
- Последствие: ошибка в деталях (FK на удалённый элемент, CHECK, длина, deadlock-victim) оставляет обновлённую шапку и номер из счётчика при старых/частичных строках; пользователь видит ошибку, а документ в базе уже другой.
- Серьёзность: высокая.

### С2. Два писателя табличных параметров с разной семантикой для одного IDataModel
- Где: Metadata строит TVP сама — `Platform/A2v10.Metadata/Database/DataTableBuilder.cs:79-120`; классический путь — `a2v10.data.core/A2v10.Data/DataModelWriter.cs` + `SqlExtensions.ConvertTo` (`SqlExtensions.cs:86-133`). Расхождения, проверенные чтением: (1) пустая строка — Data.Core пишет NULL, если не `AllowEmptyStrings` (`SqlExtensions.cs:123-124`); Metadata — `''` для любой строковой колонки (пустота проверяется только для Guid, `DataTableBuilder.cs:108-110`), настройку `AllowEmptyStrings` не читает; (2) превышение длины — Data.Core даёт понятное «Line N. The string ... is too long for the 'X' field» (`DataModelWriter.cs:78-84`), Metadata — `DataColumn.MaxLength` (`DataTableBuilder.cs:69`) и сырое `ArgumentException` ADO.NET; (3) приведение типов — Data.Core через инвариантный разбор с форматами дат, Metadata — неявной конвертацией `DataRow` (`:119`).
- Принцип: DRY знания о записи модели; одна модель — одна семантика сохранения (CLAUDE.md: «Save posts it back via a TVP» — одна труба).
- Последствие: одно и то же очищенное поле сохраняется как NULL в классическом endpoint-е и как `''` в metadata-endpoint-е; фильтры `is null` и отчёты ведут себя по-разному; ошибки длины выглядят по-разному.
- Серьёзность: средняя.

### С3. Грамматика recordset-ов размножена строками, а парсер прощает опечатки
- Где: Metadata пишет имена `[X!TX!Array]`, `!!Id`, `!RefId`, `!Map`, `!MainObject`, `!Tree`, `!Filter`, `!!RowCount`, `!$System!` интерполяцией строк примерно в 50 местах в 15+ файлах (`Database/SqlBuilderPlain.cs:103-227`, `SqlBuilderIndex.cs:329-423`, `SqlBuilderPrint.cs:246-305`, `SqlBuilderFetch.cs:32-165`, `RefMapBuilder.cs:219`, `Reports/ReportGrouping.cs:80`, `Reports/LedgerReportBuilder.cs:58` и др.; есть общий помощник лишь для части колонок — `Database/SqlExtensions.cs:294-300`). Владелец грамматики — `DataModelReader`/`FieldInfo` в Data.Core, и он деградирует молча: неизвестный модификатор третьего сегмента превращается в `Scalar` (`InternalHelpers.cs:65-70`), неизвестный спец-тип — в `Unknown` (`:87-95`, комментарий `:91` это признаёт).
- Принцип: единственный владелец формата; fail-fast на шве (CLAUDE.md Core: «beam verifies the assembled result at the assembly seam»).
- Последствие: опечатка вида `!Arary`/`!MainObjet` в генераторе даёт не ошибку, а скалярное поле и пустой экран; проверить формат может только интеграционный тест с базой. Тип-строитель имени recordset-а (в Data.Interfaces или в Metadata) снял бы класс ошибок.
- Серьёзность: средняя.

### С4. Ошибки SQL доходят до пользователя по строковому протоколу, и Metadata его соблюдает наполовину
- Где: клиент различает два вида ошибок только по префиксу текста `UI:` (`Platform/A2v10.Web.Assets/wwwroot/scripts/main.js:5231-5239`, `:11297-11300`: с префиксом — диалог платформы, без — `alert()` браузера); сервер отдаёт `ex.Message` как есть, после одного разворачивания `InnerException` (`Platform/A2v10.Platform.Web/Controllers/BaseController.cs:89-99`); `SqlException` нигде не отображается в пользовательскую ошибку (ни в Data.Core, ни в Core нет обработки `SqlException`). Metadata: 6 `throw` с `UI:` (`SqlBuilderPlain.cs:405,432,525`, `SqlBuilder.cs:102`, `SqlBuilderDbRemove.cs:39`, `SqlBuilderTree.cs:248`) и 6 без — среди них пользовательские состояния документа `@[Error.Document.AlreadyPosted]`, `NotPosted`, `AlreadyApplied`, `NoCompany` (`SqlBuilderPost.cs:42,59,87`, `SqlBuilderDbRemove.cs:57`). Коды тоже разнобойные (60000 и 600000), и номер нигде не читается — несёт ноль информации.
- Принцип: типизированный контракт ошибок вместо строкового префикса; единообразие.
- Последствие: «документ уже проведён» показывается системным `alert` браузера вместо диалога; любая неожиданная ошибка SQL (FK, unique, deadlock) уходит пользователю сырым текстом с именами таблиц и ограничений.
- Серьёзность: средняя.

### С5. XamlBeamTests проверяет идемпотентность, а не верность записи
- Где: `Tests/TestsMetadata/XamlBeamTests.cs:64-74` — инвариант «write(read(write(built))) == write(built)» и совпадение типа корня. Свойство, которое писатель теряет при первой записи, теряется и при второй — тексты равны, beam зелёный. Конкретные классы таких потерь в пакете: get-only коллекции (X8), значения непокрытых типов (X2, часть случаев), прикреплённые свойства, если менеджер не тот (X3). Сравнения `built` с `read` по объектному графу (или хотя бы рендера) нет. Кроме того, тест пропускает endpoint-ы без `BakedForms` и отчёты (`:45`, `:90-104`), а `ChessboardReportBuilder` строит XAML вне `XamlBuilder`.
- Принцип: beam должен сравнивать с источником правды (CLAUDE.md Core, «Loop first»), а не с самим собой.
- Последствие: «материализованный экран отличается от рантайм-экрана» не ловится, пока потеря симметрична.
- Серьёзность: низкая–средняя.

### С6. Мёртвые опции ридера в XamlRenderer
- Где: `ViewEngines/A2v10.ViewEngine.Xaml/XamlRenderer.cs:17-23` — создаётся `XamlServicesOptions` с `OnCreateReader`, который никуда не передаётся; реальное чтение идёт через `XamlPartProvider` → `AppXamlReaderService` со своими опциями (`AppXamlReaderService.cs:12-19`). Комментарий `:27` («XamlServices.Load sets IUriContext») описывает путь, которого нет.
- Принцип: мёртвый код, вводящий в заблуждение о том, какие сервисы видит ридер.
- Последствие: правка этих опций (например, добавление сервиса для расширения разметки) не подействует, и ошибка проявится только на рендере.
- Серьёзность: низкая.

### С7. MULTIDB-DESIGN.md не учитывает крупнейшего потребителя — Metadata
- Где: `a2v10.data.core/MULTIDB-DESIGN.md` отвергает «несколько SELECT в одном тексте, сгенерированном в C#» (раздел «Alternatives considered») и снимает TVP/`Structured`/`DeriveParameters` («What this removes»). Metadata делает ровно отвергнутое: генерирует T-SQL (`merge ... output`, `throw`, `set xact_abort`) и шлёт его `LoadModelSqlAsync` с `AddStructured` (`SqlBuilderPlain.cs:746-759`, `SqlBuilderTags.cs:94`), то есть привязана к SQL Server сильнее классического пути. В «Open questions» Metadata не упомянута (упомянут лишь `IParameterBuilder`).
- Принцип: дизайн-решение должно назвать всех потребителей контракта.
- Последствие: реализация MULTIDB по этому документу перенесёт на другую СУБД только процедурные приложения; metadata-приложения — главная линия развития по CLAUDE.md — останутся на SQL Server без записанного решения.
- Серьёзность: низкая (решение не начато), но фиксировать до старта.

## Общая оценка

Оба пакета компактны и в основном выдержаны. В System.Xaml обещание «нет `catch`» выполнено буквально, разделение на две фазы и прекомпиляция делегатов описаны честно. Слабые места System.Xaml — writer и общие кеши. Writer — поздняя и неполная пара ридеру: строка не различает литерал и разметку (это и есть место ISSUES 2.21, `XamlWriter.cs:52/54` с корнем в `XamlWriteNode.cs:97/128`), есть непокрытые типы и get-only коллекции. Кеш дескрипторов не учитывает часть того, от чего дескриптор зависит, и держит первый `NodeBuilder`. Обещание «один тип ошибки» не выполнено, поэтому план LINE-INFO.md не дотянется до части ошибок. Самая чувствительная для LLM-петли дыра — прикреплённые свойства и хвосты расширений: их ошибки `a2 view validate` не видит совсем.

В Data.Core главный риск — потокобезопасность: синглтон `MetadataCache` на обычных `Dictionary` (пишется даже при выключенном кеше) и `ClassFactory`. Следом идут синхронный I/O в async-сохранении и широкий `IDbContext`.

На шве с Core Metadata честно пользуется текстовым API (`LoadModelSqlAsync`) и не смешивает «имя процедуры» и «текст» в одном параметре. platformid типизирован последовательно через `AppPlatformId`. Но Metadata дублирует два знания, которые живут в Data.Core: семантику записи TVP и грамматику recordset-ов. При этом она теряет гарантию, которую Data.Core даёт своему batch-у: самый частый путь записи, save документа, идёт без транзакции. Это единственная находка высокой серьёзности на шве, и её стоит закрыть первой. Следом — `MetadataCache`.
