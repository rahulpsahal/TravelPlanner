using System.Text.Json;
using System.Text.Json.Nodes;

namespace TravelPlanner.Api.Logging;

internal static class ApiLogCapture
{
    public const int BodyLimit = 16 * 1024;

    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "cookie", "setcookie", "password", "secret", "clientsecret", "token",
        "accesstoken", "refreshtoken", "accesskey", "apikey", "xapikey", "groqapikey", "tavilyapikey",
        "aviationstackapikey", "railwayapikey", "connectionstring", "phonenumber", "phone", "mobile",
        "email", "otp", "code", "developmentcode", "challengeid"
    };

    private static readonly string[] TravelQueryFields =
        ["destination", "origin", "source", "travelClass", "date", "quota", "flightCode"];

    public static Dictionary<string, string> ReadQuery(IQueryCollection query)
    {
        var result = new Dictionary<string, string>();
        foreach (var key in TravelQueryFields)
        {
            if (query.TryGetValue(key, out var values) && values.Count == 1 && values[0] is { Length: <= 100 } value)
                result[key] = value;
        }
        return result;
    }

    public static async Task<JsonNode?> ReadRequestAsync(HttpRequest request)
    {
        var length = request.Body.Position;
        if (length is <= 0 or > BodyLimit || length != request.ContentLength)
            return null;

        var buffer = new byte[(int)length];
        request.Body.Position = 0;
        try
        {
            await request.Body.ReadExactlyAsync(buffer, request.HttpContext.RequestAborted);
            return Parse(buffer);
        }
        finally
        {
            request.Body.Position = length;
        }
    }

    public static JsonNode? Parse(ReadOnlySpan<byte> body)
    {
        try
        {
            var json = JsonNode.Parse(body);
            Redact(json);
            return json;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsJson(string? contentType)
    {
        var mediaType = contentType?.Split(';', 2)[0].Trim();
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                var name = property.Key.Replace("_", "").Replace("-", "");
                if (SensitiveFields.Contains(name))
                    obj[property.Key] = "[redacted]";
                else
                    Redact(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                Redact(item);
        }
    }
}
