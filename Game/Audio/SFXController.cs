// /Game/Audio/SFXController.cs
using System;
using System.Collections.Concurrent;

namespace Snake2000.Audio
{
    public class SFXController
    {
        private readonly ConcurrentQueue<(string name, DateTime timestamp)> _queue = new();
        private readonly object _lock = new();

        public void QueueSFX(string name)
        {
            _queue.Enqueue((name, DateTime.UtcNow));
        }

        public void Update(float deltaTime)
        {
            while (_queue.TryDequeue(out var item))
            {
                if ((DateTime.UtcNow - item.timestamp).TotalSeconds < 0.1) // Déduire le délai de latence
                    SoundLibrary.Play(item.name);
            }
        }
    }
}