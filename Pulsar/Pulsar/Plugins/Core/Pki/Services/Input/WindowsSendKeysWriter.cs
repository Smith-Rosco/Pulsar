using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public class WindowsSendKeysWriter : ISendKeysWriter
    {
        private readonly ILogger<WindowsSendKeysWriter> _logger;

        public WindowsSendKeysWriter(ILogger<WindowsSendKeysWriter> logger)
        {
            _logger = logger;
        }

        public void SendWait(string keys)
        {
            _logger.LogDebug("[WindowsSendKeysWriter] Sending keys");
            SendKeys.SendWait(keys);
        }

        public string EscapeForSendKeys(string? input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

            var sb = new StringBuilder(input.Length * 2);
            foreach (char c in input)
            {
                switch (c)
                {
                    case '{': sb.Append("{{}"); break;
                    case '}': sb.Append("{}}"); break;
                    case '[': sb.Append("{[}"); break;
                    case ']': sb.Append("{]}"); break;
                    case '+': sb.Append("{+}"); break;
                    case '^': sb.Append("{^}"); break;
                    case '%': sb.Append("{%}"); break;
                    case '~': sb.Append("{~}"); break;
                    case '(': sb.Append("{(}"); break;
                    case ')': sb.Append("{)}"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
