// /Game/Core/EventBus.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using Snake2000.Gameplay;

namespace Snake2000.Core
{
    /// <summary>
    /// Bus d'événements du jeu.
    ///
    /// La version précédente stockait les abonnés dans des
    /// <c>WeakReference&lt;Action&lt;object&gt;&gt;</c> enveloppant un lambda créé
    /// à la volée. Deux conséquences :
    ///
    /// 1. Ce lambda n'était retenu par rien d'autre. Au premier passage du
    ///    ramasse-miettes l'abonné disparaissait, sans erreur ni trace : les
    ///    événements cessaient simplement d'arriver, quelques secondes après le
    ///    lancement.
    /// 2. <c>Unsubscribe</c> comparait le handler d'origine à l'enveloppe, deux
    ///    objets différents — la comparaison n'aurait jamais été vraie. Elle ne
    ///    compilait d'ailleurs pas : <c>WeakReference&lt;T&gt;</c> n'expose pas de
    ///    propriété <c>Target</c>, seulement <c>TryGetTarget</c>.
    ///
    /// Les abonnements sont désormais des références FORTES. Le contrat change
    /// donc : un abonné qui ne se désabonne pas reste en vie tant que le bus
    /// existe. C'est le comportement normal d'un bus d'événements, et c'est
    /// pour cela que <see cref="IEventBus"/> expose <c>Unsubscribe</c> — les
    /// systèmes à durée de vie limitée doivent l'appeler quand ils s'arrêtent.
    /// </summary>
    public class EventBus : IEventBus
    {
        /// <summary>
        /// Le handler d'origine sert de clé au désabonnement ; l'enveloppe est
        /// ce qu'on invoque. Garder les deux est ce qui rend
        /// <c>Unsubscribe</c> possible.
        /// </summary>
        private readonly struct Abonnement
        {
            public readonly object Origine;
            public readonly Action<object> Enveloppe;

            public Abonnement(object origine, Action<object> enveloppe)
            {
                Origine = origine;
                Enveloppe = enveloppe;
            }
        }

        private readonly ConcurrentDictionary<Type, List<Abonnement>> _abonnes = new();

        /// <summary>
        /// Appelé quand un abonné lève une exception. Sans ce point de sortie,
        /// le <c>catch</c> silencieux de la version précédente pouvait masquer
        /// une panne pendant des semaines. Le bus continue malgré tout : un
        /// abonné fautif ne doit pas interrompre la boucle de jeu ni priver les
        /// autres abonnés de l'événement.
        /// </summary>
        public Action<Type, Exception> OnHandlerError { get; set; }

        public void Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var liste = _abonnes.GetOrAdd(typeof(T), _ => new List<Abonnement>());
            lock (liste)
            {
                liste.Add(new Abonnement(handler, args => handler((T)args)));
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null) return;

            if (_abonnes.TryGetValue(typeof(T), out var liste))
            {
                lock (liste)
                {
                    liste.RemoveAll(a => a.Origine.Equals(handler));
                }
            }
        }

        public void Publish<T>(T @event) where T : class
        {
            if (@event == null) return;
            if (!_abonnes.TryGetValue(typeof(T), out var liste)) return;

            // Copie sous verrou, invocation hors verrou : un abonné qui
            // s'abonne ou se désabonne en réagissant à l'événement provoquerait
            // sinon un interblocage ou une modification de collection en cours
            // d'énumération.
            Abonnement[] instantane;
            lock (liste)
            {
                if (liste.Count == 0) return;
                instantane = liste.ToArray();
            }

            foreach (var abonnement in instantane)
            {
                try
                {
                    abonnement.Enveloppe(@event);
                }
                catch (Exception e)
                {
                    OnHandlerError?.Invoke(typeof(T), e);
                }
            }
        }

        /// <summary>
        /// Retire tous les abonnements. Utile entre deux parties, pour repartir
        /// d'un bus propre sans reconstruire les systèmes.
        /// </summary>
        public void Clear()
        {
            _abonnes.Clear();
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
