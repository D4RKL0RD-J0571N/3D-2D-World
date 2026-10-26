//using UnityEngine;
//using UnityEditor;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//
//namespace NobleMuffins.LimbHacker
//{
//	[CustomEditor (typeof(Hackable))]
//	public class HackableEditor : Editor
//	{
//		Hackable manager;
//
//		void OnEnable ()
//		{
//			manager = (Hackable)target;
//		}
//
//		public override void OnInspectorGUI ()
//		{
//			DrawDefaultInspector ();
//
//			EditorGUILayout.Space ();
//
//			if (GUILayout.Button ("Search Body Parts")) {
//				manager.searchBodyParts ();
//			}
//
//			EditorGUILayout.Space ();
//		}
//
//		public static IEnumerable<Transform> FindBonesInTree (GameObject go)
//		{
//			var skinnedMeshRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer> (true);
//
//			var bones = new HashSet<Transform> ();
//
//			var rootCandidates = new HashSet<Transform> ();
//
//			foreach (var smr in skinnedMeshRenderers) {
//				if (smr.rootBone != null) {
//					rootCandidates.Add (smr.rootBone);
//				} else {
//					//Just pick a bone and crawl up the path until we can verify that we found the root.
//
//					var rootCandidate = smr.bones.First ();
//
//					while (IsThisTheRootBone (rootCandidate, smr.bones)) {
//						rootCandidate = rootCandidate.parent;
//					}
//
//					rootCandidates.Add (rootCandidate);
//				}
//
//				foreach (var bone in smr.bones) {
//					bones.Add (bone);
//				}
//			}
//
//			//LimbHacker requires a single tree; there must be precisely one root. Conceptually
//			//a root has no parent. In Unity, the root may have a parent but that is fine provided
//			//that the parent is not part of the bone set.
//
//			//First we need to determine, from the set of root candidates, what the root is.
//			//The root is the root candidate for which every other root is a child.
//
//			Transform root = null;
//
//			if (rootCandidates.Count == 1) {
//				root = rootCandidates.First ();
//			} else if (rootCandidates.Count > 0) {
//				foreach (var rootCandidate in rootCandidates) {
//					bool valid = true;
//
//					foreach (var possibleChild in rootCandidates) {
//						if (possibleChild == rootCandidate)
//							continue;
//
//						valid &= IsThisChildOfThat (possibleChild, rootCandidate);
//
//						if (!valid)
//							break;
//					}
//
//					if (valid) {
//						root = rootCandidate;
//						break;
//					}
//				}
//			}
//
//			if (root == null) {
//				var boneDescriptor = new StringBuilder ();
//				foreach (var bone in bones) {
//					boneDescriptor.AppendFormat ("{0}: {1}\n", bone.name, bone.parent == null ? "nil" : bone.parent.name);
//				}
//				throw new ForestException (string.Format ("{0} does not have a single, valid tree. LimbHacker compatible objects must have a single bone tree. Tree dump:\n{1}", go.name, boneDescriptor.ToString ()));
//			}
//
//			return bones;
//		}
//
//		private static bool IsThisTheRootBone (Transform candidate, ICollection<Transform> boneSet)
//		{
//			//A candidate can't be anything but the root if its parent is null.
//
//			if (candidate.parent == null) {
//				return true;
//			}
//
//			//A candidate is NOT the root if its parent is a subset of the bone set.
//
//			if (boneSet.Contains (candidate.parent)) {
//				return false;
//			}
//
//			//A candidate is the root bone if every other bone is a child of it.
//
//			bool allOthersAreChildren = true;
//
//			foreach (var bone in boneSet) {
//				if (candidate == bone.parent) {
//					continue;
//				}
//				if (IsThisChildOfThat (bone, candidate) == false) {
//					allOthersAreChildren = false;
//					break;
//				}
//			}
//
//			return allOthersAreChildren;
//		}
//
//
//		public class ForestException : System.Exception
//		{
//			public ForestException (string message) : base (message)
//			{
//			}
//		}
//
//		private static bool IsThisChildOfThat (Transform subject, Transform parent)
//		{
//			do {
//				subject = subject.parent;
//			} while (subject != null && subject != parent);
//			return subject == parent;
//		}
//	}
//}