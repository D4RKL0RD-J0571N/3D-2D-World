#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Maintains every owner for a lookup key in priority order. Removing or
    /// invalidating the current owner promotes the next owner without a scene
    /// scan or a per-resolution allocation.
    /// </summary>
    internal sealed class PrefabOwnerLookup
    {
        private sealed class OwnerNode
        {
            internal string Key;
            internal GameObject Owner;
            internal OwnerNode KeyPrevious;
            internal OwnerNode KeyNext;
            internal OwnerNode OwnerPrevious;
            internal OwnerNode OwnerNext;
        }

        private sealed class OwnerChain
        {
            internal OwnerNode First;
            internal OwnerNode Last;
        }

        private readonly Dictionary<string, OwnerChain> ownersByKey = new Dictionary<string, OwnerChain>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, OwnerNode> firstNodeByOwner = new Dictionary<GameObject, OwnerNode>();
        internal void Add(string key, GameObject owner, bool promote)
        {
            if (string.IsNullOrEmpty(key) || owner == null)
                return;
            OwnerNode existing = FindNode(owner, key);
            if (existing != null)
            {
                if (promote)
                    Promote(existing);
                return;
            }

            if (!ownersByKey.TryGetValue(key, out OwnerChain chain))
            {
                chain = new OwnerChain();
                ownersByKey.Add(key, chain);
            }

            var node = new OwnerNode
            {
                Key = key,
                Owner = owner
            };
            if (promote)
            {
                node.KeyNext = chain.First;
                if (chain.First != null)
                    chain.First.KeyPrevious = node;
                else
                    chain.Last = node;
                chain.First = node;
            }
            else
            {
                node.KeyPrevious = chain.Last;
                if (chain.Last != null)
                    chain.Last.KeyNext = node;
                else
                    chain.First = node;
                chain.Last = node;
            }

            if (firstNodeByOwner.TryGetValue(owner, out OwnerNode ownerHead))
            {
                node.OwnerNext = ownerHead;
                ownerHead.OwnerPrevious = node;
            }

            firstNodeByOwner[owner] = node;
        }

        internal bool TryGet(string key, out GameObject owner)
        {
            owner = null;
            if (string.IsNullOrEmpty(key) || !ownersByKey.TryGetValue(key, out OwnerChain chain))
            {
                return false;
            }

            while (chain.First != null && (ReferenceEquals(chain.First.Owner, null) || chain.First.Owner == null))
            {
                Detach(chain.First);
                if (!ownersByKey.TryGetValue(key, out chain))
                    return false;
            }

            owner = chain.First?.Owner;
            return owner != null;
        }

        internal void Remove(string key, GameObject owner)
        {
            if (string.IsNullOrEmpty(key) || ReferenceEquals(owner, null))
            {
                return;
            }

            OwnerNode node = FindNode(owner, key);
            if (node != null)
                Detach(node);
        }

        internal void RemoveOwner(GameObject owner)
        {
            if (ReferenceEquals(owner, null) || !firstNodeByOwner.TryGetValue(owner, out OwnerNode node))
            {
                return;
            }

            while (node != null)
            {
                OwnerNode next = node.OwnerNext;
                Detach(node);
                node = next;
            }
        }

        private OwnerNode FindNode(GameObject owner, string key)
        {
            if (!firstNodeByOwner.TryGetValue(owner, out OwnerNode node))
                return null;
            while (node != null)
            {
                if (string.Equals(node.Key, key, StringComparison.Ordinal))
                    return node;
                node = node.OwnerNext;
            }

            return null;
        }

        private void Promote(OwnerNode node)
        {
            if (node == null || !ownersByKey.TryGetValue(node.Key, out OwnerChain chain) || chain.First == node)
            {
                return;
            }

            if (node.KeyPrevious != null)
                node.KeyPrevious.KeyNext = node.KeyNext;
            if (node.KeyNext != null)
                node.KeyNext.KeyPrevious = node.KeyPrevious;
            else
                chain.Last = node.KeyPrevious;
            node.KeyPrevious = null;
            node.KeyNext = chain.First;
            chain.First.KeyPrevious = node;
            chain.First = node;
        }

        private void Detach(OwnerNode node)
        {
            if (node == null || !ownersByKey.TryGetValue(node.Key, out OwnerChain chain))
            {
                return;
            }

            if (node.KeyPrevious != null)
                node.KeyPrevious.KeyNext = node.KeyNext;
            else
                chain.First = node.KeyNext;
            if (node.KeyNext != null)
                node.KeyNext.KeyPrevious = node.KeyPrevious;
            else
                chain.Last = node.KeyPrevious;
            if (chain.First == null)
                ownersByKey.Remove(node.Key);
            if (node.OwnerPrevious != null)
                node.OwnerPrevious.OwnerNext = node.OwnerNext;
            else if (!ReferenceEquals(node.Owner, null))
                firstNodeByOwner[node.Owner] = node.OwnerNext;
            if (node.OwnerNext != null)
                node.OwnerNext.OwnerPrevious = node.OwnerPrevious;
            if (!ReferenceEquals(node.Owner, null) && firstNodeByOwner.TryGetValue(node.Owner, out OwnerNode ownerHead) && ownerHead == null)
            {
                firstNodeByOwner.Remove(node.Owner);
            }

            node.KeyPrevious = null;
            node.KeyNext = null;
            node.OwnerPrevious = null;
            node.OwnerNext = null;
        }
    }
}
#endif
