namespace Systems.Console
{
    using System;

    /// <summary>
    /// Contract for an executable developer console command.
    /// Modders and developers can implement this interface to introduce custom console commands.
    /// </summary>
    public interface IConsoleCommand
    {
        /// <summary>Primary command identifier (case-insensitive, e.g. "tgm", "player.additem").</summary>
        string Name { get; }

        /// <summary>Brief summary of what the command does.</summary>
        string Description { get; }

        /// <summary>Syntax usage string shown in help (e.g. "player.additem <itemId> [count]").</summary>
        string Usage { get; }

        /// <summary>
        /// Executes the command with provided arguments.
        /// </summary>
        /// <param name="args">Whitespace-delimited arguments following the command name.</param>
        /// <param name="print">Callback to output log messages back to the console window.</param>
        /// <returns>True if the command succeeded; false otherwise.</returns>
        bool Execute(string[] args, Action<string> print);
    }
}
