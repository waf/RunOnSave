using EditorConfig.Core;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;

namespace RunOnSave
{
    /// <summary>
    /// When you open a C# file in Visual Studio, this implementation is called, and will execute dotnet-csharpier on save.
    /// Heavily based on the Visual Studio extension for sass compilation here: https://github.com/madskristensen/WebCompiler
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("Any")]
    [Name("run on save text view handler")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public sealed class TextViewCreationListener : IVsTextViewCreationListener
    {
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
            if(this.document != null)
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

            // don't bother reformatting if the file hasn't been changed.
            if (previousSnapshot == this.document.TextBuffer.CurrentSnapshot)
            {
                Logger.Log("Skipping formatting -- file not changed");
                return;
            }

            this.previousSnapshot = this.document.TextBuffer.CurrentSnapshot;

            IO.QueueUserWorkItem(_ =>
            {
                try
                {
                    var (stdout, stderr) = IO.RunProcess(this.command, e.FilePath);
                    Log(stdout);
                    Log(stderr);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private static void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var trimmed = message.Trim();
            if (trimmed.Length > 0)
            {
                Logger.Log(trimmed);
            }
        }
    }
}
