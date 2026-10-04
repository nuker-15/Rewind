using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>Gameplay sound effects. Leave a slot empty to keep that moment silent.</summary>
    [CreateAssetMenu(menuName = "Rewind/Sfx Library", fileName = "SfxLibrary", order = 5)]
    public sealed class SfxLibrary : ScriptableObject
    {
        public AudioClip step;
        public AudioClip bump;
        public AudioClip push;
        public AudioClip doorOpen;
        public AudioClip doorClose;
        public AudioClip paradox;
        public AudioClip crumble;
        public AudioClip fill;
        public AudioClip rewind;
        public AudioClip loopStart;
        public AudioClip warning;
        public AudioClip solved;
        [Range(0f, 1f)] public float volume = 0.8f;
    }
}
