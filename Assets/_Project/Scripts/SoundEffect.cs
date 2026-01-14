using System;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioSystem
{
    [Serializable]
    public class SoundData
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float baseVolume = 1f;
        public AudioMixerGroup mixerGroup;
        public bool loop;
        public bool playOnAwake;
        public bool frequentSound;
    }
}