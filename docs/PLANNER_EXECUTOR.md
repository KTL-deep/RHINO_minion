# Planner / Executor

## Поток выполнения

```text
Frontend prompt
→ POST /api/sessions/{session_id}/prompt
→ get_scene через Rhino bridge
→ Planner
→ GeometryPlan validation
→ execute_batch через Rhino bridge
→ UI-thread RhinoCommon operations
→ result + plan во frontend
```

## Провайдеры

### deterministic

Используется по умолчанию, не требует сети или ключа. Поддерживает явное создание box с тремя размерами и единицами `m`, `cm`, `mm`, а также русскими эквивалентами. Нужен для smoke-тестов всего контура.

### openai

Использует OpenAI Responses API и structured JSON output. Имя модели и ключ задаются явно через окружение:

```dotenv
RHINO_MINION_PLANNER=openai
RHINO_MINION_OPENAI_API_KEY=...
RHINO_MINION_OPENAI_MODEL=...
```

Ответ модели преобразуется в `GeometryPlan` и валидируется Pydantic. Допускаются только `create_box`, `create_polyline`, `extrude` и `transform`. Даже валидный план повторно проверяется C# bridge до изменения RhinoDoc.

## Ограничения безопасности

- Максимум 20 операций AI-плана по умолчанию.
- Максимум 4000 символов в пользовательском prompt.
- Модель не получает инструмент исполнения произвольного кода.
- GUID разрешено брать только из переданного scene graph.
- OpenAI request использует `store: false`.
- API-ключ не возвращается frontend и не журналируется.
- Ошибка планирования не изменяет RhinoDoc.

## API response

```json
{
  "message": "Create a 30 × 20 × 80 box.",
  "plan": {
    "summary": "Create a 30 × 20 × 80 box.",
    "operations": [
      {
        "operation_id": "create-box-1",
        "tool": "create_box",
        "arguments": {}
      }
    ]
  },
  "rhino": {
    "ok": true,
    "result": { "operations": [] }
  }
}
```
