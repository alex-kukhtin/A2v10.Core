# 01 — Швы A2v10.Metadata с остальными пакетами платформы

Только чтение. Номера строк на 2026-10-10. Не повторяет REVIEW-2026-09-26.md, ISSUES.md и уже записанное
другими проходами (ILicenseManager/порядок регистрации, ранний return в DataService.InvokeAsync,
опечатки Infrastructure, service locator в IModelCommand/IModelReport, NullAppRuntimeBuilder-совет).

## Находки

### 1. IAppRuntimeBuilder несёт два поколения: «auto» без единой живой реализации
- `Platform/A2v10.Infrastructure/IAppRuntimeBuilder.cs:18,29` — `IsAutoSupported`, `ExecuteCommandAsync`;
  `Platform/A2v10.Metadata/AppMetadataBuilder.cs:22,68-72` — `false` и `NotSupportedException`;
  `Platform/A2v10.Infrastructure/Impl/NullAppRuntimeBuilder.cs:13,44-47` — `false` и throw.
  Единственная реализация с `IsAutoSupported => true` лежит в `Obsolete/A2v10.AppRuntimeBuilder/AppRuntimeBuilder.cs:17`,
  которая не входит в `A2v10.Core.sln`.
- Ветка-потребитель жива в Services: `ModelJsonReader.cs:30-61,102-103` (`CreateAuto`), `Commands/InvokeCommandAuto.cs:14-17`,
  а `IModelBase.HasMetadata` (`IModelJsonReader.cs:72`) = `Auto != null || IsMeta` — то есть model.json с ключом `auto`
  в metadata-приложении маршрутизируется в `AppMetadataBuilder`, который о `auto` ничего не знает.
- Принцип: ISP/LSP (у всех реальных реализаций два члена — «не поддерживаю»), мёртвый контракт в опубликованном пакете.
- Последствие: опубликованный интерфейс Infrastructure обещает режим, которого нет; `HasMetadata` смешивает два
  смысла («auto» и «$meta») под одним именем — `auto`-ключ молча уводит экран в метаданные.
- Серьёзность: средняя.

### 2. Параметры хоста не доходят до metadata: две сборки параметров с разными правилами
- Хост: `Platform.Web/Controllers/BaseController.cs:39-56` — `UserId`; `TenantId` только при `IsMultiTenant`;
  `CompanyId` при `IsMultiCompany`; плюс `parameters` из model.json через `view.CreateParameters`.
- Metadata: `Database/SqlBuilder.cs:30-36` `AddDefaultParameters` — `@TenantId` по `Identity.Tenant != null`,
  `@UserId` из `ICurrentUser`, `CompanyId` нет нигде в пакете (grep). Та же сборка ещё в девяти местах
  (`SqlBuilderUnique.cs:41`, `SqlBuilderFetch.cs:41,176`, `SqlBuilderPost.cs:107`, `SqlBuilderTags.cs:93`,
  `LedgerReportBuilder.cs:46`, `TurnoverReportBuilder.cs:27`, `Admin/AdminGate.cs:33`, `Mcp/CatalogFindTool.cs:152-153`).
- Контракт шва это подтверждает: `DataService.cs:172-175` зовёт `RenderAsync` без `setParams`/`loadPrms`;
  `DataService.cs:320-322` `LoadLazyAsync` без `propertyName` и параметров; в `SaveAsync` `savePrms` передаётся
  (`DataService.cs:391-393`), но `SqlBuilderPlain.cs:515` его не читает; `DbRemove` берёт из `execPrms` только `Id`
  (`SqlBuilderDbRemove.cs:74`).
- Принцип: DRY (одно понятие «контекст запроса» собирается дважды и по-разному), честность контракта (ISP: параметры
  в сигнатуре, которые реализация игнорирует).
- Обратная сторона той же развилки: `Platform/A2v10.Services/Api/EndpointDataService.cs:58,69,83,94,107` (API, CLI, сценарии)
  передаёт `prms => { }` — пустой `setParams`. Это работает только потому, что metadata его игнорирует; классический
  эндпоинт, вызванный через этот «общий» сервис, получит процедуру без `@UserId`/`@TenantId`. Сервис с общим именем в
  `A2v10.Services` фактически годен лишь для metadata.
- Последствие: на multi-company стенде metadata-экраны не видят `CompanyId`; `parameters` в model.json на `$meta`-действии
  молча игнорируются; правило «когда слать TenantId» расходится с хостом.
- Серьёзность: средняя.

### 3. Второй, строковый шов для печати: keyed-сервис `":Metadata.Report"` мимо IAppRuntimeBuilder
- `Platform/A2v10.Metadata/ServicesExtensions.cs:27` регистрирует `PrintReportHandler` под ключом-литералом;
  `Platform/A2v10.Services/ModelJson.cs:380-385` резолвит его тем же литералом (общей константы нет), проверяя
  `IsMeta` и `IsMetaSupported`. Все остальные входы страниц и данных из DataService идут через `IAppRuntimeBuilder`.
- Обход проявляется в поведении: `PrintReportHandler.cs:24-48` сам парсит путь, сам строит `BuilderDescriptor`
  (без `PlatformId` — `:40-46`, в отличие от остальных мест создания: `ModelBuilderFactory.cs:43-50`,
  `EndpointMaterializer.cs:53-58`, `EndpointValidator.cs:225-229`) и не вызывает `CheckDeployAsync`, который
  `AppMetadataBuilder.cs:41,49,57,64,76,82` делает на каждом входе.
- Принцип: один плагин — две точки расширения (cohesion шва), «магическая строка» как контракт между пакетами (coupling).
- Последствие: печать — единственный metadata-вход без проверки деплоя и с неполным дескриптором
  (`PlatformId` остаётся `null` под `default!`; сегодня print-путь его не читает, первое же чтение — NRE, компилятор не предупредит, хотя у `UseGrants` для этого стоит `required`); переименование ключа в одном пакете ломает
  печать без ошибки компиляции.
- Серьёзность: средняя.

### 4. `IAppRuntimeBuilder` — «толстый» интерфейс: ассеты layout, флаг меню и CRUD-конвейер в одном типе
- `Platform/A2v10.Infrastructure/IAppRuntimeBuilder.cs:16-33`: `MetadataScripts/MetadataStyles` (HTML-строки для
  layout), `UseGrantsAsync` (флаг app.json), шесть операций данных, плюс два флага поддержки.
- Потребители берут по одному куску: `Views/Shared/_Layout.tabbed.cshtml:7,34,47` — только HTML;
  `Platform.Web/Meta/JsonMenu.cs:59,70` и `ShellController.cs:32,350` — только `UseGrantsAsync`;
  `SpecialEndpoints/AuxMenuEndpointHandler.cs:30` — то же через `GetRequiredService`; `DataService` — CRUD.
- Принцип: ISP, SRP (рендер ресурсов страницы, политика меню и конвейер данных — три причины меняться).
- Последствие: меню и layout зависят от всего конвейера данных; любой новый вопрос «metadata-приложение ли это?»
  расширяет опубликованный интерфейс Infrastructure (что уже случилось с `UseGrantsAsync`).
- Серьёзность: средняя.

### 5. Metadata вставляет `<script>`/`<link>` на ассеты, которых не поставляет ни один пакет
- `Platform/A2v10.Metadata/AppMetadataBuilder.cs:26-37` — `/scripts/meta/a2v10spreadsheet.*js`, `/css/meta/a2v10spreadsheet.*css`.
- Файлы лежат только в `Web/A2v10.Core.Web.Site/wwwroot/scripts/meta/` и `wwwroot/css/meta/` (стенд); в
  `Platform/A2v10.Web.Assets/wwwroot` их нет, `A2v10.Metadata.targets` копирует только sql и localization.
- Плюс вставка сделана лишь в `_Layout.tabbed.cshtml:34,47`; `_Layout.cshtml` и `_Layout.singlepage.cshtml` её не содержат.
- Принцип: пакет обещает ресурс, которого не владеет (cohesion пакета), неявная зависимость от стенда.
- Последствие: приложение на пакетах получает 404 на ресурсы отчётов-таблиц (Sheet в `Reports/BaseReportBuilder.cs:391+`);
  в нетабличном layout metadata-отчёты без стилей/скриптов вовсе.
- Серьёзность: средняя (проверено только в пределах этого репозитория: другие поставщики ассетов не видны).

### 6. Деплой DDL и запись файла из веб-рантайма на первом запросе; расходится с решением «Seed»
- `DatabaseMetadataCache.cs:46` (`_metadataDirty = true` при старте) → `AppMetadataBuilder.cs:41` и др. →
  `DatabaseMetadataProvider.cs:33-40,53-61` (`AllElementsMetadata` всего приложения) →
  `SqlDbGenerator.cs:103-157` → `:179-187` `Directory.CreateDirectory` + `File.WriteAllTextAsync` в
  `_sqlscripts/deploydatabase.sql` главного модуля (путь от `IAppCodeProvider.GetMainModuleFullPath`, запись мимо него).
- Решение `Platform/A2v10.Metadata/CLAUDE.md:410` утверждает: «in release there is no `AllElementsMetadata` walk»;
  там же `:459` — «the deploy check loads every endpoint of the application on the first request». Код соответствует
  второму; ветвления по Release/Watch вокруг деплоя нет (`Environment.Watch` читается только для watcher'а, `DatabaseMetadataCache.cs:53`).
- Принцип: ambient authority (веб-процесс пишет в собственный content root и держит DDL-права; `IAppCodeProvider` —
  абстракция чтения, запись идёт напрямую в FS), расхождение решения и кода.
- Последствие: на read-only файловой системе (контейнер, модуль из сборки) первый запрос падает на записи файла;
  каждый старт — полный обход метаданных и DDL-права у пользователя пула; решение «Seed» опирается на неверную посылку.
- Серьёзность: средняя.

### 7. Композиция DI в `a2` CLI противоречит `UseAppMetadata`: двойные регистрации, captive-зависимости, scoped из корня
- `Tools/A2v10.Cli/Program.cs:91,99` регистрирует `DatabaseMetadataCache` и `DatabaseMetadataProvider` как Singleton,
  а `:108` `UseAppMetadata()` регистрирует их снова (`Platform/A2v10.Metadata/ServicesExtensions.cs:16,18` — Singleton
  и **Scoped**). Побеждает последняя (Scoped), первая остаётся мёртвой записью в коллекции.
- `Program.cs:92-93` — `DataService` и `ModelJsonReader` как Singleton, хотя оба требуют `IAppRuntimeBuilder`,
  зарегистрированный Scoped (`ServicesExtensions.cs:19`; `DataService.cs:51-54`, `ModelJsonReader.cs:7`) —
  классическая captive dependency. `ICurrentUser` — Singleton веб-класса `Platform.Web/Middleware/CurrentUser.cs`.
- Scoped-сервисы берутся из корневого провайдера без scope: `Meta/MaterializeCommand.cs:27`, `Meta/DeployCommand.cs:29-30`,
  `Meta/ValidateCommand.cs:19`, `Endpoint/ResolveEndpointCommand.cs:19`.
- `Host.CreateApplicationBuilder()` (`Program.cs:73`) включает `ValidateScopes`/`ValidateOnBuild` в окружении
  Development — тогда `host.Build()` (`:151`) упадёт на этих графах (и ещё на keyed `PrintReportHandler`, которому нужен
  `IReportEngineProvider`, не зарегистрированный в CLI); сейчас работает только потому, что окружение по
  умолчанию Production и процесс одноразовый.
- Попутно: CLI тянет `A2v10.Platform.Web` (ASP.NET MVC, `A2v10.Cli.csproj:43`) ради `CurrentUser`, `WebApplicationHost`,
  `VueDataScripter` (`Program.cs:96,100-101`).
- Принцип: согласованность lifetimes (captive dependency), DIP/coupling (инструмент командной строки зависит от
  веб-слоя), DRY композиции (CLI повторяет часть `UseAppMetadata` с другими временами жизни).
- Последствие: `DOTNET_ENVIRONMENT=Development` на машине разработчика ломает весь `a2`; поведение кэша и провайдера
  в CLI не то, что в хосте, а значит beam (#1) проверяет не ту композицию, в которой код работает на стенде.
- Серьёзность: средняя.

### 8. Внутри Metadata `IServiceProvider` проходит сквозь все построители — ambient authority против оси CLAUDE.md
- `Builders/ModelBuilderFactory.cs:11,30-43` раздаёт `_serviceProvider` каждому построителю; дальше он
  спускается в `BaseModelBuilder.cs:18-22,111-112`, `SqlBuilder.cs:18-22`, `SqlBuilderTags.cs:20-21`,
  `SqlBuilderOperations.cs:24`, `Admin/SqlBuilderUsers.cs:20`, `Admin/UserAdminBuilder.cs:24,36-37,104`,
  `Admin/AdminGate.cs:20-23`, `Reports/BaseReportBuilder.cs:21-26` (резолв на каждое обращение к свойству),
  `Xaml/CodeLoader.cs:14-17`, `Print/PrintReportHandler.cs:14,47`, `Generator/EndpointValidator.cs:37,264`.
  Тот же приём во ViewEngine: `ViewEngines/A2v10.ViewEngine.Xaml/DynamicRenderer/DynamicRenderer.cs:12-16`.
- Комментарий `AppMetadataBuilder.cs:12-16` фиксирует, что провайдер оттуда убран «потому что он был нужен только чтобы
  new-нуть три построителя» — но он просто переехал на уровень ниже, в фабрику и во все SQL-построители.
- Принцип: DIP/явные зависимости; корневой `CLAUDE.md` («The axis: bounded vs ambient») — сигнатура `IServiceProvider`
  разрешает дотянуться до чего угодно, бокс перестаёт быть «typed in/out, zero ambient authority».
- Последствие: реальные зависимости построителя видны только чтением тела; отсутствие регистрации обнаруживается
  в момент клика, а не при сборке хоста: `UserAdminBuilder.cs:104` резолвит `IAppUserAdmin` только внутри команды,
  а регистрирует его лишь `Identity/A2v10.Web.Identity.UI/ServicesExtensions.cs:21` — в `A2v10.ApiHost/Startup.cs:26`
  и в CLI `UseAppMetadata` есть, `IAppUserAdmin` нет. Тесты построителя требуют собирать весь контейнер.
- Серьёзность: средняя.

### 9. «Кто админ» записано трижды: C#-текст в Metadata и две процедуры платформенного скрипта
- `Platform/A2v10.Metadata/Admin/AdminGate.cs:24-33` — свой inline-SQL `exists(... a2security.UserRoles ... [Role] = N'Admin')`.
- `Platform/SqlScripts/a2v10_security_simple.sql:305-306` (`User.Grants.Load`, читает меню `JsonMenu.cs:71-73`) и `:346`
  (`Permission.Check`, гейт батчей) — тот же предикат.
- Metadata при этом пишет сырой SQL против схемы `a2security`, которой владеет платформенный скрипт, ещё в
  `SqlBuilderUsers.cs` (9 мест), `SqlDbGenerator.cs` (9), `Gate.cs`, `AppRoles.cs`, `EndpointGrants.cs`.
- Принцип: DRY (одно правило — три записи в двух пакетах), coupling к чужой схеме по тексту.
- Последствие: смена правила «админ» (например, учёт `Void` роли или пользователя 99, CLAUDE.md «Roles») должна
  пройти три места синхронно; разойдутся — админ-экраны, меню и гейт по-разному ответят одному пользователю.
  Правило живёт в платформенном скрипте; Metadata могла бы спросить его процедурой, как спрашивает гейт (`Permission.Check`), а не повторять текст.
- Серьёзность: низкая.

### 10. Решение «metadata или процедуры» размазано по каждому методу DataService; пропущенные входы уходят в `[$meta.*]`
- Ветвление `view.HasMetadata`/`cmd.HasMetadata` повторено шесть раз: `Platform/A2v10.Services/DataService.cs:172,269,290,320,391,438`;
  ещё два места решают то же по-своему: `ModelJson.cs:380-385` (печать, по `IsMeta` + `IsMetaSupported`) и
  `ModelJsonReader.cs:102-105` (синтез `$meta` для папки без model.json).
- Не ветвятся: `ExportAsync` (`DataService.cs:79-86`) и все blob-входы (`DataService.Blobs.cs:34-56` и далее). При этом
  `HasModel()` для `$meta` истинно (`ModelJson.cs:51`), а `ModelJson.IsMeta` синтезирует любое неописанное действие
  (`ModelJson.cs:445-512`) — такие входы молча строят имена процедур `[dbo].[$meta.Load.Export]`, `[dbo].[$meta.Load]`
  и падают ошибкой SQL «процедура не найдена», а не отказом «не поддерживается metadata».
- Два одноимённых `IsMeta` с разным смыслом: `IModelBase.IsMeta` — по `CurrentModel` с наследованием
  (`Infrastructure/IModelJsonReader.cs:71`), `ModelJson.IsMeta` (private) — только по корню (`ModelJson.cs:445`).
- Принцип: OCP (каждый новый вход DataService обязан помнить про ветку), SRP DataService (диспетчер и
  классический исполнитель в одном классе), Tell-Don't-Ask.
- Последствие: поведение `$meta` на «непредусмотренном» входе — случайная SQL-ошибка; добавление входа в DataService
  без ветки — тихая дыра в маршрутизации. Одна точка выбора исполнителя (стратегия «classic | metadata» по `IModelBase`)
  убрала бы все восемь мест.
- Серьёзность: средняя.

### 11. Проверки доступа на шве: три механизма, покрытие по входам разное
- Механизмы: `roles`/`permissions` model.json в DataService (`CheckRoles` через `LoadViewAsync`, `DataService.cs:135-146`;
  `CheckPermissions` `:148-156`), состояние read-only пользователя (`_currentUser.State.IsReadOnly`, `:377,460-469`),
  и гейт `grants` внутри SQL-батчей metadata (`Database/Gate.cs`, `SqlBuilder.cs:39-42`).
- Покрытие (оба пути, кроме отмеченного):
  `Load` — роли + permissions (`:168-170`); `Expand` и `LoadLazy` — только роли (`:262`, `:318`); `DbRemove` — только роли,
  ни permissions, ни read-only (`:280-294`) — пользователь в read-only может удалять и на классике, и на metadata;
  `Save` — всё (`:375-386`); `ExportAsync` — ничего: берёт view через `_modelReader.GetViewAsync` напрямую, мимо
  `LoadViewAsync` (`:82`). Metadata-слой read-only не проверяет нигде (grep `IsReadOnly` в пакете — только MCP-проекция). Больше того, metadata-ветка
  `Load` возвращается на `DataService.cs:172-176`, до `SetReadOnly(model)` (`:241`) — пользователю в read-only состоянии
  metadata-карточка приходит редактируемой (классическая — помеченной read-only); отказ он получит только на Save.
  (Ранний return в `InvokeAsync` — уже записан другим проходом, сюда не входит.)
- Принцип: DRY/единая точка политики; Defense-in-depth без согласованной матрицы.
- Последствие: на metadata-эндпоинте без `useGrants` удаление защищено только ролями model.json, а read-only
  состояние не защищает ни удаление, ни проводку; на классике экспорт не проверяет роли вовсе.
- Серьёзность: высокая (DbRemove/Export — достижимые запросы из UI).

### 12. Сохранение metadata обходит писатель A2v10.Data; своя конвертация расходится с `ConvertTo` ядра
- Metadata строит TVP сам: `Database/DataTableBuilder.cs:62-121` и передаёт их через `LoadModelSqlAsync` +
  `AddStructured` (`SqlBuilderPlain.cs:720-754`). Классический путь — `IDbContext.SaveModelAsync` → `DataModelWriter`
  → `SqlExtensions.ConvertTo` ядра (`a2v10.data.core/A2v10.Data/DataModelWriter.cs:98,107`, `SqlExtensions.cs:99-131`).
- Расхождения, видимые чтением: ядро по умолчанию (`AllowEmptyStrings = false`, `a2v10.data.core/A2v10.Data/SqlDbContext.cs:471`) превращает `""` в NULL
  и парсит строку в целевой тип (`SqlExtensions.cs:121-127`); `DataTableBuilder.AddRow` (`:94-117`) обнуляет пустую строку
  только для `Guid`, остальное кладёт как есть — `""` в строковую колонку ложится пустой строкой, в колонку даты/числа
  уходит в `DataRow`, который бросает на конвертации. Настройка `DataConfigurationOptions.AllowEmptyStrings` хоста на metadata
  не действует.
- Решение `CLAUDE.md` «NOT NULL: the domain's zero» (`:121-125`): у строки «ничто — это отсутствие, NULL», и клиент шлёт `''`.
- Принцип: DRY (второй писатель ExpandoObject → SQL), согласованность с ядром («same IDataModel pipe» из корневого CLAUDE.md
  верно для чтения, но не для записи).
- Последствие: одна и та же форма, сохранённая классикой и metadata, даёт в БД NULL и `''`; `required`/`unique` по такой колонке
  ведут себя по-разному; позиционное соответствие колонок TVP (`DataTableBuilder.cs:12-15`) держится только комментарием.
- Серьёзность: средняя.

### 13. Публичная поверхность пакета расширена нуждами CLI и тестов; типы размещены по графу
- `A2v10.Metadata.csproj:56-58` открывает internals только `TestsMetadata`; CLI (`Tools/A2v10.Cli`) так не может, поэтому
  публичны конкретные сервисы без абстракций — `DatabaseMetadataProvider`, `DatabaseMetadataCache`, `SqlDbGenerator`,
  `EndpointValidator`, `EndpointMaterializer`, `TestEnvironment` — и вся модель `TableMetadata`/`TableColumn`/…
  (≈80 публичных типов в 30 файлах).
- `Platform/A2v10.Metadata/Cli/CliDatabaseCreator.cs:10` — публичный, в пространстве `A2v10.Metadata.Cli`, но это
  генератор DDL рантайм-деплоя (`Database/SqlDbGenerator.cs:55,254,303`): имя говорит о потребителе, а не о владельце.
- `Print/NullReportEngineProvider.cs:9` — заглушка `IReportEngineProvider` без отношения к метаданным, единственный
  пользователь `A2v10.ApiHost/Startup.cs:29`.
- `Platform/A2v10.Platform.Web/A2v10.Platform.Web.csproj:88` — веб-пакет открывает internals тестам метаданных
  (ради `JsonMenuRoot.Transform`).
- Принцип: ISP/инкапсуляция пакета, размещение «по графу, а не по владельцу» (корневой `CLAUDE.md`, абзац про Infrastructure —
  то же правило применимо к любому опубликованному пакету).
- Последствие: любое переименование внутри слоя — ломающее изменение NuGet-контракта; хост может инжектить
  `DatabaseMetadataProvider` напрямую и обойти `IAppRuntimeBuilder` (что и делает `MetadataMcpToolProvider`, `Mcp/MetadataMcpToolProvider.cs:19`).
- Серьёзность: низкая.

### 14. Правила платформы переписаны в Metadata вместо обращения к её абстракциям
- Поиск вида: `Xaml/CodeLoader.cs:30-50` хардкодит `[".vxaml", ".xaml"]` и сам зовёт `IXamlPartProvider`, тогда как платформа
  держит реестр движков `IViewEngineProvider` (`Infrastructure/IViewProvider.cs:13-17`, реализация
  `Platform.Web/WebViewEngineProvider.cs:44-70`, регистрация `.vxaml`/`.xaml` в `Platform/A2v10.Platform/ServicesExtensions.cs:91-92`);
  та же последовательность ещё в `ViewEngine.Xaml/Base/Components.cs:62-71`. Комментарий `CodeLoader.cs:30-32` прямо называет
  это копией «the same probe the platform makes».
- Путь шаблона: `CodeLoader.cs:23` — `{Template}.js`, то же правило в `Platform.Web/Builders/VueDataScripter.cs:547,574,631`.
- Грамматика URL: `Meta/MetadataExtensions.cs:83-88` сам собирает `_page`/`_dialog` + путь и создаёт конкретный
  `A2v10.Services.PlatformUrl`; так же `Print/PrintReportHandler.cs:44`. Конкретные типы Services ещё: `InvokeResult`,
  `JsonHelpers` (`Database/DataModelExtensions.cs:7,13-18`, `Admin/UserAdminBuilder.cs:14,122`). При этом в опубликованном
  контракте уже есть фабрика ровно для этого — `IPlatformUrl.CreateFromMetadata` (`Infrastructure/IPlatformUrl.cs:30`,
  реализация `Services/PlatformUrl.cs:118-121`) — и у неё ноль вызывающих во всём репозитории, включая `Obsolete/`.
- Принцип: DRY между пакетами, DIP (плагин зависит от реализаций хоста, а не от абстракций Infrastructure), OCP
  (новый движок вида, зарегистрированный в платформе, metadata-папке недоступен).
- Последствие: изменение правила в платформе (порядок расширений, новый движок, формат URL) не доходит до metadata без
  ручной синхронизации; `A2v10.Metadata` версионно привязан к реализации `A2v10.Services`, а не только к контракту.
- Серьёзность: низкая.

### 15. `IAppUserAdmin` в опубликованном Infrastructure: три из семи членов без вызывающих
- `Platform/A2v10.Infrastructure/IAppUserAdmin.cs:14-25`: `InviteAsync`, `InviteAgainAsync`, `ResetTwoFactorAsync` не вызываются
  нигде (grep по репозиторию без `Obsolete/`: только объявление и реализация `Identity/A2v10.Web.Identity.UI/AppUserAdmin.cs:30,38,51`).
  Единственный потребитель — `Admin/UserAdminBuilder.cs:101-123` (Create, SetPassword, SetBlocked, Delete).
- `CLAUDE.md` Metadata `:26`: «Invitation is out until a stand has mail» — решение «не строить» принято, а контракт его уже несёт.
- Интерфейс существует ради одной пары «Metadata ↔ Identity.UI» и положен в Infrastructure по графу (обе стороны не могут
  сослаться друг на друга), при этом владелец потребности — экран Metadata.
- Принцип: ISP/YAGNI («default don't build» корневого CLAUDE.md), правило размещения «по владельцу, не по графу».
- Последствие: каждое будущее уточнение приглашения (письмо, срок, роль) — ломающее изменение опубликованного пакета, от
  которого зависят все; сейчас эти члены — непроверяемая поверхность без потребителя.
- Серьёзность: низкая.

### 16. MCP читает model.json своим парсером и копией предиката ролей
- `Mcp/McpIndex.cs:81-88` — `JObject.Parse(...)["roles"]` в обход `IModelJsonReader`/`ModelJsonPartProvider`
  (`Services/Providers/ModelJsonPartProvider.cs:23`, настройки `CamelCaseSerializerSettings`).
- `Mcp/McpIndex.cs:22-24` — копия `ModelJson.CheckRoles` (`Services/ModelJson.cs:34-46`) с комментарием «ModelJson.CheckRoles».
  Решение `A2v10.Mcp/CLAUDE.md:101` и Metadata `CLAUDE.md:454`: «the same predicate … so a role means one thing in the UI and here» —
  в коде это не тот же предикат, а его копия.
- `permissions` model.json (их проверяет UI, `DataService.cs:148-156`) MCP не читает вовсе.
- Принцип: DRY, единый источник правды для «кто видит эндпоинт».
- Последствие: изменение семантики `roles` в платформе (наследование, `Admin`, регистр) молча разведёт UI и модель; эндпоинт,
  закрытый `permissions`, виден модели в `domain_info`/`entity_info`.
- Серьёзность: низкая.

### 17. Ветка CLR-хуков: единственная причина ссылки Metadata → App.Infrastructure, нигде не включена
- `A2v10.Metadata.csproj:33` ссылается на `A2v10.App.Infrastructure`; используется только в `AppMetadataClrProvider.cs:7-23`,
  `AppMetadataClrManager.cs:32-66` и `ServicesExtensions.cs:42-50` (`UseApplicationClr`).
- Включение закомментировано у единственного хоста: `Web/A2v10.Core.Web.Site/Startup.cs:134-139`. «Сгенерированный» вход
  `Web/MainApp/Generated/_provider.cs:9-21` написан руками («GENERATED CODE», генератора в репозитории нет; внутри
  `// ??? TODO: catalog/agent!!!! Singular`, ключ `catalog/agents` — не того вида, что адреса metadata).
- Механизм не metadata-специфичен (работает по `IPlatformUrl.LocalPath` для любого Save, вызывается из
  `Services/DataService.cs:388,394-395,408-409,421-422`), а его контракт разнесён по трём пакетам: `IAppClrManager` —
  `Platform/A2v10.Infrastructure/IAppClrManager.cs:9`, `IAppClrProvider`/`AppMetadataClrOptions` — App.Infrastructure,
  реализация — Metadata. `AppMetadataClrManager.cs:62` вызывает `elem.ToExpando()` ради побочного эффекта (`IClrElement.ToExpando`
  возвращает `void`), фабрика элемента получает `IServiceProvider` (`IAppClrProvider.cs:11-24`, `ElementBase.cs:7-27` —
  `Activator.CreateInstance` с провайдером).
- Принцип: «default don't build / defer dead code» корневого CLAUDE.md; cohesion (общий хук хоста живёт в плагине);
  ambient authority в контракте элемента; решение «ничего не генерится в .cs» (REVIEW §5) против папки `Generated`.
- Последствие: опубликованные контракты в двух пакетах и лишняя зависимость Metadata держатся ради выключенного пути;
  DataService несёт четыре проверки `_appClrManager != null` в Save.
- Серьёзность: низкая.

### 18. Флаг «деплой нужен» один на процесс, а всё остальное — по источнику данных
- `DatabaseMetadataCache.cs:46,91-109`: `_metadataDirty` — один `volatile Boolean`; `CheckDeployAsync(Func<Task>)` сбрасывает его
  после деплоя того источника, с которым пришёл первый запрос (`DatabaseMetadataProvider.cs:33-34`, источник —
  `view.DataSource`/`cmd.DataSource` из model.json, `AppMetadataBuilder.cs:41,49,57,64,76,82`; `ModelBuilderFactory.cs:22`).
- Рядом всё ключуется источником: эндпоинты (`:130-133`), referrers (`:152-155`), platformid (`:162-167`, с комментарием
  «one data source is one database», `:28-29`), XAML-формы (`:183`).
- Принцип: согласованность модели кэша (cohesion); скрытое допущение «один источник данных» в шве с `IDbContext`.
- Последствие: в приложении, где metadata-эндпоинты указывают `source` на разные базы, проверку хэша и DDL получает только
  база первого запроса после старта/изменения файла; остальные работают по старой схеме до следующего события watcher'а,
  без ошибки. Либо флаг по ключу источника, либо явный отказ от `source` на `$meta`.
- Серьёзность: средняя (если `source` на `$meta` не поддерживается — низкая, но тогда отказа нет нигде).

### 19. `IDbContext` используется как фабрика `SqlCommand` — даункаст там, где хватает `DbCommand`
- `DatabaseMetadataProvider.cs:67-73` (`HasMetaAsync`) и `Database/SqlDbGenerator.cs:866-868` (`DeployDatabaseAsync`) берут
  `GetDbConnectionAsync` и приводят `CreateCommand()` к `SqlCommand` с исключением «Invalid Database provider», хотя используют
  только `CommandText`, `ExecuteScalarAsync`, `ExecuteNonQueryAsync` — члены `DbCommand`. Ради этого в коде пакета появляется
  `using Microsoft.Data.SqlClient` (`SqlDbGenerator.cs:14`; транзитивно через `A2v10.Data.Core`, `A2v10.Metadata.csproj:39`).
- Принцип: LSP/DIP (абстракция ядра используется с проверкой конкретного типа).
- Последствие: мелкое; но любая обёртка соединения (профайлер, tenant-прокси) в `IDbContext` ломает деплой с вводящим
  в заблуждение текстом «Invalid Database provider».
- Серьёзность: низкая.

### 20. Свой `FileSystemWatcher` по главному модулю вместо конвенции платформы `EnumerateWatchedDirs`
- `DatabaseMetadataCache.cs:208-235`: watcher на `GetMainModuleFullPath(".", "")` с `IncludeSubdirectories`. Платформа для той
  же задачи спрашивает `IAppCodeProvider.EnumerateWatchedDirs` (`Infrastructure/IAppCodeProvider.cs:23`) и следит за всеми
  модулями: `ViewEngine.Xaml/XamlPartProvider.cs:30-40`, `Platform.Web/WebLocalizerDictiorany.cs:87,148`.
- Принцип: DRY/единая конвенция «что такое код приложения» (IAppCodeProvider знает модули, Metadata — нет); ambient File IO
  в обход абстракции, через которую платформа читает тот же код.
- Последствие: правка `metadata.json`/`app.json`/`mcp.json` в модуле (`$module/...`) не сбрасывает кэш и не помечает деплой;
  на это накладывается REVIEW 1.11 (модульный `metadata.json` роняет обход). Модуль как единица поставки для metadata-слоя
  не работает ни на чтении, ни на слежении — по одной и той же причине: слой не спрашивает `IAppCodeProvider` о модулях.
- Серьёзность: низкая.

## Общая оценка швов

Направление зависимостей в целом правильное: Metadata — плагин, ядро (`A2v10.Services`, `Platform.Web`) знает о нём только
через `IAppRuntimeBuilder`, строку `$meta` и один keyed-ключ; A2v10.Mcp о Metadata не знает вовсе, Infrastructure несёт
только BCL-контракты. Слабость не в графе, а в том, что шов «classic | metadata» проведён не одной точкой, а
развилкой в каждом методе DataService (#10), и контракт этого шва говорит не то, что делает: параметры, которые хост
собирает, metadata игнорирует и собирает заново по своим правилам (#2), печать идёт мимо шва вовсе (#3), проверки доступа
и read-only распределены по входам неравномерно (#11), а сам `IAppRuntimeBuilder` одновременно — поставщик HTML для layout,
флаг меню, CRUD-конвейер и останки режима `auto` (#1, #4). Внутри пакета ось bounded/ambient корневого CLAUDE.md
последовательно соблюдена для авторского кода (процедура `post`, `model.json` как люк), но не для собственной реализации:
`IServiceProvider` проходит сквозь все построители (#8), веб-рантайм пишет в свой content root и держит DDL-права (#6),
слежение и чтение кода идут мимо модульной конвенции `IAppCodeProvider` (#20). Второй систематический класс —
правила платформы, переписанные в Metadata вместо вызова её абстракций (вид, шаблон, URL, писатель TVP, предикат ролей,
«кто админ»: #9, #12, #14, #16): каждое по отдельности дешёвое, вместе они делают Metadata версионно привязанным к
реализации `A2v10.Services` и ядра данных, а не к контракту. Композиция в CLI (#7) — единственное место, где шов
реально ломается от окружения, и как раз там, где по замыслу стоит beam. Самое ценное к исправлению: одна точка
выбора исполнителя в DataService с одним контрактом параметров (снимает #2, #10, большую часть #11 и #3), lifetimes
CLI через `UseAppMetadata` без повторных регистраций (#7), и явное решение о рантайм-деплое против строки `CLAUDE.md:410` (#6).
