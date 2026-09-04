// [Path]: Pulsar/Pulsar.E2E/AI/LlmClient.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pulsar.E2E.AI
{
    /// <summary>Configuration for the LLM provider used by the iteration loop.</summary>
    public sealed class LlmConfig
    {
        /// <summary>OpenAI-compatible chat-completions endpoint base URL
        /// (e.g. https://api.openai.com/v1 or a local Ollama gateway).</summary>
        public string BaseUrl { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);
    }

    /// <summary>
    /// Minimal OpenAI-compatible chat-completions client that supports
    /// image+text (base64 data URL) messages — the exact shape the iteration
    /// loop needs: diagnostic package text plus screenshot(s).
    /// </summary>
    public sealed class LlmClient
    {
        private readonly LlmConfig _config;
        private readonly HttpClient _http;

        public LlmClient(LlmConfig config)
        {
            _config = config;
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
            }
        }

        /// <summary>Sends a text+images prompt and returns the model's text reply.</summary>
        public async Task<string> CompleteAsync(string systemPrompt, string userText, IEnumerable<string> imagePaths)
        {
            var url = _config.BaseUrl.TrimEnd('/') + "/chat/completions";

            var userContent = new List<object>();
            userContent.Add(new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = userText
            });

            foreach (var path in imagePaths.Where(File.Exists).Take(3))
            {
                var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(path).ConfigureAwait(false))}";
                userContent.Add(new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, object> { ["url"] = dataUrl }
                });
            }

            var body = new Dictionary<string, object>
            {
                ["model"] = _config.Model,
                ["messages"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = userContent
                    }
                },
                ["temperature"] = 0.1
            };

            var json = JsonSerializer.Serialize(body);
            using var response = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"LLM request failed ({(int)response.StatusCode} {response.StatusCode}): {Truncate(responseText, 500)}");
            }

            using var doc = JsonDocument.Parse(responseText);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return content ?? string.Empty;
        }

        private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";
    }

    /// <summary>The structured fix proposal the AI must return.</summary>
    public sealed class FixProposal
    {
        /// <summary>File paths (relative to the workspace) with full replacement or patch text.</summary>
        public Dictionary<string, string> FilePatches { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Reasoning { get; set; } = string.Empty;
    }

    public static class FixProposalParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Parses the AI reply. The model must answer with a JSON object:
        /// { "reasoning": "...", "patches": [{ "file": "...", "content": "..." }] }.
        /// Fenced code blocks around the JSON are tolerated.
        /// </summary>
        public static FixProposal Parse(string reply)
        {
            var json = ExtractJson(reply);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var proposal = new FixProposal();
            if (root.TryGetProperty("reasoning", out var reasoning) && reasoning.ValueKind == JsonValueKind.String)
            {
                proposal.Reasoning = reasoning.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("patches", out var patches) && patches.ValueKind == JsonValueKind.Array)
            {
                foreach (var patch in patches.EnumerateArray())
                {
                    var file = patch.TryGetProperty("file", out var f) ? f.GetString() : null;
                    var content = patch.TryGetProperty("content", out var c) ? c.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(file) && content != null)
                    {
                        proposal.FilePatches[file] = content;
                    }
                }
            }

            if (proposal.FilePatches.Count == 0)
            {
                throw new InvalidOperationException("AI proposal contained no usable file patches.");
            }

            return proposal;
        }

        private static string ExtractJson(string reply)
        {
            var text = reply.Trim();
            var fenceStart = text.IndexOf("```", StringComparison.Ordinal);
            if (fenceStart >= 0)
            {
                var start = text.IndexOf('\n', fenceStart);
                var end = text.LastIndexOf("```", StringComparison.Ordinal);
                if (start >= 0 && end > start)
                {
                    text = text[(start + 1)..end].Trim();
                }
            }

            var brace = text.IndexOf('{');
            return brace >= 0 ? text[brace..] : text;
        }
    }
}
