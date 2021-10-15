using EditorConfig.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RunOnSave
{
    /// <summary>
    /// Command configuration read from .onsaveconfig.
    /// The .onsaveconfig file format is the same as .editorconfig, and accepts the keys defined in this class.
    /// </summary>
    internal class CommandConfiguration
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

        public static bool TryParse(FileConfiguration configuration, out CommandConfiguration parsed)
        {
            if (!configuration.Properties.TryGetValue(CommandKey, out string command))
            {
                parsed = null;
                return false;
            }

            var config = new CommandConfiguration { Command = command };

            if (configuration.Properties.TryGetValue(ArgumentsKey, out string argumentTemplate))
            {
                config.Arguments = argumentTemplate;
            }

            if (configuration.Properties.TryGetValue(WorkingDirectoryKey, out string workingDirectory))
            {
                config.WorkingDirectory = workingDirectory;
            }

            if (configuration.Properties.TryGetValue(TimeoutKey, out string timeout)
                && int.TryParse(timeout, out int seconds))
            {
                config.Timeout = TimeSpan.FromSeconds(seconds);
            }

            parsed = config;
            return true;
        }
    }
}
