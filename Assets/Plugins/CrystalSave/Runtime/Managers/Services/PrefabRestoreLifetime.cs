#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    // Runtime-only lifetime owner for prefab batches and their temporary scene scopes.
    // Invalidate first, clear destination queues, then dispose retired batches: disposal
    // may invoke scene callbacks that start work belonging to the new generation.
    internal sealed class PrefabRestoreLifetime
    {
        object generation = new object ();
        readonly List<GenerationBoundRestore> batches = new();
        readonly List<SceneOverride> sceneOverrides = new();
        internal IEnumerator Bind(UnityEngine.Object host, IEnumerator routine) => new GenerationBoundRestore(this, host, routine);
        internal IDisposable OverrideScene(Scene scene) => new SceneOverride(this, scene);
        internal IDisposable[] Invalidate()
        {
            generation = new object ();
            return batches.ToArray();
        }

        internal void Retire() => DisposeRetired(Invalidate());
        internal static void DisposeRetired(IDisposable[] batches)
        {
            List<Exception> failures = null;
            for (int index = batches.Length - 1; index >= 0; index--)
            {
                try
                {
                    // Dispose now, before changing scenes: a yielded batch may temporarily
                    // own the active scene and a prefab-pool disposal suppression scope.
                    // Multiple batches can nest these scopes, so unwind newest first.
                    batches[index].Dispose();
                }
                catch (Exception error)
                {
                    (failures ??= new List<Exception>()).Add(error);
                }
            }

            if (failures != null)
                throw new AggregateException("Retiring previous prefab restoration failed.", failures);
        }

        // Streaming batches can yield and finish in a different order. Their temporary
        // active-scene overrides therefore cannot each restore an independent snapshot.
        private sealed class SceneOverride : IDisposable
        {
            private readonly PrefabRestoreLifetime owner;
            private readonly Scene target;
            private Scene previous;
            private SceneOverride previousOwner;
            private IDisposable poolDisposalSuppression;
            private bool disposed;
            internal SceneOverride(PrefabRestoreLifetime owner, Scene target)
            {
                this.owner = owner;
                this.target = target;
                previous = SceneManager.GetActiveScene();
                if (owner.sceneOverrides.Count > 0)
                {
                    SceneOverride latest = owner.sceneOverrides[owner.sceneOverrides.Count - 1];
                    if (latest.target == previous)
                        previousOwner = latest;
                }

                poolDisposalSuppression = SaveablePrefabPoolCache.SuppressDisposal();
                owner.sceneOverrides.Add(this);
                try
                {
                    SceneManager.SetActiveScene(target);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                var overrides = owner.sceneOverrides;
                bool restore = overrides.Count > 0 && ReferenceEquals(overrides[overrides.Count - 1], this) && SceneManager.GetActiveScene() == target;
                overrides.Remove(this);
                foreach (SceneOverride successor in overrides)
                {
                    if (!ReferenceEquals(successor.previousOwner, this))
                        continue;
                    successor.previous = previous;
                    successor.previousOwner = previousOwner;
                }

                try
                {
                    // An external scene switch supersedes this override. Do not undo it.
                    if (restore && previous.IsValid() && previous.isLoaded)
                        SceneManager.SetActiveScene(previous);
                }
                finally
                {
                    poolDisposalSuppression?.Dispose();
                    poolDisposalSuppression = null;
                }
            }
        }

        // Public streaming coroutines can outlive the snapshot that supplied their data.
        // Keep their lifetime separate from ClearPendingPrefabs, which only discards queues.
        private sealed class GenerationBoundRestore : IEnumerator, IDisposable
        {
            private readonly PrefabRestoreLifetime owner;
            private readonly object generation;
            private readonly UnityEngine.Object host;
            private IEnumerator routine;
            private bool registered;
            internal GenerationBoundRestore(PrefabRestoreLifetime owner, UnityEngine.Object host, IEnumerator routine)
            {
                this.owner = owner;
                this.host = host;
                generation = owner.generation;
                this.routine = routine;
            }

            public object Current => routine?.Current;

            public bool MoveNext()
            {
                if (routine == null)
                    return false;
                if (!host || !ReferenceEquals(generation, owner.generation))
                {
                    Dispose();
                    return false;
                }

                if (!registered)
                {
                    owner.batches.Add(this);
                    registered = true;
                }

                try
                {
                    if (routine.MoveNext())
                        return true;
                }
                catch
                {
                    Dispose();
                    throw;
                }

                Dispose();
                return false;
            }

            public void Reset() => throw new NotSupportedException();
            public void Dispose()
            {
                IEnumerator retired = routine;
                routine = null;
                if (registered)
                {
                    owner.batches.Remove(this);
                    registered = false;
                }

                (retired as IDisposable)?.Dispose();
            }
        }
    }
}
#endif

