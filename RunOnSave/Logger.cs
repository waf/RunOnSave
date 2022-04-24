using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RunOnSave
{
    /// <summary>
    /// Logger that logs to the Visual Studio Output Window.
    /// </summary>
    public static class Logger
    {
        private static IVsOutputWindow _output;
        private static IVsOutputWindowPane _pane;
        private static string _name;

        public static void Initialize(IVsOutputWindow output, string name)
        {
            _output = output;
            _name = name;
        }

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LogToOutputWindow(message);
                });
            }
            catch
            {
                // this only throws in unit tests where the threading model is different.
            }
        }

        public static void Log(Exception ex) =>
            Log("Exception: " + ex.ToString());

        private static void LogToOutputWindow(string message)
        {
            try
            {
                if (_pane == null)
                {
                    Guid guid = Guid.NewGuid();
                    if (_output != null)
                    {
                        _output.CreatePane(ref guid, _name, 1, 1);
                        _output.GetPane(ref guid, out _pane);
                    }
                }

                _pane?.OutputStringThreadSafe(message + Environment.NewLine);
            }
            catch
            {
                // Do nothing
            }
        }
    }
}
