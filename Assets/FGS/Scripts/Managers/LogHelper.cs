using UnityEngine;


namespace FGS
{
    public static class LogHelper
    {
        public static void Info(Object ctx, string msg) =>
            Debug.Log($"[{ctx.GetType().Name}] {msg}");

        public static void Warn(Object ctx, string msg) =>
            Debug.LogWarning($"[{ctx.GetType().Name}] {msg}");

        public static void Error(Object ctx, string msg) =>
            Debug.LogError($"[{ctx.GetType().Name}] {msg}");
    }
}
