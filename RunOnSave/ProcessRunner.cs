using System;
using System.Diagnostics;
using System.Text;

namespace RunOnSave
{
    internal class ProcessRunner
    {
        public static (string stdout, string stderr) Run(CommandTemplate command, string solutionDirectory, string filePath)
        {
            var processStartInfo = command.ToProcessStartInfo(solutionDirectory, filePath);

            var output = new StringBuilder($"{DateTime.Now:s}: running {processStartInfo.FileName} {processStartInfo.Arguments}" + Environment.NewLine);
            var error = new StringBuilder();

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
