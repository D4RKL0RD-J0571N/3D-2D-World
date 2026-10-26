using UnityEngine;

namespace NaughtyWaterBuoyancy
{
	public static class DebugUtils
	{
		public static void DrawPoint (Vector3 point)
		{
			DrawPoint (point, Color.white);
		}

		public static void DrawPoint (Vector3 point, Color color)
		{
			Debug.DrawLine (point - 0.1f * Vector3.right, point + 0.1f * Vector3.right, color);

			Debug.DrawLine (point - 0.1f * Vector3.up, point + 0.1f * Vector3.up, color);

			Debug.DrawLine (point - 0.1f * Vector3.forward, point + 0.1f * Vector3.forward, color);
		}

		public static void DrawPoint (float x, float y, float z)
		{
			DrawPoint (new Vector3 (x, y, z));
		}

		public static void DrawPoint (float x, float y, float z, Color color)
		{
			DrawPoint (new Vector3 (x, y, z), color);
		}
	}
}
