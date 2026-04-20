# Project Status

## Кратко

Проект находится на стадии **рабочей desktop-платформы**, а не прототипа.

Базовые технические риски уже закрыты:

- WinUI 3 приложение собрано и стабильно запускается
- auth flow работает
- shell работает
- reference workspace работает
- собственная table platform работает

## Что реализовано на текущий момент

### 1. Авторизация и запуск приложения

- login/logout
- сохранение учетных данных через Windows Credential Manager
- переход в `AppShell` после успешного входа

### 2. Shell

- sidebar
- top bar
- content host
- audit panel
- footer
- общий shell-state
- breadcrumbs с иерархией раздела и текущего справочника
- compact header контент-области вместо дублирующего title
- компактный и визуально отделенный navigation sidebar

### 3. Универсальный экран справочников

- route-driven references screen
- `ReferenceDefinitionService`
- lazy loading и viewport-based подгрузка
- custom `CbsTableView`
- базовый CRUD flow для справочников:
  - create
  - update
  - delete
- `ReferenceEditDialog` и `ReferenceEditViewModel`
- отдельный `ReferenceEditPayloadBuilder`
- reload списка после успешных CRUD-операций
- первый complex reference screen:
  - `/users`
  - модель `Profile`
  - nested columns для `email`, `ФИО`, `department`, `position`, `last_login`
  - route открывается в том же `ContentHostView`, без отдельного shell-path

### 4. Табличная платформа

- компактный spreadsheet-like стиль
- выравнивание колонок по типу
- resize колонок
- локальное сохранение ширин
- row selection
- sorting
- header hot filters
- text/numeric/date-time filter modes
- nested-value display/read path для вложенных API-объектов
- раздельные metadata поля:
  - `DisplayField`
  - `FilterField`
  - `SortField`
- `DateTime` filter для datetime-колонок:
  - сравнительные режимы через `CalendarDatePicker`
  - текстовые режимы через ISO-like masked input
  - нормализация значения в API payload формата `yyyy-MM-ddTHH:mm:ss`
- `MultiSelect` filter для lookup-колонок:
  - `Button + Flyout + CheckBox list`
  - lookup options source
  - локальный поиск
  - compact summary `Все` / `Выбрано: N`
- table settings menu:
  - reset column widths
  - reset filters
  - reset sorting
- корректный reset filter inputs в UI, а не только во view model

### 5. Тесты

Покрыты критичные части:

- query builder
- lazy data view state
- reference definitions
- reference edit view models
- reference CRUD service
- settings persistence
- auth/user/login базовые сценарии
- navigation menu rules
- ContentHostView settings-menu regression checks
- регрессия на `ReferenceEditDialog` без зависимости от `LostFocus`
- `ReferenceDataRow` nested-path resolution
- `CbsTableRowView` formatting для date/time и boolean icon rendering
- `CbsTableView` multiselect filter UI и options-source wiring
- `CbsTableView` date/time filter modes, masked input и `CalendarDatePicker` switch
- `DataQueryStateBuilder` mapping date/time criteria в API payload
- `ReferenceDefinitionService` metadata для `last_login` filter в `/users`

## Что сейчас в разработке по смыслу

Текущая фаза проекта:

**расширение прикладного функционала поверх уже готовой shell + table platform**

То есть команда больше не строит “скелет”, а доращивает рабочие сценарии.

Отдельно важно:

- общий диагностический слой lazy/table/API-пайплайна сохранен и штатно выключен
- временная диагностика `ReferenceEditDialog` снята после фикса регрессии с `PrimaryButton`

## Что еще не является завершенным

- доменные действия над строками
- полноценный CRUD справочников:
  - read/details
  - delete/archive с учетом бизнес-правил
- specialized editor для complex references:
  - `ProfileEditDialog`
  - lookup editors для связанных сущностей
- дополнительные рабочие экраны внутри shell
- дальнейшая чистка и упрощение диагностического слоя по мере стабилизации интеграций

## Что логично делать дальше

1. Довести CRUD справочников до details/archive и backend-aware ограничений
2. Переносить следующие экраны из web-клиента
3. Добавлять следующие специализированные типы колонок и фильтров поверх уже готовых text/numeric/date-time/multiselect
4. Расширять доменный контекст в audit/context panel
5. Укреплять тестовое покрытие вокруг новых shell/table сценариев
