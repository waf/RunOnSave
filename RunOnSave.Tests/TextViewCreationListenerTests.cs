﻿using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System.Threading;
using System.Diagnostics;

namespace RunOnSave.Tests
{
    [TestClass]
    public class TextViewCreationListenerTests
    {
        private readonly IInputOutput IO;

        public TextViewCreationListenerTests()
        {
            this.IO = Substitute.For<IInputOutput>();

            // synchronously invoke QueueUserWorkItem
            IO.WhenForAnyArgs(io => io.QueueUserWorkItem(default))
              .Do(cb => cb.Arg<WaitCallback>().Invoke(null));
        }

        [TestMethod]
        public void TextViewCreationListener_FileOpenedAndSaved_RunsProcess()
        {
            const string OpenedFile = @"C:\repos\Foo.cs";

            var textViewCreationListener = new TextViewCreationListener
            {
                IO = IO,
                EditorAdaptersFactoryService = Substitute.For<IVsEditorAdaptersFactoryService>(),
                TextDocumentFactoryService = Substitute.For<ITextDocumentFactoryService>(),
            };

            // set up the values read from the .onsaveconfig
            textViewCreationListener.IO
                .ReadConfigFile(OpenedFile)
                .Returns(new Dictionary<string, string>
                {
                    ["command"] = "dotnet",
                    ["arguments"] = "csharpier {file}",
                });

            var textView = Substitute.For<IVsTextView>();
            var document = Substitute.For<ITextDocument>();
            document.FilePath.Returns(OpenedFile);

            StubVisualStudioServices(textViewCreationListener, textView, document);

            // capture the process info that will be run, so we can assert on it.
            ProcessStartInfo process = null;
            textViewCreationListener.IO
                .WhenForAnyArgs(io => io.RunProcess(default, default))
                .Do(cb =>
                {
                    process = cb.Arg<CommandTemplate>().ToProcessStartInfo(cb.Arg<string>());
                });

            // system under test -- file opened
            textViewCreationListener.VsTextViewCreated(textView);

            // system under test -- file saved
            document.FileActionOccurred += Raise.EventWith(
                new object(),
                new TextDocumentFileActionEventArgs(OpenedFile, DateTime.Now, FileActionTypes.ContentSavedToDisk)
            );

            textViewCreationListener.IO.ReceivedWithAnyArgs(1).RunProcess(default, default);

            Assert.AreEqual(@"dotnet", process.FileName);
            Assert.AreEqual(@"csharpier C:\repos\Foo.cs", process.Arguments);
            Assert.AreEqual(@"C:\repos", process.WorkingDirectory);
        }

        [TestMethod]
        public void TextViewCreationListener_IgnoredFileOpenedAndSaves_DoesNotRunProcess()
        {
            const string OpenedFile = @"C:\repos\Foo.cs";

            var textViewCreationListener = new TextViewCreationListener
            {
                IO = IO,
                EditorAdaptersFactoryService = Substitute.For<IVsEditorAdaptersFactoryService>(),
                TextDocumentFactoryService = Substitute.For<ITextDocumentFactoryService>(),
            };

            // set up the values read from the .onsaveconfig
            textViewCreationListener.IO
                .ReadConfigFile(OpenedFile)
                .Returns(new Dictionary<string, string>
                {
                    ["command"] = "ignore",
                    ["arguments"] = "csharpier {file}",
                });

            var textView = Substitute.For<IVsTextView>();
            var document = Substitute.For<ITextDocument>();
            document.FilePath.Returns(OpenedFile);

            StubVisualStudioServices(textViewCreationListener, textView, document);

            // capture the process info that will be run, so we can assert on it.
            ProcessStartInfo process = null;
            textViewCreationListener.IO
                .WhenForAnyArgs(io => io.RunProcess(default, default))
                .Do(cb =>
                {
                    process = cb.Arg<CommandTemplate>().ToProcessStartInfo(cb.Arg<string>());
                });

            // system under test -- file opened
            textViewCreationListener.VsTextViewCreated(textView);

            // system under test -- file saved
            document.FileActionOccurred += Raise.EventWith(
                new object(),
                new TextDocumentFileActionEventArgs(OpenedFile, DateTime.Now, FileActionTypes.ContentSavedToDisk)
            );

            textViewCreationListener.IO.DidNotReceiveWithAnyArgs().RunProcess(default, default);

            Assert.IsNull(process);
        }

        private static void StubVisualStudioServices(TextViewCreationListener textViewCreationListener, IVsTextView textView, ITextDocument document)
        {
            var wpfTextView = Substitute.For<IWpfTextView>();

            textViewCreationListener.EditorAdaptersFactoryService
                .GetWpfTextView(textView)
                .Returns(wpfTextView);

            textViewCreationListener.TextDocumentFactoryService
                .TryGetTextDocument(wpfTextView.TextDataModel.DocumentBuffer, out Arg.Any<ITextDocument>())
                .Returns(x =>
                {
                    x[1] = document;
                    return true;
                });
        }
    }
}
