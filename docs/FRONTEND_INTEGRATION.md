# Frontend integration

`RHINO_minion_frontend` совмещает landing page и рабочую панель V0.

## Возможности

- проверка `/health`;
- автоматическое обновление списка подключённых Rhino-сессий;
- выбор сессии;
- чтение scene graph;
- отображение числа объектов и единиц документа;
- создание тестового massing box через реальный `execute_batch`;
- пересчёт размеров 30 × 20 × 80 м в единицы активного RhinoDoc;
- состояния offline, waiting, working, success и error;
- адаптивное отображение на узких экранах.

Поле prompt пока выполняет только явно указанный demo prompt. Произвольный текст не имитируется и не преобразуется во frontend: это ответственность будущего Planner/Executor backend.

## Локальная схема

```text
React/Vite :5173
  └── /health, /api (Vite proxy)
        └── FastAPI :8766
              └── /ws/rhino
                    └── Rhino 8 plugin
```

Production frontend может задать `VITE_API_BASE_URL`; backend разрешает browser CORS только для localhost Vite origins.
