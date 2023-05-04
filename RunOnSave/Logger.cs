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
        private static IServiceProvider _serviceProvider;
        private static IVsOutputWindow _output;
        private static IVsOutputWindowPane _pane;
        private static string _name;

        public static void Initialize(IServiceProvider serviceProvider, string name)
        {
            _serviceProvider = serviceProvider;
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
                ThreadHelper.ThrowIfNotOnUIThread();

                if (_pane == null)
                {
                    _output ??= _serviceProvider.GetService<SVsOutputWindow, IVsOutputWindow>();
                    var guid = Guid.NewGuid();
                    _output.CreatePane(ref guid, _name, 1, 1);
                    _output.GetPane(ref guid, out _pane);
                }

                _pane?.OutputStringThreadSafe(message + Environment.NewLine);
            }
            catch
            {
                // Do nothing, error logging shouldn't throw errors!
            }
        }
    }
}
