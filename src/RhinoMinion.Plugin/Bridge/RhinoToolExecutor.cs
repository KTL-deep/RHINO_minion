using System.Text.Json;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoMinion.Protocol;

namespace RhinoMinion.Plugin.Bridge;

internal static class RhinoToolExecutor
{
    private static readonly HashSet<string> SupportedTools = new(StringComparer.Ordinal)
    {
        "create_box",
        "create_polyline",
        "extrude",
        "transform"
    };

    public static void Validate(ToolOperation operation)
    {
        if (!SupportedTools.Contains(operation.Tool))
        {
            throw new ToolException(ErrorCode.InvalidArgument, $"Unknown tool: {operation.Tool}");
        }

        switch (operation.Tool)
        {
            case "create_box":
                ValidateCreateBox(Deserialize<CreateBoxArguments>(operation));
                break;
            case "create_polyline":
                ValidatePolyline(Deserialize<CreatePolylineArguments>(operation));
                break;
            case "extrude":
                ValidateExtrude(Deserialize<ExtrudeArguments>(operation));
                break;
            case "transform":
                ValidateTransform(Deserialize<TransformArguments>(operation));
                break;
        }
    }

    public static IReadOnlyList<Guid> Execute(RhinoDoc document, ToolOperation operation) =>
        operation.Tool switch
        {
            "create_box" => CreateBox(document, Deserialize<CreateBoxArguments>(operation)),
            "create_polyline" => CreatePolyline(document, Deserialize<CreatePolylineArguments>(operation)),
            "extrude" => Extrude(document, Deserialize<ExtrudeArguments>(operation)),
            "transform" => TransformObjects(document, Deserialize<TransformArguments>(operation)),
            _ => throw new ToolException(ErrorCode.InvalidArgument, $"Unknown tool: {operation.Tool}")
        };

    public static JsonElement GetScene(RhinoDoc? document, JsonElement payload)
    {
        if (document is null)
        {
            throw new ToolException(ErrorCode.DocumentUnavailable, "No active Rhino document.");
        }

        var selectionOnly = ReadBoolean(payload, "selection_only", false);
        var maxObjects = Math.Max(1, Math.Min(ReadInteger(payload, "max_objects", 500), 2000));
        var source = selectionOnly
            ? document.Objects.GetSelectedObjects(false, false)
            : document.Objects.GetObjectList(ObjectType.AnyObject);
        var objects = new List<object>();
        var truncated = false;

        foreach (var rhinoObject in source)
        {
            if (objects.Count >= maxObjects)
            {
                truncated = true;
                break;
            }

            var boundingBox = rhinoObject.Geometry.GetBoundingBox(true);
            var layer = document.Layers[rhinoObject.Attributes.LayerIndex];
            objects.Add(new
            {
                id = rhinoObject.Id.ToString(),
                name = rhinoObject.Attributes.Name ?? string.Empty,
                type = rhinoObject.ObjectType.ToString(),
                layer = layer?.FullPath ?? string.Empty,
                selected = rhinoObject.IsSelected(false) != 0,
                bbox = new
                {
                    min = new[] { boundingBox.Min.X, boundingBox.Min.Y, boundingBox.Min.Z },
                    max = new[] { boundingBox.Max.X, boundingBox.Max.Y, boundingBox.Max.Z }
                }
            });
        }

        return RhinoRequestDispatcher.ToJsonElement(new
        {
            document_id = document.RuntimeSerialNumber.ToString(),
            units = document.ModelUnitSystem.ToString(),
            absolute_tolerance = document.ModelAbsoluteTolerance,
            angle_tolerance_degrees = RhinoMath.ToDegrees(document.ModelAngleToleranceRadians),
            objects,
            truncated
        });
    }

    private static IReadOnlyList<Guid> CreateBox(RhinoDoc document, CreateBoxArguments arguments)
    {
        ValidateCreateBox(arguments);
        var origin = Point(arguments.Origin, "origin");
        var box = new Box(
            Plane.WorldXY,
            new Interval(origin.X, origin.X + arguments.Width),
            new Interval(origin.Y, origin.Y + arguments.Depth),
            new Interval(origin.Z, origin.Z + arguments.Height));
        var id = document.Objects.AddBrep(box.ToBrep(), Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "box");
    }

    private static IReadOnlyList<Guid> CreatePolyline(
        RhinoDoc document,
        CreatePolylineArguments arguments)
    {
        ValidatePolyline(arguments);
        var points = arguments.Points.Select(point => Point(point, "points")).ToList();
        if (arguments.Closed && points[0].DistanceToSquared(points[points.Count - 1]) > 0)
        {
            points.Add(points[0]);
        }
        var id = document.Objects.AddCurve(
            new PolylineCurve(points),
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "polyline");
    }

    private static IReadOnlyList<Guid> Extrude(RhinoDoc document, ExtrudeArguments arguments)
    {
        ValidateExtrude(arguments);
        var source = document.Objects.FindId(arguments.CurveId)
            ?? throw new ToolException(ErrorCode.ObjectNotFound, $"Curve {arguments.CurveId} was not found.");
        if (source.Geometry is not Curve curve)
        {
            throw new ToolException(ErrorCode.InvalidArgument, $"Object {arguments.CurveId} is not a curve.");
        }
        if (arguments.Cap && !curve.IsClosed)
        {
            throw new ToolException(ErrorCode.InvalidArgument, "A capped extrusion requires a closed curve.");
        }

        var extrusion = global::Rhino.Geometry.Extrusion.Create(curve, arguments.Height, arguments.Cap)
            ?? throw new ToolException(ErrorCode.GeometryFailed, "Rhino could not create the extrusion.");
        var id = document.Objects.AddExtrusion(
            extrusion,
            Attributes(document, arguments.Layer, arguments.Name));
        Created(id, "extrusion");
        if (arguments.DeleteInput && !document.Objects.Delete(arguments.CurveId, true))
        {
            throw new ToolException(ErrorCode.GeometryFailed, "Extrusion was created but input deletion failed.");
        }
        return new[] { id };
    }

    private static IReadOnlyList<Guid> TransformObjects(
        RhinoDoc document,
        TransformArguments arguments)
    {
        ValidateTransform(arguments);
        var transform = CreateTransform(arguments);
        var results = new List<Guid>(arguments.ObjectIds.Length);
        foreach (var sourceId in arguments.ObjectIds)
        {
            if (document.Objects.FindId(sourceId) is null)
            {
                throw new ToolException(ErrorCode.ObjectNotFound, $"Object {sourceId} was not found.");
            }
            var resultId = document.Objects.Transform(sourceId, transform, !arguments.Copy);
            if (resultId == Guid.Empty)
            {
                throw new ToolException(ErrorCode.GeometryFailed, $"Could not transform object {sourceId}.");
            }
            results.Add(resultId);
        }
        return results;
    }

    private static Rhino.Geometry.Transform CreateTransform(TransformArguments arguments)
    {
        var center = Point(arguments.Center ?? new[] { 0d, 0d, 0d }, "center");
        return arguments.Kind switch
        {
            "move" => Rhino.Geometry.Transform.Translation(Vector(arguments.Vector, "vector")),
            "rotate" => Rhino.Geometry.Transform.Rotation(
                RhinoMath.ToRadians(arguments.AngleDegrees!.Value),
                Vector(arguments.Axis, "axis"),
                center),
            "scale" => Rhino.Geometry.Transform.Scale(center, arguments.Factor!.Value),
            _ => throw new ToolException(ErrorCode.InvalidArgument, "Transform kind must be move, rotate or scale.")
        };
    }

    private static ObjectAttributes Attributes(RhinoDoc document, string? layerPath, string? name)
    {
        var attributes = new ObjectAttributes { Name = name ?? string.Empty };
        if (!string.IsNullOrWhiteSpace(layerPath))
        {
            var layerIndex = document.Layers.FindByFullPath(layerPath, -1);
            if (layerIndex < 0)
            {
                layerIndex = document.Layers.Add(new Layer { Name = layerPath.Replace("::", "_") });
            }
            if (layerIndex < 0)
            {
                throw new ToolException(ErrorCode.GeometryFailed, $"Could not create layer {layerPath}.");
            }
            attributes.LayerIndex = layerIndex;
        }
        return attributes;
    }

    private static void ValidateCreateBox(CreateBoxArguments arguments)
    {
        Point(arguments.Origin, "origin");
        Positive(arguments.Width, "width");
        Positive(arguments.Depth, "depth");
        Positive(arguments.Height, "height");
    }

    private static void ValidatePolyline(CreatePolylineArguments arguments)
    {
        if (arguments.Points is null || arguments.Points.Length < (arguments.Closed ? 3 : 2))
        {
            throw new ToolException(ErrorCode.InvalidArgument, "Polyline has too few points.");
        }
        foreach (var point in arguments.Points)
        {
            Point(point, "points");
        }
    }

    private static void ValidateExtrude(ExtrudeArguments arguments)
    {
        if (arguments.CurveId == Guid.Empty)
        {
            throw new ToolException(ErrorCode.InvalidArgument, "curve_id is required.");
        }
        Finite(arguments.Height, "height");
        if (Math.Abs(arguments.Height) < 1e-12)
        {
            throw new ToolException(ErrorCode.InvalidArgument, "height must not be zero.");
        }
    }

    private static void ValidateTransform(TransformArguments arguments)
    {
        if (arguments.ObjectIds is null || arguments.ObjectIds.Length == 0 || arguments.ObjectIds.Any(id => id == Guid.Empty))
        {
            throw new ToolException(ErrorCode.InvalidArgument, "object_ids must contain valid GUIDs.");
        }
        switch (arguments.Kind)
        {
            case "move":
                Vector(arguments.Vector, "vector");
                break;
            case "rotate":
                Vector(arguments.Axis, "axis");
                Point(arguments.Center ?? new[] { 0d, 0d, 0d }, "center");
                if (arguments.AngleDegrees is null)
                {
                    throw new ToolException(ErrorCode.InvalidArgument, "angle_degrees is required.");
                }
                Finite(arguments.AngleDegrees.Value, "angle_degrees");
                break;
            case "scale":
                Point(arguments.Center ?? new[] { 0d, 0d, 0d }, "center");
                if (arguments.Factor is null)
                {
                    throw new ToolException(ErrorCode.InvalidArgument, "factor is required.");
                }
                Positive(arguments.Factor.Value, "factor");
                break;
            default:
                throw new ToolException(ErrorCode.InvalidArgument, "Transform kind must be move, rotate or scale.");
        }
    }

    private static T Deserialize<T>(ToolOperation operation)
    {
        try
        {
            return operation.Arguments.Deserialize<T>(JsonDefaults.Options)
                ?? throw new ToolException(ErrorCode.InvalidArgument, "Tool arguments are required.");
        }
        catch (JsonException exception)
        {
            throw new ToolException(ErrorCode.InvalidArgument, exception.Message);
        }
    }

    private static Point3d Point(double[]? values, string field)
    {
        if (values is null || values.Length != 3)
        {
            throw new ToolException(ErrorCode.InvalidArgument, $"{field} must contain three numbers.");
        }
        foreach (var value in values)
        {
            Finite(value, field);
        }
        return new Point3d(values[0], values[1], values[2]);
    }

    private static Vector3d Vector(double[]? values, string field)
    {
        var point = Point(values, field);
        var vector = new Vector3d(point.X, point.Y, point.Z);
        if (vector.IsTiny())
        {
            throw new ToolException(ErrorCode.InvalidArgument, $"{field} must not be zero.");
        }
        return vector;
    }

    private static void Positive(double value, string field)
    {
        Finite(value, field);
        if (value <= 0)
        {
            throw new ToolException(ErrorCode.InvalidArgument, $"{field} must be greater than zero.");
        }
    }

    private static void Finite(double value, string field)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ToolException(ErrorCode.InvalidArgument, $"{field} must be finite.");
        }
    }

    private static IReadOnlyList<Guid> Created(Guid id, string geometryType)
    {
        if (id == Guid.Empty)
        {
            throw new ToolException(ErrorCode.GeometryFailed, $"Rhino could not create {geometryType}.");
        }
        return new[] { id };
    }

    private static bool ReadBoolean(JsonElement payload, string name, bool fallback) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static int ReadInteger(JsonElement payload, string name, int fallback) =>
        payload.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : fallback;
}
