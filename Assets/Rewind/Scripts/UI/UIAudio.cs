using UnityEngine;

namespace Rewind.UI
{
    /// <summary>Menu blips. Listens to <see cref="UIEvents"/>, so widgets stay unaware of audio.</summary>
    public sealed class UIAudio : MonoBehaviour
    {
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip navigate;
        [SerializeField] AudioClip activate;
        [SerializeField, Range(0f, 1f)] float volume = 0.5f;

        void OnEnable()
        {
            UIEvents.Navigated += OnNavigated;
            UIEvents.Activated += OnActivated;
        }

        void OnDisable()
        {
            UIEvents.Navigated -= OnNavigated;
            UIEvents.Activated -= OnActivated;
        }

        void OnNavigated() { if (source && navigate) source.PlayOneShot(navigate, volume); }
        void OnActivated() { if (source && activate) source.PlayOneShot(activate, volume); }
    }
}
