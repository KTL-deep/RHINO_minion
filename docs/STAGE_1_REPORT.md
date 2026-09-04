# Отчёт по этапу 1 — Rhino bridge

Дата реализации: 2026-09-04.

## Реализовано

- Rhino plugin автоматически подключается к локальному FastAPI WebSocket server.
- Соединение восстанавливается после запуска или перезапуска backend.
- Handshake передаёт protocol version, session ID, plugin/Rhino version и опциональный общий secret.
- Backend ведёт реестр Rhino-сессий и предоставляет HTTP API тестового управления.
- Реализован компактный `get_scene` с единицами, tolerance, GUID, именем, типом, слоем, selection и bounding box.
- Реализованы `create_box`, `create_polyline`, `extrude` и `transform` (`move`, `rotate`, `scale`).
- До изменения документа валидируются tool names, operation IDs, GUID, числа, размеры и обязательные аргументы.
- Batch ограничен 50 операциями и выполняется в Rhino UI thread.
- Batch создаёт одну Undo-запись; при исключении выполняется автоматический Undo.
- Ответы содержат GUID созданных или преобразованных объектов.
- Реализованы коды ошибок протокола и лимит WebSocket-сообщения 1 MiB.
- Добавлен тестовый клиент `examples/client.py`.

## Локальный запуск

1. Задать одинаковый `RHINO_MINION_BRIDGE_SECRET` для процесса backend и процесса Rhino либо оставить его пустым в локальном dev-режиме.
2. Запустить backend через Uvicorn.
3. Собрать `.rhp`, загрузить его через Rhino PlugInManager и выполнить `RhinoMinion`.
4. Получить session ID командой `python examples/client.py sessions`.
5. Выполнить `scene` и `box` smoke-команды.
6. Нажать `Ctrl+Z` и убедиться, что весь batch отменился одним шагом.

## Требуемая runtime-верификация

Текущий sandbox не содержит .NET SDK, блокирует Python и не запускает Rhino UI. Поэтому реализация прошла только статическую проверку. Перед признанием этапа полностью завершённым необходимо собрать и выполнить smoke-сценарий минимум в пользовательской Rhino 8.16, затем в Rhino 8.0 и последнем Rhino 8 SR.
