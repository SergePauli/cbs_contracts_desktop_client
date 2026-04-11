# Исследование табличного контрола для WinUI 3

## Короткий вывод

В `WinUI 3` до сих пор нет встроенного полноценного `DataGrid` уровня WPF/WinForms. Для нового проекта наиболее практичный open-source вариант на сегодня - `WinUI.TableView`.

## Что удалось найти

### 1. Старый `DataGrid` из Windows Community Toolkit

- Исторически `DataGrid` существовал в Windows Community Toolkit.
- Актуальная документация Microsoft помечает его как archived/deprecated для новых сценариев.
- Для нового WinUI 3 development Microsoft Learn рекомендует смотреть в сторону `DataTable` или `WinUI.TableView`.

Полезные ссылки:

- Microsoft Learn: archived `DataGrid`
  - https://learn.microsoft.com/en-us/dotnet/communitytoolkit/archive/windows/datagrid
- API reference старого пакета
  - https://learn.microsoft.com/en-us/dotnet/api/communitytoolkit.winui.ui.controls.datagridcell?view=win-comm-toolkit-dotnet-7.0

### 2. `WinUI.TableView`

- GitHub: https://github.com/w-ahmad/WinUI.TableView
- NuGet: https://www.nuget.org/packages/WinUI.TableView/
- Пакет совместим с `net8.0-windows10.0.19041`, что совпадает с текущим проектом.
- В NuGet и GitHub видны свежие релизы и активное развитие.

Ключевые возможности:

- ручные и auto-generated колонки
- сортировка
- фильтрация по колонкам
- редактирование ячеек
- copy/export
- grid lines
- fluent-стилистика под WinUI

## Решение для этого репозитория

Вместо разработки собственного пакета с нуля проекту выгоднее сначала опереться на `WinUI.TableView`, потому что:

- библиотека уже существует и соответствует нашему стеку
- покрывает основной сценарий "центральной таблицы приложения"
- позволяет быстро перейти от placeholder-экрана к рабочей странице
- собственный `DataGrid`-пакет потребует отдельной большой ветки работ: virtualization, selection model, keyboard navigation, column sizing, sorting/filtering, editing, templates, accessibility

## Когда стоит делать свою библиотеку

Отдельный пакет имеет смысл разрабатывать только если позже подтвердится хотя бы одно из условий:

- критично нужен API/UX, которого нет в `WinUI.TableView`
- потребуется глубокая интеграция со специфичным бизнес-поведением
- появятся требования к виртуализации/редактору ячеек, которые нельзя закрыть расширением существующего контрола
- понадобятся корпоративные ограничения на внешние зависимости

## Рекомендуемый следующий этап

1. Использовать `WinUI.TableView` как базовый табличный control.
2. Вынести таблицу в первую реальную рабочую страницу shell.
3. Подключить живые данные из API.
4. Если ограничения проявятся на реальных сценариях, тогда проектировать собственную пакет-библиотеку поверх уже понятных требований.
