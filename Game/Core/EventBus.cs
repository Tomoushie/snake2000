// /Game/Core/EventBus.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Snake2000.Core
{
    public class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, List<WeakReference<Action<object>>>> _subscribers = new();

        public void Subscribe<T>(Action<T> handler) where T : class
        {
            var list = _subscribers.GetOrAdd(typeof(T), _ => new List<WeakReference<Action<object>>>());
            lock (list)
            {
                list.Add(new WeakReference<Action<object>>(args => handler((T)args)));
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (_subscribers.TryGetValue(typeof(T), out var list))
            {
                lock (list)
                {
                    list.RemoveAll(wr => wr.Target == null || wr.Target == (Action<object>)(object)handler);
                }
            }
        }

        public void Publish<T>(T @event) where T : class
        {
            if (_subscribers.TryGetValue(typeof(T), out var list))
            {
                List<Action<object>> handlersToInvoke = new List<Action<object>>();
                lock (list)
                {
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (list[i].TryGetTarget(out var handler))
                        {
                            handlersToInvoke.Add(handler);
                        }
                        else
                        {
                            list.RemoveAt(i); // Nettoyage des références mortes
                        }
                    }
                }
                foreach (var handler in handlersToInvoke)
                {
                    try { handler(@event); }
                    catch { /* Log error */ }
                }
            }
        }
    }

    // Événements clés
    public class GameStartedEvent { public GameMode Mode; }
    public class PlayerDiedEvent { public int LivesLeft; }
    public class AchievementUnlockedEvent { public string Id; public string Name; }
    public class HitStopTriggeredEvent { public float DurationMs; public HitStopType Type; }
    public class CameraShakeEvent { public float Intensity; public float Duration; public ShakeAxis Axis; }
    public class ParticleSpawnEvent { public Point Position; public Color Color; public int Lifetime; public ParticleType Type; }
    public class ChaosEffectActivatedEvent { public string EffectName; }
}

public enum HitStopType
{
    Eat, Kill, Combo, PowerUp, BossHit, Chaos
}

public enum ShakeAxis
{
    Both, X, Y
}

public enum ParticleType
{
    Spark, Trail, Impact, Weather, Chaos, Boss, UI
}