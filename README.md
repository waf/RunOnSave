Visual Studio Extension: Run On Save
====================================

A simple Visual Studio extension that runs a command (i.e. a command line application), whenever a file is saved in Visual Studio. Available for free on the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=WillFuqua.RunOnSave).

The command to be run is specified in a `.onsaveconfig` file, which has the same syntax and behavior as [.editorconfig files](https://editorconfig.org/).

Here's an example `.onsaveconfig` file that calls [dotnet-csharpier](https://github.com/belav/csharpier) on C# files:

```ini
# run dotnet csharpier on C# files, whenever they're saved.
[*.cs]
command = dotnet
arguments = csharpier {file}
```

The following options are supported:

- **command** - the command to run. It should be either fully qualified or available on the Path environment variable. For `.cmd` and `.bat` files, make sure the file extension is included in the command.
- **arguments** - the arguments to supply to the command. It supports the following placeholders:
  - `{file}` - The fully qualifed file that was saved (e.g. C:\Foo\Bar.cs)
  - `{filename}` - The file name only (e.g. Bar.cs)
  - `{directory}` - The directory only (e.g. C:\Foo)
- **working_directory** - The working directory to run the command in. Defaults to the directory of the file that was saved.
- **timeout_seconds** - How long to wait for the command to finish. Defaults to 30 seconds.
- **always_run** - by default, RunOnSave only runs the command when the input file has changed (so repeatedly pressing ctrl-s will only call the command once). Set this to `true` to disable this behavior, so the command will always run. This may be required if you have additional extensions that also modify the file on save.

Similar to `.editorconfig`, specific files can be ignored by setting the command to `unset` or `ignore`:

```ini
[*.cs]
command = dotnet
arguments = csharpier {file}

[BigFile.cs]
command = ignore
```

Logo by [icons8](https://www.visualpharm.com/free-icons/save-595b40b85ba036ed117da9ec).
