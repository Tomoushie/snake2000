// /Game/Audio/MusicController.cs
using System;
using System.Media;

namespace Snake2000.Audio
{
    public class MusicController
    {
        private SoundPlayer _currentMusic;
        private readonly Dictionary<string, SoundPlayer> _tracks = new();

        public void LoadTrack(string name, string filePath)
        {
            _tracks[name] = new SoundPlayer(filePath);
        }

        public void Play(string name, bool loop = true)
        {
            if (_currentMusic != null) _currentMusic.Stop();
            if (_tracks.TryGetValue(name, out var track))
            {
                _currentMusic = track;
                _currentMusic.PlayLooping();
            }
        }

        public void Stop()
        {
            _currentMusic?.Stop();
            _currentMusic = null;
        }

        public void Pause() => _currentMusic?.Stop();
        public void Resume() => _currentMusic?.PlayLooping();
    }
}