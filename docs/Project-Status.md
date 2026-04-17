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

### 4. Табличная платформа

- компактный spreadsheet-like стиль
- выравнивание колонок по типу
- resize колонок
- локальное сохранение ширин
- row selection
- sorting
- header hot filters
- text/numeric filter modes
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
- settings persistence
- auth/user/login базовые сценарии
- navigation menu rules
- ContentHostView settings-menu regression checks

## Что сейчас в разработке по смыслу

Текущая фаза проекта:

**расширение прикладного функционала поверх уже готовой shell + table platform**

То есть команда больше не строит “скелет”, а доращивает рабочие сценарии.

Отдельно важно:

- диагностический код lazy/table/API-пайплайна сохранен в репозитории, но штатно выключен через флаги
- это позволяет возвращаться к детальной диагностике без повторного внедрения trace-кода

## Что еще не является завершенным

- date/time filters и специальные типы фильтров
- доменные действия над строками
- полноценный CRUD справочников:
  - create
  - read/details
  - update
  - delete/archive
- дополнительные рабочие экраны внутри shell
- финальная чистка и упрощение диагностического слоя после завершения активной оптимизации

## Что логично делать дальше

1. Сделать оставшийся CRUD для справочников следующим прикладным этапом
2. Переносить следующие экраны из web-клиента
3. Добавлять специализированные типы колонок и фильтров
4. Расширять доменный контекст в audit/context panel
5. Укреплять тестовое покрытие вокруг новых shell/table сценариев
