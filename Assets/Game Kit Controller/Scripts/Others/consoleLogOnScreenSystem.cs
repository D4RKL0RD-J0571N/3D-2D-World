using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class consoleLogOnScreenSystem : MonoBehaviour
{
    [Header ("UI")]
    [Space]

    public Text consoleLogText;
    public ScrollRect scrollRect;

    [Space]
    [Header ("General Options")]
    [Space]

    public int maxEntries = 100;
    public bool showOnlyMainMessage = false;
    public bool showStackTrace = true;
    public bool showTimestamp = true;
    public bool autoScroll = true;

    [Space]
    [Header ("Filters")]
    [Space]

    public bool showLogs = true;
    public bool showWarnings = true;
    public bool showErrors = true;

    static List<string> logs = new List<string> ();
    private bool needsRefresh = false;

    void OnEnable ()
    {
        Application.logMessageReceived += Log;
    }

    void OnDisable ()
    {
        Application.logMessageReceived -= Log;

        ClearConsole ();
    }

    public void Log (string logString, string stackTrace, LogType type)
    {
        // 🔍 FILTRO POR TIPO
        if (!ShouldShow (type)) return;

        string color = GetColor (type);

        // 🕒 TIMESTAMP
        string time = showTimestamp ? $"[{Time.time:0.00}] " : "";

        // 🧾 MENSAJE
        string message = showOnlyMainMessage
            ? logString
            : $"[{type}] {logString}";

        // 📌 STACKTRACE
        if (!showOnlyMainMessage && showStackTrace && !string.IsNullOrEmpty (stackTrace)) {
            message += "\n" + stackTrace;
        }

        message = $"{time}{message}";

        // 🎨 COLOR
        message = $"<color={color}>{message}</color>";

        logs.Insert (0, message);

        if (logs.Count > maxEntries)
            logs.RemoveAt (logs.Count - 1);

        needsRefresh = true;
    }

    void LateUpdate ()
    {
        if (!needsRefresh) return;

        consoleLogText.text = string.Join ("\n\n", logs);

        // 📜 AUTO SCROLL
        if (autoScroll && scrollRect != null) {
            Canvas.ForceUpdateCanvases ();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        needsRefresh = false;
    }

    bool ShouldShow (LogType type)
    {
        switch (type) {
            case LogType.Log:
                return showLogs;
            case LogType.Warning:
                return showWarnings;
            case LogType.Error:
            case LogType.Exception:
                return showErrors;
            default:
                return true;
        }
    }

    string GetColor (LogType type)
    {
        switch (type) {
            case LogType.Error:
            case LogType.Exception:
                return "red";

            case LogType.Warning:
                return "yellow";

            default:
                return "white";
        }
    }

    // 🧹 LIMPIAR CONSOLA (botón UI)
    public void ClearConsole ()
    {
        logs.Clear ();
        consoleLogText.text = "";
    }
}