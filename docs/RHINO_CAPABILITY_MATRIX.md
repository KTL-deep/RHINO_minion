# Матрица возможностей Rhino

Цель — предоставить AI широкий, но типизированный доступ к Rhino 8 без выполнения
произвольного кода и командной строки. Плагин сообщает фактически доступные инструменты
через `get_capabilities`; planner не должен предлагать инструменты, которых нет в runtime.

Статусы: **готово**, **следующая очередь**, **запланировано**, **только с подтверждением**.

## Документ и сцена

| Возможности | Статус |
|---|---|
| Единицы, tolerance, объекты, GUID, selection, bbox | готово |
| Capabilities discovery | готово |
| Слои, группы, материалы, user strings | следующая очередь |
| Named views, construction planes, layouts | запланировано |
| Import/export/save | только с подтверждением |

## Создание геометрии

| Возможности | Статус |
|---|---|
| Point, line, polyline, circle | готово |
| Box, sphere, cylinder | готово |
| Arc, ellipse, rectangle, polygon, NURBS curve | готово |
| Plane/edge/network surfaces, extrusion, loft, sweep, revolve | следующая очередь |
| Text, dimensions, annotations, hatches | запланировано |

## Редактирование

| Возможности | Статус |
|---|---|
| Move, rotate, uniform/non-uniform scale, copy | готово |
| Duplicate, delete, name/layer | готово |
| Mirror, orient, linear/polar arrays | следующая очередь |
| Trim, split, join, extend, offset, fillet, chamfer | следующая очередь |
| Control points, rebuild, simplify, seam/direction | запланировано |

## Solid и surface operations

| Возможности | Статус |
|---|---|
| Boolean union/difference/intersection | следующая очередь |
| Cap planar holes, shell, offset surface, fillet/chamfer edges | следующая очередь |
| Contours, sections, intersections, projections | запланировано |

## Mesh, SubD и point clouds

| Возможности | Статус |
|---|---|
| Mesh primitives/conversion/repair/reduce | запланировано |
| SubD primitives/conversion/editing | запланировано |
| Point cloud create/filter/sample | запланировано |

## Анализ и представление

| Возможности | Статус |
|---|---|
| Distance, length, area, volume, centroid | следующая очередь |
| Geometry validity and naked-edge diagnostics | следующая очередь |
| Selection, zoom, display mode, viewport capture | следующая очередь |
| Render settings, lights, render execution | только с подтверждением |

## Grasshopper и расширения

| Возможности | Статус |
|---|---|
| Открыть/читать GH document и параметры | запланировано |
| Создать типизированный GH graph | запланировано |
| Решить definition и bake результата | только с подтверждением |
| Сторонние plug-ins | отдельные adapters по обнаруженным версиям |

## Ограничения безопасности

- `RunScript`, RhinoPython, C# compilation и shell не выдаются модели как общий инструмент.
- Запись файлов, импорт, экспорт, render и bake требуют отдельной политики подтверждений.
- Каждая мутация проходит validation, UI thread и один Rhino Undo record.
- GUID для редактирования должен существовать в актуальном scene graph.
- Матрица означает покрытие публичных пользовательских сценариев, а не экспорт каждой
  внутренней функции RhinoCommon как отдельного AI tool.
