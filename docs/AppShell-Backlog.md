# AppShell Backlog

## Где проект сейчас

Shell уже больше не является только каркасом после логина.

Внутри него уже работает реальный reference workspace, поэтому основной риск проекта сместился с shell-архитектуры на развитие прикладных сценариев.

## Что уже завершено

### Shell layout

- `AppShellPage` с 5 регионами:
  - `NavigationSidebarView`
  - `TopBarView`
  - `ContentHostView`
  - `AuditPanelView`
  - `FooterBarView`

### Shell-state

В `AppShellViewModel` уже живут:

- `IsSidebarVisible`
- `IsAuditPanelOpen`
- `SelectedNavigationItem`
- `BreadcrumbItems`
- `FooterState`
- `ContextNavigationItems`
- `AuditPanelState`
- `AuditPanelText`

### После логина

- переход в shell
- logout flow
- role-based navigation
- collapsible раздел `Справочники`
- breadcrumb hierarchy в top bar
- компактный gradient-sidebar

### Центральный контент

Центральная область уже не placeholder:

- в shell встроен рабочий экран справочников
- он route-driven
- поддерживает lazy loading
- публикует технический контекст в audit panel
- показывает ошибки загрузки через верхний `InfoBar`
- использует компактный header формата `Название - N записей`
- поддерживает create / update / delete для подключенных справочников
- открывает code-based `ReferenceEditDialog` с read-only `ID` в edit-режиме
- собирает create/update payload через отдельный `ReferenceEditPayloadBuilder`

## Что теперь считается следующим этапом

Теперь следующий этап уже не “сделать shell”, а:

1. Расширять CRUD-слой справочников от базового create/update/delete к details и доменным ограничениям
2. Развивать reference workspace как основу прикладных процессов
3. Публиковать в `ContextNavigationItems` и `AuditPanelState` не только техническую телеметрию, но и доменный контекст
4. При росте числа экранов выделить более формальный `INavigationService` и реестр страниц
5. Продолжать уплотнение и полировку shell UX только по месту, без возврата к broad redesign

## Практический next step

Наиболее логичный следующий шаг:

- довести reference CRUD от базового flow до прикладного уровня:
  - открытие / просмотр карточки
  - delete/archive правила по доменной модели
  - сообщения об ограничениях и конфликтах backend
  - точечный UX-polish диалогов редактирования
- определить, какие справочники переводятся на CRUD первыми и в каком порядке
- затем на этой же shell-платформе переносить следующие рабочие страницы из web-клиента
- parallel track: поддерживать регрессионные тесты на shell navigation / content chrome

## Короткий CRUD-план

Последовательность внедрения:

1. `ReferenceDefinition.Fields` выполнено
2. `ReferenceEditDialog` + `ReferenceEditViewModel` выполнено
3. `DirtyFields -> payload builder` выполнено
4. `Create/Update/Delete` сервисные методы выполнено
5. Дальше: `details/read flow` и доменные ограничения CRUD

## Следующий крупный этап: сложные справочники со связями

Первый кандидат: экран пользователей / профилей из web-клиента (`ProfilesPage`).

Ключевой вывод после анализа web-версии:

- list/table сценарий можно строить на уже готовой reference-platform desktop-клиента
- основное усложнение находится не в таблице, а в edit/create форме
- для связанных моделей не стоит насильно расширять generic `ReferenceEditDialog` до универсального конструктора любой сложности

### Что переиспользуем без изменения концепции

- route-driven reference workspace в `ContentHostView`
- `ReferencesContentViewModel`
- `CbsTableView` и lazy loading pipeline
- filters / sorting / width persistence
- success/error UX и reload после сохранения
- `ReferenceCrudService` как transport-слой

### Что делаем специализированным

- edit/create dialog для сложной модели
- payload builder для сложной модели
- lookup/selectors для связанных сущностей
- mapping полей complex model -> API / dataset

### Архитектурный принцип

Для простых справочников сохраняем current path:

- `ReferenceDefinition`
- `ReferenceEditDialog`
- `ReferenceEditViewModel`
- `ReferenceEditPayloadBuilder`

Для сложных справочников вводим specialized path внутри того же workspace:

- generic list view остается общей
- editor/dialog выбирается по типу definition
- для `Profile` появляется отдельный `ProfileEditDialog` и `ProfileEditViewModel`

## Implementation Plan: Complex Reference / Users Profile

### Phase 1. Подключить complex list-screen на существующей table-platform

Цель:

- показать пользователей в том же reference workspace без отдельного экрана-сателлита

Шаги:

1. Добавить definition для `Profile` / `Users` в desktop reference registry
2. Описать table columns и `ApiField` mapping по аналогии с web `ProfilesPage`
3. Поддержать route для открытия этого справочника через текущий `ContentHostView`
4. Проверить filters / sorts / width persistence на nested fields

Ожидаемый результат:

- users table работает в том же shell workspace, что и простые справочники

Статус:

- выполнено
- `Profile` definition добавлен
- route `/users` подключен в текущий `ContentHostView`
- nested display/filter/sort metadata работает
- widths и sorting persistence протянуты на complex columns

### Phase 2. Ввести тип editor experience для справочников

Цель:

- отделить generic editing от specialized editing без дублирования list-shell

Шаги:

1. Добавить в definition признак editor strategy / editor kind
2. Оставить `Generic` как дефолт для текущих справочников
3. Добавить `Profile` как specialized editor kind
4. В `ContentHostView` переключать открываемый dialog по kind

Ожидаемый результат:

- текущий CRUD простых справочников не ломается
- появляется легальная точка расширения для сложных редакторов

### Phase 3. Реализовать специализированный `ProfileEditDialog`

Цель:

- вынести сложную форму пользователя в отдельный bounded context

Шаги:

1. Создать `ProfileEditViewModel`
2. Создать `ProfileEditDialog`
3. Определить состав полей первого этапа:
   - login
   - role
   - position
   - department
   - activated / used
4. Поля высокой сложности (`person`, контактные данные, сложные вложенные сущности) сначала поддержать как read-only или отложить

Ожидаемый результат:

- edit-profile реализован без перегрузки generic dialog

### Phase 4. Добавить lookup infrastructure для связанных сущностей

Цель:

- дать reusable основу для select/search полей в сложных справочниках

Шаги:

1. Ввести lightweight lookup service для связных справочников
2. Сделать reusable view model для selected item (`id + display text`)
3. Добавить базовый lookup editor control / dialog picker
4. Поддержать минимум для `department` и `position`

Ожидаемый результат:

- первая reusable инфраструктура связей появляется без полного form-builder

### Phase 5. Сделать specialized payload builder

Цель:

- отделить сохранение сложной модели от generic `DirtyFields -> payload builder`

Шаги:

1. Создать `ProfilePayloadBuilder`
2. Явно определить, какие поля идут в create/update payload
3. Учитывать правила backend для nested ids / foreign keys
4. Добавить tests на payload contract

Ожидаемый результат:

- complex edit сохраняется предсказуемо и независимо от generic editor path

### Phase 6. Ограничить scope первой поставки

Первую поставку complex reference лучше делать не как full CRUD, а как:

1. users list
2. open selected profile
3. edit existing profile
4. save + reload + notifications

Отложить до отдельного шага:

- create profile
- delete / archive profile
- редактирование всех вложенных сущностей в одной форме

### Почему именно так

- максимальное переиспользование уже готовой desktop table/reference platform
- минимальный риск сломать стабильный generic CRUD для простых справочников
- появление reusable pattern для следующих сложных справочников со связями
- архитектурное разделение между generic scalar editor и specialized relational editor

## UI Plan: MultiSelect Filter for relational columns

Контекст:

- для колонок вида `department_id`, `position_id` и других lookup-связей обычный `TextBox` filter недостаточен
- готового подходящего WinUI control в проекте нет
- `ComboBox` не подходит, потому что нужен выбор нескольких значений

Принятое решение:

- делаем собственный reusable filter UI по схеме `Button + Flyout + CheckBox list`
- реализация идет не как хак под `department_id`, а как общая capability `CbsTableView`

### Целевое UX-поведение

В header filter row для relational column:

- вместо `TextBox` показывается компактная кнопка
- кнопка открывает `Flyout`
- внутри `Flyout`:
  - строка поиска по доступным опциям
  - список `CheckBox`
  - кнопка `Применить`
  - кнопка `Сбросить`
- в закрытом состоянии кнопка показывает summary:
  - `Все`
  - либо `Выбрано: N`

### Implementation Plan по слоям

#### Phase A. Metadata layer

Файлы:

- `src/Models/Table/CbsTableColumnFilterDefinition.cs`
- при необходимости новый файл рядом в `src/Models/Table/`

Шаги:

1. Добавить тип visual editor для фильтра:
   - `Text`
   - `Numeric`
   - `Boolean`
   - `MultiSelect`
2. Добавить metadata для options source:
   - статические опции
   - lookup-source key / provider key
3. Добавить default summary text для пустого выбора

Результат:

- `CbsTableView` сможет понять, какой filter UI строить для каждой колонки

Статус:

- выполнено
- введены `CbsTableFilterEditorKind`, `CbsTableFilterOptionDefinition`
- metadata добавлена в `CbsTableColumnFilterDefinition`

#### Phase B. Filter value contract

Файлы:

- `src/Views/Controls/CbsTableView.xaml.cs`
- `src/Models/Data/DataFilterCriterion.cs`
- при необходимости новый вспомогательный model-файл

Шаги:

1. Зафиксировать, что `MultiSelect` использует `DataFilterMatchMode.In`
2. Передавать из UI не строку, а набор значений:
   - `object?[]`
   - либо `IReadOnlyList<object?>`
3. Убедиться, что текущий `DataQueryStateBuilder.BuildInFilter(...)` может принимать этот контракт без дополнительных обходов

Результат:

- UI и data-query pipeline говорят на одном reusable контракте для `in` filters

Статус:

- выполнено
- `CbsTableMultiSelectFilterValue` введен как reusable contract
- pipeline принимает список значений для `__in`
- UI не возвращает выбранные row-objects, а сразу передает значения

#### Phase C. Reusable Flyout UI inside `CbsTableView`

Файлы:

- `src/Views/Controls/CbsTableView.xaml.cs`
- при необходимости новый helper/control в `src/Views/Controls/`

Шаги:

1. Добавить builder для `MultiSelect` filter control:
   - `Button`
   - `Flyout`
   - `TextBox` для локального поиска
   - `ScrollViewer + StackPanel` со списком `CheckBox`
2. Добавить внутреннее состояние:
   - доступные options
   - выбранные options
   - текущий search text
3. Добавить summary text на кнопку:
   - `Все`
   - `Выбрано: N`

Статус:

- выполнено
- built-in `MultiSelect` filter UI работает в `CbsTableView`
- lookup options подгружаются через `OptionsSourceKey`
- список опций фильтруется локальным поиском
- flyout и option-item доведены до компактного рабочего состояния

#### Первый production scenario: Department filter for Users/Profile

Результат:

- `department` column использует:
  - `DisplayField = department.name`
  - `FilterField = department_id`
  - `SortField = department.name`
- lookup options загружаются как `Department item`
- `department_id__in` уходит в payload корректно
- таблица `/users` работает как первый реальный complex reference screen
2. Добавить внутреннее состояние:
   - доступные options
   - выбранные options
   - текущий search text
3. Добавить summary text на кнопку:
   - `Все`
   - `Выбрано: N`
4. Добавить `Применить` / `Сбросить`

Результат:

- в `CbsTableView` появляется reusable multiselect-filter без внешних библиотек

#### Phase D. Options provider layer

Файлы:

- новый lookup service в `src/Services/`
- либо отдельный provider в `src/Services/References/`

Шаги:

1. Ввести lightweight provider для filter options
2. Поддержать минимум два режима:
   - статический набор
   - lookup from reference / API
3. Возвращать унифицированную модель:
   - `Value`
   - `Label`
   - при необходимости `IsSelected`

Результат:

- `CbsTableView` не знает, откуда пришли опции, а просто рисует их

#### Phase E. First production target: `department_id`

Файлы:

- `src/Services/References/ReferenceDefinitionService.cs`
- `Profile` / `Users` related definition files

Шаги:

1. Подключить `MultiSelect` filter metadata для department column
2. Настроить options source на departments lookup
3. Проверить payload вида `department_id__in`
4. Проверить сочетание с sorting / width persistence / lazy reload

Результат:

- первый рабочий relational multiselect filter на users/profile table

#### Phase F. Tests

Файлы:

- `tests/CbsContractsDesktopClient.Tests/DataQueryStateBuilderTests.cs`
- новые tests для `CbsTableView` / metadata / options provider

Шаги:

1. Зафиксировать `In` payload contract
2. Зафиксировать summary text logic
3. Зафиксировать reset behavior
4. Зафиксировать binding между metadata и UI builder

Результат:

- reusable multiselect-filter закрыт регрессией до подключения новых сложных таблиц
