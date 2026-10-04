# Сессия: палитра цветов на CSS-переменных (LESS)

Цель — палитра, в которой каждый цвет определён один раз как CSS-переменная, а контролы её читают.
Первый потребитель уже есть (селектор), остальные копии палитры переводятся постепенно, по одной.
Начать с дизайна в прозе; код — после согласования.

## Что уже сделано (не трогать)

- **Селектор, клиент** — `colorOf(el)` → класс `color color-<имя>`: на `.input-group` для выбранного
  значения (`colorClass`) и на каждой строке списка (`itemClass`). Файлы: `scripts/tabmain.js`,
  `scripts/main.js` в `Platform/A2v10.Web.Assets/wwwroot` и в `Web/A2v10.Core.Web.Site/wwwroot`.
- **Селектор, XAML** — `Selector.ColorProperty` → атрибут `color-prop`
  (`ViewEngines/A2v10.ViewEngine.Xaml/Controls/Selector.cs`).
- **Метаслой** ставит `ColorProperty` селекторам, чья цель имеет колонку типа `Color`, и везёт цвет в
  элементе (`Platform/A2v10.Metadata`: `Form/ControlsXaml.cs`, `Database/SqlBuilder.cs` → `ColorField`).
- Сейчас класс `color color-<имя>` **ничего не красит**: правил `.color` / `.color-*` в CSS нет.

## Что прочитать

- `Platform/A2v10.Metadata/ISSUES.md` → **3.12** (словарь цветов в трёх копиях, направление закрытия)
  и **3.15** (селектор: что сделано, что осталось); подробности 3.15 — `ISSUES-3.15-selector-color.md`.
- `Platform/A2v10.Metadata/CLAUDE.md` → «Colour: a property of the row, never of the reference».

## Факты о LESS

- Исходники — `Web/A2v10.Core.Web.Site/themes/tabbed/` и `themes/tabbed_mobile/` (почти те же
  файлы, но разные). Сборка — `Web/A2v10.Core.Web.Site/compilerconfig.json` → `wwwroot/css/tabbed.css`,
  `tabbed_mobile.css`, схемы `*.colorscheme.css`. Собирает Web Compiler в VS — **руками, не машиной**.
  `wwwroot/css/*.css` — сгенерированное, руками не править.
- Копии палитры сегодня:
  - `TagLabelStyle` (`ViewEngines/A2v10.ViewEngine.Xaml/Text/TagLabel.cs`) — **эталон**: загрузка
    метаданных проверяет имя цвета по нему, принятое имя обязано рисоваться;
  - `Text.less` → `.tag-label.<имя>` (+ `.outline`: цвет текста вместо фона);
  - список `colors` пикера в JS;
  - `CardStyle` (`Controls/StateCard.cs`) → `.a2-state-card-*`.
- `Colors.less` уже переводит `@`-переменные LESS в `var(--…)` цветовых схем
  (`@success-base-color: var(--success-base)` и т.п.; схемы — `_ColorSchemes.less`, `_*.colorscheme.less`).
- `Control.less:312` — `&.has-value` (зелёная рамка выбранного фильтра); по 3.15 цвет строки ложится
  на тот же `.input-group`: `color` + `border-color`, то есть как `outline`-бейдж.

## Инвариант

Каждое имя `TagLabelStyle` (включая `white`) должно иметь `.color-<имя>`. Иначе имя пройдёт загрузку
и молча ничего не покрасит. Пока перевод не закончен, `.tag-label.<имя>` и `.color-<имя>` — два носителя
одного списка, оба обязаны знать каждое имя.

## Решить первым (в прозе)

1. Форма переменных: одна `--color` на имя или пара (фон / текст); как из неё получаются «залитый»
   бейдж и `outline`.
2. Связь с цветовыми схемами: берут ли цвета палитры значения из схемы (`--success-base` и т.п.) или
   задаются сами; что со светлой и тёмной схемой.
3. Где живут правила: новый файл (`Palette.less`?) и его импорт; `tabbed` и `tabbed_mobile` — общий
   файл или две копии.
4. Как селектор читает переменную: `.input-group.color` (текст + рамка) и строка списка.
5. Какая копия переводится следующей (`.tag-label`?) и как её перевод проверить.
