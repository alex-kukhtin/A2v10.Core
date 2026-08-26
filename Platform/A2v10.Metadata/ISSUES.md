# ISSUES — найдено при чтении кода

Рабочий список. Всё, что здесь, найдено чтением конкретных мест и проверено grep'ом —
догадок и «возможных рисков без адреса» нет. Номера строк на момент правки
`table`/`storage` (удаление `GetDefaultStorage`).

Отдельно от `todo.txt`: там фичи, здесь дефекты и долги.

---

## 1. Блокирующее прямо сейчас

### 1.1. Приложение не поднимается без явного `table` / `storage`

После удаления умолчаний каждый endpoint обязан объявить, где лежат данные.
В `sandbox/metadata-driven/` (репозиторий скила) **все 17** `metadata.json` не объявляют
ни `table`, ни `storage` — каждый падает на `CheckDataLocation`.

Чинится дописыванием ключа, но **не механически**: см. 2.1 — имя таблицы, которое
реально создано в развёрнутой базе, может отличаться от правильного множественного
числа. Сначала посмотреть, что в базе, потом писать в файл.

---

## 2. Баги

### 2.1. `Plural()` ломает слова на `y`

`StringExtensions.cs:19`

```csharp
if (src.EndsWith("y"))
    return src + "ies";     // Currency -> Currencyies
```

`y` не отбрасывается. `Currency → Currencyies`, `Company → Companyies`,
`Category → Categoryies`.

Живые потребители:
- `TableMetadata.cs:287` — `CollectionName => Model.Plural()`, имена коллекций
  в рантайм-JSON. Модель `Currency` даёт коллекцию `Currencyies`.
- `TableMetadata.cs:341` — `SetDetailDefaults`, имя таблицы строк (см. 3.1).

Из `SetDefaults` (имя таблицы endpoint-а) вызов убран — там теперь требуется явный `table`.

**Важно для миграции:** если в развёрнутой базе таблица создана этим кодом, её имя
кривое, и в `metadata.json` надо писать **существующее** имя, а не правильное.
Исправление функции без сверки с базой переименует таблицу.

### 2.2. `Singular()` не обратен `Plural()`

`StringExtensions.cs:9`

`"ses"` → отбрасывает `es`: `Houses → Hous`, `Cases → Cas`.
Пара не round-trip ни в одну сторону: `Singular(Plural("Currency"))` = `Currencyy`.

Где это важно — не проверял. Отмечаю как факт: полагаться на пару как на биекцию нельзя.

### 2.3. Мёртвый метод `LoadTableMetadataDbAsync`

`DatabaseMetadataProvider.cs:301` — ни одного вызова во всём проекте (проверено grep'ом
по `--include="*.cs"`).

Внутри него — `:318`, сообщение жёстко называет `a2meta.[Table.Schema]`, хотя процедура
выбирается `switch`'ем (`Report.Schema`, `Enum.Schema`, `Operation.Schema`). То есть при
падении на enum'е сообщение назвало бы не ту процедуру.

Метод либо удалить, либо вернуть в оборот — но тогда сообщение чинить.

### 2.4. Мёртвая проверка «файл пуст»

`DatabaseMetadataProvider.cs:147`

```csharp
var text = await sr.ReadToEndAsync()
    ?? throw new InvalidOperationException($"{fileName} is empty");
```

`ReadToEndAsync()` возвращает non-nullable `String` — `??` не срабатывает никогда.
Реально пустой файл даёт `""` и проходит дальше, где падает на десериализации
**без имени файла** (см. 4).

### 2.5. Недостижимая ветка в `ParsePath`

`DatabaseMetadataProvider.cs` (`ParsePath`)

```csharp
if (split.Length == 1) return (split[0], String.Empty);
if (split.Length < 2)  throw ...          // это уже только Length == 0
```

`String.Split` не возвращает пустой массив. Вторая проверка мертва.

---

## 3. Долги дизайна

### 3.1. Имя таблицы строк выводится тем же угадыванием

`TableMetadata.cs:341`, `SetDetailDefaults`:

```csharp
if (String.IsNullOrEmpty(Table))
    Table = Model.ToPascalCase().Plural();
```

Ровно та болезнь, которую вылечили этажом выше: имя таблицы получается вычислением,
которое надо повторить в голове везде, где это имя всплывает, и промах тихий.
У `details` в формате ключа `table` вообще нет — то есть даже перебить нечем.

Решение (объявлять явно / оставить как есть) не принято.

### 3.2. `EndpointKindOf` не знает `report` и `autonum`

`DatabaseMetadataProvider.cs` (`EndpointKindOf`) — оба уходят в `Undefined`.

~~Из-за этого `DeclaresDataLocation` вынужден дописывать report отдельным `||`.~~ Снято:
после того как `storage` стал ключом только для `document`, отчёт выпал из проверки
естественно — он не объявляет таблицу вообще, он называет поверхность (`source`, пока
пишется как `storage`). Хвост из частных случаев исчез сам.

Остаётся сам факт: kinds `report` и `autonum` в enum не заведены. Пока это не мешает —
но следующая проверка вида «а этот kind работает с таблицей?» может снова начать
обрастать частными случаями. Место в коде одно и помечено комментарием («this is the
single place that will learn the new kinds»).

### 3.3. `source` у отчёта пишется как `storage`

**Решено (2026-08-02): для отчёта это именно `source`.** Не хранилище, а поверхность, над
которой строится отчёт — своих данных у отчёта нет вообще. Разные оси, не два написания
одного.

**Строить нечего — не хватает одного ключа.** Разрешённая сторона уже есть и правильная:
`ReportEndpointMetadata.Surface` против `NormalEndpointMetadata.Storage`, с комментарием
`EndpointMetadata.cs:21` («'Storage' is where my data lives, 'Surface' is what I read»).
Нет объявленной стороны — строки-пути. У обычного endpoint'а пара полная
(`DeclarationMetadata.Storage` → `NormalEndpointMetadata.Storage`), у отчёта есть только
вторая половина, и `Surface` сегодня заполняется из `DeclarationMetadata.Storage`.

Перевесить ключ:

- `ReportMetadata.Source` (обязательный), рядом с `type` / `reportItems` —
  `skill/references/report.md:53`;
- ветку Report в `LoadEndpointAsync` поднять **до** общего резолва storage: сначала
  `ReportMetadata`, потом поверхность из `report.Source`;
- правило проверки для отчёта: `source` обязателен, `table` и `storage` запрещены.

Перестановка ветки — не побочная работа, а то, чем достигается результат: `LoadEndpointAsync`
сейчас резолвит storage до ветвления, и для отчёта это резолв по чужому ключу.
`ReportEndpointMetadata` не хранит `Declaration` вообще, так что это последнее место, где
отчёт читает ключ документа. После правки ветка отчёта перестаёт трогать
`DeclarationMetadata`, и тот становится тем, чем себя называет, — декларацией **данного**
endpoint'а.

Тогда же каждый ключ живёт ровно в одном kind-е: `storage` — только document, `source` —
только report, `table` — все остальные. И `CheckDataLocation` перестаёт пропускать отчёт
по случайности (сейчас он выпадает через `Undefined` — верно по факту, но не по правилу).

### 3.4. Значения перечислений: два написания в схеме и один расходящийся парсер

`@schemas/metadata-endpoint-json-schema.json` — две копии, правятся синхронно:
`Platform/A2v10.App.Assets2026/Application/@schemas/` и `Web/MainApp/@schemas/`.

Восемь перечислений перечислены дважды, в PascalCase и в camelCase: `columnType` — это 45
значений против 90, `endpointKind` — 10 против 20. Схема тем самым говорит «оба написания
одинаково правильны», хотя ключи файла уже camelCase (`fields`, `taskpad`) — их делает
`CamelCaseNamingStrategy`. Пока в схеме оба, разнобой не ловит никто: Newtonsoft парсит
перечисления регистронезависимо, так что `"MoNeY"` тоже загрузится.

Исключение одно, и это не решение, а расхождение: `CommandBarItemConverter.ReadJson`
(`Meta/Converters.cs:38`) вызывает `Enum.Parse<EntityCommandType>(token)` **без**
`ignoreCase`. Схема это честно помечает комментарием «Case-sensitive: unlike every other
enum here» — то есть документирует случайность одного написанного руками конвертера.

Порядок правки: `ignoreCase: true` в `Enum.Parse` → единое camelCase-написание в обеих
схемах → комментарий про исключение снять, исключения больше нет. Коллизий по регистру в
`EntityCommandType` нет.

Цена: существующие `metadata.json` в PascalCase продолжат грузиться (парсер не строже
схемы), но редактор со схемой начнёт их подчёркивать. Лечится массовой заменой, момент
выбирает автор.

### 3.5. Неизвестный ключ в `metadata.json` пропадает молча

`JsonSettings.cs:51` — `MissingMemberHandling` не задан, то есть `Ignore`.

Опечатка в имени ключа не даёт ни ошибки, ни предупреждения: узел просто приезжает пустым.
Живой случай — `"body"` вместо `"elements"` внутри `formElement` (`body` есть только у корня
формы): таскпад разобрался, отрендерился пустым, сообщения нет ни одного.

В схеме у `formElement` стоит `additionalProperties: false`, так что редактор с поддержкой
схемы такое показывает. Загрузчик — нет, а он единственный, кто работает и в CI, и в рантайме.

Закрывается одной строкой `MissingMemberHandling = Error`, но она касается всей
десериализации метаданных — `TableMetadata`, `DeclarationMetadata`, `ReportMetadata`,
`AppMetadata`. Решение не принято.

### 3.6. Дефолтную форму неоткуда взять текстом

Решение — в `CLAUDE.md`, «Forms: whole or nothing»: объявленная форма заменяет дефолтную
целиком, по частям не дописывается. Здесь — его невыполненное следствие.

Раз форму нельзя дописать, её надо уметь **получить**.
Сегодня взять дефолт как текст неоткуда — `EndpointGenerator.GenerateFormAsync` на этом
месте бросает `"What should be done here?"`. То есть «всё или ничего» на практике означает
«набирай с нуля», а формат при этом такой, что три опечатки подряд — норма (см. 3.5).

Само дерево уже есть и уже правильное: `DefaultFormBuilder.Create*Form(table)` выдаёт ровно
то, что кладётся в файл — `Columns`/`RowSet`/`TabState` помечены `[JsonIgnore]`. Эжектить
надо **до** `Bake`.

Что мешает:

- `DefaultFormBuilder`, `Constants.FormNames`, `TableColumnPredicates` — `internal`, а CLI
  это другая сборка. Ни `public` на билдере, ни `InternalsVisibleTo`, а порт на
  `DatabaseMetadataProvider`: он уже public и уже инжектится в CLI (`DeployCommand`), а
  написание формы в JSON принадлежит слою метаданных — он владеет схемой;
- перечисления сериализуются числами: `StringEnumConverter` не подключён ни в
  `A2v10.Metadata/JsonSettings.cs`, ни в `A2v10.Cli/Utils/JsonSettings.cs` — выйдет
  `"is": 3` вместо `"is": "dataGrid"`;
- пустые коллекции печатаются: `DefaultValueHandling.Ignore` сравнивает с `null`, а пустой
  список не null — каждый узел получит `"elements": [], "fields": [], "commands": []`;
- `CommandBarItemConverter.WriteJson` пишет PascalCase.

Последнее делает 3.4 **предусловием**, а не косметикой: эжект выдаёт текст, который автор
вставляет обратно, значит писатель и схема обязаны совпадать.

Место в дереве команд: `meta form <endpoint> --name index|edit|browse`. Лист называет то,
что получаешь, — ближайший сосед `db table-columns`.

---

## 4. Сообщения об ошибках без адреса

`DatabaseMetadataProvider.cs`: `:86`, `:130`, `:165`, `:188`, `:209`, `:324`

```
"GetModelInfo fails"
"AppMetadata deserialization fails"
"TableMetadata deserialization fails"
"DeclarationMetadata deserialization fails"
"ReportMetadata deserialization fails"
```

Ни одно не называет файл. Тот, кто чинит — человек или модель — получает факт «что-то
не разобралось» и ноль информации о том, какой из сотни `metadata.json` открывать.

Файл в этих местах известен: `ReadMetadataFileAsync` его строит и тут же выбрасывает.
Достаточно вернуть `fileName` из него и подставить.

Образец того, как это должно выглядеть, — `CheckDataLocation` (`:265`–`:296`) и
`LoadPlatformIdAsync` (`:119`): называют место, называют причину, и дают выбор,
а не «required property missing».

---

## 5. Не проверено — отмечено, чтобы не потерялось

- Ключ кеша storage vs endpoint (`DatabaseMetadataCache`): не смотрел, коллидируют ли
  адреса `document/invoice` (операция) и `document` (её storage). Может быть в порядке.
- `AllElementsMetadata` пропускает `schema == "autonum"` строкой с `TODO: skip other
  elements` — список пропускаемых явно неполон, но какие ещё нужны, не выяснял.
