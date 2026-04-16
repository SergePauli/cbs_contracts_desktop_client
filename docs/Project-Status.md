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

### 5. Тесты

Покрыты критичные части:

- query builder
- lazy data view state
- reference definitions
- settings persistence
- auth/user/login базовые сценарии

## Что сейчас в разработке по смыслу

Текущая фаза проекта:

**расширение прикладного функционала поверх уже готовой shell + table platform**

То есть команда больше не строит “скелет”, а доращивает рабочие сценарии.

## Что еще не является завершенным

- date/time filters и специальные типы фильтров
- доменные действия над строками
- полноценные сценарии редактирования
- дополнительные рабочие экраны внутри shell
- финальная чистка и упрощение диагностического слоя после завершения активной оптимизации

## Что логично делать дальше

1. Продолжать развитие reference workspace
2. Переносить следующие экраны из web-клиента
3. Расширять доменный контекст в audit/context panel
4. Укреплять тестовое покрытие вокруг новых сценариев
