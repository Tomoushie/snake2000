// /Game/Core/IEventBus.cs
using System;

namespace Snake2000.Core
{
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : class;
        void Unsubscribe<T>(Action<T> handler) where T : class;
        void Publish<T>(T @event) where T : class;
    }
}