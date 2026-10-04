using UnityEngine;

namespace Rewind.UI
{
    /// <summary>The rules and legend. The content is authored in the prefab; this only wires the back button.</summary>
    public sealed class HowToPlayPanel : UIScreen
    {
        [SerializeField] MenuButton back;

        void Awake()
        {
            if (back) back.Button.onClick.AddListener(() => router.CloseHowTo());
        }
    }
}
