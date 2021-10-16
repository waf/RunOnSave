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
    /// This template is evaluted in the context of a specific file being saved.
    /// </summary>
    public sealed class CommandTemplate
    {
        public const string FileName = ".onsaveconfig";

        private const string CommandKey = "command";
        private const string ArgumentsKey = "arguments";
        private const string WorkingDirectoryKey = "working_directory";
        private const string TimeoutKey = "timeout_seconds";

        private readonly IReadOnlyList<string> ignoreValues = new[] { "ignore", "unset" };

        /// <summary>
        /// The command to execute. Should represent an executable on
        /// disk (optionally on the environment PATH variable)..
        /// </summary>
        public string Command { get; private set; }

        /// <summary>
        /// The arguments to pass to the above <see cref="Command"/>. Can
        /// contain template values that represent the file being saved.
        /// </summary>
        public string ArgumentsTemplate { get; private set; }

        /// <summary>
        /// The working directory to start the <see cref="Command"/> in.
        /// </summary>
        public string WorkingDirectory { get; private set; }

        /// <summary>
        /// The working directory to start the <see cref="Command"/> in.
        /// </summary>
        public TimeSpan Timeout { get; private set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether or not the current configuration is an "Ignore" configuration,
        /// so no action should be taken when the file is saved.
        /// </summary>
        public bool ShouldIgnore => string.IsNullOrWhiteSpace(Command)
            || ignoreValues.Contains(Command, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fills in the current CommandTemplate, creating a ProcessStartInfo.
        /// This can then be passed to System.Diagnostics.Process.
        /// </summary>
        public ProcessStartInfo ToProcessStartInfo(string filePath)
        {
            string arguments = ArgumentsTemplate;
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

        /// <summary>
        /// Create a CommandTemplate from a configuration file. This configuration file contains a
        /// list of keys/values that were read from an .onsaveconfig file.
        /// </summary>
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
                config.ArgumentsTemplate = argumentTemplate;
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
