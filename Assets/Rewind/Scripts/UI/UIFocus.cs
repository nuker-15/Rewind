using TMPro;
using UnityEngine.EventSystems;

namespace Rewind.UI
{
    public static class UIFocus
    {
        /// <summary>True while a text field has the keyboard, so shortcut keys should be left alone.</summary>
        public static bool IsTyping
        {
            get
            {
                var es = EventSystem.current;
                var go = es ? es.currentSelectedGameObject : null;
                return go && go.TryGetComponent(out TMP_InputField field) && field.isFocused;
            }
        }
    }
}
