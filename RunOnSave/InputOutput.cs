using EditorConfig.Core;
using System.Collections.Generic;
using System.Threading;

namespace RunOnSave
{
    public interface IInputOutput
    {
        void QueueUserWorkItem(WaitCallback callBack);
        IReadOnlyDictionary<string, string> ReadConfigFile(string filepath);
        (string stdout, string stderr) RunProcess(CommandTemplate command, string filePath);
    }

    internal class InputOutput : IInputOutput
    {
        public IReadOnlyDictionary<string, string> ReadConfigFile(string filepath) =>
            new EditorConfigParser(CommandTemplate.FileName).Parse(filepath)?.Properties;

        public void QueueUserWorkItem(WaitCallback callBack) =>
            ThreadPool.QueueUserWorkItem(callBack);

        public (string stdout, string stderr) RunProcess(CommandTemplate command, string filePath) =>
            ProcessRunner.Run(command, filePath);
    }
}
