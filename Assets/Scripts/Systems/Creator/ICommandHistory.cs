namespace Systems.Creator
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Reversible editor operation executed inside the In-Game Creator Studio (<c>ARCH-05</c>).
    /// Never retains destroyed <c>UnityEngine.Object</c> pointers across Undo/Redo boundaries.
    /// </summary>
    public interface IEditorCommand
    {
        string Description { get; }
        void Execute();
        void Undo();
    }

    /// <summary>
    /// Contract for deterministic Undo/Redo command history in the In-Game Editor (<c>ARCH-05</c>).
    /// </summary>
    public interface ICommandHistory
    {
        int UndoCount { get; }
        int RedoCount { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }

        void ExecuteCommand(IEditorCommand command);
        bool Undo();
        bool Redo();
        void Clear();
    }

    /// <summary>
    /// Bounded Undo/Redo stack implementation for <see cref="CreatorStudioService"/>.
    /// </summary>
    public sealed class CommandHistory : ICommandHistory
    {
        private readonly List<IEditorCommand> _undoStack = new List<IEditorCommand>();
        private readonly Stack<IEditorCommand> _redoStack = new Stack<IEditorCommand>();
        private readonly int _maxCapacity;

        public CommandHistory(int maxCapacity = 128)
        {
            _maxCapacity = Math.Max(8, maxCapacity);
        }

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void ExecuteCommand(IEditorCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            command.Execute();
            _undoStack.Add(command);
            _redoStack.Clear();

            if (_undoStack.Count > _maxCapacity)
            {
                _undoStack.RemoveAt(0);
            }
        }

        public bool Undo()
        {
            if (_undoStack.Count == 0)
            {
                return false;
            }

            int lastIndex = _undoStack.Count - 1;
            IEditorCommand cmd = _undoStack[lastIndex];
            _undoStack.RemoveAt(lastIndex);

            cmd.Undo();
            _redoStack.Push(cmd);
            return true;
        }

        public bool Redo()
        {
            if (_redoStack.Count == 0)
            {
                return false;
            }

            IEditorCommand cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Add(cmd);
            return true;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
