using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Snake2000.Engine.AI;
using Snake2000.Engine.Animation;
using Snake2000.Engine.Gameplay;
using Snake2000.Engine.Physics;
namespace Snake2000.Engine.Core;


// --- 137. CORE ENGINE (Déjà présent, à étendre) ---

// Messaging interne (Déjà présent)
public interface IMessage { }

public class GameStartedMessage : IMessage { }
public class GamePausedMessage : IMessage { }
public class GameResumedMessage : IMessage { }
public class GameStoppedMessage : IMessage { }

// EventBus (Déjà présent, à étendre)
public class EventBus
{
    private static EventBus _instance;
    public static EventBus Instance => _instance ??= new EventBus();

    private Dictionary<Type, List<Delegate>> _subscribers = new();

    // `virtual` sur les trois membres : le bus est passe en dependance a tout
    // le moteur et DummyEventBus doit pouvoir le neutraliser dans les tests.
    // C'est la base qui s'ouvre, et elle s'ouvre parce qu'il y a de vrais
    // appelants — Publish neuf fois, Subscribe trois — donc la substitution
    // veut dire quelque chose. Profiler et ResourceManager, mesures a zero
    // appelant d'instance, ne recoivent rien.
    public virtual void Subscribe<T>(Action<T> handler) where T : IMessage
    {
        var msgType = typeof(T);
        if (!_subscribers.ContainsKey(msgType))
            _subscribers[msgType] = new List<Delegate>();

        _subscribers[msgType].Add(handler);
    }

    public virtual void Unsubscribe<T>(Action<T> handler) where T : IMessage
    {
        var msgType = typeof(T);
        if (_subscribers.ContainsKey(msgType))
            _subscribers[msgType].Remove(handler);
    }

    /// <summary>Alias de Publish : le bridge ecrit `_eventBus?.Raise(...)`.</summary>
    public void Raise<T>(T message) where T : IMessage => Publish(message);

    public virtual void Publish<T>(T message) where T : IMessage
    {
        var msgType = typeof(T);
        if (_subscribers.TryGetValue(msgType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                ((Action<T>)handler).Invoke(message);
            }
        }
    }
}

// Service Locator (Déjà présent)
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> Services = new();

    public static void RegisterService<T>(T service) where T : class
    {
        var type = typeof(T);
        if (Services.ContainsKey(type))
        {
            Console.WriteLine($"Service {type.Name} already registered.");
            return;
        }
        Services[type] = service;
    }

    public static T GetService<T>() where T : class
    {
        var type = typeof(T);
        if (Services.TryGetValue(type, out var service))
            return (T)service;

        Console.WriteLine($"Service {type.Name} not found.");
        return null;
    }
}

// Manager de base (Déjà présent)
public interface IManager
{
    void Initialize();
    void Update(float deltaTime);
    void Shutdown();
}

// SceneManager (Déjà présent, à étendre)
public class SceneManager : IManager
{
    private Stack<string> _sceneStack = new();
    private Dictionary<string, Func<Task>> _sceneLoaders = new();
    private string _nextSceneToLoad = null; // Pour transitions asynchrones
    private bool _isLoading = false;

    public void Initialize() { }

    public void Update(float deltaTime)
    {
        if (_nextSceneToLoad != null && !_isLoading)
        {
            _ = LoadSceneInternalAsync(_nextSceneToLoad);
            _nextSceneToLoad = null;
        }
    }

    public void Shutdown() { }

    public void RegisterScene(string name, Func<Task> loader)
    {
        _sceneLoaders[name] = loader;
    }

    public void LoadScene(string sceneName) // API publique non-bloquante
    {
        _nextSceneToLoad = sceneName;
    }

    private async Task LoadSceneInternalAsync(string sceneName)
    {
        _isLoading = true;
        if (_sceneLoaders.TryGetValue(sceneName, out var loader))
        {
            await loader();
            _sceneStack.Push(sceneName);
            EventBus.Instance.Publish(new GameStartedMessage());
        }
        _isLoading = false;
    }

    public async Task UnloadSceneAsync()
    {
        if (_sceneStack.Count > 0)
        {
            _sceneStack.Pop();
            if (_sceneStack.Count > 0)
            {
                await LoadSceneInternalAsync(_sceneStack.Peek()); // Reload previous scene
            }
        }
    }
}

// ResourceManager (Déjà présent, à étendre)
public class ResourceManager : IManager
{
    private Dictionary<string, object> _resources = new();
    private Dictionary<string, Func<string, Task<object>>> _loaders = new();
    private Dictionary<string, object> _cache = new(); // Pour pré-chargement

    public void Initialize() { }

    public void Update(float deltaTime) { }

    public void Shutdown()
    {
        _resources.Clear();
        _cache.Clear();
    }

    public void RegisterLoader<T>(Func<string, Task<object>> loader)
    {
        _loaders[typeof(T).Name] = loader;
    }

    public async Task<T> LoadResourceAsync<T>(string path)
    {
        if (_cache.ContainsKey(path))
        {
            _resources[path] = _cache[path];
            _cache.Remove(path);
            return (T)_resources[path];
        }

        if (_resources.ContainsKey(path))
            return (T)_resources[path];

        var typeName = typeof(T).Name;
        if (_loaders.TryGetValue(typeName, out var loader))
        {
            var resource = await loader(path);
            _resources[path] = resource;
            return (T)resource;
        }
        return default(T);
    }

    public void CacheResource(string path, object resource)
    {
        _cache[path] = resource;
    }

    public void UnloadResource(string path)
    {
        _resources.Remove(path);
    }
}

// --- 138. RENDERING ENGINE (Déjà présent, à étendre) ---
public struct Vector2
{
    public float X, Y;
    public Vector2(float x, float y) { X = x; Y = y; }

    // Les huit membres ci-dessous ne sont pas des ajouts d'agrement : chacun est
    // reclame par un site d'appel reel du depot, que le compilateur a nomme une
    // fois l'analyse des corps de methode enfin atteinte.
    public static Vector2 Zero => new Vector2(0f, 0f);
    public static Vector2 Up => new Vector2(0f, -1f);   // Y croit vers le BAS a l'ecran
    public static Vector2 UnitY => new Vector2(0f, 1f);

    public float LengthSquared => X * X + Y * Y;
    public float Length => (float)Math.Sqrt(LengthSquared);

    /// <summary>Rend le vecteur unitaire. Un vecteur nul reste nul plutot que de produire NaN.</summary>
    public Vector2 Normalized
    {
        get
        {
            var l = Length;
            return l > 1e-6f ? new Vector2(X / l, Y / l) : Zero;
        }
    }

    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    public static float Distance(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y).Length;

    public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 v, float k) => new Vector2(v.X * k, v.Y * k);
    public static Vector2 operator *(float k, Vector2 v) => v * k;
}

public struct RectangleF
{
    public float X, Y, Width, Height;
    public float Left => X;     // reclame par un appelant reel
    public float Top => Y;      // idem
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public RectangleF(float x, float y, float w, float h) { X = x; Y = y; Width = w; Height = h; }
}

public interface IRenderer
{
    void BeginFrame();
    void DrawSprite(string texturePath, Vector2 position);
    void DrawText(string text, Vector2 position);
    void EndFrame();
}

public class RenderingEngine : IManager
{
    private IRenderer _renderer;
    private List<IRenderable> _renderables = new(); // Ajout d'une liste d'objets rendus

    public RenderingEngine(IRenderer renderer) => _renderer = renderer;

    public void Initialize() { }

    public void Update(float deltaTime) { }

    public void Shutdown() { }

    public void Render()
    {
        _renderer.BeginFrame();
        foreach (var renderable in _renderables.OrderBy(r => r.Layer)) // Tri par couche
        {
            renderable.Draw(_renderer);
        }
        _renderer.DrawText("Game Frame", new Vector2(10, 10));
        _renderer.EndFrame();
    }

    public void AddRenderable(IRenderable renderable) => _renderables.Add(renderable);
    public void RemoveRenderable(IRenderable renderable) => _renderables.Remove(renderable);
}

public interface IRenderable
{
    int Layer { get; } // Ordre de rendu
    void Draw(IRenderer renderer);
}

// --- 139. PHYSICS ENGINE (Déjà présent, à étendre) ---
public interface IPhysicsBody
{
    Vector2 Position { get; set; }
    Vector2 Velocity { get; set; }
    float Mass { get; }
    RectangleF Bounds { get; }
    void ApplyForce(Vector2 force);
}

public class PhysicsEngine : IManager
{
    private List<IPhysicsBody> _bodies = new();
    private List<Func<IPhysicsBody, IPhysicsBody, bool>> _collisionHandlers = new();
    private float _gravity = 9.81f; // Gravité modifiable

    public void Initialize() { }

    public void Update(float deltaTime)
    {
        foreach (var body in _bodies)
        {
            // Intégration simple avec gravité
            body.ApplyForce(new Vector2(0, body.Mass * _gravity));
            body.Velocity = new Vector2(
                body.Velocity.X,
                body.Velocity.Y
            );
            body.Position = new Vector2(
                body.Position.X + body.Velocity.X * deltaTime,
                body.Position.Y + body.Velocity.Y * deltaTime
            );
        }

        // Détection de collision
        for (int i = 0; i < _bodies.Count; i++)
        {
            for (int j = i + 1; j < _bodies.Count; j++)
            {
                if (Intersects(_bodies[i].Bounds, _bodies[j].Bounds))
                {
                    foreach (var handler in _collisionHandlers)
                    {
                        handler(_bodies[i], _bodies[j]);
                    }
                }
            }
        }
    }

    public void Shutdown() { }

    public void AddBody(IPhysicsBody body) => _bodies.Add(body);
    public void RemoveBody(IPhysicsBody body) => _bodies.Remove(body);

    public void SetGravity(float gravity) => _gravity = gravity;

    private bool Intersects(RectangleF a, RectangleF b)
    {
        return !(a.Right < b.Left || a.Left > b.Right || a.Bottom < b.Top || a.Top > b.Bottom);
    }
}

// --- 140. INPUT ENGINE (Déjà présent, à étendre) ---
public class InputManager : IManager
{
    private Dictionary<string, Func<bool>> _bindings = new();
    private Dictionary<Keys, string> _keyMap = new(); // Map clavier vers actions
    private Dictionary<Buttons, string> _buttonMap = new(); // Map manette (hypothétique)

    public void Initialize() { }

    public void Update(float deltaTime)
    {
        // Vérifier les états d'entrée basés sur les liaisons
        // Cette méthode devrait être appelée avec les événements réels du système d'exploitation
    }

    public void Shutdown() { }

    public void BindAction(string actionName, Func<bool> inputChecker)
    {
        _bindings[actionName] = inputChecker;
    }

    public void MapKey(Keys key, string actionName)
    {
        _keyMap[key] = actionName;
    }

    public bool IsActionPressed(string actionName)
    {
        return _bindings.TryGetValue(actionName, out var checker) && checker();
    }

    // Exemple de gestion d'événement clavier (à appeler depuis le système d'interface)
    public void OnKeyDown(Keys key)
    {
        if (_keyMap.TryGetValue(key, out var actionName))
        {
            // Gérer l'état de l'action (pressed, held, released)
            // Cela nécessite un système d'état plus complexe
        }
    }
}

// Enums pour les entrées (hypothétiques)
public enum Keys { Up, Down, Left, Right, Space, Enter } // Simplifié
public enum Buttons { A, B, X, Y, LB, RB, Start, Back } // Pour manette

// --- 141. AUDIO ENGINE (Déjà présent, à étendre) ---
public interface IAudioSource
{
    void Play();
    void Stop();
    void SetVolume(float volume);
}

public class AudioEngine : IManager
{
    private Dictionary<string, Func<IAudioSource>> _sourceFactories = new();
    private List<IAudioSource> _activeSources = new(); // Sources en cours de lecture

    public void Initialize() { }

    public void Update(float deltaTime) { }

    public void Shutdown()
    {
        foreach (var source in _activeSources)
        {
            source.Stop();
        }
        _activeSources.Clear();
    }

    public void RegisterSourceFactory(string type, Func<IAudioSource> factory)
    {
        _sourceFactories[type] = factory;
    }

    public IAudioSource CreateSource(string type)
    {
        if (_sourceFactories.TryGetValue(type, out var factory))
        {
            var source = factory();
            _activeSources.Add(source);
            return source;
        }
        return null;
    }

    public void RemoveSource(IAudioSource source)
    {
        source.Stop();
        _activeSources.Remove(source);
    }
}

// --- 142. ANIMATION ENGINE (Déjà présent, à étendre) ---
public class AnimationClip
{
    public string Name { get; set; }
    public float Duration { get; set; }
    // ... données des keyframes
}

public class AnimationController : IComponent // IComponent pour ECS
{
    private Dictionary<string, AnimationClip> _clips = new();
    private AnimationClip _currentClip;
    private float _time;
    private bool _isPlaying = false;

    public void AddClip(AnimationClip clip) => _clips[clip.Name] = clip;

    public void Play(string clipName)
    {
        if (_clips.TryGetValue(clipName, out var clip))
        {
            _currentClip = clip;
            _time = 0f;
            _isPlaying = true;
        }
    }

    public void Stop()
    {
        _isPlaying = false;
    }

    public void Update(float deltaTime)
    {
        if (_isPlaying && _currentClip != null)
        {
            _time += deltaTime;
            if (_time >= _currentClip.Duration)
            {
                _time = 0f; // Ou boucler, ou s'arrêter
                _isPlaying = false; // Arrêt automatique si non-bouclé
            }
            // Ici, on interpole les propriétes basées sur _time et _currentClip
        }
    }
}

// --- 143. ECS (Ajout) ---
public struct Entity
{
    public int Id { get; set; }
    public Entity(int id) => Id = id;
}

public interface IComponent { }

public interface ISystem
{
    void Update(float deltaTime);

    // Initialize et Shutdown sont reclames par le bridge sur IAnimationSystem,
    // IAudioVisualSystem et IDebugSystem, qui heritent tous trois d'ISystem.
    //
    // Corps par DEFAUT et non membres abstraits : huit classes implementent deja
    // ISystem (AIThreatSystem, AIMemorySystem, AISystemManager…) et des membres
    // abstraits les casseraient toutes. C'est le mecanisme prevu par C# 8 pour
    // etendre une interface sans rompre ses implementeurs — a ne pas confondre
    // avec un corps pose sur une interface neuve, qui lui ne fait que dispenser
    // d'ecrire le membre.
    void Initialize() { }
    void Shutdown() { }
    // Méthode pour filtrer les entités pertinentes
    // Ex: IEnumerable<Entity> GetEntitiesWith(params Type[] componentTypes);
}

public class EntityManager
{
    private Dictionary<int, List<IComponent>> _entities = new();
    private int _nextId = 0;

    public Entity CreateEntity()
    {
        var id = _nextId++;
        _entities[id] = new List<IComponent>();
        return new Entity(id);
    }

    // Les trois membres suivants sont reclames par MovementAnimationBridgeSystem.
    // ForEach recoit un delegue a parametres `ref` — d'ou le type RefAction, un
    // Action<> ordinaire ne pouvant pas porter de `ref`.
    public delegate void RefAction<T1, T2>(Entity entity, ref T1 a, ref T2 b);

    public IEnumerable<Entity> GetAllEntitiesWith<T1, T2>() where T1 : IComponent where T2 : IComponent
        => Array.Empty<Entity>();

    public IEnumerable<Entity> GetAllEntitiesWith<T1, T2, T3>() where T1 : IComponent where T2 : IComponent where T3 : IComponent
        => Array.Empty<Entity>();

    public void SetComponent<T>(Entity entity, T component) where T : IComponent { }

    public void ForEach<T1, T2>(RefAction<T1, T2> action) where T1 : struct, IComponent where T2 : struct, IComponent { }

    public void AddComponent<T>(Entity entity, T component) where T : IComponent
    {
        if (_entities.TryGetValue(entity.Id, out var components))
        {
            components.Add(component);
        }
    }

    // La contrainte etait `where T : class, IComponent` — fausse par construction :
    // dans cet ECS les composants sont des STRUCTS (MovementComponent,
    // RigidBodyComponent, AnimationStateComponent…). Tout appel avec un composant
    // reel donnait un CS0452. `return default` remplace `return null`, qui n'a plus
    // de sens sans la contrainte.
    public T GetComponent<T>(Entity entity) where T : IComponent
    {
        if (_entities.TryGetValue(entity.Id, out var components))
        {
            return components.OfType<T>().FirstOrDefault();
        }
        return default;
    }

    public void DestroyEntity(Entity entity)
    {
        _entities.Remove(entity.Id);
    }
}

public class MovementSystem : ISystem
{
    private EntityManager _entityManager;
    private PhysicsEngine _physicsEngine;

    public MovementSystem(EntityManager entityManager, PhysicsEngine physicsEngine)
    {
        _entityManager = entityManager;
        _physicsEngine = physicsEngine;
    }

    public void Update(float deltaTime)
    {
        // Itérer sur les entités ayant à la fois un TransformComponent ET un VelocityComponent
        // Exemple simplifié sans requêtes complexes
        // foreach (var entity in GetEntitiesWith(typeof(TransformComponent), typeof(VelocityComponent)))
        // {
        //     var transform = _entityManager.GetComponent<TransformComponent>(entity);
        //     var velocity = _entityManager.GetComponent<VelocityComponent>(entity);
        //     transform.Position += velocity.Value * deltaTime;
        // }
    }
}

// --- 144. NETWORKING ENGINE (Ajout conceptuel) ---
public interface INetworkManager
{
    void Connect(string address, int port);
    void Disconnect();
    void SendData(byte[] data);
    byte[] ReceiveData();
}

public class DummyNetworkManager : INetworkManager // Exemple factice
{
    public void Connect(string address, int port) => Console.WriteLine($"Connecting to {address}:{port}");
    public void Disconnect() => Console.WriteLine("Disconnected");
    public void SendData(byte[] data) => Console.WriteLine($"Sending {data.Length} bytes");
    public byte[] ReceiveData() => new byte[0]; // Placeholder
}

// --- 145. SAVE SYSTEM (Déjà présent, à étendre) ---
public interface ISaveable
{
    object SaveState();
    void LoadState(object state);
}

public class SaveSystem : IManager
{
    private Dictionary<Type, Func<string, object>> _deserializers = new();
    private Dictionary<Type, Func<object, string>> _serializers = new();

    public void Initialize()
    {
        // Enregistrer des sérialiseurs/désérialiseurs (ex: JSON, binaire)
        // _serializers[typeof(object)] = obj => JsonConvert.SerializeObject(obj);
        // _deserializers[typeof(object)] = str => JsonConvert.DeserializeObject(str);
    }

    public void Update(float deltaTime) { }

    public void Shutdown() { }

    public void Save(ISaveable saveable, string path)
    {
        var state = saveable.SaveState();
        if (_serializers.TryGetValue(state.GetType(), out var serializer))
        {
            var serializedState = serializer(state);
            // Écrire serializedState dans le fichier à 'path'
            System.IO.File.WriteAllText(path, serializedState);
        }
    }

    public void Load(ISaveable saveable, string path)
    {
        if (System.IO.File.Exists(path))
        {
            var serializedState = System.IO.File.ReadAllText(path);
            // Désérialiser serializedState en object
            // var state = ... (utiliser _deserializers)
            // saveable.LoadState(state);
        }
    }
}

// --- 146. PROFILER (Ajout conceptuel) ---
public class Profiler
{
    private static readonly Dictionary<string, Stopwatch> _timers = new();

    public static void BeginSample(string name)
    {
        if (!_timers.ContainsKey(name))
        {
            _timers[name] = new Stopwatch();
        }
        _timers[name].Start();
    }

    public static void EndSample(string name)
    {
        if (_timers.TryGetValue(name, out var timer))
        {
            timer.Stop();
            Console.WriteLine($"Sample '{name}' took {timer.ElapsedMilliseconds} ms");
            timer.Reset();
        }
    }
}

// --- 147. JOB SYSTEM (Ajout conceptuel) ---
public interface IJob
{
    void Execute();
}

public class JobSystem
{
    public void Schedule(IJob job)
    {
        Task.Run(() => job.Execute()); // Exemple simple avec Task
        // Un vrai système utiliserait un pool de threads et une file d'attente
    }
}

// --- 148. MODDING SYSTEM (Ajout conceptuel) ---
// Voir commentaire dans Engine.cs original
// Interface IMod, ModManager, chargement dynamique d'assemblages (.dll)

// --- 149. EDITOR (Ajout conceptuel) ---
// Voir commentaire dans Engine.cs original
// Interface graphique (WinForms/WPF), outils d'édition

// --- 150. SYSTEMES DE JEU (Ajout conceptuel) ---

// Stats
public class StatsComponent : IComponent
{
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public float Speed { get; set; } = 1.0f;
    // ... autres stats
}

// Buffs/Debuffs
public abstract class Buff
{
    public string Name { get; set; }
    public float Duration { get; set; }
    public abstract void Apply(StatsComponent stats);
    public abstract void Remove(StatsComponent stats);
}

public class SpeedBuff : Buff
{
    private float _speedModifier;
    public SpeedBuff(float modifier, float duration) : base() { _speedModifier = modifier; Duration = duration; }
    public override void Apply(StatsComponent stats) => stats.Speed += _speedModifier;
    public override void Remove(StatsComponent stats) => stats.Speed -= _speedModifier;
}

// Inventory
public class InventoryComponent : IComponent
{
    public List<object> Items { get; set; } = new(); // Type générique pour simplifier
    public int MaxSlots { get; set; } = 10;

    public bool AddItem(object item)
    {
        if (Items.Count < MaxSlots)
        {
            Items.Add(item);
            return true;
        }
        return false;
    }

    public bool RemoveItem(object item) => Items.Remove(item);
}

// Talents (Skills/Traits)
public class Talent
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int Level { get; set; } = 0;
    public int MaxLevel { get; set; } = 5;
    public float EffectValue { get; set; } // Valeur de l'effet du talent
}

public class TalentComponent : IComponent
{
    public List<Talent> KnownTalents { get; set; } = new();
    public int SkillPoints { get; set; } = 0;

    public bool UnlockTalent(Talent talent)
    {
        if (SkillPoints > 0 && !KnownTalents.Contains(talent) && talent.Level == 0)
        {
            KnownTalents.Add(talent);
            SkillPoints--;
            talent.Level = 1;
            ApplyTalentEffect(talent);
            return true;
        }
        return false;
    }

    public bool UpgradeTalent(Talent talent)
    {
        if (talent.Level < talent.MaxLevel && KnownTalents.Contains(talent))
        {
            talent.Level++;
            ApplyTalentEffect(talent);
            return true;
        }
        return false;
    }

    private void ApplyTalentEffect(Talent talent)
    {
        // Appliquer l'effet du talent (modifie Stats, comporments, etc.)
    }
}

// Quêtes
public class Quest
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public List<QuestObjective> Objectives { get; set; } = new();
    public bool IsActive { get; set; } = false;
    public bool IsCompleted { get; set; } = false;
}

public class QuestObjective
{
    public string Description { get; set; }
    public int CurrentCount { get; set; } = 0;
    public int RequiredCount { get; set; } = 1;
    public bool IsCompleted => CurrentCount >= RequiredCount;
}

public class QuestSystem
{
    private List<Quest> _activeQuests = new();
    private List<Quest> _completedQuests = new();

    public void StartQuest(Quest quest)
    {
        quest.IsActive = true;
        _activeQuests.Add(quest);
    }

    public void UpdateObjective(string questId, string objectiveDesc, int amount = 1)
    {
        var quest = _activeQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            var objective = quest.Objectives.FirstOrDefault(o => o.Description == objectiveDesc);
            if (objective != null)
            {
                objective.CurrentCount += amount;
                if (objective.IsCompleted)
                {
                    CheckQuestCompletion(quest);
                }
            }
        }
    }

    private void CheckQuestCompletion(Quest quest)
    {
        if (quest.Objectives.All(o => o.IsCompleted))
        {
            quest.IsCompleted = true;
            _completedQuests.Add(quest);
            _activeQuests.Remove(quest);
            // Récompenser le joueur
        }
    }
}

// Météo
public enum WeatherType { Sunny, Rainy, Stormy, Snowy }
public class WeatherSystem
{
    public WeatherType CurrentWeather { get; private set; } = WeatherType.Sunny;
    public float Intensity { get; private set; } = 0.0f; // Pour pluie/neige

    public void ChangeWeather(WeatherType newWeather, float intensity = 1.0f)
    {
        CurrentWeather = newWeather;
        Intensity = intensity;
        // Notifier les systèmes affectés (rendu, physique, IA, etc.)
        EventBus.Instance.Publish(new WeatherChangedMessage(newWeather, intensity));
    }
}

public class WeatherChangedMessage : IMessage
{
    public WeatherType NewWeather { get; }
    public float Intensity { get; }
    public WeatherChangedMessage(WeatherType weather, float intensity) { NewWeather = weather; Intensity = intensity; }
}

// Biomes
public enum BiomeType { Forest, Desert, Tundra, Ocean, Mountain }
public class BiomeSystem
{
    public BiomeType GetCurrentBiomeAt(Vector2 position) // Logique basée sur la carte
    {
        // Exemple simplifié
        if (position.Y > 100) return BiomeType.Forest;
        if (position.Y < 50) return BiomeType.Desert;
        return BiomeType.Tundra;
    }
}

// Saisons
public enum Season { Spring, Summer, Autumn, Winter }
public class SeasonSystem
{
    public Season CurrentSeason { get; private set; } = Season.Spring;
    private float _seasonProgress = 0f; // 0.0 à 1.0
    private float _seasonDuration = 100f; // Durée arbitraire

    public void Update(float deltaTime)
    {
        _seasonProgress += deltaTime;
        if (_seasonProgress >= _seasonDuration)
        {
            _seasonProgress = 0f;
            CurrentSeason = (Season)(((int)CurrentSeason + 1) % Enum.GetValues(typeof(Season)).Length);
            // Notifier les systèmes affectés
            EventBus.Instance.Publish(new SeasonChangedMessage(CurrentSeason));
        }
    }
}

public class SeasonChangedMessage : IMessage
{
    public Season NewSeason { get; }
    public SeasonChangedMessage(Season season) { NewSeason = season; }
}

// --- Système de base du jeu utilisant les composants ci-dessus ---
class SnakeGameEngine
{
    private readonly SceneManager _sceneManager;
    private readonly ResourceManager _resourceManager;
    private readonly InputManager _inputManager;
    private readonly PhysicsEngine _physicsEngine;
    private readonly RenderingEngine _renderingEngine;
    private readonly AudioEngine _audioEngine;
    private readonly SaveSystem _saveSystem;
    private readonly EntityManager _entityManager;
    private readonly List<ISystem> _systems = new();
    private readonly WeatherSystem _weatherSystem = new();
    private readonly SeasonSystem _seasonSystem = new();
    private readonly QuestSystem _questSystem = new();

    public SnakeGameEngine()
    {
        _sceneManager = new SceneManager();
        _resourceManager = new ResourceManager();
        _inputManager = new InputManager();
        _physicsEngine = new PhysicsEngine();
        _renderingEngine = new RenderingEngine(null); // Besoin d'un IRenderer concret
        _audioEngine = new AudioEngine();
        _saveSystem = new SaveSystem();
        _entityManager = new EntityManager();

        ServiceLocator.RegisterService(_sceneManager);
        ServiceLocator.RegisterService(_resourceManager);
        ServiceLocator.RegisterService(_inputManager);
        ServiceLocator.RegisterService(_physicsEngine);
        ServiceLocator.RegisterService(_renderingEngine);
        ServiceLocator.RegisterService(_audioEngine);
        ServiceLocator.RegisterService(_saveSystem);
        ServiceLocator.RegisterService(_entityManager);

        // Enregistrer les systèmes ECS
        _systems.Add(new MovementSystem(_entityManager, _physicsEngine));
        // Ajouter d'autres systèmes ECS ici
    }

    public void Run()
    {
        _sceneManager.Initialize();
        _resourceManager.Initialize();
        _inputManager.Initialize();
        _physicsEngine.Initialize();
        _renderingEngine.Initialize();
        _audioEngine.Initialize();
        _saveSystem.Initialize();

        EventBus.Instance.Subscribe<GameStartedMessage>(msg => Console.WriteLine("Game Started!"));
        EventBus.Instance.Subscribe<WeatherChangedMessage>(msg => Console.WriteLine($"Weather changed to {msg.NewWeather} (Intensity: {msg.Intensity})"));
        EventBus.Instance.Subscribe<SeasonChangedMessage>(msg => Console.WriteLine($"Season changed to {msg.NewSeason}"));

        float deltaTime = 1f / 60f; // Supposons 60 FPS
        bool running = true;
        while (running)
        {
            Profiler.BeginSample("Frame");
            _inputManager.Update(deltaTime);
            _seasonSystem.Update(deltaTime); // Mise à jour de la saison
            _physicsEngine.Update(deltaTime);

            foreach (var system in _systems)
            {
                system.Update(deltaTime);
            }

            _renderingEngine.Update(deltaTime);
            _renderingEngine.Render();

            // Gestion de la boucle
            if (_inputManager.IsActionPressed("Quit")) running = false;

            Profiler.EndSample("Frame");
        }

        _saveSystem.Shutdown();
        _audioEngine.Shutdown();
        _renderingEngine.Shutdown();
        _physicsEngine.Shutdown();
        _inputManager.Shutdown();
        _resourceManager.Shutdown();
        _sceneManager.Shutdown();
    }
}

// Entry point
class Program
{
    static void Main(string[] args)
    {
        var game = new SnakeGameEngine();
        game.Run();
    }
}