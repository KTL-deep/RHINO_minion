using System.Text.Json.Serialization;

namespace RhinoMinion.Plugin.Bridge;

internal sealed record PointDto(double X, double Y, double Z);

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
    [property: JsonPropertyName("copy")] bool Copy);
