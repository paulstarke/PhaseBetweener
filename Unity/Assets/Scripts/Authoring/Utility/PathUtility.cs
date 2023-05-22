using System.Collections;
using UnityEngine;

namespace PathCreation.Utility
{
    public static class PathUtility
    {
		/// Returns point at time 't' (between 0 and 1)  along catmull rom spline defined by 4 points ( control_1, control_2, control_3, control_4)
		public static Vector3 GetCatmullRomVector(float t, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
		{
			Vector3 a = 2f * v1;
			Vector3 b = v2 - v0;
			Vector3 c = 2f * v0 - 5f * v1 + 4f * v2 - v3;
			Vector3 d = -v0 + 3f * v1 - 3f * v2 + v3;
			return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
		}

		public static Vector3 GetCatmullRomVector(float t, Vector3[] cVectors)
        {
			return GetCatmullRomVector(t, cVectors[0], cVectors[1], cVectors[2], cVectors[3]);
		}

		public static float GetCatmullRomValue(float t, float v0, float v1, float v2, float v3)
		{
			float a = 2f * v1;
			float b = v2 - v0;
			float c = 2f * v0 - 5f * v1 + 4f * v2 - v3;
			float d = -v0 + 3f * v1 - 3f * v2 + v3;
			return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
		}
		public static float GetCatmullRomValue(float t, float[] cValues)
		{
			return GetCatmullRomValue(t, cValues[0], cValues[1], cValues[2], cValues[3]);
		}

		//e.g.  float[] array = CreateArray(0f, 1f, 2f);
		public static T[] CreateArray<T>(params T[] values)
		{
			return values;
		}

		public static Vector3 ClosestPointOnLineSegment(Vector3 p, Vector3 a, Vector3 b)
		{
			Vector3 aB = b - a;
			Vector3 aP = p - a;
			float sqrLenAB = aB.sqrMagnitude;

			if (sqrLenAB == 0)
				return a;

			float t = Mathf.Clamp01(Vector3.Dot(aP, aB) / sqrLenAB);
			return a + aB * t;
		}
	}
}