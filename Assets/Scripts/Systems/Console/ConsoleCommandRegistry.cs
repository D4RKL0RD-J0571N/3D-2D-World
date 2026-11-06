namespace Systems.Console
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Central registry managing console commands, autocompletion, and execution.
    /// Provides extensibility allowing both built-in engine commands and user/mod scripts
    /// to register new commands at runtime.
    /// </summary>
    public class ConsoleCommandRegistry
    {
        #region Fields

        private readonly Dictionary<string, IConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        public IReadOnlyCollection<IConsoleCommand> AllCommands => _commands.Values;

        #endregion

        #region Registration API

        /// <summary>
        /// Registers a console command. Overwrites existing command if names match.
        /// </summary>
        public void RegisterCommand(IConsoleCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Name)) return;
            _commands[command.Name] = command;
        }

        /// <summary>
        /// Registers a custom delegate command directly.
        /// </summary>
        public void Register(string name, Func<string[], string> handler, string description = "", string usage = "")
        {
            if (string.IsNullOrWhiteSpace(name) || handler == null) return;
            RegisterCommand(new DelegateConsoleCommand(name, handler, description, usage));
        }

        /// <summary>
        /// Registers all standard built-in Skyrim developer console commands.
        /// </summary>
        public void RegisterDefaults()
        {
            RegisterCommand(new ToggleGodModeCommand());
            RegisterCommand(new ToggleCollisionCommand());
            RegisterCommand(new AddItemCommand());
            RegisterCommand(new SetActorValueCommand());
            RegisterCommand(new AdvanceSkillCommand());
            RegisterCommand(new KillCommand());
            RegisterCommand(new KillAllCommand());
            RegisterCommand(new ResurrectCommand());
            RegisterCommand(new DisableCommand());
            RegisterCommand(new EnableCommand());
            RegisterCommand(new SetScaleCommand());
            RegisterCommand(new UnlockCommand());
            RegisterCommand(new ToggleAICommand());
            RegisterCommand(new MoveToPlayerCommand());
            RegisterCommand(new CenterOnCellCommand());
            RegisterCommand(new QuitCommand());
            RegisterCommand(new HelpCommand(this));
        }

        /// <summary>
        /// Removes a console command by name.
        /// </summary>
        public bool UnregisterCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return false;
            return _commands.Remove(commandName);
        }

        /// <summary>
        /// Checks if a command with the given name is registered.
        /// </summary>
        public bool HasCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return false;
            return _commands.ContainsKey(commandName);
        }

        #endregion

        #region Execution & Autocomplete

        /// <summary>
        /// Parses and executes a command line, returning any logged message output.
        /// </summary>
        public string Execute(string line)
        {
            string output = "";
            ExecuteLine(line, msg => output = msg);
            return output;
        }

        /// <summary>
        /// Parses and executes a raw command line string.
        /// </summary>
        public bool ExecuteLine(string line, Action<string> print)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;

            string[] tokens = ParseCommandLine(line);
            if (tokens.Length == 0) return false;

            string cmdName = tokens[0];
            if (!_commands.TryGetValue(cmdName, out var cmd))
            {
                print?.Invoke($"<color=red>Unknown command: '{cmdName}'. Type 'help' for a list of commands.</color>");
                return false;
            }

            string[] args = new string[tokens.Length - 1];
            Array.Copy(tokens, 1, args, 0, args.Length);

            try
            {
                return cmd.Execute(args, print);
            }
            catch (Exception ex)
            {
                print?.Invoke($"<color=red>Error executing '{cmdName}': {ex.Message}</color>");
                return false;
            }
        }

        /// <summary>
        /// Returns all registered command names starting with the given prefix for Tab completion.
        /// </summary>
        public List<string> GetCompletions(string prefix)
        {
            var matches = new List<string>();
            if (string.IsNullOrEmpty(prefix)) return matches;

            foreach (var kvp in _commands)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(kvp.Key);
                }
            }

            matches.Sort(StringComparer.OrdinalIgnoreCase);
            return matches;
        }

        #endregion

        #region Helpers

        private string[] ParseCommandLine(string line)
        {
            var tokens = new List<string>();
            bool inQuotes = false;
            string current = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length > 0) tokens.Add(current);
            return tokens.ToArray();
        }

        #endregion
    }

    /// <summary>
    /// Lightweight IConsoleCommand implementation that wraps a delegate handler.
    /// </summary>
    public class DelegateConsoleCommand : IConsoleCommand
    {
        private readonly Func<string[], string> _handler;

        public string Name { get; }
        public string Description { get; }
        public string Usage { get; }

        public DelegateConsoleCommand(string name, Func<string[], string> handler, string description = "", string usage = "")
        {
            Name = name;
            _handler = handler;
            Description = description ?? "";
            Usage = string.IsNullOrEmpty(usage) ? name : usage;
        }

        public bool Execute(string[] args, Action<string> print)
        {
            string result = _handler?.Invoke(args);
            if (!string.IsNullOrEmpty(result))
            {
                print?.Invoke(result);
            }
            return true;
        }
    }
}
