using System.Text.Json.Serialization;

namespace RhinoMinion.Plugin.Bridge;

internal sealed record PointDto(double X, double Y, double Z);

internal sealed record CreatePointArguments(
    [property: JsonPropertyName("point")] double[] Point,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateLineArguments(
    [property: JsonPropertyName("start")] double[] Start,
    [property: JsonPropertyName("end")] double[] End,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateCircleArguments(
    [property: JsonPropertyName("center")] double[] Center,
    [property: JsonPropertyName("normal")] double[] Normal,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateArcArguments(
    [property: JsonPropertyName("start")] double[] Start,
    [property: JsonPropertyName("point_on_arc")] double[] PointOnArc,
    [property: JsonPropertyName("end")] double[] End,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateEllipseArguments(
    [property: JsonPropertyName("center")] double[] Center,
    [property: JsonPropertyName("normal")] double[] Normal,
    [property: JsonPropertyName("radius_x")] double RadiusX,
    [property: JsonPropertyName("radius_y")] double RadiusY,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateRectangleArguments(
    [property: JsonPropertyName("origin")] double[] Origin,
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("height")] double Height,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreatePolygonArguments(
    [property: JsonPropertyName("center")] double[] Center,
    [property: JsonPropertyName("normal")] double[] Normal,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("sides")] int Sides,
    [property: JsonPropertyName("rotation_degrees")] double RotationDegrees,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateNurbsCurveArguments(
    [property: JsonPropertyName("points")] double[][] Points,
    [property: JsonPropertyName("degree")] int Degree,
    [property: JsonPropertyName("closed")] bool Closed,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateSphereArguments(
    [property: JsonPropertyName("center")] double[] Center,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateCylinderArguments(
    [property: JsonPropertyName("base_center")] double[] BaseCenter,
    [property: JsonPropertyName("axis")] double[] Axis,
    [property: JsonPropertyName("radius")] double Radius,
    [property: JsonPropertyName("height")] double Height,
    [property: JsonPropertyName("cap")] bool Cap,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record ObjectIdsArguments(
    [property: JsonPropertyName("object_ids")] Guid[] ObjectIds);

internal sealed record SetObjectAttributesArguments(
    [property: JsonPropertyName("object_ids")] Guid[] ObjectIds,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreateBoxArguments(
    [property: JsonPropertyName("origin")] double[] Origin,
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("depth")] double Depth,
    [property: JsonPropertyName("height")] double Height,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CreatePolylineArguments(
    [property: JsonPropertyName("points")] double[][] Points,
    [property: JsonPropertyName("closed")] bool Closed,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record ExtrudeArguments(
    [property: JsonPropertyName("curve_id")] Guid CurveId,
    [property: JsonPropertyName("height")] double Height,
    [property: JsonPropertyName("cap")] bool Cap,
    [property: JsonPropertyName("delete_input")] bool DeleteInput,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record TransformArguments(
    [property: JsonPropertyName("object_ids")] Guid[] ObjectIds,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("vector")] double[]? Vector,
    [property: JsonPropertyName("center")] double[]? Center,
    [property: JsonPropertyName("axis")] double[]? Axis,
    [property: JsonPropertyName("angle_degrees")] double? AngleDegrees,
    [property: JsonPropertyName("factor")] double? Factor,
    [property: JsonPropertyName("factors")] double[]? Factors,
    [property: JsonPropertyName("copy")] bool Copy);
