# Отчёт по этапу 0 — каркас репозитория

Дата выполнения: 2026-09-04.

## Выполнено

- Создана Visual Studio solution с проектами Rhino plugin, Protocol и Protocol.Tests.
- Плагин таргетирует `net48`, собирается в `.rhp` и рассчитан на все сервис-релизы Rhino 8.
- Для обратной совместимости закреплена базовая версия RhinoCommon `8.0.23304.9001`; API более поздних Rhino 8 SR использовать нельзя без совместимого fallback.
- Добавлена команда Rhino `RhinoMinion` для smoke-проверки загрузки каркаса.
- Добавлен Python 3.11 backend на FastAPI/Pydantic с `/health`.
- Добавлены C# и Python модели envelope и ошибок протокола 0.1.
- Добавлены JSON Schema и канонические fixtures.
- Добавлены unit/contract test skeletons.
- Добавлены PowerShell-команды bootstrap, build и test.
- Добавлен GitHub Actions CI для .NET и Python.
- Добавлены `.gitignore`, `.editorconfig`, `.env.example`, документация протокола и безопасности.

## Проверено в текущем окружении

- Все JSON-файлы синтаксически корректны.
- Все `.csproj` и `.props` являются корректным XML.
- Все PowerShell-скрипты проходят синтаксический парсер.

## Ограничение проверки

В текущем окружении установлен .NET runtime 8.0, но отсутствует любой .NET SDK; запуск `python.exe` и `py.exe` запрещён окружением. Поэтому restore, компиляция и тесты здесь не выполнялись. Для закрытия последнего критерия нужны .NET 8 SDK, .NET Framework 4.8 targeting pack и Python 3.11+, после чего следует запустить:

```powershell
./scripts/bootstrap.ps1
./scripts/build.ps1
./scripts/test.ps1
```

После успешного выполнения этих команд этап 0 можно считать полностью верифицированным и переходить к WebSocket bridge этапа 1.
