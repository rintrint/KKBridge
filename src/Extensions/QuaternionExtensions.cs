using UnityEngine;

namespace KKBridge.Extensions
{
    public static class QuaternionExtensions
    {
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(new Quaternion(1, 2, 3, 4).sqrMagnitude());
        /// </summary>
        public static float sqrMagnitude(this Quaternion q)
        {
            return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        }
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(QuaternionExtensions.SqrMagnitude(new Quaternion(1, 2, 3, 4)));
        /// </summary>
        public static float SqrMagnitude(Quaternion q)
        {
            return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        }
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(new Quaternion(1, 2, 3, 4).magnitude());
        /// </summary>
        public static float magnitude(this Quaternion q)
        {
            return Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        }
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(QuaternionExtensions.Magnitude(new Quaternion(1, 2, 3, 4)));
        /// </summary>
        public static float Magnitude(Quaternion q)
        {
            return Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        }
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(new Quaternion(1, 2, 3, 4).normalized());
        /// </summary>
        public static Quaternion normalized(this Quaternion q)
        {
            float magnitude = Magnitude(q);

            if (magnitude > 1E-05f)
            {
                return new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
            }
            return Quaternion.identity;
        }
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(QuaternionExtensions.Normalize(new Quaternion(1, 2, 3, 4)));
        /// </summary>
        public static Quaternion Normalize(Quaternion q)
        {
            float magnitude = Magnitude(q);

            if (magnitude > 1E-05f)
            {
                return new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
            }
            return Quaternion.identity;
        }
        /// <summary>
        /// 使用方法:
        /// Quaternion q = new Quaternion(1, 2, 3, 4);
        /// q.Normalize();
        /// Log.LogInfo(q);
        /// </summary>
        public static void Normalize(this ref Quaternion q)
        {
            float magnitude = Magnitude(q);

            if (magnitude > 1E-05f)
            {
                q.Set(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
            }
            else
            {
                q = Quaternion.identity;
            }
        }
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(new Quaternion(1, 2, 3, 4).conjugated());
        /// </summary>
        public static Quaternion conjugated(this Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }
        /// <summary>
        /// 使用方法:
        /// Log.LogInfo(QuaternionExtensions.Conjugate(new Quaternion(1, 2, 3, 4)));
        /// </summary>
        public static Quaternion Conjugate(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }
        /// <summary>
        /// 使用方法:
        /// Quaternion q = new Quaternion(1, 2, 3, 4);
        /// q.Conjugate();
        /// Log.LogInfo(q);
        /// </summary>
        public static void Conjugate(this ref Quaternion q)
        {
            q.Set(-q.x, -q.y, -q.z, q.w);
        }
    }
}
