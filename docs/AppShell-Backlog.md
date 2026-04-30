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
- публикует доменный контекст в audit panel
- показывает ошибки загрузки через верхний `InfoBar`
- использует компактный header формата `Название - N записей`
- поддерживает create / update / delete для подключенных справочников
- открывает code-based `ReferenceEditDialog` с read-only `ID` в edit-режиме
- собирает create/update payload через отдельный `ReferenceEditPayloadBuilder`
- поддерживает complex references `/users` и `/employees`
- переключает generic/specialized editor по `ReferenceEditorKind`

### Audit panel

- `AuditPanelView` переведен из текстового placeholder в вертикальный timeline
- события отображаются карточками от последнего к первому
- при выбранной записи загружается аудит записи
- без выбранной записи загружаются последние события активного справочника
- timeline поддерживает прокрутку с ленивым смещением окна вверх/вниз
- размер буфера событий остается постоянным
- сбой/timeout загрузки страницы не блокирует дальнейшую прокрутку
- добавлены фильтры по диапазону дат и действиям
- фильтр действий использует строковые API-константы в UI и конвертацию в backend `smallint` только при сборке запроса
- `deleted` поддерживается только в отображении как вариант `removed`
- ошибки аудита можно копировать в буфер обмена
- логика форматирования action вынесена в reusable `AuditPanelFormatter`
- добавлены регрессионные тесты на отображение, фон и `string -> smallint` mapping

## Что теперь считается следующим этапом

Теперь следующий этап уже не “сделать shell”, а:

1. Расширять CRUD-слой справочников от базового create/update/delete к details и доменным ограничениям
2. Развивать reference workspace как основу прикладных процессов
3. При росте числа экранов выделить более формальный `INavigationService` и реестр страниц
4. Продолжать уплотнение и полировку shell UX только по месту, без возврата к broad redesign
5. Поддерживать audit panel как общий cross-reference сценарий для всех новых таблиц

## Ближайшая следующая задача

Следующий production-шаг: справочник `Контрагенты` как новый complex reference внутри текущего reference workspace.

Цель:

- подключить `/contragents` на уже готовой table/reference platform
- использовать контрагентов как первый сценарий для нового reusable detail-widget `EmployeeBox`
- сделать `EmployeeBox` базовым UI-блоком для отображения ответственных сотрудников во всех последующих таблицах

Ожидаемый первый scope:

1. Добавить `ReferenceDefinition` для `Contragent`
2. Настроить list-screen: колонки, nested display/filter/sort mapping, lazy loading, width persistence
3. Добавить `ContragentDetailView` под таблицей
4. В detail-view вывести связанные роли/сотрудников через новый `EmployeeBox`
5. Спроектировать `EmployeeBox` как самостоятельный reusable control:
   - ФИО
   - должность
   - контакты или compact contact summary
   - статус активности
   - визуальный вариант для compact/detail density
6. Закрыть `EmployeeBox` регрессионными тестами на contract/rendering hooks
7. Подключить audit panel к `Contragent` через общий audit flow

После этого:

- переносить следующие complex references уже с готовым паттерном detail-widget
- возвращаться к delete/archive правилам и доменным ограничениям CRUD
- parallel track: поддерживать регрессионные тесты на shell navigation / content chrome / audit

## Короткий CRUD-план

Последовательность внедрения:

1. `ReferenceDefinition.Fields` выполнено
2. `ReferenceEditDialog` + `ReferenceEditViewModel` выполнено
3. `DirtyFields -> payload builder` выполнено
4. `Create/Update/Delete` сервисные методы выполнено
5. `details/read flow` выполнен для `/employees`
6. Дальше: delete/archive правила и доменные ограничения CRUD

## Закрытый крупный этап: сложные справочники со связями

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

Статус:

- выполнено
- добавлен `ReferenceEditorKind`
- `ContentHostView` выбирает generic/profile/employee dialog по kind

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

Статус:

- выполнено: `ProfileEditDialog` переведен на `AutoSuggestBox` для поля `position`
- выполнено: загрузка lookup-опций `Position/item` по `name__cnt` и алфавитная сортировка
- выполнено: устранена проблема обновления suggestions через уведомление `PositionSuggestionLabels`
- выполнено: глобально уплотнен `DefaultAutoSuggestBoxStyle` через `App.xaml`
- выполнено: поле `role` в `ProfileEditDialog` переведено на multi-select (`user`, `admin`, `excel`, `intern`) с правилом взаимного исключения `intern` vs `admin/excel`
- выполнено: добавлен API-ready контракт роли `RoleApiValue` в формате CSV (`user,admin,excel`)
- выполнено: завершен этап верстки `ProfileEditDialog` (компактный layout, стили меток, поведение select-полей, служебный `InfoBar` для ошибок)
- выполнено: submit-flow подключен к `ReferenceCrudService`
- выполнено: payload для `create/update` строится через `ProfileEditPayloadBuilder`
- выполнено: ошибки API и валидации показываются через встроенный `InfoBar`
- выполнено: успешное сохранение закрывает диалог и перезагружает список

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

Статус:

- выполнено для первых production scenarios
- `ProfileEditDialog` и `EmployeeEditDialog` используют lookup/autocomplete для связанных сущностей
- `DialogLookupEditors` стал reusable основой для searchable lookup в specialized dialogs
- `CbsTableFilterOptionDefinition` используется как общий `Value + Label` contract для выбора связанных записей

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

Статус:

- выполнено
- добавлен `ProfileEditPayloadBuilder` для `create/update` с отдельным контрактом сложной модели
- `ProfileEditDialog` подключен к реальному submit-flow:
  - `create` -> `CreateAsync`
  - `update` -> `UpdateAsync`
- обязательные поля вынесены в явную валидацию перед отправкой:
  - `роли`, `логин`, `ФИО`, `email`, `должность`, `отдел`
  - для `create` дополнительно `пароль`
- ошибки валидации и API показываются через встроенный `InfoBar` в диалоге
- добавлен `list_key`:
  - всегда для `create`
  - для `update` при включенном аудите модели (`IsAuditEnabled`)
- добавлены тесты payload-contract и submit-пути

Итог по диалогу:

- задача по `ProfileEditDialog` (верстка + submit + валидация + payload + create/update + reload + notifications) закрыта

### Phase 6. Ограничить scope первой поставки

Первую поставку complex reference лучше делать не как full CRUD, а как:

1. users list
2. open selected profile
3. edit existing profile
4. save + reload + notifications

Статус:

- выполнено
- фактическая поставка расширена до create/update profile через specialized dialog

Отложить до отдельного шага:

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

Статус:

- выполнено в составе `ReferencesContentViewModel`
- options для table multiselect приходят через `CurrentFilterOptionsSources`
- `CbsTableView` получает готовые option sources и остается UI-only контролом

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

Статус:

- выполнено
- `/users` использует multiselect-filter для lookup-колонок
- `department_id__in` payload зафиксирован тестами

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

Статус:

- выполнено
- добавлены тесты на `CbsTableMultiSelectFilterValue`
- добавлены регрессионные тесты `CbsTableView` на flyout, search, checkbox-list, summary и reset behavior
- `DataQueryStateBuilderTests` фиксируют `__in` payload contract

## Production Update: DateTime Filter for `last_login`

Контекст:

- по backlog `date/time filters` оставались следующим шагом для table-platform
- в последних незакоммиченных изменениях это направление уже частично закрыто на реальном сценарии users/profile table

Что сделано:

- добавлен `DataFilterMode.DateTime`
- `DataQueryStateBuilder` теперь маппит date/time значения в API payload формата `yyyy-MM-ddTHH:mm:ss`
- для колонки `last_login` в `Profile` / `/users` definition включен filter metadata c default mode `GreaterThanOrEqual`
- `CbsTableView` поддерживает два UX-режима для date/time:
  - `CalendarDatePicker` для сравнительных операторов
  - masked text input для `Contains` / `StartsWith` / `EndsWith` / `NotContains`
- reset filter state очищает и текстовое поле, и `DatePicker`

Статус:

- выполнено для первого production scenario
- закрыт базовый reusable date/time filter path в table-platform

Что еще остается по этой теме:

- при необходимости добавить date/time filter в другие reference definitions
- при необходимости расширить mask/validation UX под более строгие пользовательские сценарии

## Production Update: Audit Panel Timeline

Контекст:

- audit panel стала общей частью reference workspace
- все последующие таблицы будут использовать один и тот же сценарий аудита
- важно сохранить контракт API: в ответе `action` приходит строковой константой, а фильтр отправляется как backend enum `smallint`

Что сделано:

- `AuditRecord.Action` читается как `string`
- отображение action:
  - `added` -> `Добавлено:`
  - `updated` -> `Изменено:`
  - `removed` -> `Удалено:`
  - `deleted` -> `Удалено:` только для отображения
  - `archived` -> `Архивировано:`
  - `imported` -> `Импорт:`
- `removed` и `deleted` получают отдельный красноватый фон `ShellAuditRemovedBackgroundBrush`
- фильтр действий отправляет только поддержанный enum:
  - `added` -> `0`
  - `updated` -> `1`
  - `removed` -> `2`
  - `archived` -> `3`
  - `imported` -> `4`
- `deleted` не отправляется в фильтр запроса
- дата-фильтр аудита отправляет `created_at__gte` / `created_at__lte`
- список событий скроллится окном фиксированного размера вместо бесконечного наращивания массива
- ошибки загрузки показываются карточкой и доступны для копирования
- добавлен `AuditPanelFormatter` и тесты `AuditPanelFormatterTests`

Статус:

- выполнено
- закрыто регрессионными тестами

## Implementation Plan: Complex Reference / Employees

Контекст:

- переносим web-страницу `EmploeesPage.tsx` не отдельным экраном, а как complex reference внутри текущего reference workspace
- таблица, lazy loading, фильтры, сортировки, resize колонок и persistence остаются общими
- сложность сотрудников находится в DetailView и специализированном editor-flow: `Employee -> Person -> Names/Contacts`, `Contragent`, `Position`

### Phase 1. Подключить list-screen на текущей reference-platform

Цель:

- открыть `/employees` в том же `ContentHostView`, что и остальные справочники

Шаги:

1. Добавить `ReferenceDefinition`:
   - `Route = /employees`
   - `Model = Employee`
   - `Preset = card`
   - default sort: `id desc`
2. Описать колонки:
   - `id`
   - `used`
   - `name`
   - `contragent`
   - `position`
   - `contacts`
3. Использовать nested display/filter/sort mapping по web-версии:
   - `name` -> `person.full_name`, filter `person.person_name.naming.fio`
   - `contragent` -> `contragent.name`, filter `org.name_or_org.full_name`
   - `position` -> `position.name`
   - `contacts` -> `person.contacts.name`, filter `person.person_contacts.contact.value`
4. Зафиксировать definition tests.

Статус:

- завершено
- `/employees` подключен как complex reference list
- generic editor для сотрудников заменен специализированным `EmployeeEditDialog`
- definition зафиксирован тестами

### Phase 2. Добавить DetailView под таблицей

Цель:

- повторить ключевое поведение web Fieldset под таблицей без дублирования table-screen

Шаги:

1. Ввести detail strategy / detail kind для complex reference.
2. Добавить `EmployeeDetailView`:
   - ФИО
   - должность
   - контрагент
   - статус `уже не работает`
   - контакты с типами и clickable links
3. Перенести action buttons из верхнего header в контекст DetailView там, где это удобнее для сложной карточки.
4. При смене выбранной строки обновлять доменный audit context по `auditable_type = Employee`, `auditable_id = selected.id`.

Статус:

- завершено для первой поставки
- `EmployeeDetailView` добавлен вторым footer над основным footer
- высота detail области зафиксирована на 130
- контакты выводятся через общий contact-chip без кнопки удаления
- доменный audit context работает через общий audit panel flow
- перенос action buttons в DetailView остается отдельной UX-задачей

### Phase 3. Реализовать specialized `EmployeeEditDialog`

Цель:

- не расширять generic scalar dialog до вложенной формы сотрудников

Шаги:

1. Создать `EmployeeEditViewModel`.
2. Создать `EmployeeEditDialog`.
3. Перед edit загружать свежую запись:
   - `Model = Employee`
   - `Preset = edit`
   - `filters: id__eq`
4. Поддержать поля первого этапа:
   - ФИО
   - должность через autocomplete / lookup
   - контрагент через searchable lookup
   - контакты
   - `used`
   - `priority`
   - `description`

Статус:

- завершено для первой поставки
- edit/double-click загружает свежую запись через `IDataQueryService`
- должность и контрагент используют общий lookup UI
- контрагент выбирается только из списка найденных значений
- контакты редактируются через `DialogContactsEditor` с контролем типа и валидности

### Phase 4. Сделать payload builder сотрудников

Цель:

- явно повторить backend contract web `EmployeeStore`, но в typed desktop-коде

Шаги:

1. Создать `EmployeeEditPayloadBuilder`.
2. Для create формировать:
   - `list_key`
   - `contragent_id`
   - `position_id` или `position_attributes`
   - `person_attributes.person_names_attributes`
   - `person_attributes.person_contacts_attributes`
3. Для update формировать только изменения:
   - scalar fields
   - смена `contragent_id`
   - смена `position_id` или новая `position_attributes`
   - новая запись имени при изменении ФИО
   - delta контактов: created + removed
4. Добавить тесты на create/update payload contract.

Статус:

- завершено
- create/update payload вынесен в `EmployeeEditPayloadBuilder`
- covered by tests: create payload, update payload, contact delta, unknown contact type rejection

### Phase 5. Ограничить первую поставку

Первая поставка:

1. `/employees` list - готово
2. выбор строки - готово
3. DetailView под таблицей - готово
4. edit existing employee - готово
5. save + reload + notification - готово

Отложить:

- Excel export
- delete/archive правила
- массовые операции
- расширенные шаблоны ячеек таблицы для clickable contact chips

## Reusable UI / Service Components Closed

В ходе работ над `/users`, `/employees` и audit panel появились reusable building blocks:

- `CbsTableView`
  - lazy table UI
  - resize колонок
  - sorting
  - text/numeric/boolean/date-time/multiselect filters
  - row selection и double-click event
- `CbsTableMultiSelectFilterValue`
  - общий контракт `MultiSelect -> __in`
- `CbsTableFilterOptionDefinition`
  - общий контракт option `Value + Label`
- `ReferenceEditorKind`
  - переключение generic/profile/employee editor flow
- `ProfileEditDialog` / `ProfileEditPayloadBuilder`
  - specialized pattern для сложного справочника с lookup-полями
- `EmployeeEditDialog` / `EmployeeEditPayloadBuilder`
  - specialized pattern для nested payload и contacts delta
- `DialogLookupEditors`
  - reusable searchable lookup UI для специализированных диалогов
- `DialogContactsEditor`
  - reusable editor контактов с классификацией типа
- `ContactTypeClassifier`
  - определение `Email`, `Phone`, `Fax`, `SiteUrl`, `Telegram` и link-uri
- `EmployeeDetailView`
  - первый reusable-ish detail footer pattern под таблицей
- `AuditPanelFormatter`
  - общий контракт отображения/фильтрации audit actions

Следующий принцип:

- новые справочники должны переиспользовать эти блоки до появления новых специализированных controls
- расширять generic editor только для scalar-сценариев
- сложные nested-сценарии вести через specialized editor + typed payload builder
