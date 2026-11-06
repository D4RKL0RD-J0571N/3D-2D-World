namespace Core.Scheduling
{
    using System.Collections.Generic;

    /// <summary>
    /// Deterministic per-frame execution phase for <see cref="ITickable"/> systems.
    /// Ensures input polling always runs before simulation, simulation before AI, and UI after world updates.
    /// </summary>
    public enum TickGroup
    {
        Input = 0,
        Simulation = 100,
        AI = 200,
        WorldStreaming = 300,
        UI = 400,
        LateCleanup = 500
    }

    /// <summary>
    /// Optional extension of <see cref="ITickable"/> allowing a service to declare its <see cref="TickGroup"/>
    /// and an optional throttle interval (e.g., <c>0.1f</c> for 10 Hz AI line-of-sight checks).
    /// </summary>
    public interface IScheduledTickable : ITickable
    {
        TickGroup Group { get; }

        /// <summary>Minimum elapsed seconds between ticks, or <c>0f</c> to tick every frame.</summary>
        float TickIntervalSeconds { get; }
    }

    /// <summary>
    /// Deterministic tick scheduler that drives all registered <see cref="ITickable"/> systems
    /// ordered by <see cref="TickGroup"/> and supports throttled interval ticking.
    /// </summary>
    public sealed class TickScheduler : IGameService
    {
        private sealed class TickEntry
        {
            public ITickable Target;
            public TickGroup Group;
            public float IntervalSeconds;
            public float AccumulatedTime;
        }

        private readonly List<TickEntry> _entries = new();
        private bool _isDirty;

        public int RegisteredCount => _entries.Count;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _entries.Clear();
            _isDirty = false;
        }

        /// <summary>
        /// Registers an <see cref="ITickable"/> system. If it implements <see cref="IScheduledTickable"/>,
        /// uses its declared group and interval; otherwise defaults to <see cref="TickGroup.Simulation"/> every frame.
        /// </summary>
        public void Register(ITickable tickable, TickGroup defaultGroup = TickGroup.Simulation, float defaultInterval = 0f)
        {
            if (tickable == null) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (ReferenceEquals(_entries[i].Target, tickable))
                {
                    return;
                }
            }

            TickGroup group = defaultGroup;
            float interval = defaultInterval;

            if (tickable is IScheduledTickable scheduled)
            {
                group = scheduled.Group;
                interval = scheduled.TickIntervalSeconds;
            }

            _entries.Add(new TickEntry
            {
                Target = tickable,
                Group = group,
                IntervalSeconds = interval > 0f ? interval : 0f,
                AccumulatedTime = 0f
            });

            _isDirty = true;
        }

        /// <summary>Unregisters a previously added <see cref="ITickable"/>.</summary>
        public void Unregister(ITickable tickable)
        {
            if (tickable == null) return;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_entries[i].Target, tickable))
                {
                    _entries.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>Ticks all active entries in ascending <see cref="TickGroup"/> order.</summary>
        public void TickAll(float deltaTime)
        {
            if (_entries.Count == 0) return;

            if (_isDirty)
            {
                _entries.Sort((a, b) => a.Group.CompareTo(b.Group));
                _isDirty = false;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                TickEntry entry = _entries[i];
                if (entry.Target == null) continue;

                if (entry.IntervalSeconds <= 0f)
                {
                    entry.Target.Tick(deltaTime);
                }
                else
                {
                    entry.AccumulatedTime += deltaTime;
                    if (entry.AccumulatedTime >= entry.IntervalSeconds)
                    {
                        float stepDelta = entry.AccumulatedTime;
                        entry.AccumulatedTime = 0f;
                        entry.Target.Tick(stepDelta);
                    }
                }
            }
        }
    }
}
