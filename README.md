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

### Экран справочников

В shell уже встроен реальный универсальный workspace для справочников:

- route-driven открытие `/references/{Model}`
- `ReferenceDefinitionService` с описанием поддержанных таблиц
- `ReferenceFieldDefinition` и field metadata для CRUD-диалогов
- `ReferencesContentViewModel` как orchestration-слой
- `ReferenceEditViewModel` / `ReferenceEditDialog` для create и edit
- `ReferenceEditPayloadBuilder` для create/update payload
- `ReferenceCrudService` для `create/update/delete`
- lazy/virtual pipeline через:
  - `LazyDataCollection<T>`
  - `LazyDataViewState<T>`
  - `CbsVirtualTableRows<T>`

### Собственный `CbsTableView`

Сейчас reference screen опирается на собственный табличный контрол:

- компактная spreadsheet-like стилистика
- двухрядная шапка
- сортировка по колонкам
- горячие фильтры в header
- ручной resize колонок
- сохранение пользовательских ширин
- single-row selection
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
- `date-time`

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

## На каком этапе сейчас разработка

Текущий этап:

**Фаза платформы завершена.**

Это означает, что уже готовы:

- auth flow
- shell
- reference workspace
- собственная table platform

Текущая работа теперь смещается с «собрать основу приложения» на:

- расширение сценариев поверх reference workspace
- дальнейшую полировку UX таблицы
- перенос следующих рабочих экранов из web-клиента

## Ближайшие направления

- details/read scenarios и доменные ограничения CRUD
- следующие специализированные типы колонок и фильтров поверх уже готовых `text` / `numeric` / `date-time`
- развитие audit/context state от реальных бизнес-сценариев
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
- локальные настройки ширин колонок
- alignment и filter mode дефолты колонок
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
