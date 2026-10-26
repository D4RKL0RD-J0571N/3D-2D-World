using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public static class DestroyHelper
	{
		public static void DestroyWithLogging(GameObject obj, string reason)
		{
			if (Logger.ShouldLog(LogCategory.Other))
			{
				Logger.Log($"Destroying GameObject '{Logger.GetUnityObjectName(obj)}' due to: {reason}", LogCategory.Other, LogLevel.Info);
			}
			if (Application.isPlaying)
				UnityEngine.Object.Destroy(obj);
			else
				UnityEngine.Object.DestroyImmediate(obj);
		}
	}
}
