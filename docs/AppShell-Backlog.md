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
