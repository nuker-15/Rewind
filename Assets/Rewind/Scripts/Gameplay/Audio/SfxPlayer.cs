using Rewind.Core;
using Rewind.Data;
using UnityEngine;

namespace Rewind.Gameplay
{
    /// <summary>Plays gameplay sounds in response to <see cref="RewindGame"/> events.</summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] RewindGame game;
        [SerializeField] SfxLibrary library;
        [SerializeField] AudioSource effects;
        [SerializeField, Tooltip("Separate source so the rewind sweep can be pitched to fit its duration.")] AudioSource rewindSource;

        void OnEnable()
        {
            GameSettings.Apply();
            if (!game) return;
            game.Ticked += OnTicked;
            game.LoopStarted += OnLoopStarted;
            game.RewindStarted += OnRewindStarted;
            game.Solved += OnSolved;
        }

        void OnDisable()
        {
            if (!game) return;
            game.Ticked -= OnTicked;
            game.LoopStarted -= OnLoopStarted;
            game.RewindStarted -= OnRewindStarted;
            game.Solved -= OnSolved;
        }

        void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (clip && effects && library) effects.PlayOneShot(clip, library.volume * volumeScale);
        }

        void OnTicked(TickReport r)
        {
            if (!library) return;
            if (r.Paradoxes.Count > 0) Play(library.paradox);
            if (r.Crumbled.Count > 0) Play(library.crumble);
            if (r.Filled.Count > 0) Play(library.fill);
            if (r.CratesPushed > 0) Play(library.push);
            else if (!r.PlayerStep.IsWait) Play(library.step, 0.6f);
            else if (r.PlayerBumped) Play(library.bump, 0.6f);
            if (r.DoorsOpened) Play(library.doorOpen);
            else if (r.DoorsClosed) Play(library.doorClose);

            var config = game.Config;
            int tick = r.Tick + 1;
            if (tick >= config.LoopTicks - config.WarningTicks && tick < config.LoopTicks && tick % config.TicksPerSecond == 0)
                Play(library.warning, 0.7f);
        }

        void OnLoopStarted()
        {
            if (library && game.EchoCount > 0) Play(library.loopStart, 0.7f);
        }

        void OnRewindStarted(float seconds)
        {
            if (!library || !library.rewind || !rewindSource) return;
            rewindSource.clip = library.rewind;
            rewindSource.pitch = Mathf.Clamp(library.rewind.length / Mathf.Max(0.1f, seconds), 0.5f, 3f);
            rewindSource.volume = library.volume * 0.7f;
            rewindSource.Play();
        }

        void OnSolved(SolveResult result)
        {
            if (rewindSource) rewindSource.Stop();
            if (library) Play(library.solved);
        }
    }
}
