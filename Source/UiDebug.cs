using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KSTS
{
    // Diagnostics for the auto-sizing KSTS window, enabled with "debugUI = true" in KSTS.cfg. Every line starts with
    // "[KSTS-UI]" so it can be grepped out of KSP.log. Logs only on change, plus a warning when a size flips back and
    // forth (the signature of a layout feedback loop, which shows up in game as flicker).
    public static class UiDebug
    {
        private const int OSCILLATION_WINDOW = 8;        // Changes remembered per key.
        private const float OSCILLATION_SECONDS = 2f;    // That many changes within this time counts as oscillating.
        private const float WARNING_INTERVAL = 10f;      // Seconds between repeated warnings for the same key.

        private static readonly Dictionary<string, Queue<KeyValuePair<float, float>>> history = new Dictionary<string, Queue<KeyValuePair<float, float>>>();
        private static readonly Dictionary<string, float> lastWarning = new Dictionary<string, float>();
        private static readonly Dictionary<string, string> lastRequest = new Dictionary<string, string>();
        private static Rect lastWindowRect;

        private static bool Enabled => KSTSSettings.DebugUI;

        public static void ScrollRequested(string key, float contentHeight, float height, float maxHeight)
        {
            if (!Enabled) return;
            var summary = $"content={contentHeight:F0} applied={height:F0} max={maxHeight:F0}";
            if (lastRequest.TryGetValue(key, out var previous) && previous == summary) return;
            lastRequest[key] = summary;
            Debug.Log($"[KSTS-UI] scroll {key}: {summary} (screen {Screen.width}x{Screen.height}, scale {KSTSSettings.UiScale})");
        }

        public static void ContentMeasured(string key, float previous, float measured)
        {
            if (!Enabled) return;
            Debug.Log($"[KSTS-UI] measured {key}: {previous:F0} -> {measured:F0}");
            Track("content " + key, measured);
        }

        public static void WindowRect(Rect before, Rect after, int tab)
        {
            if (!Enabled) return;
            if (Approximately(after, lastWindowRect)) return;
            Debug.Log($"[KSTS-UI] window [{Event.current.type}] tab={tab} in={Format(before)} out={Format(after)}");
            lastWindowRect = after;
            Track("window height", after.height);
        }

        private static void Track(string key, float value)
        {
            if (!history.TryGetValue(key, out var changes)) history[key] = changes = new Queue<KeyValuePair<float, float>>();
            changes.Enqueue(new KeyValuePair<float, float>(Time.realtimeSinceStartup, value));
            while (changes.Count > OSCILLATION_WINDOW) changes.Dequeue();

            var now = Time.realtimeSinceStartup;
            if (changes.Count < OSCILLATION_WINDOW || now - changes.Peek().Key > OSCILLATION_SECONDS) return;
            if (lastWarning.TryGetValue(key, out var warnedAt) && now - warnedAt < WARNING_INTERVAL) return;
            lastWarning[key] = now;
            Debug.LogWarning($"[KSTS-UI] OSCILLATING {key}: {OSCILLATION_WINDOW} changes in {now - changes.Peek().Key:F2}s, values "
                + string.Join(", ", changes.Select(c => c.Value.ToString("F0"))));
        }

        private static bool Approximately(Rect a, Rect b) =>
            Mathf.Abs(a.x - b.x) < 1 && Mathf.Abs(a.y - b.y) < 1 && Mathf.Abs(a.width - b.width) < 1 && Mathf.Abs(a.height - b.height) < 1;

        private static string Format(Rect r) => $"({r.x:F0},{r.y:F0} {r.width:F0}x{r.height:F0})";
    }
}
