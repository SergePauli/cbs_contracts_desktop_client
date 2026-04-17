# WinUI Table Control Research

## Исходная гипотеза

На старте проекта нужно было понять, опираться ли на готовый open-source grid/control или сразу строить собственный табличный слой.

## Что было найдено

Для WinUI 3 нет встроенного `DataGrid` уровня WPF/WinForms, поэтому естественным кандидатом выглядел `WinUI.TableView`.

Полезные ссылки:

- GitHub: https://github.com/w-ahmad/WinUI.TableView
- NuGet: https://www.nuget.org/packages/WinUI.TableView/
- архивная заметка по старому `DataGrid`:
  - https://learn.microsoft.com/en-us/dotnet/communitytoolkit/archive/windows/datagrid

## Какое решение принято фактически

Первичный research был полезен, но итоговая реализация в репозитории пошла дальше:

- в проект действительно был добавлен пакет `WinUI.TableView`
- однако реальный production UX reference-экрана сейчас построен вокруг собственного `CbsTableView`
- данные, виртуализация и viewport-driven загрузка тоже реализованы собственным слоем:
  - `LazyDataCollection<T>`
  - `LazyDataViewState<T>`
  - `CbsVirtualTableRows<T>`

То есть к текущему моменту проект опирается уже не на чужой grid как на ядро, а на собственную table platform.

## Почему так произошло

В процессе реализации выяснилось, что проекту нужен очень конкретный UX:

- компактная spreadsheet-like таблица
- двухрядная шапка
- горячие фильтры в header
- плотная визуальная сетка
- собственная логика viewport retention / lazy loading
- локальное сохранение ширин колонок
- меню быстрых table-actions в shell header
- точный контроль над шапкой, строками и selection state

Это оказалось проще и чище довести в собственном `CbsTableView`, чем пытаться адаптировать готовый универсальный control под все требования.

## Текущий вывод

На сегодня архитектурное решение такое:

- shell и рабочий reference screen уже опираются на собственный `CbsTableView`
- пакет `WinUI.TableView` в проекте остается, но не является ключевой основой текущей реализации

Позже можно отдельно решить:

1. оставить пакет как вспомогательную зависимость,
2. убрать его, если он больше не нужен,
3. использовать только для сравнения или отдельных экспериментальных сценариев.

## Что считается следующим этапом для table platform

- date/time filter modes
- дальнейшая полировка UX и keyboard flow
- row actions / editing scenarios
- сохранение дополнительных table preferences в локальных настройках
- более глубокие regression/UI-contract tests на shell/table integration
