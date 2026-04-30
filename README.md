# cbs_contracts_desktop_client

Нативный Windows-клиент системы «База контрактов и контрагентов» на `WinUI 3` и `.NET 8`, развиваемый параллельно с web-клиентом `cbs_contracts_webclient`.

## Текущий статус

Проект уже находится на стадии рабочей платформы, а не только shell-каркаса.

Сейчас уже реализовано:

- рабочее WinUI 3 desktop-приложение на `.NET 8`
- авторизация через API
- сохранение учетных данных через Windows Credential Manager
- полноценный `AppShell` после логина
- общий shell-state через `AppShellViewModel`
- рабочий экран справочников в центральной области shell
- create / update / delete для поддержанных справочников
- complex reference `/users` со специализированным `ProfileEditDialog`
- complex reference `/employees` с detail footer, специализированным editor flow и контактами с определением типа
- audit panel в виде timeline-карточек с фильтрами, ленивой прокруткой и копированием ошибок
- собственный `CbsTableView` с lazy loading, resize, selection, sorting и hot filters
- локальное сохранение ширин колонок
- компактный `AppShell` с breadcrumbs, role-based sidebar и контекстным меню настроек таблицы
- unit-тесты на ключевую инфраструктуру и query/table-state

## Что уже сделано

### Инфраструктура

- `Microsoft.WindowsAppSDK`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Http`
- `app.manifest` с `PerMonitorV2` DPI awareness
- композиция сервисов в `App.xaml.cs`

### Авторизация

- `AuthService`
- `UserService`
- `CredentialManagerService`
- вход, logout и возврат на `LoginPage`

### Shell

- `AppShellPage` с layout на 5 регионов:
  - `NavigationSidebarView`
  - `TopBarView`
  - `ContentHostView`
  - `AuditPanelView`
  - `FooterBarView`
- breadcrumbs, footer-state и audit panel state живут в `AppShellViewModel`
- `BreadcrumbBar` показывает иерархию `Справочники -> {Текущий справочник}`
- `ContentHostView` больше не дублирует заголовок раздела, а использует компактный однострочный header
- `NavigationSidebarView` стал компактнее и отделен от content-area отдельной более темной gradient-панелью
- `AuditPanelView` показывает события активного справочника или выбранной записи

### Экран справочников

В shell уже встроен реальный универсальный workspace для справочников:

- route-driven открытие `/references/{Model}`
- `ReferenceDefinitionService` с описанием поддержанных таблиц
- `ReferenceFieldDefinition` и field metadata для CRUD-диалогов
- `ReferencesContentViewModel` как orchestration-слой
- `ReferenceEditViewModel` / `ReferenceEditDialog` для create и edit
- `ReferenceEditPayloadBuilder` для create/update payload
- `ReferenceCrudService` для `create/update/delete`
- `ReferenceEditorKind` для переключения generic/specialized editor flow
- `ProfileEditDialog` / `ProfileEditPayloadBuilder` для `/users`
- `EmployeeEditDialog` / `EmployeeEditPayloadBuilder` для `/employees`
- lazy/virtual pipeline через:
  - `LazyDataCollection<T>`
  - `LazyDataViewState<T>`
  - `CbsVirtualTableRows<T>`

### Complex references

Поддержаны первые сложные справочники поверх той же table/reference platform:

- `/users`
  - nested display/filter/sort metadata
  - specialized profile editor
  - роли через compact multi-select
  - lookup/autocomplete для связанных сущностей
  - typed create/update payload
- `/employees`
  - list-screen на `Employee/card`
  - `EmployeeDetailView` под таблицей
  - fresh edit load через `Employee/edit`
  - lookup для должности и контрагента
  - contacts editor с классификацией типа контакта
  - typed create/update payload, включая delta контактов

### Собственный `CbsTableView`

Сейчас reference screen опирается на собственный табличный контрол:

- компактная spreadsheet-like стилистика
- двухрядная шапка
- сортировка по колонкам
- горячие фильтры в header
- ручной resize колонок
- сохранение пользовательских ширин
- single-row selection
- double-click event для edit/open flow
- scope-safe состояние фильтров по `table route + fieldKey`
- кнопка "настройки таблицы" с действиями:
  - сброс ширин
  - сброс фильтров
  - сброс сортировки
- выравнивание колонок по типу:
  - текст слева
  - числа справа
  - bool по центру

### Фильтрация

Поддержаны три режима фильтрации колонок:

- `text`
- `numeric`
- `boolean`
- `date-time`
- `multiselect`

Для text:

- `Contains`
- `StartsWith`
- `Equals`
- `EndsWith`
- `NotContains`

Для numeric:

- `Equals`
- `LessThan`
- `LessThanOrEqual`
- `GreaterThan`
- `GreaterThanOrEqual`
- плюс строковые операции по цифрам при необходимости

Для numeric textbox уже есть мягкая валидация заведомо нечислового ввода.

Для date-time:

- сравнительные режимы работают через `CalendarDatePicker`
- текстовые режимы работают через masked input в формате `ГГГГ-ММ-ДД ЧЧ:ММ:СС`
- значение нормализуется в API payload формата `yyyy-MM-ddTHH:mm:ss`

Для multiselect:

- используется `Button + Flyout + CheckBox list`
- есть локальный поиск по опциям
- значение уходит в API как `__in`
- options поступают через `CurrentFilterOptionsSources`

### Audit panel

Audit panel теперь является reusable частью reference workspace:

- вертикальный timeline карточками от последнего события к первому
- при выбранной записи загружается аудит записи
- без выбранной записи загружаются последние события активного справочника
- прокрутка смещает окно загруженных событий, не раздувая буфер
- timeout/ошибка страницы не блокирует дальнейшую прокрутку
- фильтр по диапазону дат
- фильтр по действиям
- ошибки аудита доступны для копирования
- `AuditPanelFormatter` фиксирует контракт отображения action и mapping фильтра `string -> smallint`

## На каком этапе сейчас разработка

Текущий этап:

**Фаза платформы завершена.**

Это означает, что уже готовы:

- auth flow
- shell
- reference workspace
- собственная table platform
- первые complex references: `/users`, `/employees`
- audit timeline как общий сценарий для всех справочников

Текущая работа теперь смещается с «собрать основу приложения» на:

- расширение сценариев поверх reference workspace
- дальнейшую полировку UX таблицы и audit panel
- перенос следующих рабочих экранов из web-клиента

## Ближайшие направления

- details/read scenarios и доменные ограничения CRUD
- следующие специализированные типы колонок и фильтров поверх уже готовых `text` / `numeric` / `boolean` / `date-time` / `multiselect`
- поиск по аудиту
- delete/archive правила для сложных справочников
- новые рабочие страницы внутри shell
- возможная чистка диагностического слоя после завершения активной оптимизации

## Тесты

В solution есть `tests/CbsContractsDesktopClient.Tests`.

Уже покрыты:

- `AuthService`
- `UserService`
- `LoginViewModel`
- `DataQueryStateBuilder`
- `LazyDataViewState`
- `ReferenceDefinitionService`
- `ReferenceEditViewModel`
- `ReferenceCrudService`
- `ProfileEditPayloadBuilder`
- `EmployeeEditPayloadBuilder`
- `ContactTypeClassifier`
- `AuditPanelFormatter`
- локальные настройки ширин колонок
- alignment и filter mode дефолты колонок
- table multiselect/date-time filter UI regressions
- employee/profile specialized editor payload/state
- видимость `СЗИ` в меню для `admin` / `ОЗИ`
- регрессия на меню настроек `ContentHostView`
- регрессия на `ReferenceEditDialog` без зависимости от `LostFocus`

Запуск:

```powershell
dotnet test
```

## Структура проекта

- `src/Models/` — модели данных, shell-state, table definitions
- `src/Services/` — API, auth, settings, navigation, reference definitions
- `src/ViewModels/` — MVVM-логика
- `src/Views/` — XAML и `UserControl`
- `src/Collections/` — lazy/virtual data pipeline
- `tests/` — unit и integration tests
- `docs/` — технические заметки и backlog

## Документация

- [docs/Project-Status.md](docs/Project-Status.md)
- [docs/AppShell-Backlog.md](docs/AppShell-Backlog.md)
- [docs/WinUI-Table-Control-Research.md](docs/WinUI-Table-Control-Research.md)
