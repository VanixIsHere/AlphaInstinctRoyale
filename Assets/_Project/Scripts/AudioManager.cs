using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;
using UnityUtils;

namespace AudioSystem {
    public class AudioManager : PersistentSingleton<AudioManager>
    {
        // public static AudioManager Instance;

        IObjectPool<SoundEmitter> soundEmitterPool;
        readonly List<SoundEmitter> activeSoundEmitters = new();
        public readonly Queue<SoundEmitter> FrequentSoundEmitters = new();

        [SerializeField] SoundEmitter soundEmitterPrefab;
        [SerializeField] bool collectionCheck = true;
        [SerializeField] int defaultCapacity = 10;
        [SerializeField] int maxPoolSize = 100;
        [SerializeField] int maxSoundInstances = 30;

        void Start()
        {
            InitializePool();
        }

        public SoundBuilder CreateSound() => new SoundBuilder(this);

        public bool CanPlaySound(SoundData data)
        {
            // Checks if the sound is being played more than 'maxSoundInstances'.
            if (!data.frequentSound) return true;
            if (FrequentSoundEmitters.Count >= maxSoundInstances && FrequentSoundEmitters.TryDequeue(out var soundEmitter))
            {
                try
                {
                    soundEmitter.Stop();
                    return true;
                }
                catch
                {
                    Debug.Log("Sound Emitter is already released");
                }
                return false;
            }
            return true;
        }

        public SoundEmitter Get()
        {
            return soundEmitterPool.Get();
        }

        public void ReturnToPool(SoundEmitter soundEmitter)
        {
            soundEmitterPool.Release(soundEmitter);
        }

        void OnDestroyPoolObject(SoundEmitter soundEmitter)
        {
            Destroy(soundEmitter.gameObject);
        }

        void OnReturnedToPool(SoundEmitter soundEmitter)
        {
            soundEmitter.gameObject.SetActive(false);
            activeSoundEmitters.Remove(soundEmitter);
        }

        void OnTakeFromPool(SoundEmitter soundEmitter)
        {
            soundEmitter.gameObject.SetActive(true);
            activeSoundEmitters.Add(soundEmitter);
        }

        SoundEmitter CreateSoundEmitter()
        {
            var soundEmitter = Instantiate(soundEmitterPrefab);
            soundEmitter.gameObject.SetActive(false);
            return soundEmitter;
        }

        void InitializePool()
        {
            soundEmitterPool = new ObjectPool<SoundEmitter>(
                CreateSoundEmitter,
                OnTakeFromPool,
                OnReturnedToPool,
                OnDestroyPoolObject,
                collectionCheck,
                defaultCapacity,
                maxPoolSize
            );
        }
    }
}
