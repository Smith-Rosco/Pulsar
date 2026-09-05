// [Path]: Pulsar/Pulsar.E2E/Driver/StatsFixtureValidator.cs

using System;
using System.IO;
using System.Text.Json;

namespace Pulsar.E2E.Driver
{
    /// <summary>
    /// Structural pre-check for the usage-stats fixture
    /// (<c>PluginUsageStats.json</c>), mirroring what the analytics page expects.
    ///
    /// The tracker persists <c>List&lt;PluginUsageStats&gt;</c> with System.Text.Json
    /// camelCase naming, so the fixture must be a top-level JSON <b>array</b> of
    /// objects that each carry a string <c>pluginId</c>. Anything else — a
    /// dictionary keyed by plugin id, a single object, a PascalCase
    /// <c>PluginId</c>, or invalid JSON — deserializes to an empty list at runtime
    /// and the page silently falls back to its empty state, which is exactly the
    /// confusing failure this validator turns into a fast, explicit error.
    /// </summary>
    public static class StatsFixtureValidator
    {
        /// <summary>
        /// Validates <paramref name="fixturePath"/> in place and throws
        /// <see cref="InvalidOperationException"/> with an actionable message when
        /// the shape is not what the app's stats reader accepts.
        /// </summary>
        public static void Validate(string fixturePath)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(fixturePath), new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Stats fixture is not valid JSON: '{fixturePath}'. Expected a top-level JSON array " +
                    $"of objects with a string 'pluginId' (camelCase), e.g. [{{\"pluginId\":\"com.x\",\"executions\":1}}]. " +
                    $"Raw error: {ex.Message}", ex);
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException(
                        $"Stats fixture must be a top-level JSON array, but '{fixturePath}' is " +
                        $"{DescribeKind(doc.RootElement.ValueKind)}. The tracker persists " +
                        $"List<PluginUsageStats> as an array (camelCase), so a dictionary keyed by plugin id " +
                        $"or a single object will silently render an empty stats page. " +
                        $"Example: [{{\"pluginId\":\"com.x\",\"executions\":1}}].");
                }

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidOperationException(
                            $"Stats fixture array item must be an object, but '{fixturePath}' contains " +
                            $"{DescribeKind(item.ValueKind)} at this position. Each entry needs at least a " +
                            $"string 'pluginId' (camelCase).");
                    }

                    if (!item.TryGetProperty("pluginId", out var pluginId)
                        || pluginId.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(pluginId.GetString()))
                    {
                        throw new InvalidOperationException(
                            $"Stats fixture item must carry a non-empty string 'pluginId' (camelCase), but " +
                            $"'{fixturePath}' has an entry without one. Common cause: using PascalCase " +
                            $"'PluginId' or a dictionary keyed by plugin id instead of an array. " +
                            $"Example entry: {{\"pluginId\":\"com.x\",\"executions\":1,\"successes\":1}}.");
                    }
                }
            }
        }

        private static string DescribeKind(JsonValueKind kind) => kind switch
        {
            JsonValueKind.Object => "an object",
            JsonValueKind.String => "a string",
            JsonValueKind.Number => "a number",
            JsonValueKind.True or JsonValueKind.False => "a boolean",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => "undefined",
            _ => kind.ToString()
        };
    }
}
