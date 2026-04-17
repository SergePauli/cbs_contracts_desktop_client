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

## Что теперь считается следующим этапом

Теперь следующий этап уже не “сделать shell”, а:

1. Дорабатывать оставшийся CRUD-функционал справочников
2. Развивать reference workspace как основу прикладных процессов
3. Публиковать в `ContextNavigationItems` и `AuditPanelState` не только техническую телеметрию, но и доменный контекст
4. При росте числа экранов выделить более формальный `INavigationService` и реестр страниц
5. Продолжать уплотнение и полировку shell UX только по месту, без возврата к broad redesign

## Практический next step

Наиболее логичный следующий шаг:

- реализовать оставшиеся CRUD-сценарии для справочников:
  - создание записи
  - открытие / просмотр карточки
  - редактирование записи
  - удаление / архивирование там, где это допускает доменная модель
  - обновление списка после CRUD-операций без поломки lazy/table-state
- определить, какие справочники переводятся на CRUD первыми и в каком порядке
- затем на этой же shell-платформе переносить следующие рабочие страницы из web-клиента
- parallel track: поддерживать регрессионные тесты на shell navigation / content chrome
