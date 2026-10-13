#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Rewind.UI
{
    /// <summary>
    /// Copy and paste text. Browsers keep the clipboard away from WebGL builds, so there it goes through a small
    /// JavaScript plugin (Plugins/WebGL/RewindClipboard.jslib): copy uses the browser clipboard (or shows the text to
    /// copy by hand), and paste asks for the text in a prompt.
    /// </summary>
    public static class ClipboardBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void RewindClipboardCopy(string text);
        [DllImport("__Internal")] static extern string RewindClipboardPrompt(string message, string defaultText);
#endif

        public static void Copy(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RewindClipboardCopy(text ?? string.Empty);
#else
            GUIUtility.systemCopyBuffer = text ?? string.Empty;
#endif
        }

        /// <summary>Text to paste: the clipboard on desktop and mobile, a prompt in the browser.</summary>
        public static string Paste(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return RewindClipboardPrompt(message, string.Empty);
#else
            return GUIUtility.systemCopyBuffer;
#endif
        }
    }
}
