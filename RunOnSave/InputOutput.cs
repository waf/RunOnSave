using EditorConfig.Core;
using System.Collections.Generic;
using System.Threading;

namespace RunOnSave
{
    /// <summary>
    /// Layer of indirection for IO operations, mostly for mocking / testing purposes.
    /// </summary>
    public interface IInputOutput
    {
        void QueueUserWorkItem(WaitCallback callBack);
        IReadOnlyDictionary<string, string> ReadConfigFile(string filepath);
        (string stdout, string stderr) RunProcess(CommandTemplate command, string solutionDirectory, string filePath);
    }

    /// <summary>
    /// Concrete implementation of <see cref="IInputOutput"/> that actually does Input/Output.
    /// </summary>
    internal class InputOutput : IInputOutput
    {
        public IReadOnlyDictionary<string, string> ReadConfigFile(string filepath) =>
            new EditorConfigParser(CommandTemplate.FileName).Parse(filepath)?.Properties;

        public void QueueUserWorkItem(WaitCallback callBack) =>
            ThreadPool.QueueUserWorkItem(callBack);

        public (string stdout, string stderr) RunProcess(CommandTemplate command, string solutionDirectory, string filePath) =>
            ProcessRunner.Run(command, solutionDirectory, filePath);
    }
}
