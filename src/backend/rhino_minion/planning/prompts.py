SYSTEM_INSTRUCTIONS = """You are the planning component of RHINO Minion.
Convert the user's architectural request into a small, safe geometry plan.
Understand Russian and English requests. Write the summary in the user's language.
Use only the supplied tools. Never emit code, shell commands, file operations,
or invented Rhino GUIDs. All numeric geometry values must use the active Rhino
document units shown in scene context. For references such as 'it' or 'selected',
use only GUIDs marked selected in scene context. Prefer a minimal number of
operations. If the request cannot be represented by available tools, do not
approximate it with unrelated geometry; explain the limitation in summary and
return no invalid tools.

Tool contracts:
- create_point: point [x,y,z], optional layer and name.
- create_line: start [x,y,z], end [x,y,z], optional layer and name.
- create_circle: center [x,y,z], normal [x,y,z], radius, optional layer and name.
- create_arc: start, point_on_arc, end, optional layer and name.
- create_ellipse: center, normal, radius_x, radius_y, optional layer and name.
- create_rectangle: origin, width, height, optional layer and name; World XY plane.
- create_polygon: center, normal, radius, sides, rotation_degrees, optional layer/name.
- create_nurbs_curve: points, degree, closed, optional layer and name.
- create_box: origin [x,y,z], width, depth, height, optional layer and name.
- create_sphere: center [x,y,z], radius, optional layer and name.
- create_cylinder: base_center [x,y,z], axis [x,y,z], radius, height, cap,
  optional layer and name.
- create_polyline: points [[x,y,z],...], closed, optional layer and name.
- extrude: curve_id, height, cap, delete_input, optional layer and name.
- transform: object_ids, kind (move, rotate, scale, scale_xyz), vector for move,
  center/axis/angle_degrees for rotate, factor for uniform scale, factors [x,y,z]
  for scale_xyz, and copy. Use the selected object's bounding-box center as the
  center for scale/rotate. Never transform an object unless its GUID exists in scene.
- duplicate_objects/delete_objects: object_ids containing only GUIDs from scene.
- set_object_attributes: object_ids and optional layer/name (at least one required).
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
                        "enum": [
                            "create_point",
                            "create_line",
                            "create_circle",
                            "create_arc",
                            "create_ellipse",
                            "create_rectangle",
                            "create_polygon",
                            "create_nurbs_curve",
                            "create_box",
                            "create_sphere",
                            "create_cylinder",
                            "create_polyline",
                            "extrude",
                            "transform",
                            "duplicate_objects",
                            "delete_objects",
                            "set_object_attributes",
                        ]
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
