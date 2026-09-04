# Точка продолжения RHINO Minion

Обновлено: 4 сентября 2026 года.

## Где остановились

Завершён каркас V0, рабочий локальный bridge и первый пакет расширенного Rhino API.
Следующая работа начинается с **категории 2: surface modeling**.

Первый следующий пакет:

1. `create_planar_surface`;
2. `create_surface_from_points`;
3. `loft_curves`;
4. `sweep_one_rail`;
5. `sweep_two_rail`;
6. `revolve_curve`;
7. тесты, planner contracts и Release-сборка для Rhino 8.

## Что готово

### Desktop и frontend

- Отдельное Electron-приложение `350 × 450 px`, всегда поверх окон.
- Интерфейс оставлен только в виде чата: статус Rhino, сообщения и поле ввода.
- Автоматическое обнаружение backend и первой Rhino-сессии.
- Windows NSIS installer и portable build.
- Покупка и управление аккаунтом убраны из desktop-клиента. В дальнейшем при
  отсутствии entitlement приложение открывает отдельный сайт RHINO Minion.

### Backend и AI orchestration

- FastAPI на `127.0.0.1:8766`.
- WebSocket bridge `/ws/rhino`.
- Scene graph с units, tolerance, GUID, type, layer, name, selection и bbox.
- Deterministic planner понимает базовые русские и английские команды.
- Русские команды создания блока, move, rotate и изменения размеров выбранного объекта.
- OpenAI Responses adapter со strict structured output подготовлен, но не активирован:
  `.env`, API key и model пока не настроены.
- Произвольный Python/C#/shell код модели запрещён.

### Rhino plugin

- Target: `net48`, RhinoCommon baseline 8.0; предназначен для всех Rhino 8 SR Windows.
- Reconnecting WebSocket client, UI-thread execution и один Undo record на AI batch.
- Validation до мутации и автоматический Undo при ошибке.
- Runtime discovery через `get_capabilities`.

Готовые инструменты:

- scene: `get_scene`, `get_capabilities`;
- curves: `create_point`, `create_line`, `create_polyline`, `create_circle`,
  `create_arc`, `create_ellipse`, `create_rectangle`, `create_polygon`,
  `create_nurbs_curve`;
- solids: `create_box`, `create_sphere`, `create_cylinder`, `extrude`;
- editing: `transform` с move/rotate/uniform scale/non-uniform scale;
- objects: `duplicate_objects`, `delete_objects`, `set_object_attributes`.

### Авторизация и оплата — каркас

- Keycloak/OIDC Authorization Code + PKCE.
- YooKassa one-time credit checkout, webhook verification и SQLite credit ledger.
- Коммерческий режим не включён и требует отдельного сайта, production deployment,
  merchant credentials и юридических настроек.
- ChatGPT-подписка не используется как API entitlement.

## Что предстоит

1. Surface modeling: planar/point surfaces, loft, sweep1/2, revolve.
2. Solid/curve editing: boolean, trim, split, join, offset, fillet, chamfer.
3. Transformations: mirror, orient, linear/polar arrays.
4. Document organization: groups, materials, colors, user strings.
5. Mesh, SubD и point clouds.
6. Measurements, geometry analysis, selection и viewport capture/control.
7. Import/export с отдельным подтверждением и ограничением путей.
8. Grasshopper adapter с отдельными разрешениями для solve и bake.
9. Динамически строить AI schema из runtime capabilities вместо ручного enum.
10. Расширить русские deterministic-команды или подключить серверный AI gateway.
11. Интеграционные smoke-тесты в Rhino 8.0, пользовательской Rhino 8.16 и последнем SR.
12. Отдельный web-сайт авторизации, тарифов, оплаты и customer portal.

Подробная матрица находится в `docs/RHINO_CAPABILITY_MATRIX.md`.

## Проверенное состояние

- Python: 9 тестов прошли.
- Ruff: все проверки прошли.
- C# Release build: 0 ошибок, 0 предупреждений.
- C# protocol tests: прошли.
- Frontend production build: прошёл.
- Electron installer: собран.
- Backend, desktop и Rhino bridge проверялись вместе на этой машине.

## Последние артефакты

- Rhino plugin: `artifacts/rhino-curves/RhinoMinion.rhp`.
- Desktop installer: `RHINO_minion_frontend/release/RHINO Minion Setup 0.1.0.exe`.
- Portable desktop: `RHINO_minion_frontend/release/win-unpacked/RHINO Minion.exe`.

Для применения нового Rhino plugin необходимо закрыть Rhino, загрузить свежий `.rhp`
через `PlugInManager` и снова открыть Rhino. Сборку нельзя перезаписывать, пока Rhino
держит DLL загруженными; поэтому промежуточные версии собирались в `artifacts/`.

## Как возобновить работу

```powershell
cd C:\Users\KTL\PycharmProjects\RHINO_minion
.\.venv\Scripts\python.exe -m pytest
dotnet build RHINO_minion.sln --configuration Release
```

Запуск backend:

```powershell
.\.venv\Scripts\python.exe -m uvicorn rhino_minion.main:app `
  --app-dir src/backend --host 127.0.0.1 --port 8766
```

Запуск desktop в dev-режиме:

```powershell
cd RHINO_minion_frontend
npm.cmd run desktop
```

## Зафиксированные ограничения безопасности

- Не предоставлять AI общий `RhinoApp.RunScript`, RhinoPython, C# compilation или shell.
- Все операции оформлять как строго типизированные tools.
- Import/export, save, render, Grasshopper solve и bake требуют отдельной permission policy.
- Не передавать OpenAI/provider keys во frontend или Rhino plugin.
