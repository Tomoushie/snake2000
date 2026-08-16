using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Snake2000.Engine.AI;
using Snake2000.Engine.Animation;
using Snake2000.Engine.Core;
using Snake2000.Engine.Physics;
namespace Snake2000.Engine.Gameplay;


// --- 137. CORE ENGINE (Concepts repris) ---
// IManager, IMessage, EventBus, IComponent, ISystem, Entity, EntityManager sont définis dans Engine.cs
// IPhysicsBody, ColliderComponent, RigidBodyComponent, PhysicsSystem sont définis dans PhysicsEngine.cs

// --- 147. GAMEPLAY SYSTEMS (Systèmes de jeu) ---

// --- 147.1. Système de Progression ---
public class ExperienceComponent : IComponent
{
    public int CurrentXP { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int XPToNextLevel => Level * 100; // Exemple simple

    public void AddXP(int xp)
    {
        CurrentXP += xp;
        while (CurrentXP >= XPToNextLevel)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        CurrentXP -= XPToNextLevel;
        Level++;
        EventBus.Instance.Publish(new LevelUpMessage(this));
    }
}

public class LevelUpMessage : IMessage
{
    public ExperienceComponent ExperienceComponent { get; }
    public LevelUpMessage(ExperienceComponent expComp) { ExperienceComponent = expComp; }
}

public class ProgressionSystem : ISystem
{
    private EntityManager _entityManager;

    public ProgressionSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Update(float deltaTime)
    {
        // Le système de progression réagit aux messages LevelUpMessage
        // ou aux changements de composant ExperienceComponent via ECS
        // Pour simplifier, on suppose que LevelUpMessage est publié dans ExperienceComponent
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 147.2. Système d'Inventaire ---
public class Item
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    // ... autres propriétés spécifiques à l'item
}

public class InventoryComponent : IComponent
{
    public List<Item> Items { get; set; } = new();
    public int MaxSlots { get; set; } = 10;

    public bool AddItem(Item item)
    {
        if (Items.Count < MaxSlots)
        {
            Items.Add(item);
            EventBus.Instance.Publish(new InventoryChangedMessage(this));
            return true;
        }
        return false;
    }

    public bool RemoveItem(Item item) => Items.Remove(item);
}

public class InventoryChangedMessage : IMessage
{
    public InventoryComponent InventoryComponent { get; }
    public InventoryChangedMessage(InventoryComponent inventory) { InventoryComponent = inventory; }
}

public class InventorySystem : ISystem
{
    private EntityManager _entityManager;

    public InventorySystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Update(float deltaTime)
    {
        // Réagit aux messages InventoryChangedMessage
        // ou gère les interactions avec l'interface utilisateur
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 147.3. Système de Talents ---
public class Talent
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Level { get; set; } = 0;
    public int MaxLevel { get; set; } = 5;
    public int CostToUpgrade => Level + 1; // Exemple de coût progressif
    public Action<Entity> OnApply { get; set; } // Effet du talent
    public Action<Entity> OnRemove { get; set; } // Effet inverse
}

public class TalentComponent : IComponent
{
    public List<Talent> KnownTalents { get; set; } = new();
    public List<Talent> ActiveTalents { get; set; } = new();
    public int TalentPoints { get; set; } = 0;

    public bool UnlockTalent(Talent talent)
    {
        if (TalentPoints >= talent.CostToUpgrade && !KnownTalents.Contains(talent))
        {
            TalentPoints -= talent.CostToUpgrade;
            KnownTalents.Add(talent);
            EventBus.Instance.Publish(new TalentChangedMessage(this, talent, TalentChangedMessage.ChangeType.Unlocked));
            return true;
        }
        return false;
    }

    public bool UpgradeTalent(Talent talent)
    {
        if (KnownTalents.Contains(talent) && talent.Level < talent.MaxLevel && TalentPoints >= talent.CostToUpgrade)
        {
            TalentPoints -= talent.CostToUpgrade;
            talent.Level++;
            EventBus.Instance.Publish(new TalentChangedMessage(this, talent, TalentChangedMessage.ChangeType.Updated));
            return true;
        }
        return false;
    }

    public bool ActivateTalent(Talent talent, Entity owner)
    {
        if (KnownTalents.Contains(talent) && talent.Level > 0 && !ActiveTalents.Contains(talent))
        {
            ActiveTalents.Add(talent);
            talent.OnApply?.Invoke(owner);
            EventBus.Instance.Publish(new TalentChangedMessage(this, talent, TalentChangedMessage.ChangeType.Activated));
            return true;
        }
        return false;
    }
}

public class TalentChangedMessage : IMessage
{
    public enum ChangeType { Unlocked, Updated, Activated, Deactivated }
    public TalentComponent TalentComponent { get; }
    public Talent Talent { get; }
    public ChangeType Type { get; }

    public TalentChangedMessage(TalentComponent talentComp, Talent talent, ChangeType changeType)
    {
        TalentComponent = talentComp;
        Talent = talent;
        Type = changeType;
    }
}

public class TalentSystem : ISystem
{
    private EntityManager _entityManager;

    public TalentSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Update(float deltaTime)
    {
        // Réagit aux messages TalentChangedMessage
        // ou applique les effets des talents actifs
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 147.4. Système de Stats ---
public class StatsComponent : IComponent
{
    public int Health { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public float Speed { get; set; } = 1.0f;
    public float DamageMultiplier { get; set; } = 1.0f;
    // ... autres stats
}

public class StatsSystem : ISystem
{
    private EntityManager _entityManager;

    public StatsSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Update(float deltaTime)
    {
        // Met à jour les stats en fonction des buffs/debuffs, talents, etc.
        // Appliquer les effets cumulatifs
        // Vérifier les conditions de mort (Health <= 0)
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 147.5. Système de Buffs/Debuffs ---
public abstract class Buff
{
    public string Name { get; set; }
    public float Duration { get; set; }
    public int Stacks { get; set; } = 1; // Pour les buffs qui s'accumulent
    public abstract void Apply(StatsComponent stats);
    public abstract void Remove(StatsComponent stats);
    public virtual void Update(StatsComponent stats, float deltaTime) {} // Pour les effets over-time
}

public class SpeedBuff : Buff
{
    private float _speedModifier;
    public SpeedBuff(float modifier, float duration) { _speedModifier = modifier; Duration = duration; }
    public override void Apply(StatsComponent stats) => stats.Speed += _speedModifier * Stacks;
    public override void Remove(StatsComponent stats) => stats.Speed -= _speedModifier * Stacks;
}

public class BuffComponent : IComponent
{
    public List<Buff> ActiveBuffs { get; set; } = new();
}

public class BuffSystem : ISystem
{
    private EntityManager _entityManager;

    public BuffSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Update(float deltaTime)
    {
        // Parcourir les entités avec BuffComponent
        // Mettre à jour la durée des buffs
        // Appeler Update() sur les buffs over-time
        // Supprimer les buffs expirés et appeler Remove()
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 147.6. Système de Quêtes Avancées ---
public class Quest
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public List<QuestObjective> Objectives { get; set; } = new();
    public List<Reward> Rewards { get; set; } = new(); // Récompenses potentielles
    public bool IsActive { get; set; } = false;
    public bool IsCompleted { get; set; } = false;
    public bool IsFailed { get; set; } = false; // Condition d'échec
    public DateTime? StartTime { get; set; } = null; // Pour les quêtes chronométrées
    public TimeSpan? TimeLimit { get; set; } = null; // Limite de temps
}

public class QuestObjective
{
    public string Description { get; set; }
    public int CurrentCount { get; set; } = 0;
    public int RequiredCount { get; set; } = 1;
    public string TargetEntityId { get; set; } = null; // Pour les quêtes spécifiques à un type d'entité
    public bool IsCompleted => CurrentCount >= RequiredCount;
}

public class Reward
{
    public string Type { get; set; } // "XP", "Item", "TalentPoint", etc.
    public object Value { get; set; } // La quantité ou l'identifiant de la récompense
}

public class QuestComponent : IComponent
{
    public List<Quest> ActiveQuests { get; set; } = new();
    public List<Quest> CompletedQuests { get; set; } = new();
    public List<Quest> FailedQuests { get; set; } = new();
}

public class QuestSystem
{
    private List<Quest> _allQuests; // Bibliothèque de toutes les quêtes possibles
    private EntityManager _entityManager;

    public QuestSystem(EntityManager entityManager, List<Quest> questLibrary)
    {
        _entityManager = entityManager;
        _allQuests = questLibrary;
    }

    public void StartQuest(Entity playerEntity, string questId)
    {
        var questTemplate = _allQuests.FirstOrDefault(q => q.Id == questId);
        if (questTemplate != null)
        {
            var questInstance = new Quest
            {
                Id = questTemplate.Id,
                Title = questTemplate.Title,
                Description = questTemplate.Description,
                Objectives = new List<QuestObjective>(questTemplate.Objectives),
                Rewards = new List<Reward>(questTemplate.Rewards),
                TimeLimit = questTemplate.TimeLimit
            };

            questInstance.StartTime = DateTime.Now;
            questInstance.IsActive = true;

            var playerQuestComp = _entityManager.GetComponent<QuestComponent>(playerEntity);
            playerQuestComp.ActiveQuests.Add(questInstance);
            EventBus.Instance.Publish(new QuestStartedMessage(questInstance));
        }
    }

    public void UpdateObjective(Entity playerEntity, string questId, string objectiveDesc, int amount = 1)
    {
        var playerQuestComp = _entityManager.GetComponent<QuestComponent>(playerEntity);
        var quest = playerQuestComp.ActiveQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            var objective = quest.Objectives.FirstOrDefault(o => o.Description == objectiveDesc);
            if (objective != null)
            {
                objective.CurrentCount += amount;
                EventBus.Instance.Publish(new QuestObjectiveUpdatedMessage(quest, objective));

                if (objective.IsCompleted)
                {
                    CheckQuestCompletion(playerEntity, quest);
                }
            }
        }
    }

    private void CheckQuestCompletion(Entity playerEntity, Quest quest)
    {
        if (quest.Objectives.All(o => o.IsCompleted))
        {
            quest.IsCompleted = true;
            var playerQuestComp = _entityManager.GetComponent<QuestComponent>(playerEntity);
            playerQuestComp.ActiveQuests.Remove(quest);
            playerQuestComp.CompletedQuests.Add(quest);
            EventBus.Instance.Publish(new QuestCompletedMessage(quest));
            GrantRewards(playerEntity, quest.Rewards);
        }
    }

    private void CheckQuestFailure(Entity playerEntity, Quest quest)
    {
        if (quest.TimeLimit.HasValue && quest.StartTime.HasValue)
        {
            if (DateTime.Now - quest.StartTime.Value > quest.TimeLimit.Value)
            {
                quest.IsFailed = true;
                var playerQuestComp = _entityManager.GetComponent<QuestComponent>(playerEntity);
                playerQuestComp.ActiveQuests.Remove(quest);
                playerQuestComp.FailedQuests.Add(quest);
                EventBus.Instance.Publish(new QuestFailedMessage(quest));
            }
        }
    }

    private void GrantRewards(Entity playerEntity, List<Reward> rewards)
    {
        foreach (var reward in rewards)
        {
            switch (reward.Type)
            {
                case "XP":
                    var expComp = _entityManager.GetComponent<ExperienceComponent>(playerEntity);
                    expComp?.AddXP((int)reward.Value);
                    break;
                case "Item":
                    var invComp = _entityManager.GetComponent<InventoryComponent>(playerEntity);
                    // Trouver l'item dans une bibliothèque par son ID
                    // Item item = FindItemById((string)reward.Value);
                    // invComp?.AddItem(item);
                    break;
                case "TalentPoint":
                    var talentComp = _entityManager.GetComponent<TalentComponent>(playerEntity);
                    talentComp.TalentPoints += (int)reward.Value;
                    break;
                // ... autres types de récompenses
            }
        }
    }

    public void Update(float deltaTime)
    {
        // Vérifier les conditions d'échec (temps, mort du joueur, etc.)
        // foreach (var player in GetAllPlayerEntities())
        // {
        //     var questComp = _entityManager.GetComponent<QuestComponent>(player);
        //     foreach (var quest in questComp.ActiveQuests.ToList()) // ToList pour éviter les problèmes de modification pendant l'itération
        //     {
        //         CheckQuestFailure(player, quest);
        //     }
        // }
    }
}

public class QuestStartedMessage : IMessage { public Quest Quest { get; } public QuestStartedMessage(Quest q) { Quest = q; } }
public class QuestObjectiveUpdatedMessage : IMessage { public Quest Quest { get; } public QuestObjective Objective { get; } public QuestObjectiveUpdatedMessage(Quest q, QuestObjective o) { Quest = q; Objective = o; } }
public class QuestCompletedMessage : IMessage { public Quest Quest { get; } public QuestCompletedMessage(Quest q) { Quest = q; } }
public class QuestFailedMessage : IMessage { public Quest Quest { get; } public QuestFailedMessage(Quest q) { Quest = q; } }

// --- 147.7. Système de Dialogues ---
public class DialogueNode
{
    public string Id { get; set; }
    public string Text { get; set; }
    public List<DialogueOption> Options { get; set; } = new();
    public string OnEnterEvent { get; set; } // ID d'un événement à déclencher
}

public class DialogueOption
{
    public string Text { get; set; }
    public string NextNodeId { get; set; }
    public string Requirement { get; set; } // Condition pour que l'option soit disponible
}

public class DialogueTree
{
    public string Id { get; set; }
    public Dictionary<string, DialogueNode> Nodes { get; set; } = new();
    public string StartNodeId { get; set; }
}

public class DialogueSystem
{
    private Dictionary<string, DialogueTree> _dialogueTrees = new();
    private DialogueTree _currentDialogue = null;
    private string _currentNodeId = null;
    private Entity _currentSpeaker = Entity.Null; // Entité NPC parlant

    public void LoadDialogue(DialogueTree tree)
    {
        _dialogueTrees[tree.Id] = tree;
    }

    public void StartDialogue(string treeId, Entity speaker, Entity player)
    {
        if (_dialogueTrees.TryGetValue(treeId, out var tree))
        {
            _currentDialogue = tree;
            _currentNodeId = tree.StartNodeId;
            _currentSpeaker = speaker;
            EventBus.Instance.Publish(new DialogueStartedMessage(treeId, speaker, player));
            DisplayCurrentNode();
        }
    }

    public void SelectOption(int optionIndex)
    {
        if (_currentDialogue == null || _currentNodeId == null) return;

        var currentNode = _currentDialogue.Nodes[_currentNodeId];
        if (optionIndex >= 0 && optionIndex < currentNode.Options.Count)
        {
            var selectedOption = currentNode.Options[optionIndex];
            // Vérifier la condition (Requirement) ici si implémentée
            _currentNodeId = selectedOption.NextNodeId;
            DisplayCurrentNode();
        }
    }

    private void DisplayCurrentNode()
    {
        if (_currentDialogue == null || _currentNodeId == null) return;

        var node = _currentDialogue.Nodes[_currentNodeId];
        EventBus.Instance.Publish(new DialogueDisplayMessage(node.Text, node.Options));
        if (!string.IsNullOrEmpty(node.OnEnterEvent))
        {
            // Déclencher l'événement lié au noeud
            EventBus.Instance.Publish(new DialogueEventMessage(node.OnEnterEvent));
        }
    }

    public void EndDialogue()
    {
        _currentDialogue = null;
        _currentNodeId = null;
        _currentSpeaker = Entity.Null;
        EventBus.Instance.Publish(new DialogueEndedMessage());
    }

    public void Update(float deltaTime)
    {
        // Gérer les entrées du joueur pour sélectionner les options
        // Gérer la fermeture du dialogue
    }
}

public class DialogueStartedMessage : IMessage { public string TreeId { get; } public Entity Speaker { get; } public Entity Player { get; } public DialogueStartedMessage(string id, Entity s, Entity p) { TreeId = id; Speaker = s; Player = p; } }
public class DialogueDisplayMessage : IMessage { public string Text { get; } public List<DialogueOption> Options { get; } public DialogueDisplayMessage(string t, List<DialogueOption> o) { Text = t; Options = o; } }
public class DialogueEventMessage : IMessage { public string EventId { get; } public DialogueEventMessage(string id) { EventId = id; } }
public class DialogueEndedMessage : IMessage { }

// --- 147.8. Système de Cutscenes ---
public class CutsceneAction
{
    public string Type { get; set; } // "Wait", "Move", "Speak", "PlayAnimation", "FadeOut", etc.
    public Dictionary<string, object> Parameters { get; set; } = new(); // Paramètres spécifiques à l'action
}

public class Cutscene
{
    public string Id { get; set; }
    public List<CutsceneAction> Actions { get; set; } = new();
    public float Duration => Actions.Sum(a => (float)a.Parameters.GetValueOrDefault("Duration", 0f)); // Exemple simplifié
}

public class CutsceneSystem
{
    private Dictionary<string, Cutscene> _cutscenes = new();
    private Cutscene _currentCutscene = null;
    private int _currentActionIndex = 0;
    private float _timeInCurrentAction = 0f;
    private bool _isPlaying = false;

    public void LoadCutscene(Cutscene cutscene)
    {
        _cutscenes[cutscene.Id] = cutscene;
    }

    public void PlayCutscene(string cutsceneId)
    {
        if (_cutscenes.TryGetValue(cutsceneId, out var cutscene))
        {
            _currentCutscene = cutscene;
            _currentActionIndex = 0;
            _timeInCurrentAction = 0f;
            _isPlaying = true;
            EventBus.Instance.Publish(new CutsceneStartedMessage(cutsceneId));
            ExecuteCurrentAction();
        }
    }

    public void Update(float deltaTime)
    {
        if (!_isPlaying || _currentCutscene == null) return;

        _timeInCurrentAction += deltaTime;

        var currentAction = _currentCutscene.Actions[_currentActionIndex];
        float actionDuration = (float)currentAction.Parameters.GetValueOrDefault("Duration", 0f);

        if (_timeInCurrentAction >= actionDuration)
        {
            _currentActionIndex++;
            _timeInCurrentAction = 0f;

            if (_currentActionIndex >= _currentCutscene.Actions.Count)
            {
                EndCutscene();
            }
            else
            {
                ExecuteCurrentAction();
            }
        }
    }

    private void ExecuteCurrentAction()
    {
        if (_currentActionIndex >= _currentCutscene.Actions.Count) return;

        var action = _currentCutscene.Actions[_currentActionIndex];
        // Exécuter l'action en fonction de son type
        // Ex: if (action.Type == "Speak") { ... }
        // Ex: if (action.Type == "FadeOut") { ... }
        EventBus.Instance.Publish(new CutsceneActionExecutedMessage(action));
    }

    private void EndCutscene()
    {
        _isPlaying = false;
        _currentCutscene = null;
        EventBus.Instance.Publish(new CutsceneEndedMessage());
    }
}

public class CutsceneStartedMessage : IMessage { public string CutsceneId { get; } public CutsceneStartedMessage(string id) { CutsceneId = id; } }
public class CutsceneActionExecutedMessage : IMessage { public CutsceneAction Action { get; } public CutsceneActionExecutedMessage(CutsceneAction a) { Action = a; } }
public class CutsceneEndedMessage : IMessage { }

// --- 147.9. Système de Météo (étendu) ---
// Déjà partiellement implémenté dans Engine.cs via WeatherSystem
// On peut l'étendre ici avec des composants ou des effets plus spécifiques
public class WeatherEffectComponent : IComponent
{
    public WeatherType Type { get; set; }
    public float Intensity { get; set; } = 1.0f;
    public float Duration { get; set; } = -1f; // -1 pour infini
    public Action OnApply { get; set; } // Effet visuel/sonore
    public Action OnRemove { get; set; } // Effet inverse
}

public class AdvancedWeatherSystem : ISystem
{
    private EntityManager _entityManager;
    private List<WeatherEffectComponent> _activeEffects = new();

    public AdvancedWeatherSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void AddWeatherEffect(WeatherEffectComponent effect)
    {
        _activeEffects.Add(effect);
        effect.OnApply?.Invoke();
    }

    public void RemoveWeatherEffect(WeatherEffectComponent effect)
    {
        _activeEffects.Remove(effect);
        effect.OnRemove?.Invoke();
    }

    public void Update(float deltaTime)
    {
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = _activeEffects[i];
            if (effect.Duration > 0)
            {
                effect.Duration -= deltaTime;
                if (effect.Duration <= 0)
                {
                    RemoveWeatherEffect(effect);
                }
            }
        }
        // Mettre à jour les effets en cours (animations, sons, etc.)
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 147.10. Système de Biomes (étendu) ---
// Déjà partiellement implémenté dans Engine.cs via BiomeSystem
// On peut l'étendre ici avec des composants ou des effets plus spécifiques
public class BiomeComponent : IComponent
{
    public BiomeType Type { get; set; }
    public List<Entity> Entities { get; set; } = new(); // Entités natives du biome
    public List<Item> Resources { get; set; } = new(); // Ressources du biome
}

public class AdvancedBiomeSystem : ISystem
{
    private EntityManager _entityManager;

    public AdvancedBiomeSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Update(float deltaTime)
    {
        // Vérifier la position des entités et appliquer les effets du biome
        // Générer des entités spécifiques au biome
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 147.11. Système de Saisons (étendu) ---
// Déjà partiellement implémenté dans Engine.cs via SeasonSystem
// On peut l'étendre ici avec des composants ou des effets plus spécifiques
public class SeasonalEffectComponent : IComponent
{
    public Season Season { get; set; }
    public Action OnApply { get; set; }
    public Action OnRemove { get; set; }
}

public class AdvancedSeasonSystem : ISystem
{
    private EntityManager _entityManager;
    private Season _currentSeason;

    public AdvancedSeasonSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void SetSeason(Season newSeason)
    {
        // Retirer les effets de la saison précédente
        // Appliquer les effets de la nouvelle saison
        _currentSeason = newSeason;
    }

    public void Update(float deltaTime)
    {
        // Mettre à jour les effets en cours liés à la saison
    }

    public void Initialize() { }
    public void Shutdown() { }
}

// --- 148. SAVE SYSTEM (étendu) ---
// Déjà partiellement implémenté dans Engine.cs via SaveSystem
// On peut l'étendre ici pour sauvegarder l'état spécifique des systèmes de gameplay
public class MetaProgressionData
{
    public int TotalPlayTimeHours { get; set; } = 0;
    public Dictionary<string, int> UnlockedSkins { get; set; } = new();
    public List<string> CompletedAchievements { get; set; } = new();
    // ... autres données de progression globale
}

public class SaveSystemExtensions
{
    public static void SaveMetaProgression(MetaProgressionData data, string path)
    {
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(fs, data);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur de sauvegarde de la méta-progression: {ex.Message}");
        }
    }

    public static MetaProgressionData LoadMetaProgression(string path)
    {
        if (!File.Exists(path)) return new MetaProgressionData();

        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return (MetaProgressionData)formatter.Deserialize(fs);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur de chargement de la méta-progression: {ex.Message}");
            return new MetaProgressionData(); // Retourner un état vide en cas d'erreur
        }
    }
}