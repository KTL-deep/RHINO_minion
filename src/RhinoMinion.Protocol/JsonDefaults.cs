using System.Text.Json;
using System.Text.Json.Serialization;

namespace RhinoMinion.Protocol;

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(new UpperSnakeCaseNamingPolicy()));
        return options;
    }

    private sealed class UpperSnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            var buffer = new System.Text.StringBuilder(name.Length + 4);
            for (var index = 0; index < name.Length; index++)
            {
                var character = name[index];
                if (index > 0 && char.IsUpper(character))
                {
                    buffer.Append('_');
                }

                buffer.Append(char.ToUpperInvariant(character));
            }

            return buffer.ToString();
        }
    }
}
