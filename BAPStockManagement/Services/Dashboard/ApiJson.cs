using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BAPStockManagement.Services.Dashboard;

public static class ApiJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(
                new UpperSnakeCaseNamingPolicy(),
                allowIntegerValues: false));
        return options;
    }
}

public sealed class UpperSnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var result = new StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            var current = name[index];
            if (char.IsUpper(current)
                && index > 0
                && name[index - 1] != '_'
                && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1])))
            {
                result.Append('_');
            }

            result.Append(char.ToUpperInvariant(current));
        }

        return result.ToString();
    }
}
