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
    internal class TextViewCreationListener : IVsTextViewCreationListener
    {
        private ITextDocument _document;
        private ITextSnapshot previousSnapshot;
        private CommandConfiguration command;

        [Import] public IVsEditorAdaptersFactoryService EditorAdaptersFactoryService { get; set; }
        [Import] public ITextDocumentFactoryService TextDocumentFactoryService { get; set; }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            var textView = EditorAdaptersFactoryService.GetWpfTextView(textViewAdapter);

            if (TextDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out _document))
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    var fileConfiguration = new EditorConfigParser(CommandConfiguration.FileName).Parse(_document.FilePath);

                    if (fileConfiguration is null || fileConfiguration.Properties.Count == 0)
                    {
                        return;
                    }

                    if(!CommandConfiguration.TryParse(fileConfiguration, out var command))
                    {
                        Logger.Log($"{CommandConfiguration.FileName} found, but invalid for this file. Skipping.");
                        return;
                    }

                    if (command.ShouldIgnore)
                    {
                        return;
                    }

                    this.command = command;
                    _document.FileActionOccurred += DocumentSaved;
                });
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
                    var (stdout, stderr) = CommandRunner.Run(this.command, e.FilePath);
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
            var trimmed = message.Trim();
            if (trimmed.Length > 0)
            {
                Logger.Log(trimmed);
            }
        }
    }
}
