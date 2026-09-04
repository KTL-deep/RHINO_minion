# RHINO Minion

AI-ассистент для управляемого создания и редактирования геометрии в Rhino 8.

Проект находится на этапе V0. Репозиторий содержит каркас RhinoCommon-плагина, общий JSON-протокол, Python backend, контрактные схемы и тестовую инфраструктуру.

## Требования

- Windows 10/11 и Rhino 8;
- .NET 8 SDK и .NET Framework 4.8 targeting pack;
- Python 3.11+;
- Node.js 20+ и npm;
- PowerShell 7 рекомендуется для скриптов разработки.

Плагин должен работать на всех сервис-релизах Rhino 8. Для этого он таргетирует `net48`, компилируется против базового RhinoCommon `8.0.23304.9001` и не использует API, добавленные в более поздних Rhino 8 SR. Версии централизованно закреплены в `Directory.Packages.props`.

## Быстрый старт

```powershell
./scripts/bootstrap.ps1
./scripts/build.ps1
./scripts/test.ps1
```

Запуск backend после bootstrap:

```powershell
./.venv/Scripts/python.exe -m uvicorn rhino_minion.main:app --app-dir src/backend --reload
```

Проверка: `GET http://127.0.0.1:8766/health`.

Собранный плагин имеет расширение `.rhp`. Он регистрирует команду `RhinoMinion` и автоматически подключается к `ws://127.0.0.1:8766/ws/rhino`.

После запуска backend и загрузки плагина:

```powershell
# Показать подключённые Rhino-сессии
./.venv/Scripts/python.exe examples/client.py sessions

# Получить сцену (подставьте session_id из предыдущей команды)
./.venv/Scripts/python.exe examples/client.py scene --session <session_id>

# Создать тестовый box 30 × 20 × 80 м в миллиметровом документе
./.venv/Scripts/python.exe examples/client.py box --session <session_id>
```

## Структура

```text
src/RhinoMinion.Plugin/    Rhino 8 plugin
src/RhinoMinion.Protocol/  общие C# DTO протокола
src/backend/               FastAPI/Pydantic backend
RHINO_minion_frontend/     React/Vite landing и V0 control panel
tests/                     unit и contract tests
schemas/                   JSON Schema протокола
examples/requests/         эталонные сообщения
docs/                      архитектура и план разработки
scripts/                   bootstrap/build/test
```

Подробный план: [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md).

## Конфигурация

Скопируйте `.env.example` в `.env`. Секреты и API-ключи не должны попадать в git. На этапе 0 реальный LLM provider не подключён.

## Статус этапа 0

- [x] C# solution и проекты Plugin/Protocol;
- [x] Python package backend;
- [x] спецификация и JSON Schema протокола;
- [x] unit/contract test skeleton;
- [x] команды bootstrap/build/test;
- [x] базовый CI;
- [ ] подтверждение сборки на рабочей машине с .NET SDK и Python.

## Статус этапа 1

- [x] reconnecting WebSocket-клиент внутри Rhino plugin;
- [x] реестр подключённых Rhino-сессий в backend;
- [x] `get_scene`, `create_box`, `create_polyline`, `extrude`, `transform`;
- [x] предварительная валидация batch;
- [x] выполнение мутаций на Rhino UI thread;
- [x] один Undo record на batch и автоматический Undo при ошибке;
- [x] структурированные ошибки и GUID результатов;
- [x] минимальный тестовый клиент;
- [ ] runtime-smoke в Rhino 8 после установки toolchain.
