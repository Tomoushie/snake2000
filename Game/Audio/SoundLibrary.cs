// /Game/Audio/SoundLibrary.cs
using System;
using System.Collections.Generic;
using System.Media;

namespace Snake2000.Audio
{
    public static class SoundLibrary
    {
        private static readonly Dictionary<string, SoundPlayer> _sounds = new();

        static SoundLibrary()
        {
            // Chargement des sons (exemples basiques)
            _sounds["Eat"] = new SoundPlayer(Properties.Resources.Eat);
            _sounds["Die"] = new SoundPlayer(Properties.Resources.Die);
            _sounds["PowerUp"] = new SoundPlayer(Properties.Resources.PowerUp);
            _sounds["Win"] = new SoundPlayer(Properties.Resources.Win);
            _sounds["MenuSelect"] = new SoundPlayer(Properties.Resources.MenuSelect);
        }

        public static void Play(string name)
        {
            if (_sounds.TryGetValue(name, out var player))
                player.Play();
        }

        public static void PlayAsync(string name)
        {
            if (_sounds.TryGetValue(name, out var player))
                player.PlaySync(); // Pour WinForms, PlaySync est plus fiable que Play() en arrière-plan
        }
    }
}