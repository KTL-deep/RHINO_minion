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
        "set_object_attributes"
    };

    public static JsonElement GetCapabilities() => RhinoRequestDispatcher.ToJsonElement(new
    {
        tools = SupportedTools.OrderBy(name => name).ToArray(),
        max_operations_per_batch = ProtocolConstants.DefaultMaxOperationsPerBatch,
        arbitrary_code_execution = false
    });

    public static void Validate(ToolOperation operation)
    {
        if (!SupportedTools.Contains(operation.Tool))
        {
            throw new ToolException(ErrorCode.InvalidArgument, $"Unknown tool: {operation.Tool}");
        }

        switch (operation.Tool)
        {
            case "create_point":
                ValidateCreatePoint(Deserialize<CreatePointArguments>(operation));
                break;
            case "create_line":
                ValidateCreateLine(Deserialize<CreateLineArguments>(operation));
                break;
            case "create_circle":
                ValidateCreateCircle(Deserialize<CreateCircleArguments>(operation));
                break;
            case "create_arc":
                ValidateCreateArc(Deserialize<CreateArcArguments>(operation));
                break;
            case "create_ellipse":
                ValidateCreateEllipse(Deserialize<CreateEllipseArguments>(operation));
                break;
            case "create_rectangle":
                ValidateCreateRectangle(Deserialize<CreateRectangleArguments>(operation));
                break;
            case "create_polygon":
                ValidateCreatePolygon(Deserialize<CreatePolygonArguments>(operation));
                break;
            case "create_nurbs_curve":
                ValidateCreateNurbsCurve(Deserialize<CreateNurbsCurveArguments>(operation));
                break;
            case "create_box":
                ValidateCreateBox(Deserialize<CreateBoxArguments>(operation));
                break;
            case "create_sphere":
                ValidateCreateSphere(Deserialize<CreateSphereArguments>(operation));
                break;
            case "create_cylinder":
                ValidateCreateCylinder(Deserialize<CreateCylinderArguments>(operation));
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
            case "duplicate_objects":
            case "delete_objects":
                ValidateObjectIds(Deserialize<ObjectIdsArguments>(operation).ObjectIds);
                break;
            case "set_object_attributes":
                ValidateSetObjectAttributes(Deserialize<SetObjectAttributesArguments>(operation));
                break;
        }
    }

    public static IReadOnlyList<Guid> Execute(RhinoDoc document, ToolOperation operation) =>
        operation.Tool switch
        {
            "create_point" => CreatePoint(document, Deserialize<CreatePointArguments>(operation)),
            "create_line" => CreateLine(document, Deserialize<CreateLineArguments>(operation)),
            "create_circle" => CreateCircle(document, Deserialize<CreateCircleArguments>(operation)),
            "create_arc" => CreateArc(document, Deserialize<CreateArcArguments>(operation)),
            "create_ellipse" => CreateEllipse(
                document,
                Deserialize<CreateEllipseArguments>(operation)),
            "create_rectangle" => CreateRectangle(
                document,
                Deserialize<CreateRectangleArguments>(operation)),
            "create_polygon" => CreatePolygon(
                document,
                Deserialize<CreatePolygonArguments>(operation)),
            "create_nurbs_curve" => CreateNurbsCurve(
                document,
                Deserialize<CreateNurbsCurveArguments>(operation)),
            "create_box" => CreateBox(document, Deserialize<CreateBoxArguments>(operation)),
            "create_sphere" => CreateSphere(document, Deserialize<CreateSphereArguments>(operation)),
            "create_cylinder" => CreateCylinder(document, Deserialize<CreateCylinderArguments>(operation)),
            "create_polyline" => CreatePolyline(document, Deserialize<CreatePolylineArguments>(operation)),
            "extrude" => Extrude(document, Deserialize<ExtrudeArguments>(operation)),
            "transform" => TransformObjects(document, Deserialize<TransformArguments>(operation)),
            "duplicate_objects" => DuplicateObjects(document, Deserialize<ObjectIdsArguments>(operation)),
            "delete_objects" => DeleteObjects(document, Deserialize<ObjectIdsArguments>(operation)),
            "set_object_attributes" => SetObjectAttributes(
                document,
                Deserialize<SetObjectAttributesArguments>(operation)),
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

    private static IReadOnlyList<Guid> CreatePoint(
        RhinoDoc document,
        CreatePointArguments arguments)
    {
        var id = document.Objects.AddPoint(
            Point(arguments.Point, "point"),
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "point");
    }

    private static IReadOnlyList<Guid> CreateLine(RhinoDoc document, CreateLineArguments arguments)
    {
        var line = new Line(Point(arguments.Start, "start"), Point(arguments.End, "end"));
        var id = document.Objects.AddLine(
            line,
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "line");
    }

    private static IReadOnlyList<Guid> CreateCircle(
        RhinoDoc document,
        CreateCircleArguments arguments)
    {
        var plane = new Plane(
            Point(arguments.Center, "center"),
            Vector(arguments.Normal, "normal"));
        var id = document.Objects.AddCircle(
            new Circle(plane, arguments.Radius),
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "circle");
    }

    private static IReadOnlyList<Guid> CreateArc(RhinoDoc document, CreateArcArguments arguments)
    {
        var arc = new Arc(
            Point(arguments.Start, "start"),
            Point(arguments.PointOnArc, "point_on_arc"),
            Point(arguments.End, "end"));
        var id = document.Objects.AddArc(
            arc,
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "arc");
    }

    private static IReadOnlyList<Guid> CreateEllipse(
        RhinoDoc document,
        CreateEllipseArguments arguments)
    {
        var plane = new Plane(
            Point(arguments.Center, "center"),
            Vector(arguments.Normal, "normal"));
        var id = document.Objects.AddEllipse(
            new Ellipse(plane, arguments.RadiusX, arguments.RadiusY),
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "ellipse");
    }

    private static IReadOnlyList<Guid> CreateRectangle(
        RhinoDoc document,
        CreateRectangleArguments arguments)
    {
        var origin = Point(arguments.Origin, "origin");
        var points = new[]
        {
            origin,
            origin + new Vector3d(arguments.Width, 0, 0),
            origin + new Vector3d(arguments.Width, arguments.Height, 0),
            origin + new Vector3d(0, arguments.Height, 0),
            origin
        };
        var id = document.Objects.AddPolyline(
            points,
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "rectangle");
    }

    private static IReadOnlyList<Guid> CreatePolygon(
        RhinoDoc document,
        CreatePolygonArguments arguments)
    {
        var plane = new Plane(
            Point(arguments.Center, "center"),
            Vector(arguments.Normal, "normal"));
        var rotation = RhinoMath.ToRadians(arguments.RotationDegrees);
        var points = Enumerable.Range(0, arguments.Sides)
            .Select(index =>
            {
                var angle = rotation + 2 * Math.PI * index / arguments.Sides;
                return plane.PointAt(
                    arguments.Radius * Math.Cos(angle),
                    arguments.Radius * Math.Sin(angle));
            })
            .Append(plane.PointAt(
                arguments.Radius * Math.Cos(rotation),
                arguments.Radius * Math.Sin(rotation)))
            .ToArray();
        var id = document.Objects.AddPolyline(
            points,
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "polygon");
    }

    private static IReadOnlyList<Guid> CreateNurbsCurve(
        RhinoDoc document,
        CreateNurbsCurveArguments arguments)
    {
        var points = arguments.Points.Select(point => Point(point, "points")).ToList();
        if (arguments.Closed && points[0].DistanceToSquared(points[points.Count - 1]) > 0)
        {
            points.Add(points[0]);
        }
        var curve = NurbsCurve.Create(false, arguments.Degree, points)
            ?? throw new ToolException(ErrorCode.GeometryFailed, "Could not create NURBS curve.");
        var id = document.Objects.AddCurve(
            curve,
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "NURBS curve");
    }

    private static IReadOnlyList<Guid> CreateSphere(
        RhinoDoc document,
        CreateSphereArguments arguments)
    {
        var sphere = new Sphere(Point(arguments.Center, "center"), arguments.Radius);
        var id = document.Objects.AddSphere(
            sphere,
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "sphere");
    }

    private static IReadOnlyList<Guid> CreateCylinder(
        RhinoDoc document,
        CreateCylinderArguments arguments)
    {
        var center = Point(arguments.BaseCenter, "base_center");
        var axis = Vector(arguments.Axis, "axis");
        axis.Unitize();
        var cylinder = new Cylinder(
            new Circle(new Plane(center, axis), arguments.Radius),
            arguments.Height);
        var id = document.Objects.AddBrep(
            cylinder.ToBrep(arguments.Cap, arguments.Cap),
            Attributes(document, arguments.Layer, arguments.Name));
        return Created(id, "cylinder");
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

    private static IReadOnlyList<Guid> DuplicateObjects(
        RhinoDoc document,
        ObjectIdsArguments arguments)
    {
        ValidateObjectIds(arguments.ObjectIds);
        var results = new List<Guid>(arguments.ObjectIds.Length);
        foreach (var sourceId in arguments.ObjectIds)
        {
            var source = document.Objects.FindId(sourceId)
                ?? throw new ToolException(
                    ErrorCode.ObjectNotFound,
                    $"Object {sourceId} was not found.");
            var id = document.Objects.Add(source.Geometry.Duplicate(), source.Attributes.Duplicate());
            results.Add(Created(id, "duplicate")[0]);
        }
        return results;
    }

    private static IReadOnlyList<Guid> DeleteObjects(
        RhinoDoc document,
        ObjectIdsArguments arguments)
    {
        ValidateObjectIds(arguments.ObjectIds);
        foreach (var id in arguments.ObjectIds)
        {
            if (document.Objects.FindId(id) is null)
            {
                throw new ToolException(ErrorCode.ObjectNotFound, $"Object {id} was not found.");
            }
            if (!document.Objects.Delete(id, true))
            {
                throw new ToolException(ErrorCode.GeometryFailed, $"Could not delete object {id}.");
            }
        }
        return arguments.ObjectIds;
    }

    private static IReadOnlyList<Guid> SetObjectAttributes(
        RhinoDoc document,
        SetObjectAttributesArguments arguments)
    {
        ValidateSetObjectAttributes(arguments);
        foreach (var id in arguments.ObjectIds)
        {
            var source = document.Objects.FindId(id)
                ?? throw new ToolException(ErrorCode.ObjectNotFound, $"Object {id} was not found.");
            var attributes = source.Attributes.Duplicate();
            if (arguments.Name is not null)
            {
                attributes.Name = arguments.Name;
            }
            if (arguments.Layer is not null)
            {
                attributes.LayerIndex = LayerIndex(document, arguments.Layer);
            }
            if (!document.Objects.ModifyAttributes(id, attributes, true))
            {
                throw new ToolException(
                    ErrorCode.GeometryFailed,
                    $"Could not update attributes for object {id}.");
            }
        }
        return arguments.ObjectIds;
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
            "scale_xyz" => Rhino.Geometry.Transform.Scale(
                new Plane(center, Vector3d.XAxis, Vector3d.YAxis),
                arguments.Factors![0],
                arguments.Factors[1],
                arguments.Factors[2]),
            _ => throw new ToolException(
                ErrorCode.InvalidArgument,
                "Transform kind must be move, rotate, scale or scale_xyz.")
        };
    }

    private static ObjectAttributes Attributes(RhinoDoc document, string? layerPath, string? name)
    {
        var attributes = new ObjectAttributes { Name = name ?? string.Empty };
        if (!string.IsNullOrWhiteSpace(layerPath))
        {
            attributes.LayerIndex = LayerIndex(document, layerPath!);
        }
        return attributes;
    }

    private static int LayerIndex(RhinoDoc document, string layerPath)
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
        return layerIndex;
    }

    private static void ValidateCreatePoint(CreatePointArguments arguments) =>
        Point(arguments.Point, "point");

    private static void ValidateCreateLine(CreateLineArguments arguments)
    {
        var start = Point(arguments.Start, "start");
        var end = Point(arguments.End, "end");
        if (start.DistanceToSquared(end) < 1e-24)
        {
            throw new ToolException(ErrorCode.InvalidArgument, "Line endpoints must differ.");
        }
    }

    private static void ValidateCreateCircle(CreateCircleArguments arguments)
    {
        Point(arguments.Center, "center");
        Vector(arguments.Normal, "normal");
        Positive(arguments.Radius, "radius");
    }

    private static void ValidateCreateArc(CreateArcArguments arguments)
    {
        var start = Point(arguments.Start, "start");
        var middle = Point(arguments.PointOnArc, "point_on_arc");
        var end = Point(arguments.End, "end");
        var arc = new Arc(start, middle, end);
        if (!arc.IsValid)
        {
            throw new ToolException(ErrorCode.InvalidArgument, "Arc points are invalid or collinear.");
        }
    }

    private static void ValidateCreateEllipse(CreateEllipseArguments arguments)
    {
        Point(arguments.Center, "center");
        Vector(arguments.Normal, "normal");
        Positive(arguments.RadiusX, "radius_x");
        Positive(arguments.RadiusY, "radius_y");
    }

    private static void ValidateCreateRectangle(CreateRectangleArguments arguments)
    {
        Point(arguments.Origin, "origin");
        Positive(arguments.Width, "width");
        Positive(arguments.Height, "height");
    }

    private static void ValidateCreatePolygon(CreatePolygonArguments arguments)
    {
        Point(arguments.Center, "center");
        Vector(arguments.Normal, "normal");
        Positive(arguments.Radius, "radius");
        Finite(arguments.RotationDegrees, "rotation_degrees");
        if (arguments.Sides is < 3 or > 1000)
        {
            throw new ToolException(ErrorCode.InvalidArgument, "sides must be between 3 and 1000.");
        }
    }

    private static void ValidateCreateNurbsCurve(CreateNurbsCurveArguments arguments)
    {
        if (arguments.Points is null || arguments.Points.Length < 2)
        {
            throw new ToolException(ErrorCode.InvalidArgument, "NURBS curve has too few points.");
        }
        if (arguments.Degree < 1 || arguments.Degree >= arguments.Points.Length)
        {
            throw new ToolException(
                ErrorCode.InvalidArgument,
                "degree must be at least 1 and less than the point count.");
        }
        foreach (var point in arguments.Points)
        {
            Point(point, "points");
        }
    }

    private static void ValidateCreateSphere(CreateSphereArguments arguments)
    {
        Point(arguments.Center, "center");
        Positive(arguments.Radius, "radius");
    }

    private static void ValidateCreateCylinder(CreateCylinderArguments arguments)
    {
        Point(arguments.BaseCenter, "base_center");
        Vector(arguments.Axis, "axis");
        Positive(arguments.Radius, "radius");
        Positive(arguments.Height, "height");
    }

    private static void ValidateObjectIds(Guid[]? objectIds)
    {
        if (objectIds is null || objectIds.Length == 0 || objectIds.Any(id => id == Guid.Empty))
        {
            throw new ToolException(ErrorCode.InvalidArgument, "object_ids must contain valid GUIDs.");
        }
    }

    private static void ValidateSetObjectAttributes(SetObjectAttributesArguments arguments)
    {
        ValidateObjectIds(arguments.ObjectIds);
        if (arguments.Layer is null && arguments.Name is null)
        {
            throw new ToolException(
                ErrorCode.InvalidArgument,
                "At least one of layer or name must be provided.");
        }
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
            case "scale_xyz":
                if (arguments.Factors is null || arguments.Factors.Length != 3)
                {
                    throw new ToolException(
                        ErrorCode.InvalidArgument,
                        "factors must contain three positive numbers.");
                }
                foreach (var factor in arguments.Factors)
                {
                    Positive(factor, "factors");
                }
                break;
            default:
                throw new ToolException(
                    ErrorCode.InvalidArgument,
                    "Transform kind must be move, rotate, scale or scale_xyz.");
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
