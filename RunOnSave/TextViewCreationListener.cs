using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;

namespace RunOnSave
{
    /// <summary>
    /// When you open a C# file in Visual Studio, this implementation is called, and will execute a command on save.
    /// Heavily based on the Visual Studio extension for SASS compilation here: https://github.com/madskristensen/WebCompiler
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("Any")]
    [Name("run on save text view handler")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public sealed class TextViewCreationListener : IVsTextViewCreationListener
    {
        public const int FileNotFoundWin32ExceptionCode = 0x2;

        private string solutionFilePath;
        private ITextDocument document;
        private ITextSnapshot previousSnapshot;
        private CommandTemplate command;

        public IInputOutput IO { get; set; } = new InputOutput(); // facade for indirecting hard-to-test IO.

        [Import] public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; }
        [Import] public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var textView = EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
            textView.Closed += TextViewClosed;

            if (!TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out this.document))
            {
                return;
            }

            solutionFilePath ??= RunOnSavePackage.GetSolutionDirectory() ?? Environment.CurrentDirectory;

            IO.QueueUserWorkItem(_ =>
            {
                var configProperties = IO.ReadConfigFile(document.FilePath);

                if (configProperties is null || configProperties.Count == 0)
                {
                    return;
                }

                if (!CommandTemplate.TryParse(configProperties, out var cmd))
                {
                    Logger.Log($"{CommandTemplate.FileName} found, but invalid for this file. Skipping.");
                    return;
                }

                if (cmd.ShouldIgnore)
                {
                    return;
                }

                this.command = cmd;
                this.document.FileActionOccurred += DocumentSaved;
            });
        }

        private void TextViewClosed(object sender, EventArgs e)
        {
            if (this.document != null)
            {
                this.document.FileActionOccurred -= DocumentSaved;
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

            // don't call the command if the file hasn't changed, unless always_run is specified by user.
            if (!this.command.AlwaysRun &&
                previousSnapshot == this.document.TextBuffer.CurrentSnapshot)
            {
                Logger.Log("Skipping command because the file is not changed. Configure \"always_run\" to change this behavior.");
                return;
            }

            this.previousSnapshot = this.document.TextBuffer.CurrentSnapshot;

            IO.QueueUserWorkItem(_ =>
            {
                try
                {
                    var (stdout, stderr) = IO.RunProcess(this.command, this.solutionFilePath, e.FilePath);
                    Log(stdout);
                    Log(stderr);
                }
                catch (Win32Exception win32) when (win32.NativeErrorCode == FileNotFoundWin32ExceptionCode)
                {
                    Logger.Log(
                        $"Unable to find the command '{this.command.Command}'. Please ensure the command is correct, including any file extension."
                    );
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private static void Log(string message)
        {
            message = message?.Trim();

            if (string.IsNullOrEmpty(message))
                return;

            Logger.Log(message);
        }
    }
}
