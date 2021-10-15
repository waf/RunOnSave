using EditorConfig.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RunOnSave
{
    /// <summary>
    /// Command configuration read from .onsaveconfig.
    /// The .onsaveconfig file format is the same as .editorconfig, and accepts the keys defined in this class.
    /// </summary>
    public sealed class CommandTemplate
    {
        public const string FileName = ".onsaveconfig";

        private const string CommandKey = "command";
        private const string ArgumentsKey = "arguments";
        private const string WorkingDirectoryKey = "working_directory";
        private const string TimeoutKey = "timeout_seconds";

        private readonly IReadOnlyList<string> ignoreValues = new[] { "ignore", "unset" };

        public string Command { get; private set; }
        public string Arguments { get; private set; }
        public string WorkingDirectory { get; private set; }
        public TimeSpan Timeout { get; private set; } = TimeSpan.FromSeconds(30);

        public bool ShouldIgnore => string.IsNullOrWhiteSpace(Command)
            || ignoreValues.Contains(Command, StringComparer.OrdinalIgnoreCase);

        public ProcessStartInfo ToProcessStartInfo(string filePath)
        {
            string arguments = Arguments;
            if (arguments != null)
            {
                arguments = arguments
                    .Replace("{file}", filePath)
                    .Replace("{filename}", Path.GetFileName(filePath))
                    .Replace("{directory}", Path.GetDirectoryName(filePath));
            }

            return new ProcessStartInfo
            {
                FileName = Command,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = WorkingDirectory ?? Path.GetDirectoryName(filePath)
            };
        }

        public static bool TryParse(IReadOnlyDictionary<string, string> configuration, out CommandTemplate parsed)
        {
            if (!configuration.TryGetValue(CommandKey, out string command))
            {
                parsed = null;
                return false;
            }

            var config = new CommandTemplate { Command = command };

            if (configuration.TryGetValue(ArgumentsKey, out string argumentTemplate))
            {
                config.Arguments = argumentTemplate;
            }

            if (configuration.TryGetValue(WorkingDirectoryKey, out string workingDirectory))
            {
                config.WorkingDirectory = workingDirectory;
            }

            if (configuration.TryGetValue(TimeoutKey, out string timeout)
                && int.TryParse(timeout, out int seconds))
            {
                config.Timeout = TimeSpan.FromSeconds(seconds);
            }

            parsed = config;
            return true;
        }
    }
}
