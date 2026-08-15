// /Game/Systems/CameraShakeSystem.cs
using System;
using System.Drawing;
using Snake2000.Core;

namespace Snake2000.Systems
{
    public class CameraShakeSystem
    {
        private float _intensity;
        private float _duration;
        private float _timer;
        private ShakeAxis _axis;
        private Point _offset;
        private readonly IEventBus _eventBus;

        public CameraShakeSystem(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<CameraShakeEvent>(e => Trigger(e.Intensity, e.Duration, e.Axis));
        }

        public void Trigger(float intensity, float duration, ShakeAxis axis)
        {
            _intensity = intensity;
            _duration = duration;
            _timer = 0f;
            _axis = axis;
            _offset = Point.Empty;
        }

        public void Update(float deltaTime)
        {
            if (_timer >= _duration) return;

            _timer += deltaTime;
            float t = _timer / _duration;
            float decay = 1f - t;

            float noiseX = (float)Math.Sin(13.7f * t) * (float)Math.Cos(7.3f * t);
            float noiseY = (float)Math.Cos(11.3f * t) * (float)Math.Sin(5.9f * t);

            _offset = new Point(
                _axis != ShakeAxis.Y ? (int)(noiseX * _intensity * decay) : 0,
                _axis != ShakeAxis.X ? (int)(noiseY * _intensity * decay) : 0
            );
        }

        public Point GetOffset() => _offset;

        public void Reset() => _timer = _duration + 1f;
    }
}