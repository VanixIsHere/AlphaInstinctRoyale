using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace AudioSystem
{
    public class SoundEmitter : MonoBehaviour
    {
        public SoundData Data { get; private set; }
        AudioSource audioSource;
        Coroutine playingCoroutine;

        void Awake()
        {
            audioSource = gameObject.GetOrAddComponent<AudioSource>();
        }

        public void Play()
        {
            if (playingCoroutine != null)
            {
                StopCoroutine(playingCoroutine);
            }

            audioSource.Play();
            playingCoroutine = StartCoroutine(WaitForSoundToEnd());
        }

        IEnumerator WaitForSoundToEnd()
        {
            yield return new WaitWhile(() => audioSource.isPlaying);
            AudioManager.Instance.ReturnToPool(this);
        }

        public void Stop()
        {
            if (playingCoroutine != null)
            {
                StopCoroutine(playingCoroutine);
                playingCoroutine = null;
            }

            audioSource.Stop();
            AudioManager.Instance.ReturnToPool(this);
        }

        public void Initialize(SoundData data)
        {
            Data = data;
            audioSource.clip = data.clip;
            audioSource.outputAudioMixerGroup = data.mixerGroup;
            audioSource.loop = data.loop;
            audioSource.playOnAwake = data.playOnAwake;
            audioSource.volume = data.baseVolume;
        }

        public void WithRandomPitch(float min = -0.05f, float max = 0.05f)
        {
            audioSource.pitch += UnityEngine.Random.Range(min, max);
        }
    }
}