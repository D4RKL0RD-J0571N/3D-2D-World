#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    internal sealed class PrefabReuseCandidates
    {
        private sealed class CandidateNode
        {
            internal SaveablePrefab Prefab;
            internal int ObjectId;
            internal CandidateNode Previous;
            internal CandidateNode Next;
            internal CandidateNode EmptyPrevious;
            internal CandidateNode EmptyNext;
            internal CandidateNode HandoffPrevious;
            internal CandidateNode HandoffNext;
            internal CandidateNode SameIdNext;
            internal CandidateNode SpatialParent;
            internal CandidateNode SpatialLeft;
            internal CandidateNode SpatialRight;
            internal int SpatialAxis;
            internal int SpatialAvailableCount;
            internal bool HasEmptyId;
            internal bool IsHandoffCompatible;
            internal bool Available = true;
        }

        private sealed class PositionAxisComparer : IComparer<CandidateNode>
        {
            private readonly int axis;
            internal PositionAxisComparer(int axis)
            {
                this.axis = axis;
            }

            public int Compare(CandidateNode left, CandidateNode right)
            {
                float leftCoordinate = left.Prefab.transform.position[axis];
                float rightCoordinate = right.Prefab.transform.position[axis];
                int coordinateComparison = leftCoordinate.CompareTo(rightCoordinate);
                return coordinateComparison != 0 ? coordinateComparison : CompareStableSceneOrder(left.Prefab, right.Prefab);
            }
        }

        private static readonly IComparer<CandidateNode>[] PositionComparers =
        {
            new PositionAxisComparer(0),
            new PositionAxisComparer(1),
            new PositionAxisComparer(2)
        };
        private readonly Dictionary<int, CandidateNode> byObjectId = new();
        private readonly Dictionary<string, CandidateNode> exactHeads = new Dictionary<string, CandidateNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, CandidateNode> exactTails = new Dictionary<string, CandidateNode>(StringComparer.Ordinal);
        private readonly List<CandidateNode> nodes = new List<CandidateNode>();
        private CandidateNode first;
        private CandidateNode last;
        private CandidateNode firstEmpty;
        private CandidateNode lastEmpty;
        private CandidateNode firstHandoff;
        private CandidateNode lastHandoff;
        private CandidateNode emptySpatialRoot;
        private CandidateNode nonEmptySpatialRoot;
        private int emptyCount;
        private int handoffCount;
        internal int Count { get; private set; }
        internal bool RequestsSceneReuse { get; private set; }

        internal void Add(SaveablePrefab prefab)
        {
            if (prefab == null)
                return;
            int objectId = UnityObjectHelper.GetUniqueId(prefab);
            if (byObjectId.ContainsKey(objectId))
                return;
            var node = new CandidateNode
            {
                Prefab = prefab,
                ObjectId = objectId,
                HasEmptyId = string.IsNullOrEmpty(prefab.UniqueID),
                IsHandoffCompatible = !prefab.IsAddedAtRuntime && !prefab.KeepAcrossScenes,
                Previous = last
            };
            byObjectId.Add(objectId, node);
            nodes.Add(node);
            Count++;
            string uniqueId = prefab.UniqueID;
            if (!node.HasEmptyId && exactTails.TryGetValue(uniqueId, out CandidateNode exactTail))
            {
                exactTail.SameIdNext = node;
                exactTails[uniqueId] = node;
            }
            else if (!node.HasEmptyId)
            {
                exactHeads.Add(uniqueId, node);
                exactTails.Add(uniqueId, node);
            }

            RequestsSceneReuse |= prefab.ReuseSceneInstanceOnLoad;
        }

        internal void Prepare()
        {
            nodes.Sort((left, right) => CompareStableSceneOrder(left.Prefab, right.Prefab));
            first = null;
            last = null;
            firstEmpty = null;
            lastEmpty = null;
            firstHandoff = null;
            lastHandoff = null;
            emptyCount = 0;
            handoffCount = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                CandidateNode node = nodes[i];
                node.Previous = last;
                node.Next = null;
                if (last != null)
                    last.Next = node;
                else
                    first = node;
                last = node;
                if (node.HasEmptyId)
                {
                    node.EmptyPrevious = lastEmpty;
                    node.EmptyNext = null;
                    if (lastEmpty != null)
                        lastEmpty.EmptyNext = node;
                    else
                        firstEmpty = node;
                    lastEmpty = node;
                    emptyCount++;
                }

                if (node.IsHandoffCompatible)
                {
                    node.HandoffPrevious = lastHandoff;
                    node.HandoffNext = null;
                    if (lastHandoff != null)
                        lastHandoff.HandoffNext = node;
                    else
                        firstHandoff = node;
                    lastHandoff = node;
                    handoffCount++;
                }
            }

            BuildSpatialIndex(nodeHasEmptyId: true);
            BuildSpatialIndex(nodeHasEmptyId: false);
        }

        internal bool TryClaim(SaveablePrefabData data, bool crossSceneHandoff, out SaveablePrefab chosen, out string reason, out int compatibleHandoffCount)
        {
            chosen = null;
            reason = string.Empty;
            compatibleHandoffCount = handoffCount;
            if (data == null || Count == 0)
                return false;
            CandidateNode node;
            if (crossSceneHandoff)
            {
                if (handoffCount != 1)
                    return false;
                node = firstHandoff;
                reason = "crossSceneStateHandoff";
            }
            else
            {
                node = FindExact(data.InstanceID);
                if (node != null)
                {
                    reason = "byUniqueID";
                }
                else if (Count == 1)
                {
                    node = first;
                    reason = "singleCandidate";
                }
                else if (emptyCount > 0)
                {
                    if (emptyCount == Count)
                    {
                        // Preserve the prior stable hierarchy-order behavior when
                        // every remaining candidate has an empty ID.
                        node = firstEmpty;
                        reason = emptyCount == 1 ? "emptyUID" : "stableOrder";
                    }
                    else
                    {
                        node = FindNearest(data.Position, emptySpatialRoot);
                        reason = emptyCount == 1 ? "emptyUID" : "emptyUIDNearest";
                    }
                }
                else
                {
                    node = FindNearest(data.Position, nonEmptySpatialRoot);
                    reason = "nearestByPosition";
                }
            }

            if (node == null || !node.Available || node.Prefab == null)
                return false;
            chosen = node.Prefab;
            RemoveNode(node);
            return true;
        }

        internal void Remove(SaveablePrefab prefab)
        {
            if (prefab == null)
                return;
            if (byObjectId.TryGetValue(UnityObjectHelper.GetUniqueId(prefab), out CandidateNode node))
            {
                RemoveNode(node);
            }
        }

        private CandidateNode FindExact(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !exactHeads.TryGetValue(instanceId, out CandidateNode node))
            {
                return null;
            }

            while (node != null && (!node.Available || node.Prefab == null))
                node = node.SameIdNext;
            if (node == null)
            {
                exactHeads.Remove(instanceId);
                exactTails.Remove(instanceId);
                return null;
            }

            exactHeads[instanceId] = node;
            return node;
        }

        private void RemoveNode(CandidateNode node)
        {
            if (node == null || !node.Available)
                return;
            node.Available = false;
            Count--;
            if (node.Previous != null)
                node.Previous.Next = node.Next;
            else
                first = node.Next;
            if (node.Next != null)
                node.Next.Previous = node.Previous;
            else
                last = node.Previous;
            if (node.HasEmptyId)
            {
                emptyCount--;
                if (node.EmptyPrevious != null)
                    node.EmptyPrevious.EmptyNext = node.EmptyNext;
                else
                    firstEmpty = node.EmptyNext;
                if (node.EmptyNext != null)
                    node.EmptyNext.EmptyPrevious = node.EmptyPrevious;
                else
                    lastEmpty = node.EmptyPrevious;
            }

            if (node.IsHandoffCompatible)
            {
                handoffCount--;
                if (node.HandoffPrevious != null)
                    node.HandoffPrevious.HandoffNext = node.HandoffNext;
                else
                    firstHandoff = node.HandoffNext;
                if (node.HandoffNext != null)
                    node.HandoffNext.HandoffPrevious = node.HandoffPrevious;
                else
                    lastHandoff = node.HandoffPrevious;
            }

            byObjectId.Remove(node.ObjectId);
            for (CandidateNode spatial = node; spatial != null; spatial = spatial.SpatialParent)
            {
                spatial.SpatialAvailableCount--;
            }
        }

        private void BuildSpatialIndex(bool nodeHasEmptyId)
        {
            int count = nodeHasEmptyId ? emptyCount : Count - emptyCount;
            if (count == 0)
                return;
            var categoryNodes = new CandidateNode[count];
            int next = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                CandidateNode node = nodes[i];
                if (node.HasEmptyId == nodeHasEmptyId)
                    categoryNodes[next++] = node;
            }

            CandidateNode root = BuildSpatialSubtree(categoryNodes, start: 0, length: categoryNodes.Length, depth: 0, parent: null);
            if (nodeHasEmptyId)
                emptySpatialRoot = root;
            else
                nonEmptySpatialRoot = root;
        }

        private static CandidateNode BuildSpatialSubtree(CandidateNode[] categoryNodes, int start, int length, int depth, CandidateNode parent)
        {
            if (length <= 0)
                return null;
            int axis = depth % 3;
            Array.Sort(categoryNodes, start, length, PositionComparers[axis]);
            int middle = start + length / 2;
            CandidateNode node = categoryNodes[middle];
            node.SpatialParent = parent;
            node.SpatialAxis = axis;
            node.SpatialLeft = BuildSpatialSubtree(categoryNodes, start, middle - start, depth + 1, node);
            node.SpatialRight = BuildSpatialSubtree(categoryNodes, middle + 1, start + length - middle - 1, depth + 1, node);
            node.SpatialAvailableCount = 1 + (node.SpatialLeft?.SpatialAvailableCount ?? 0) + (node.SpatialRight?.SpatialAvailableCount ?? 0);
            return node;
        }

        private static CandidateNode FindNearest(Vector3 position, CandidateNode categoryRoot)
        {
            CandidateNode best = null;
            float bestDistance = float.PositiveInfinity;
            SearchNearest(categoryRoot, position, ref best, ref bestDistance);
            return best;
        }

        private static void SearchNearest(CandidateNode node, Vector3 position, ref CandidateNode best, ref float bestDistance)
        {
            if (node == null || node.SpatialAvailableCount == 0)
                return;
            float delta = position[node.SpatialAxis] - node.Prefab.transform.position[node.SpatialAxis];
            CandidateNode near = delta <= 0f ? node.SpatialLeft : node.SpatialRight;
            CandidateNode far = delta <= 0f ? node.SpatialRight : node.SpatialLeft;
            SearchNearest(near, position, ref best, ref bestDistance);
            if (node.Available)
            {
                float distance = Vector3.SqrMagnitude(node.Prefab.transform.position - position);
                if (distance < bestDistance || (distance.Equals(bestDistance) && CompareNearestTie(node.Prefab, best?.Prefab) < 0))
                {
                    best = node;
                    bestDistance = distance;
                }
            }

            if (delta * delta <= bestDistance)
                SearchNearest(far, position, ref best, ref bestDistance);
        }

        private static int CompareNearestTie(SaveablePrefab left, SaveablePrefab right)
        {
            if (right == null)
                return -1;
            int siblingComparison = left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
            if (siblingComparison != 0)
                return siblingComparison;
            int nameComparison = string.Compare(left.name, right.name, StringComparison.Ordinal);
            return nameComparison != 0 ? nameComparison : UnityObjectHelper.GetUniqueId(left).CompareTo(UnityObjectHelper.GetUniqueId(right));
        }

        internal static int CompareStableSceneOrder(SaveablePrefab left, SaveablePrefab right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            Transform leftTransform = left.transform;
            Transform rightTransform = right.transform;
            int leftDepth = GetTransformDepth(leftTransform);
            int rightDepth = GetTransformDepth(rightTransform);
            int commonDepth = Math.Min(leftDepth, rightDepth);
            for (int depth = 0; depth <= commonDepth; depth++)
            {
                Transform leftSegment = Ascend(leftTransform, leftDepth - depth);
                Transform rightSegment = Ascend(rightTransform, rightDepth - depth);
                int segmentComparison = string.Compare(leftSegment.name, rightSegment.name, StringComparison.Ordinal);
                if (segmentComparison != 0)
                    return segmentComparison;
            }

            int depthComparison = leftDepth.CompareTo(rightDepth);
            if (depthComparison != 0)
                return depthComparison;
            return CompareNearestTie(left, right);
        }

        private static Transform Ascend(Transform transform, int levels)
        {
            while (levels-- > 0)
                transform = transform.parent;
            return transform;
        }

        private static int GetTransformDepth(Transform transform)
        {
            int depth = 0;
            while (transform != null && transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }
    }
}
#endif
