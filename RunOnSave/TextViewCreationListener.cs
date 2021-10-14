using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace RunOnSave
{
    /// <summary>
    /// When you open a C# file in Visual Studio, this implementation is called, and will execute dotnet-csharpier on save.
    /// Heavily based on the Visual Studio extension for sass compilation here: https://github.com/madskristensen/WebCompiler
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("CSharp")]
    [Name("run on save text view handler")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal class TextViewCreationListener : IVsTextViewCreationListener
    {
        private ITextDocument _document;
        private ITextSnapshot previousSnapshot;

        [Import] public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; }
        [Import] public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }


        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var textView = EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);

            if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out _document))
            {
                _document.FileActionOccurred += DocumentSaved;
            }

            textView.Closed += TextViewClosed;
        }

        private void TextViewClosed(object sender, EventArgs e)
        {
            if(_document != null)
            {
                _document.FileActionOccurred -= DocumentSaved;
            }

            if (sender is IWpfTextView textView)
            {
                textView.Closed -= TextViewClosed;
            }
        }

        private void DocumentSaved(object sender, TextDocumentFileActionEventArgs e)
        {
            if (e.FileActionType != FileActionTypes.ContentSavedToDisk)
                return;

            // Check if filename is absolute because when debugging, script files are sometimes dynamically created.
            if (string.IsNullOrEmpty(e.FilePath) || !Path.IsPathRooted(e.FilePath))
                return;

            // don't bother reformatting if the file hasn't been changed.
            if (previousSnapshot == _document.TextBuffer.CurrentSnapshot)
            {
                Logger.Log("Skipping formatting -- file not changed");
                return;
            }

            previousSnapshot = _document.TextBuffer.CurrentSnapshot;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    RunCSharpier(e.FilePath);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private static void RunCSharpier(string filePath)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet-csharpier.exe",
                Arguments = filePath,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(filePath)
            };

            using (var process = new Process() { StartInfo = processStartInfo })
            {
                process.OutputDataReceived += (_, args) => output.AppendLine(args.Data);
                process.ErrorDataReceived += (_, args) =>  error.AppendLine(args.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                process.Close();
            }

            if (output.Length > 0)
            {
                Logger.Log(TerminalOutputToVisualStudioOutput(output.ToString()));
            }
            if (error.Length > 0)
            {
                Logger.Log(TerminalOutputToVisualStudioOutput(error.ToString()));
            }
        }

        private static string TerminalOutputToVisualStudioOutput(string output)
        {
            return string.Join(" ", output.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
