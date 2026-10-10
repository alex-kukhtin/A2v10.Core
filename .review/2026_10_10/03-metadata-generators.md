# 03 — A2v10.Metadata: генераторы (Database, Cli, Form, Xaml, Script, Ts, Js, Reports, Print, SqlScripts)

Ревью только чтением, 2026-10-10. Номера строк на текущий HEAD. Записанное в REVIEW-2026-09-26.md и ISSUES.md не повторяется (кроме раздела «статус»).

## Находки

### Ф1. Деплой не атомарен, а seed идёт первым и коммитится до DDL
`Database/SqlDbGenerator.cs:860-886` (`DeployDatabaseAsync`), порядок `:130-152`.
Скрипт режется по `go` и выполняется пачками на одном соединении без транзакции и без `xact_abort`; первая пачка — seed (`GenerateMetadataSeedAsync`, `:792-848`), который merge-ит `a2meta.Tables/Columns` с `when not matched by source then delete`. Если падает поздняя пачка (FK, валидирующий существующие строки, `:151`; unique-индекс, `:152`), `a2meta` уже описывает новую схему, а хеш не записан. Решение «SyncSchema без транзакции» в процедуре обосновано (`a2v10_metadata.sql:165-167`) — но для всего деплоя такого решения в CLAUDE.md нет.
Принцип: «what is executed and what is written are the same thing» (шапка файла) соблюдён для текста, но не для состояния; seed как «картина базы о себе» (CLAUDE.md «Seed») расходится с фактом на время между падением и успешным повтором.
Последствие: `GetFkReferrers` читает референтов из seed, в который уже могли попасть ещё не созданные таблицы → `DbRemove` строит `exists(select … from <нет такой таблицы>)` и падает; устойчивая ошибка (грязные данные под FK) держит базу в полуприменённом состоянии до ручного вмешательства — каждый следующий деплой снова применяет seed первым и снова падает на том же месте.
Серьёзность: средняя.

### Ф2. Отслеживаемая копия `a2v10_metadata.sql` на стенде устарела и без штампа версии
`Web/MainApp/@sql/a2v10_metadata.sql:4-5,89,141,159` против `Platform/A2v10.Metadata/SqlScripts/a2v10_metadata.sql`.
Копия в git: «module version : 8663», нет колонки `sql_as`, `GetDbHash` не отдаёт `Version`, referrers исключают `rep` вместо `led` (REVIEW 1.5 там не закрыт), нет фазы computed-колонок в SyncSchema, нет финального merge версии. `MainApp.csproj` пакет не потребляет (ProjectReference), `.targets` её не перезаписывает.
Принцип: «одна оригинальная копия, копии билдом» (CLAUDE.md «JSON schemas» — тот же закон для схем); CLAUDE.md корня «Release: what to check» про стамп.
Последствие: база стенда, поднятая из этой копии, отказывается `EnsurePlatformVersionAsync` («of no version»); человек, читающий `@sql` стенда, видит закрытый дефект как живой.
Серьёзность: низкая.

### Ф3. Две конвенции ошибок в сгенерированном SQL: `60000, N'UI:@[…]'` и `600000, N'@[…]'`
`Database/SqlBuilderPost.cs:42,59,87`, `Database/SqlBuilderDbRemove.cs:57`, `Database/PostStatements.cs:82` — `throw 600000` без префикса `UI:`; против `SqlBuilder.cs:102`, `SqlBuilderDbRemove.cs:39`, `SqlBuilderTree.cs:248`, `SqlBuilderPlain.cs:405,432,525` — `throw 60000, N'UI:@[…]'`. Плюс два текста без ключа локализации вовсе: `PostStatements.cs:82` («The operation of the document is not one of …»), `SqlBuilderBirth.cs:104` («The document to base on is not found»).
Клиент различает их по префиксу: `UI:` → платформенный диалог, иначе нативный `alert(msg)` (`A2v10.Web.Assets/wwwroot/scripts/tabmain.js:5219-5228`, `:9771-9774`); на загрузке страницы `PageController.cs:94` по тому же префиксу выбирает «статус» против HTML-страницы исключения. Номер 600000 нигде не объяснён и нигде не читается — опечатка, размноженная копированием.
Принцип: magic strings / один закон — одна реализация (одна функция «отказ пользователю» на весь слой отсутствует, каждый билдер пишет `throw` литералом); CLAUDE.md «Declarations…» относит `UI:`-ошибки к «нашим» ключам.
Последствие: «документ уже проведён», «нет компании», «уже применён» показываются пользователю нативным `alert` браузера (текст локализуется сервером, `BaseController.cs:99`), а не диалогом платформы; английский текст без ключа виден в любой локали.
Серьёзность: средняя.

### Ф4. Литерал `N'…'` в трёх дисциплинах; путь и имя папки вставляются без удвоения `'`
Удваивают: `SqlDbGenerator.Str` (`Database/SqlDbGenerator.cs:58-59`), `SqlExtensions.SqlLiteral` (`Database/SqlExtensions.cs:275`). Вставляют как есть: `Gate.Sql` — `@Url = N'{path}'` (`Database/Gate.cs:34`), `PostStatements.DocumentTypeValue` — `N'{document.Path}'` (`Database/PostStatements.cs:107`) и текст ошибки `:82`, `CreateRefViewsScript` — `N'{table.Path}/edit'` (`SqlDbGenerator.cs:351`), `OwnOperations` — `N'{endpoint.Name}'` (`Database/SqlBuilder.cs:83`), `SqlBuilderPlain.cs:328` — то же. `Path`/`Name` — это имя папки (`Meta/TableMetadata.cs:803`), а `CheckNames` (`DatabaseMetadataProvider.cs:316-384`) проверяет schema/table/model/fields/details/kinds, но не папку: она попадает под проверку только косвенно, когда из неё выводится `Model`; при написанном `model` имя папки не проверяется ничем. (`N'{Table.Model}'` в `SqlBuilderIndex.cs:364,369`, `SqlBuilderPlain.cs:120,124` безопасен — `Model` проверен.) Тот же путь уходит и в JS-строку шаблона документа: `$invoke('post', …, '{endpoint}')` (`Script/EditTemplate.cs:275,283`); единственное место слоя, где автор экранирования для JS вообще написал, — заголовок печати (`Meta/PrintTitle.cs:94-95`).
Принцип: одно правило экранирования на слой (CLAUDE.md «Names: what every place a name lands in accepts» — правило имён существует ровно чтобы любое место приземления принимало имя; папка в этот whitelist не входит, а приземляется в SQL-литерал).
Последствие: папка с апострофом (допустима в Windows) при явном `model` даёт синтаксическую ошибку в gate каждой пачки, во view ссылок при деплое и в provenance проводки — далеко от причины. Пользовательского ввода здесь нет, инъекции нет; это вопрос согласованности и места отказа.
Серьёзность: низкая.

### Ф5. `initialValues` с `"source": "profile"`: вызов процедуры, которой платформа не поставляет, и сырое имя параметра из файла
`Database/RefMapBuilder.cs:230-251` (`GenerateInitials`), `Database/SqlBuilderPlain.cs:137-142` (`getDefaultProfile`); схема `A2v10.App.Assets2026/Application/@schemas/metadata-endpoint-json-schema.json:722` принимает `profile`.
Генератор пишет `exec usr.GetProfilePreferences @UserId = @UserId, @{значение из файла} = @Init{key} output` — процедуры `usr.GetProfilePreferences` нет ни в одном `.sql` репозитория; имя выходного параметра — авторская строка без проверки и без скобок; `declare @Init{key} platformid` независимо от типа колонки; `insert into @map({mapKeys})` — имена колонок без `[]` (единственное такое место в map-builder'е, рядом `:274` со скобками). `getDefaultProfile` ищет колонку в `Table.Columns` (авторские) и зовёт `RefTableCheck` безусловно, тогда как соседний `getDefaultLiteral` (`:153`) — в `AllColumns()` с развилкой `IsRef`. Бейк (`Meta/DeclarationBake.cs:243-256`) и `CheckLiteralInitials` (`DatabaseMetadataProvider.cs:1267`) `profile` не отказывают. REVIEW 1.12 записал `Policy`/`Sql`; `Profile` — третий член того же класса, но в отличие от них у него ЕСТЬ генератор, и он порождает невыполнимый SQL.
Принцип: «слово входит в enum вместе с первым читателем или с отказом на загрузке» (REVIEW §5); ось bounded/ambient — вызов внешней процедуры по имени, которого нет в объявленных портах.
Последствие: эндпоинт грузится и проходит `meta validate`, первая же «создать» падает в SQL «Could not find stored procedure 'usr.GetProfilePreferences'».
Серьёзность: средняя.

### Ф6. `LoadIndexModelAsync` — один метод на 466 строк, три фазы через захваченные изменяемые локали
`Database/SqlBuilderIndex.cs:20-485`.
Разбор query (`:63-116`), генерация WHERE (`:127-197`) и всего батча (`:199-417`), привязка параметров (`:436-484`) — локальные функции одного метода, связанные изменяемыми `field`, `value`, `refdescr`, `filters`, `roleValues`, `offset`, `pageSize` (`:25-36`). Решение «какой параметр какого типа» (`:466-483`: Operation/Account → string, set → string без обнуления, иначе platformid) живёт отдельно от решения «какой предикат» (`:164-167`) и от «какого типа колонка в `@map`» (`:261`), хотя все три — ответ колонки. Тот же разбор фильтра по имени с fallback на lower-case повторён трижды (`:106`, `:112`, `SqlBuilderPlain.cs:72`).
Принцип: размер/связность; одно понятие — одно место (тип ключа ссылки уже отвечен `ToSqlDbTypeInfo`, здесь его третья копия рядом с исправленной в REVIEW 1.13 копией отчётов).
Последствие: новая колонка-ссылка с новым типом ключа (как было с Account) требует правки в трёх местах одного метода; пропуск — молча пустой фильтр (комментарий `:460-465` описывает ровно этот класс).
Серьёзность: средняя.

### Ф7. `PageSize`/`Offset` индекса берутся из URL без границ
`Database/SqlBuilderIndex.cs:66-71`, `:447-448`, `:323`.
Любое целое проходит: `PageSize=0` или отрицательный, `Offset<0` — ошибка SQL `FETCH`/`OFFSET` вместо страницы; `PageSize=10000000` — весь список (с map-резолвом ссылок и `count(*) over()`) одним ответом для любого, у кого есть `View`.
Принцип: «verified at the seams» — query-строка браузера это шов, и единственные два числа из неё, идущие в SQL, не нормализуются (тогда как `Dir` нормализуется `:76-77`, а `Order` сверяется с колонками `:86-87`).
Рядом то же для периода: `AddPeriodParameters` (`Database/SqlBuilder.cs:114-122`) разбирает `?From=`/`?To=` через `DateTime.ParseExact` — мусор бросает `FormatException`, тогда как соседний `AppPlatformId.ParseId` (`SqlExtensions.cs:64-67`) нарочно отвечает null, «a malformed id that threw here would take the whole page down instead».
Последствие: тривиальная нагрузка на базу/память одним запросом; невнятная ошибка (страница исключения) на мусорном значении.
Серьёзность: низкая.

### Ф8. Сохранение записи не в транзакции: шапка коммитится, строки могут не доехать
`Database/SqlBuilderPlain.cs:669-714` (`buildSqlUpdateText`).
Батч сохранения: `set xact_abort on` (`:672`) — и ни одного `begin tran` (единственные `begin tran` слоя — проводка `SqlBuilderPost.cs:53,81` и админка `Admin/SqlBuilderUsers.cs:160`). Дальше подряд в автокоммите: merge шапки (`:686-697`), выдача номера и `update` номера (`:702`), merge каждой коллекции строк (`:706`, по одному merge на коллекцию/вид, `:504-511`), merge тегов (`:708`). `xact_abort` без транзакции только обрывает батч — уже выполненные операторы остаются.
Расхождение с решением: CLAUDE.md «Autonums» («Gaps are accepted… buying it costs the save an outer transaction») отказывается от внешней транзакции ради того, чтобы блокировка счётчика не держалась через весь save, — и обсуждает только пропуски номеров. Цена «шапка без строк» там не взвешена; при этом номер можно выдать и вне транзакции (процедура и так коммитит счётчик сама), обернув только merges.
Принцип: целостность записи как единицы (CLAUDE.md «A row is merged into its own record only» — запись = шапка + её строки); «verified at the seams».
Последствие: любая ошибка на строках (FK на удалённый товар, усечение строки, NOT NULL, нарушение в второй коллекции) оставляет в базе новую шапку (новые итоги, дату, контрагента) со старыми/частично новыми строками; клиент видит ошибку и считает, что ничего не сохранено. Для документа с несколькими коллекциями — часть коллекций новая, часть старая. Рядом того же рода: проверка `rv` (`:517-529`) — отдельный `if exists` перед merge, без блокировки и без условия `t.rv = s.rv` в самом merge, так что оптимистическая блокировка оставляет окно check-then-act для двух одновременных сохранений. Тест такого не видит (REVIEW §5: «save без теста»).
Серьёзность: высокая.

### Ф9. Резолв ссылок написан дважды: `RefMapBuilder.GenerateResolves` и `SqlBuilder.FetchMaps`
`Database/RefMapBuilder.cs:185-228` против `Database/SqlBuilderFetch.cs:78-95`.
Оба строят одно и то же: CTE `with T as (select id = [...] from @map ... group by ... union all ...)` и `select [!TR…!Map] = null, [Id!!Id], [Name!!Name] = a.[Presentation] … inner join T`. Различаются в двух местах, и оба различия — дрейф, а не замысел: fetch читает `refTable.SqlTableName`, а не `RefSourceName` (вид документа с `Url`/`Icon`), и добавляет `ChoiceField + ColorField`, а не `RefFields` (нет `Role` у состояния, нет `Done`/`Url`/`Icon` у документа). Комментарий над `FetchAsync` (`SqlBuilderFetch.cs:73-76`) сам формулирует закон: «the object the selector hands to the row has to have the same shape as the one the document load hands to it. One shape, one place that decides it» — мест два. REVIEW 1.20 записал симптом (цвет); цвет с тех пор добавлен, но копия осталась. Третье описание той же формы — `ScriptBuilder.RefTsProperties` (`Script/ScriptBuilder.cs:90-108`): `.d.ts` обещает `Role`, `Done`, `Url`, `Icon` по `RefFields`, то есть обещание верно для map и ложно для объекта, пришедшего через fetch; `TsBeamTests` сверяет шаблоны с картами, а не карты с SQL.
Принцип: один закон — одна реализация; «Colour: a property of the row, never of the reference» (CLAUDE.md) — «the element carries the column on every road».
Последствие: ссылка на состояние или документ, унаследованная через `?inherit=` при наборе текста, приезжает без `Role` / без `Url`-`Icon`-`Done`, а та же ссылка из диалога или после перезагрузки — с ними; гиперссылка на документ в строке не рисуется до перезагрузки.
Серьёзность: средняя.

### Ф10. Небрэкетированная колонка в отчёте — класс REVIEW 1.8 в другом месте
`Reports/ReportGrouping.cs:80` (`ReferenceWithGrouping`): `[{c.Column}.Id!…!Id] = T.{c.Column}`; рядом `:96-98` `{alias}.Date`, `InOut` без скобок (последнее записано в REVIEW §2 как «честна наполовину»).
REVIEW 1.8 закрыт правкой одной строки индекса; правило «колонка — всегда `[…]`» не стало проверяемым, и в отчётах осталась та же форма. `CheckNames` пропускает `Order`, `Key`, `Group`, `Plan` (буквы).
Принцип: CLAUDE.md «Names» — «SQL brackets the name»; whitelist имён держится только при условии, что каждое место приземления скобит.
Последствие: группировка оборотного отчёта по колонке-ссылке с именем-ключевым словом (`"groups": ["Order"]`) — синтаксическая ошибка на Run; `meta validate` не видит (SQL отчёта строится только на запросе).
Серьёзность: низкая.

### Ф11. `@Id` загрузок привязан строкой, при том что «все `@Id` слоя типизированы»
`Database/SqlBuilderPlain.cs:371`, `Database/SqlBuilderTrans.cs:94`, `Database/SqlBuilderIndex.cs:442` — `AddString("@Id", …)`; против `AddTyped("@Id", PlatformId.SqlDbType, PlatformId.ParseId(…))` в `SqlBuilderPost.cs:106`, `SqlBuilderDbRemove.cs:73`, `SqlBuilderTree.cs:224,258`, `SqlBuilderUnique.cs:42`. REVIEW 1.7 при закрытии своей половины утверждает «как везде» — три места остались.
Принцип: «AppPlatformId: never re-derived» (`SqlExtensions.cs:27-36`) и «a string parameter would lean on type precedence at every comparison» (`SqlBuilderPost.cs:99-103` — тот же автор, тот же довод).
Последствие: `/edit/abc` на bigint-базе — ошибка конвертации nvarchar→bigint из SQL (HTML-страница исключения), а не пустая карточка; `RecordCheck("@Id")` и gate читают тот же нетипизированный параметр. В `SqlBuilderIndex.cs:442` параметр к тому же не используется текстом (REVIEW §3).
Серьёзность: низкая.

### Ф12. Ось «действие» диспетчеризуется строковыми литералами в трёх (четырёх, с сохранением) switch-ах, которые не сверены друг с другом
`Builders/BaseModelBuilder.cs:59-75` (`LoadModelAsync`), `:77-95` (`CreateTemplateAsync`), `:99-106` (`SaveModelAsync`, `"edit"`/`"editfolder"`), `Form/IndexPageXaml.cs:389-406` (`CreateXamlContainer`).
Каждое действие (`index`, `indexpartial`, `browse`, `edit`, `show`, `browsefolder`, `editfolder`, trans, print) должно появиться во всех трёх первых (а `edit`/`editfolder` — и в четвёртом); две — константами (`Constants.Trans.Action`, `Constants.Print.Action`), остальные — литералами, при том что `Constants.FormNames` (`Constants.cs:89-94`) уже держит `"index"/"edit"/"browse"`, а `OperationEndpointMetadata.BrowseAction` (`Meta/EndpointMetadata.cs:105`) — второй `"browse"`. Таблица «действие → кто отвечает»: `show` — только Load (отсюда REVIEW 1.1, который этим и открыт); `browsefolder` — Load и Xaml, шаблон пустой строкой; остальные — все три. Ничто не проверяет, что множества ключей равны, и `meta validate` идёт по формам, а не по действиям.
Принцип: magic strings; «a new kind widens nothing shared» — новое действие расширяет три несвязанных switch-а; «один вопрос — одно место».
Последствие: класс дефектов 1.1 («кнопка ведёт в действие, у которого нет одной из трёх половин») воспроизводится при каждом новом действии; падение — `NotImplementedException`/`InvalidOperationException` на клике, не на загрузке.
Серьёзность: средняя.

### Ф13. Таблица «вопрос о колонке → сколько мест отвечают» (диспетчеризация по `ColumnType`)
Подсчитано grep-ом по `Database`, `Cli`, `Form`, `Xaml`, `Script`, `Reports`; места, где ответ уже централизован, отмечены.

| Вопрос | Места (файл:строка) | Итог |
|---|---|---|
| SQL-тип | `SqlExtensions.cs:134-195` (+ T-SQL копия рендера в `a2v10_metadata.sql:200-207`, решение автора) | один, централизован |
| CLR / TS-тип | `SqlExtensions.cs:312-327`, `Js/JsExtensions.cs:18-27` — оба выводятся из SQL-имени | централизован |
| «это ссылка» | `TableMetadata.IsRefType` (`Meta/TableMetadata.cs:178-182`); свой список в `SqlModelColumnName` (`SqlExtensions.cs:298-299`, без `User`/`Master`); свой ветвёж FK (`Cli/CliDatabaseCreator.cs:141-180`, Parent/Folder/Operation/TagEntries); свой в seed (`SqlDbGenerator.cs:759-761`); свой список селектора (`Form/ControlsXaml.cs:600-601`) | 5 мест, списки разные |
| тип параметра ключа ссылки | `ToSqlDbTypeInfo`; `SqlBuilderIndex.cs:471-482` (Operation/Account → string, set → string, иначе platformid); `SqlBuilderFetch.cs:101-105` (Owner/Company → platformid); отчёты — `BaseReportBuilder.AddFilterParameters` | 4 места, одно из них централизовано |
| «число ли / выравнивание» | `XamlExtensions.IsNumber/IsCurrency` (`Xaml/XamlExtensions.cs:29-37`) — централизовано с REVIEW; но `BindSheetCell` (`:86`) выравнивает `Date` вправо, а `ToXamlAlign` (`:57`) — по центру; `BigInt` не число в XAML (`:29-31`), но `number` в TS (`JsExtensions.cs:24`) | 1 место + 2 расхождения |
| какие колонки пишет/шлёт/показывает оператор | общие предикаты `IsIndexColumn/IsEditColumn/IsSentColumn` (`Meta/MetadataExtensions.cs:16-27`), `IsFieldUpdated/IsFieldInserted` (`SqlExtensions.cs:329-345`); локальные: `SqlBuilderPlain.cs:94` (не Void), `:251` (не RowKind/Id), `:447` (merge строк), `SqlBuilderTree.cs:85` (REVIEW §2), `SqlDbGenerator.cs:491` (`Writable` для rows), `Script/ScriptBuilder.cs:64,69`, `Form/DefaultFormBuilder.cs:127`, `RefMapBuilder.cs:82` | 5 общих + 8 локальных |
| контрол по типу | `ControlsXaml.Editor` (`:516-676`) — единый для карточки и строки с REVIEW; плюс ячейка индекса `IndexPageXaml.cs:157-165` (Name/Color/Document), колонка дерева `ControlsXaml.cs:403-412`, ячейка отчёта `XamlExtensions.BindSheetCell` | 4 места, по местам, а не по копиям |

Принцип: «A rule is keyed by KIND… a new kind widens nothing shared» (CLAUDE.md «Declarations») в применении к типу колонки; REVIEW §5 «спрашивать что объявлено, не какого ты вида».
Последствие: новый `ColumnType`-ссылка (как `Account`, `BasedOn` — оба пришли недавно) требует правки 5+4 мест; пропуск даёт молча пустой фильтр (Ф6), TextBox вместо селектора или отсутствующий FK. Самые рискованные — строки 3 и 4.
Серьёзность: средняя.

### Ф14. SQL-инъекция из строки запроса: `?Group=` оборотного отчёта вставляется в литерал как есть
`Reports/TurnoverReportBuilder.cs:108` — `[Group] = N'{_grouping.GroupParams}'`; `GroupParams` — это `_prms.Get<String>("Group")` (`Reports/ReportGrouping.cs:68`), а `_prms` — query страницы отчёта без обработки (`Builders/ReportEndpointBuilder.cs:27` → `LoadReportModelAsync(view, platformUrl.Query)` → `SetGrouping(prms)`, `Reports/BaseReportBuilder.cs:83-84`).
Сам список группировок из того же параметра безопасен (`ReportGrouping.cs:56` — join с объявленными колонками), но «эхо» параметра обратно в модель склеено строкой, без параметра и без удвоения `'`. Единственное место во всём слое, где значение из браузера попадает в текст SQL; везде остальное явно оговорено и сделано параметром (`SqlBuilderPlain.cs:38-44` «never enters the SQL as text», `SqlBuilderFetch.cs:57-59`).
Принцип: «ambient authority» / «verified at the seams» — query-строка браузера это шов; правило «значение из браузера — только параметр», которому следует весь остальной слой.
Последствие: любой пользователь, которому открыт отчёт (при `useGrants` — с правом `View` на него; без `useGrants` — любой вошедший), выполняет произвольный T-SQL в контексте подключения приложения: `…/report/x?Run=1&Group=x';<любой SQL>;--`. Gate стоит до этой строки и не мешает. `meta validate` не видит (SQL отчёта при валидации не строится, `Generator/EndpointValidator.cs:262-268`).
Серьёзность: высокая.

### Ф15. Неподвижная точка карт печати не сходится, если путь бланка возвращается в уже пройденную таблицу
`Database/SqlBuilderPrint.cs:173-187` (`CollectMaps`), `:196-209` (`GroupMaps`), `:214-223` (`Merge`), `:229-239` (`Refs`).
Карта ключуется таблицей, её узел — `Merge` всех путей на эту таблицу, а `Rows` — текст, в который вложен `Rows` карты-источника. Если в бланке путь приходит в таблицу, уже имеющую карту (самоссылка `Agent.MainAgent → Agent`, или цикл `Agent → Employee → Agent`), слитый узел карты сохраняет ребёнка, указывающего на неё же; каждый раунд `Rows` получает новый вложенный подзапрос, `Signature` (`:189-190`) не совпадает никогда. Комментарий `:170-171` («each round only reaches one level deeper… the written tree has a bottom») верен для дерева, но не для слитых по таблице узлов. Проверено моделью алгоритма (Python-повтор `CollectMaps/GroupMaps/Merge/Refs` на `Document{Agent{MainAgent{Name}}}`): длина сигнатуры растёт 268 → 441 → 614 → … без остановки.
Принцип: «Resolved in full before a line of SQL exists. Every name the blank got wrong fails the build» (`:92-94`) — здесь не отказ, а зависание; loop без верхней границы.
Последствие: бесконечный цикл с неограниченным ростом строки — в запросе печати, в `LoadPrintPageModelAsync` (заголовок бланка идёт той же дорогой, `:376-383`) и в `meta validate` (`Generator/EndpointValidator.cs:275-…`, `Prints` строит этот SQL) — валидатор виснет вместо отказа.
Серьёзность: средняя.

### Ф16. Отчёты разрешают ссылки своим диалектом: `[Name]` вместо `Presentation`, `T{Column}` вместо `TR{Model}`, таблица вместо вида
`Reports/LedgerReportBuilder.cs:58,64`, `Reports/TurnoverReportBuilder.cs:40,50`, `Reports/ReportGrouping.cs:80,83`.
Карта фильтра — `select [!T{f.Column}!Map] … [Name!!Name] = [Name] from {f.SqlTableName}`; группировка — `isnull(r.[Name], …)` по `left join {c.SqlTableName}`. Платформенный резолв (`RefMapBuilder.GenerateResolves`, `SqlBuilder.RefFields`) берёт `target.Presentation`, тип `RefTypeName`, источник `RefSourceName`. У документа колонки `Name` нет вовсе (`Meta/TableDefaultColumns.cs:47-60`), а `ReportGrouping.TypedReportItems` (`:121-139`) пускает в `filters`/`groups` любую колонку поверхности, включая `Document` журнала. Валидатор строит только страницу отчёта, не SQL (`Generator/EndpointValidator.cs:262-268`).
Принцип: «Shown and chosen: presentation и displayAs» (CLAUDE.md) — ссылку показывает `Presentation` цели, где бы она ни показывалась; один резолв на платформу.
Последствие: фильтр или группировка оборотного отчёта по `Document` — «Invalid column name 'Name'» на Run, после зелёного `meta validate`; у справочника с `presentation` не `Name` и у счёта (`DisplayName`) отчёт показывает не то, что показывают индекс и селектор; цвет ссылки в отчёте не приезжает.
Серьёзность: средняя.

### Ф17. Новые пары дублирования (сверх записанных в REVIEW §2)
- **Merge строк, два шаблона одной формы.** `Database/SqlBuilderPlain.cs:450-465` (`mergeOneDetails`) и `:477-502` (`mergeMultiDetails`): одинаковые `on`, `update set`, `insert`, отличаются источником (TVP или `union all` по видам) и колонкой вида. Загрузка ту же развилку уже свела к одному коду со списком проходов (`:256-267`, комментарий «differ in the list, not in the emitting code»), сохранение — нет.
- **Списки «коллекция или её виды».** `SqlBuilderPlain.cs:414` и `:734-736` пишут `Kinds.Count > 0 ? Kinds.Keys.Select(KindCollectionName) : [key]` руками, хотя для этого заведён `TableMetadata.RowSets()` (`Meta/TableMetadata.cs:642-645`, «written once instead of branching on Kinds.Count everywhere»).
- **Кандидаты тегов.** SQL «tags — for filter» в `SqlBuilderIndex.cs:366-370` и в `SqlBuilderPlain.cs:122-125` — один текст дважды (комментарий `:109-111` сам говорит «the same pair the index emits»).
- **`tagsSaved` в шаблонах.** `Script/IndexTemplate.cs:121-135` и `Script/EditTemplate.cs:149-158` — две версии одной JS-функции; в шаблоне документа (`EditTemplate.cs:219-234`) её нет совсем, хотя трейт `Tags` от вида не зависит.
- **Подвалы диалогов.** «Select/Cancel» собран трижды (`Form/IndexPageXaml.cs:362-382`, `:456-478`, `Xaml/XamlOperationsBuilder.cs:23-64`), «SaveAndClose/Cancel» трижды (`Form/EditDialogXaml.cs:28-39`, `Form/IndexPageXaml.cs:318-329`, `Xaml/XamlTagsBuilder.cs:71-81`) при существующем `FormButtons.SaveAndClose` (`Form/FormButtons.cs:25-30`); `Close` написан тремя спеллингами (`new BindCmd(nameof(CommandType.Close))`, `new BindCmd(CommandType.Close)`, `new BindCmd() { Command = CommandType.Close }`), и копии уже разошлись: у тегов `ValidRequired = true`, у карточки и папки — нет.
- **Параметры батча.** `AddDefaultParameters` (`Database/SqlBuilder.cs:30-36`) против ручного `AddBigInt("@UserId", …)` в `SqlBuilderFetch.cs:41,176`, `SqlBuilderUnique.cs:41`, `SqlBuilderPost.cs:107`, `Reports/LedgerReportBuilder.cs:46`, `Reports/TurnoverReportBuilder.cs:27`, `Database/SqlBuilderTags.cs:93` — `@TenantId` получает только часть батчей.
Принцип: один закон — одна реализация; «Don't touch working code» не мешает — это копии, которые уже начали расходиться (`ValidRequired`, `tagsSaved` у документа, Ф9).
Последствие: каждая правка «как сливать строки», «что делать после правки тегов», «как закрывается диалог» требует найти все копии; пропущенная копия расходится молча.
Серьёзность: низкая.

### Ф18. `SqlBuilder` — одна partial-сущность на 2 438 строк, где текст, привязка и выполнение неразделимы
`Database/SqlBuilder.cs` + `SqlBuilderBirth/DbRemove/Fetch/Index/Plain/Post/Trans/Tree/Unique.cs` — один класс, 10 файлов, 2 438 строк, 34 `public/internal` метода; поля-сервисы берутся из `IServiceProvider` в инициализаторах (`SqlBuilder.cs:20-22`).
Почти каждый вход (`LoadIndexModelAsync`, `SavePlainModelAsync`, `PostDocumentAsync`, `DbRemoveAsync`, `FetchAsync`, `LoadTransModelAsync`, все из `SqlBuilderTree.cs`) строит текст локальной функцией и тут же выполняет его через `_dbContext.LoadModelSqlAsync` с лямбдой привязки — текст наружу не выходит. Отдельно получить текст можно только у загрузки карточки и рождения (`BuildLoadPlainSqlText`, `BuildBirthSqlTextAsync`), чем и пользуются тесты (`Tests/TestsMetadata/BasedOnTests.cs`, `GateTests.cs`). Рядом автор сделал ровно противоположное для печати: `PrintSqlBuilder` (`SqlBuilderPrint.cs:27-31`) — «Nothing here holds an IDbContext… it turns metadata into text and that is all, so it is assertable without DI». В `SqlBuilderOperations`/`SqlBuilderTags` та же смесь.
Принцип: «Loop first» / «Correctness is behaviour (test green, expected JSON)» — у сохранения, индекса, проводки и гашения нет шва, на котором можно проверить сгенерированный текст; смешение уровней (генерация vs исполнение).
Последствие: дефекты вида Ф7, Ф8 и REVIEW 1.8 (небрэкетированная колонка в индексе) не ловятся без живой базы; признанный пробел «save без теста» (REVIEW §5) — структурный, а не временный.
Серьёзность: средняя.

### Ф19. `grant … on schema::X to public` в деплое: шире платформенной конвенции и при этом без `delete`
`Database/SqlDbGenerator.cs:407-408` — для каждой схемы данных `grant select, insert, update, execute on schema::{s} to public`. Платформенный скрипт даёт `public` только `execute` на свои схемы (`Web/MainApp/@sql/a2v10_platform.sql:27-29`, `a2v10_metadata.sql:18`). При этом сгенерированный SQL удаляет строки: распроведение (`PostStatements.cs:60-61`, `delete from jrn…`), merge строк (`SqlBuilderPlain.cs:463,500`, `… then delete`), merge тегов (`:550`, `SqlBuilderTags.cs:82`), гранты (`SqlDbGenerator.cs:321`) — права `delete` грант не даёт.
Принцип: «don't build» / решение, оставленное без владельца: строка либо нужна (тогда неполна), либо нет (тогда только расширяет доступ всем принципалам базы до записи в каждую таблицу приложения). В CLAUDE.md решения о правах подключения нет.
Последствие: приложение под логином без `db_owner` падает на первом распроведении/сохранении документа со строками; под `db_owner` строка ничего не даёт, кроме права записи для любого пользователя базы.
Серьёзность: низкая.

### Ф20. Временная таблица оборотного отчёта типизирована по `ColumnType` без фасетов колонки
`Reports/BaseReportBuilder.cs:40-48` (`CreateField`) — `item.DataType.ToSqlDataType()`; `ReportItemMetadata` (`Reports/ReportGrouping.cs:21-31`) несёт только `ColumnType`, без `Length/Precision/Scale` колонки. Используется для `#tmpturn` (`Reports/TurnoverReportBuilder.cs:56-74`).
Для доменов с фиксированными фасетами (Amount, Qty, Price) это совпадает с колонкой; для `Decimal` с авторскими `precision/scale` (`SqlExtensions.cs:188`) — нет: временная колонка получает `decimal(19,4)`. Комментарий `// TODO : PlatformId DataType` (`:42`) устарел — platformid уже обработан строкой ниже. Правильный ответ уже есть: `TableColumn.SqlDataType()` (`SqlExtensions.cs:261-262`) — «THE single dispatch… everything below is a one-liner over it».
Принцип: один источник SQL-типа колонки — колонка, а не её `ColumnType`.
Последствие: мера `Decimal` с `scale` > 4 тихо округляется в отчёте до 4 знаков (обороты не сходятся с журналом); с `precision` > 19 — «Arithmetic overflow» на Run. `meta validate` SQL отчёта не строит.
Серьёзность: низкая.

### Ф21. Методы длиннее 100 строк
Подсчёт от сигнатуры до закрывающей скобки, проверено чтением границ. Логика с изменяемым состоянием (риск): `SqlBuilderIndex.LoadIndexModelAsync` — 466 (`Database/SqlBuilderIndex.cs:20-485`, см. Ф6); `SqlBuilderPlain.BuildLoadPlainSqlText` — 269 (`Database/SqlBuilderPlain.cs:87-355`, четыре вложенные локальные функции генерации defaults внутри генерации загрузки); `SqlBuilderPlain.SavePlainModelAsync` — 247 (`:515-761`, автономер, merge, теги, сборка DataTable и выполнение в одном методе); `PostStatements.InsertIntoLedger` — 154 (`Database/PostStatements.cs:333-486`); `PostStatements.CreateMapping` — 111 (`:182-292`). Декларативные деревья (низкий риск, длина от инициализаторов): `ControlsXaml.Editor` — 193 (`Form/ControlsXaml.cs:485-677`), `CreateFilterControl` — 120 (`:255-374`), `IndexPageXaml.IndexColumnsXaml` — 148 (`Form/IndexPageXaml.cs:19-166`), `IndexTemplate.CreateIndexTemplate` — 166 (`Script/IndexTemplate.cs:20-185`), `EditTemplate.CreateDocumentTemplate` — 100 (`Script/EditTemplate.cs:191-290`), страницы отчётов `TurnoverReportBuilder.CreatePage` — ~159 (`Reports/TurnoverReportBuilder.cs:119-…`), `ChessboardReportBuilder.CreatePage` — 122 (`Reports/ChessboardReportBuilder.cs:70-191`), `TrialBalanceReportBuilder.CreatePage` — 108 (`Reports/TrialBalanceReportBuilder.cs:105-212`), `BaseReportBuilder.CreateTaskpad` — 105 (`Reports/BaseReportBuilder.cs:144-248`), `SqlDbGenerator.GenerateMetadataSeedAsync` — 108 (в основном raw-литерал).
Принцип: размер/связность — в первой группе один метод держит несколько решений, связанных через захваченные локали.
Последствие: см. Ф6, Ф8, Ф18 — именно эти три метода не имеют шва для проверки текста.
Серьёзность: низкая.

### Ф22. Мелкое, с адресом
- Сиротский комментарий: блок про роли (`Database/SqlDbGenerator.cs:205-211`) стоит над `CreateBoundaryScript` вплотную к его собственному блоку (`:212-216`), а сам `CreateRolesScript` (`:239`) без комментария.
- `CliDatabaseCreator` — экземпляр без состояния (`Cli/CliDatabaseCreator.cs:10`, `SqlDbGenerator.cs:55`) ради одного нестатического `CreateTable` (`:15`), остальные пять методов статические; закомментированный `nocheck` (`:121`, `:136`).
- `ReportMetadata` (`Meta/ReportMetadata.cs:11-14`, `:30`) описывает «старую форму `reportItems` с G/F/D» и «turnover — единственный тип», хотя запись уже три списка, а типов три (`turnover`, `trialBalance`, `chessboard` — `Reports/BaseReportBuilder.cs:70-77`); `ReportItemKind.G/F/D` (REVIEW §3) — остаток той формы.
- `// TODO : PlatformId DataType` (`Reports/BaseReportBuilder.cs:42`) уже сделан строкой ниже; `// Console.WriteLine(sqlQuery)` (`Database/SqlBuilderIndex.cs:434`).
- `BasedOnMapping.SourceOf/CheckDocument` (`Database/BasedOnMapping.cs:34,53`) берут равенство доменов у `PostStatements.DomainMatch` — предикат колонки живёт в классе проводки, рождение на основании зависит от проводки.
- В `SetDbHash` литерал `'dbhash'` без `N` (`SqlScripts/a2v10_metadata.sql:109`) при `N'dbhash'` в `GetDbHash` (`:96`) и `N'version'` везде.
Серьёзность: низкая.

## Статус открытых записей REVIEW-2026-09-26 в области генераторов

| Запись | Статус | Где сейчас |
|---|---|---|
| 1.1 `Show` без action | **открыто** | кнопка `Form/CommandXaml.cs:72` (бар каталога), url `:194`; модель грузится `Builders/BaseModelBuilder.cs:69`; арма `show` нет в `CreateTemplateAsync` (`:77-95`) и `CreateXamlContainer` (`Form/IndexPageXaml.cs:389-406`). Причина класса — Ф12. |
| 1.2 шаблон карточки по виду | **открыто** | generic `Script/EditTemplate.cs:94-189` (unique есть; `total`, обработчики `inherit` нет), документ `:191-290` (total/inherit есть; unique нет, `tagsSaved` нет). `.d.ts` обещает total на массиве для любого вида (`Ts/EditTSMap.cs:40-44,62-64`). |
| 1.3 FK `Document` журнала на одну таблицу | **открыто** | `Cli/CliDatabaseCreator.cs:177-178` — FK на `RefTableCheck.Storage` для любой `IsRef`, `Document` не выделен; дискриминатор `PostStatements.cs:109-123`. |
| 1.7 `unique` без индекса | **половина открыта** | `@Id` типизирован (`SqlBuilderUnique.cs:42`) — закрыто; индекса по-прежнему нет: `TableMetadata.Indexes` заполняется только в коде (`Meta/TableMetadataDefaults.cs:55`, счётчики автономеров), `Unique` читает только `EditTemplate.cs:115-141`. |
| 1.14 Browse плана счетов | **исправлено в коде, в REVIEW не зачёркнуто** | `BrowseToolbar` диспетчеризует по виду, AccPlan → `TreeToolbar` (только Edit + Reload, `CommandScope.Tree`): `Form/CommandXaml.cs:83-92`. |
| 1.18 Attachments | **открыто** | кнопка `CommandXaml.cs:131-135`, без команды `:202`. |
| 1.18 Hierarchy (`Parent` в карточке) | **открыто** | `IsEditColumn` оставляет `Parent` (`Meta/MetadataExtensions.cs:20-21`), в `ControlsXaml.Editor` арма `Parent` нет → `_ => TextBox` (`Form/ControlsXaml.cs:669-675`). |
| 1.18 `Copy` | **открыто** | `CommandXaml.cs:203` — кнопка без команды. |
| 1.20 Map fetch без `RefFields` | **частично** | цвет и колонка выбора добавлены (`SqlBuilderFetch.cs:91`), `Role`/`Done`/`Url`/`Icon` нет, источник — таблица, не вид; копия резолва — Ф9. |
| 1.20 `PrintTitle` принимает ссылку последним сегментом | **частично** | `Meta/PrintTitle.cs:84-89` по-прежнему не отказывает; отказ теперь приходит из `PrintSqlBuilder.Fields` (`Database/SqlBuilderPrint.cs:292-294`) при загрузке страницы печати — с текстом «print model», не «print title», и не в `meta validate` (валидатор строит SQL бланка, не заголовка). |
| 1.20 `isReload` в Operation/Tag builder | **открыто** (вне области, проверено мимоходом) | `Builders/OperationEndpointBuilder.cs:41`, `Builders/TagEndpointBuilder.cs:25` — параметр не читается. |
| 1.20 `DatabaseMetadataProvider:1302`, `FormMetadata.Bake` | вне области (Meta) | — |
| §2 тип → контрол дважды | **исправлено в коде, не отмечено** | один `Editor` для карточки и строки через `Reach` (`Form/ControlsXaml.cs:478-677`, комментарий `:478-484`). |
| §2 «мера» в семи местах | **в основном исправлено, не отмечено** | `IsNumber/IsCurrency` (`Xaml/XamlExtensions.cs:26-37`) читают `ToXamlDataType/Align/ColumnRole/BindSheetCell` и `DefaultFormBuilder.cs:134`; `Integer` теперь число в XAML. Остались: `BindSheetCell` ставит `Date` вправо (`:86`) против центра в `ToXamlAlign` (`:57`); `BigInt` не число в XAML, но `number` в TS (`Js/JsExtensions.cs:24`). |
| §2 литералы `rv`/`Void`/`Done` | **открыто** | `rv`: `DataTableBuilder.cs:113`, `SqlBuilderPlain.cs:524`; `Void`/`Done`: `SqlBuilderIndex.cs:134`, `SqlBuilderFetch.cs:35,144,161`, `SqlBuilderOperations.cs:36,58`, `SqlBuilderDbRemove.cs:55-66`, `SqlBuilderPlain.cs:342`. |
| §2 отчёты (ledger/turnover) | **частично** | параметры фильтров — общий `AddFilterParameters` (`BaseReportBuilder.cs:54-64`); остались копии привязки (`LedgerReportBuilder.cs:44-53` / `TurnoverReportBuilder.cs:25-34`), карт фильтров (`:58-67` / `:39-54`, `;` только у Ledger `:66`), периода (`:74-77` / `:76-79`), эха `[Filter!TFilter!Object]` (`:81-82` / `:107-109`); `ReportGrouping.cs:96-98` (`j.[InOut]`, `InOut`, `{alias}.Date`) — без изменений. |
| §2 подписи фильтров по целевой `Model` | **открыто** | `Form/ControlsXaml.cs:272,298,313,316` — `RefTableCheck.Storage.RecordLabel()`. |
| §2 локальные предикаты | **открыто** | `SqlBuilderTree.cs:85-86`; `EditTemplate.cs:121` против `:126`; `DefaultFormBuilder.cs:155` против `:161`. |
| §3 `RefMapBuilder.IsEmpty`, `GenerateInserts` nullable | **открыто** | `RefMapBuilder.cs:60` (вызовы `:280`, `:319`), `:158`/`:288`; `DataModelExtensions.cs:19`. |
| §3 `@Id` в индексе без SQL | **открыто** | `SqlBuilderIndex.cs:441-442`. |
| §3 `allColumns`/закомментированный `refs` | **открыто** | `SqlBuilderPlain.cs:89-90`. |
| §3 `!IsTags && !IsTagEntries` | **открыто** | `SqlDbGenerator.cs:673-674` (`HasTableType`). |
| §3 `PrintReportHandler` `prms` | **открыто** | `Print/PrintReportHandler.cs:26-27`. |
| §3 `_report?.` и `"@Report"` | **открыто** | `BaseReportBuilder.cs:121`, `TurnoverReportBuilder.cs:227`, `TrialBalanceReportBuilder.cs:122`, `ChessboardReportBuilder.cs:76`. |
| §3 `ReportGrouping` `Order`/`Label`, `G/F/D` | **открыто** | `Reports/ReportGrouping.cs:29-30`, `:14-16`; `Label` читается только `XamlExtensions.cs:23` (всегда null). |
| §3 `CodeLoader` `?? throw` | **открыто** | `Xaml/CodeLoader.cs:24-25`. |
| §3 `FormExtensions.cs` пустой | **исправлено** (файла нет) | — |

## Общая оценка

Генераторы в целом дисциплинированнее типичного кода, который клеит SQL строками: идентификаторы почти везде в `[]` и проходят whitelist `CheckNames`, значения из браузера идут параметрами, у SQL-типа одна точка ответа (`ToSqlDbTypeInfo`), из неё выводятся CLR- и TS-типы, деплой детерминирован и сам себя проверяет по версии. Многие записи REVIEW-2026-09-26 действительно закрыты, причём часть (тип → контрол, «мера», Browse плана счетов) исправлена в коде, но в REVIEW не отмечена. Самые серьёзные находки этого прохода — не вкус, а дефекты: SQL-инъекция через `?Group=` оборотного отчёта (Ф14) — единственное место слоя, где строка запроса попадает в текст SQL, и именно оно не ловится ничем; сохранение записи без транзакции (Ф8), из-за чего ошибка на строках оставляет новую шапку со старыми строками, а решение об автономерах обсуждает только пропуски номеров и эту цену не учитывает. Структурный источник большей части остального — то, что у сгенерированного текста почти нет шва для проверки: `SqlBuilder` на 2,4 тыс. строк строит текст и тут же исполняет его (Ф18), а `meta validate` не строит SQL отчётов и сохранения (Ф14–Ф16, Ф20). Отсюда два повторяющихся класса: одно решение размазано по нескольким switch-ам — действие (Ф12, это и есть причина REVIEW 1.1), «ссылка ли это» и тип её ключа (Ф13); и параллельные копии одного закона, которые уже разошлись — резолв ссылок трижды (Ф9, Ф16), диалоги и шаблоны (Ф17), две конвенции ошибок (Ф3). Направление, которое автор уже выбрал для печати (`PrintSqlBuilder`: текст без DI, проверяется без базы), стоит распространить на сохранение, индекс и отчёты: это дешевле, чем чинить каждый класс дефектов по отдельности, и прямо продолжает принцип «loop first».
