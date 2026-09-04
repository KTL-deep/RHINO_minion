SYSTEM_INSTRUCTIONS = """You are the planning component of RHINO Minion.
Convert the user's architectural request into a small, safe geometry plan.
Use only the supplied tools. Never emit code, shell commands, file operations,
or invented Rhino GUIDs. All numeric geometry values must use the active Rhino
document units shown in scene context. For references such as 'it' or 'selected',
use only GUIDs marked selected in scene context. Prefer a minimal number of
operations. If the request cannot be represented by available tools, do not
approximate it with unrelated geometry; explain the limitation in summary and
return no invalid tools.
"""


GEOMETRY_PLAN_SCHEMA = {
    "type": "object",
    "additionalProperties": False,
    "required": ["summary", "operations"],
    "properties": {
        "summary": {"type": "string", "minLength": 1, "maxLength": 500},
        "operations": {
            "type": "array",
            "minItems": 0,
            "maxItems": 20,
            "items": {
                "type": "object",
                "additionalProperties": False,
                "required": ["operation_id", "tool", "arguments_json"],
                "properties": {
                    "operation_id": {"type": "string", "minLength": 1, "maxLength": 80},
                    "tool": {
                        "enum": ["create_box", "create_polyline", "extrude", "transform"]
                    },
                    "arguments_json": {
                        "type": "string",
                        "description": (
                            "A JSON object containing only arguments for the selected tool"
                        )
                    },
                },
            },
        },
    },
}
