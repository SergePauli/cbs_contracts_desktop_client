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
  - route открывается через `ContentHostRouterView` в `ProfileHostView`
- complex reference screen сотрудников:
  - `/employees`
  - модель `Employee`, preset `card`
  - таблица с web-совместимыми колонками `ID`, `ФИО`, `Контрагент`, `Должность`, `Контакты`, `Ув.`, `Пор.`, `Описание`
  - `EmployeeDetailView` под таблицей
  - специализированный `EmployeeEditDialog`
  - загрузка свежей записи перед edit через `IDataQueryService` с `preset = edit` и `id__eq`
  - edit/create через `IModelMutationService`
  - lookup должности и контрагента через общий `DialogLookupEditors`
  - контакты через общий `DialogContactsEditor` с определением типа, валидацией и link-uri
- complex reference screen контрагентов:
  - `/contragents`
  - модель `Contragent`
  - list-screen на общей table/reference platform
  - `ContragentDetailView` под таблицей
  - reusable `EmployeeBox` для сотрудников контрагента
  - контакты, адреса, реквизиты, ownership и список контрактов в detail-view
  - копирование detail-view в буфер обмена без списка сотрудников
  - специализированный `ContragentEditDialog`
  - typed state/view model/payload builder
  - история регистраций `contragent.organizations`
  - смена юр.лица через ручной ввод или импорт из ФНС
  - импорт/сверка с ФНС
  - lookup/cache infrastructure для ownership и адресов
- первая функциональная таблица:
  - `/revisions`
  - модель `Revision`, preset `list`
  - подключена к общей `TablePageDefinition` platform
  - metadata перенесена из web-версии
  - contract-oriented `ContractDetailView`
  - общий `ContractWorkflowStore`
  - `EmployeeBox` для карточки контрагента
  - `CommentBox` для комментариев
  - navigation-раздел `Файлы` по выбранному контракту
  - `RevisionEditDialog`
  - копирование contract summary в буфер обмена
  - временная диагностика API-запросов снята после стабилизации
- функциональная таблица этапов:
  - `/stages`
  - модель `Stage`, preset `list`
  - metadata и состав колонок перенесены из web-версии
  - поддержан диалог изменения раскладки колонок для большой вариативной таблицы
  - conditional row styling без декоративного левого border marker
  - row selection/unselect обновляет общий `ContractWorkflowStore`
  - contract-oriented `DetailView` переиспользует общий workflow с ревизиями
  - статус этапа и контракта отображается цветными badge-компонентами
  - фильтры статуса, типа работ, СЗИ, реестра, финансирования и сохраненных пользовательских defaults приведены к Rails API contract
  - reset button применяет начальные установки фильтрации, menu reset полностью очищает фильтры после подтверждения
  - оптимизирован lazy/table pipeline: сохранены skeleton rows, снижены лишние refresh/reload сценарии
  - добавлены специализированные диалоги редактирования этапов для коммерческого, финансового и ОЗИ-профилей
  - общий `StageEditState`/`ContractEditState` и payload builders формируют только delta update payload
  - комментарии этапов сохраняются через `comments_attributes`
  - закрытие последнего открытого этапа может закрывать контракт по бизнес-правилам коммерческого профиля
  - общие UI-компоненты и форматтеры вынесены в `Pauli.WinUiKit` и `Shared`
  - `Pauli.WinUiKit` содержит reusable controls `CalendarInput`, `Dropdown` и `MultiSelect`

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
- model mutation service
- settings persistence
- auth/user/login базовые сценарии
- navigation menu rules
- ComplexHostViewBase settings-menu regression checks
- регрессия на `ReferenceEditDialog` без зависимости от `LostFocus`
- `TableDataRow` nested-path resolution
- `CbsTableRowView` formatting для date/time и boolean icon rendering
- `CbsTableView` multiselect filter UI и options-source wiring
- `CbsTableView` date/time filter modes, masked input и `CalendarDatePicker` switch
- `DataQueryStateBuilder` mapping date/time criteria в API payload
- `ReferenceDefinitionService` metadata для `last_login` filter в `/users`
- `/employees` definition, detail footer и specialized employee editor flow
- `EmployeeEditStateFactory` flatten fresh edit-row contract
- `EmployeeEditPayloadBuilder` create/update payload contract, включая delta контактов
- `ContactTypeClassifier` для поддерживаемых типов контактов
- `/contragents` definition, detail-view, specialized editor flow, FNS integration и payload contracts
- `EmployeeBox` reusable UI contract/rendering hooks
- `/revisions` functional table definition, metadata, detail footer, workflow-store hooks, edit dialog и copy action
- `/stages` functional table definition, filters/defaults, row update, workflow-store hooks, edit dialogs, payload builders и button behavior
- `/contracts` functional table definition, route/host wiring, workflow-store hooks, contract info dialog и commercial edit payload

## Что сейчас в разработке по смыслу

Текущая фаза проекта:

**развитие рабочих экранов поверх готовой shell + table platform**

То есть команда больше не строит “скелет”, завершила крупный этап сложных справочников и закрыла функциональные таблицы `/revisions`, `/stages` и `/contracts`. Реализован первый рабочий экран `/orders` с master-detail компоновкой заказов и позиций. Следующий прикладной фокус - сквозной процесс формирования потребности и заказов на поставку.

### Текущее направление: потребность и заказы на поставку

Согласован следующий жизненный цикл:

1. Специалист коммерческого отдела в ветке этапа `Поставка` создает позиции `StageOrder` без привязки к заказу.
2. Позиция содержит материал/СЗИ, количество и состояние поставки.
3. Специалист ОЗИ работает с общей таблицей `Потребность`, объединяет нужные позиции и привязывает их к новому `Order`.
4. Новый заказ создается со статусом `Запрос цены` (`OrderStatus.id = 0`); номер заказа на этом этапе может отсутствовать.
5. По мере получения счета, оплаты и поступления товара специалист ОЗИ меняет статус и заполняет реквизиты заказа на странице `/orders`.
6. Изменения общей записи `StageOrder` отражаются в ветке `Поставка` этапа как для коммерческого отдела, так и для ОЗИ.

Архитектурные границы:

- `StageOrder` является самостоятельной mutation-сущностью и не сериализуется внутри update контракта или этапа;
- полный редактор позиции заказа принадлежит `StageOrderEditWorkflow`, а компактный коммерческий сценарий этапа — `StageSupplyEditWorkflow`;
- ветка `Поставка` использует отдельные store и `CbsTableView`, отображающие подтвержденный массив `Stage.card.stage_orders: StageOrder.stage[]`;
- таблица `/needs` работает с `StageOrder.card`, начальным фильтром `order_id__null = true` и
  сортировкой `isecurity_tool_id`;
- привязка позиции к заказу выполняется изменением `order_id`, а не созданием копии позиции;
- множественная привязка принадлежит одной операции `StageOrderNeedsStore`, которая последовательно
  обновляет выбранные `StageOrder` payload из `id`, `list_key` и целевого `order_id`; при создании
  нового заказа store сначала создает `Order` и получает его `id`;
- mutation-ответ используется только как подтверждение и источник `id`; отображаемая строка перечитывается соответствующим preset;
- точный API-контракт зафиксирован в [Supply-Order-API-Contract.md](Supply-Order-API-Contract.md).

Достигнуто:

- реализован экран `/orders` на `Order.list` с зависимой таблицей позиций;
- в коммерческий редактор контракта добавлена ветка этапа `Поставка`;
- отсутствие `stage_orders` обрабатывается как штатная пустая таблица;
- таблица поставок имеет создание, выбор, редактирование и удаление незакрепленных позиций;
- компактный `Flyout` не открывает вложенный `ContentDialog` и содержит только СЗИ, количество и важность;
- СЗИ выбирается через autocomplete, загруженный из `IsecurityTool.card`;
- при выборе СЗИ `default_cost` переносится в `StageOrder.price_cost`;
- при изменении количества автоматически вычисляется `StageOrder.cost = price_cost × amount`;
- важность позиции содержит значения `0 — Потребность`, `1 — В наличии`, `2 — На контроле`
  и `3 — Не согласовано`;
- начальный фильтр `/needs` исключает `1 — В наличии`;
- позиция с заполненным `order.order_number` доступна только для просмотра: редактирование и удаление блокируются;
- после create/update/delete перечитывается `Stage.card.stage_orders`;
- таблица `Поставка` компенсирует служебный отступ вложенного leaf-элемента `TreeView`, не меняя остальные ветки.

Порядок реализации:

1. Синхронизировать backend-пресеты и числовые значения состояния поставки — выполнено.
2. Реализовать workflow позиции для контекстов этапа и заказа — выполнено.
3. Добавить ветку `Поставка` в редактор контракта — выполнено.
4. Разрешить `Order.order_number = null` и создавать заказ со статусом `0` — выполнено.
5. Создать отдельный экран `Потребность` — выполнено.
6. Добавить операцию привязки выбранных потребностей к заказу.
7. Обновлять ветку `Поставка` после изменений из обоих рабочих контекстов — частично выполнено; перечитывание работает после локальных операций ветки и экрана заказов.

Отдельно важно:

- общий диагностический слой lazy/table/API-пайплайна сохранен и штатно выключен
- временная диагностика `ReferenceEditDialog` снята после фикса регрессии с `PrimaryButton`
- монолитный `ContentHostView` удален; content-area разбита на router, общий table-host слой и конкретные host views
- `Pauli.WinUiKit` стал отдельной точкой владения компактными WinUI controls для форм и фильтров

## Что еще не является завершенным

- дальнейшая чистка конкретных host views и вынос повторяющихся detail/workflow-паттернов по мере развития отчета `Активность`
- полировка `DetailView` для сложных таблиц, где одновременно нужны contract/stage/revision-specific summaries
- развитие `Pauli.WinUiKit` по мере появления повторяемых UI primitives
- отчет `Активность`
- доменные действия над строками
- полноценный CRUD справочников:
  - read/details
  - delete/archive с учетом бизнес-правил
- расширение общей библиотеки dialog editors по мере появления новых доменных форм
- дополнительные рабочие экраны внутри shell
- дальнейшая чистка и упрощение диагностического слоя по мере стабилизации интеграций

## Что логично делать дальше

1. Реализовать сквозной процесс `Поставка` → `Потребность` → `Заказ`.
2. Продолжить развитие `ContentHostRouterView`, `ComplexHostViewBase` и конкретных host views без возврата к монолитному content host.
3. Отполировать master-detail компоновку для нескольких таблиц на одной странице.
4. Начать разработку отчета `Активность`.
5. Развивать `Pauli.WinUiKit` как общий набор compact desktop controls для доменных форм.
6. Довести CRUD справочников до details/archive и backend-aware ограничений.
7. Добавлять следующие специализированные типы колонок и фильтров поверх уже готовых text/numeric/date-time/multiselect.
8. Расширять доменный контекст в audit/context panel.
9. Укреплять тестовое покрытие вокруг новых shell/table сценариев.
