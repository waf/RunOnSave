using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RunOnSave
{
    internal class CommandRunner
    {
        public static (string stdout, string stderr) Run(CommandConfiguration command, string filePath)
        {
            string arguments = command.Arguments;
            if (arguments != null)
            {
                arguments = arguments
                    .Replace("{file}", filePath)
                    .Replace("{filename}", Path.GetFileName(filePath))
                    .Replace("{directory}", Path.GetDirectoryName(filePath));
            }

            var output = new StringBuilder($"{DateTime.Now:s}: running {command.Command} {arguments}" + Environment.NewLine);
            var error = new StringBuilder();

            var processStartInfo = new ProcessStartInfo
            {
                FileName = command.Command,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = command.WorkingDirectory ?? Path.GetDirectoryName(filePath)
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();

                output.AppendLine(process.StandardOutput.ReadToEnd());
                error.AppendLine(process.StandardError.ReadToEnd());

                process.WaitForExit(command.Timeout.Milliseconds);
                process.Close();
            }

            return (output.ToString(), error.ToString());
        }
    }
}
