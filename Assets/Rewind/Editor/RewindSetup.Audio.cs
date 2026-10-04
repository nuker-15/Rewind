using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rewind.EditorTools
{
    // Synthesised placeholder sounds written as WAV files. Replace any of them with real audio.
    public static partial class RewindSetup
    {
        static class Sfx
        {
            public static AudioClip Step, Bump, Push, DoorOpen, DoorClose, Paradox, Crumble, Fill, Rewind, LoopStart,
                Warning, Solved, UiMove, UiSelect;
        }

        const int SampleRate = 44100;
        enum Wave { Sine, Square, Triangle, Saw }

        sealed class Synth
        {
            public readonly float[] Buffer;
            readonly System.Random random = new System.Random(7);

            public Synth(float seconds) => Buffer = new float[Mathf.CeilToInt(seconds * SampleRate)];

            public Synth Tone(float start, float duration, float f0, float f1, Wave wave, float volume)
            {
                int s0 = Mathf.RoundToInt(start * SampleRate), n = Mathf.RoundToInt(duration * SampleRate);
                double phase = 0;
                for (int i = 0; i < n && s0 + i < Buffer.Length; i++)
                {
                    float t = i / (float)n;
                    float f = f1 > 0 && f1 != f0 ? f0 * Mathf.Pow(f1 / f0, t) : f0;   // exponential glide
                    phase += f / SampleRate;
                    float p = (float)(phase - Math.Floor(phase));
                    float v;
                    switch (wave)
                    {
                        case Wave.Square: v = p < 0.5f ? 1f : -1f; break;
                        case Wave.Triangle: v = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                        case Wave.Saw: v = 2f * p - 1f; break;
                        default: v = Mathf.Sin(p * Mathf.PI * 2f); break;
                    }
                    Buffer[s0 + i] += v * volume * Envelope(i, n);
                }
                return this;
            }

            public Synth Noise(float start, float duration, float cutoff0, float cutoff1, float volume)
            {
                int s0 = Mathf.RoundToInt(start * SampleRate), n = Mathf.RoundToInt(duration * SampleRate);
                float y = 0f;
                for (int i = 0; i < n && s0 + i < Buffer.Length; i++)
                {
                    float t = i / (float)n;
                    float cutoff = cutoff0 * Mathf.Pow(cutoff1 / cutoff0, t);
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);   // one-pole low-pass
                    y += a * ((float)random.NextDouble() * 2f - 1f - y);
                    Buffer[s0 + i] += y * volume * Envelope(i, n);
                }
                return this;
            }

            // 8 ms attack, then an exponential fall to silence, like the browser version's gain ramps.
            static float Envelope(int i, int n)
            {
                int attack = Mathf.Min(n / 4, Mathf.RoundToInt(0.008f * SampleRate));
                if (i < attack) return i / (float)Mathf.Max(1, attack);
                float t = (i - attack) / (float)Mathf.Max(1, n - attack);
                return Mathf.Pow(0.0001f, t);
            }
        }

        static void BuildAudio()
        {
            Progress("Synthesising sounds", 0.2f);
            var rng = new System.Random(3);
            Sfx.Step = MakeClip("Step", new Synth(0.06f).Tone(0, 0.05f, 175, 0, Wave.Triangle, 0.45f));
            Sfx.Bump = MakeClip("Bump", new Synth(0.07f).Tone(0, 0.06f, 85, 0, Wave.Square, 0.25f));
            Sfx.Push = MakeClip("Push", new Synth(0.12f).Noise(0, 0.1f, 520, 520, 0.9f).Tone(0, 0.1f, 68, 0, Wave.Saw, 0.3f));
            Sfx.DoorOpen = MakeClip("DoorOpen", new Synth(0.16f).Tone(0, 0.14f, 380, 680, Wave.Triangle, 0.5f));
            Sfx.DoorClose = MakeClip("DoorClose", new Synth(0.16f).Tone(0, 0.14f, 560, 260, Wave.Triangle, 0.5f));
            var paradox = new Synth(0.42f).Noise(0, 0.35f, 4000, 300, 0.6f);
            for (int i = 0; i < 7; i++) paradox.Tone(i * 0.035f, 0.05f, 220 + (float)rng.NextDouble() * 1500, 0, Wave.Square, 0.3f);
            Sfx.Paradox = MakeClip("Paradox", paradox);
            Sfx.Crumble = MakeClip("Crumble", new Synth(0.42f).Noise(0, 0.4f, 300, 80, 1.4f));
            Sfx.Fill = MakeClip("Fill", new Synth(0.22f).Noise(0, 0.18f, 200, 200, 1.1f).Tone(0, 0.2f, 58, 0, Wave.Sine, 0.8f));
            Sfx.Rewind = MakeClip("Rewind", new Synth(1.5f).Tone(0, 1.5f, 900, 110, Wave.Saw, 0.22f).Noise(0, 1.5f, 2400, 400, 0.3f));
            Sfx.LoopStart = MakeClip("LoopStart", new Synth(0.15f).Tone(0, 0.05f, 1320, 0, Wave.Square, 0.25f).Tone(0.06f, 0.07f, 880, 0, Wave.Square, 0.25f));
            Sfx.Warning = MakeClip("Warning", new Synth(0.05f).Tone(0, 0.035f, 1250, 0, Wave.Square, 0.3f));
            var solved = new Synth(0.65f);
            float[] notes = { 523, 659, 784, 1046, 1318 };
            for (int i = 0; i < notes.Length; i++) solved.Tone(i * 0.08f, 0.2f, notes[i], 0, Wave.Square, 0.3f);
            Sfx.Solved = MakeClip("Solved", solved);
            Sfx.UiMove = MakeClip("UiMove", new Synth(0.05f).Tone(0, 0.035f, 620, 0, Wave.Square, 0.22f));
            Sfx.UiSelect = MakeClip("UiSelect", new Synth(0.12f).Tone(0, 0.05f, 880, 0, Wave.Square, 0.25f).Tone(0.05f, 0.06f, 1320, 0, Wave.Square, 0.22f));
        }

        static AudioClip MakeClip(string name, Synth synth)
        {
            string path = $"{AudioDir}/{name}.wav";
            if (ShouldBuild(path))
            {
                WriteWav(path, synth.Buffer);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static void WriteWav(string path, float[] samples)
        {
            float peak = 0f;
            foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            float gain = peak > 0.95f ? 0.95f / peak : 1f;
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);            // PCM
            w.Write((short)1);            // mono
            w.Write(SampleRate);
            w.Write(SampleRate * 2);      // byte rate
            w.Write((short)2);            // block align
            w.Write((short)16);           // bits per sample
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s * gain, -1f, 1f) * 32767f));
        }
    }
}
