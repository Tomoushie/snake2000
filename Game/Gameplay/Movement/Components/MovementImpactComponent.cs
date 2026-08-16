// Game/Gameplay/Movement/Components/MovementImpactComponent.cs
using System;
using Snake2000.Engine.AI;
using Snake2000.Engine.Core;

/// <summary>
/// Etat d'impact d'une entite mobile : seuil de declenchement, rebond, et
/// bascule en ragdoll. Consomme par MovementImpactSystem.
/// </summary>
public struct MovementImpactComponent : IComponent, IReusableComponent
{
    private const float SeuilParDefaut = 5.0f;
    private const float RebondParDefaut = 0.3f;

    // Champs prives pour les deux valeurs bornees : une propriete automatique
    // laisserait un setter public libre, et `Bounciness = 12f` annulerait la
    // contrainte. Le serrage doit vivre dans le setter, pas dans le seul
    // constructeur.
    private float _bounciness;
    private float _ragdollBlendWeight;

    /// <summary>
    /// Vitesse relative en dessous de laquelle l'impact est ignore.
    /// </summary>
    public float ImpactThreshold { get; set; }

    /// <summary>
    /// Coefficient de restitution, serre entre 0 et 1.
    /// </summary>
    public float Bounciness
    {
        get => _bounciness;
        set => _bounciness = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Vrai quand la vitesse relative depasse deux fois <see cref="ImpactThreshold"/>.
    /// </summary>
    public bool IsRagdollActive { get; set; }

    /// <summary>
    /// Poids de fondu du ragdoll, serre entre 0 et 1.
    /// </summary>
    public float RagdollBlendWeight
    {
        get => _ragdollBlendWeight;
        set => _ragdollBlendWeight = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Un struct portant des valeurs par defaut doit declarer ce constructeur
    /// explicitement, sinon le compilateur emet CS8983.
    /// </summary>
    public MovementImpactComponent()
    {
        _bounciness = RebondParDefaut;
        _ragdollBlendWeight = 0f;
        ImpactThreshold = SeuilParDefaut;
        IsRagdollActive = false;
    }

    public MovementImpactComponent(float impactThreshold, float bounciness,
                                   bool isRagdollActive, float ragdollBlendWeight)
    {
        _bounciness = 0f;
        _ragdollBlendWeight = 0f;
        ImpactThreshold = impactThreshold;
        Bounciness = bounciness;
        IsRagdollActive = isRagdollActive;
        RagdollBlendWeight = ragdollBlendWeight;
    }

    /// <summary>
    /// Remet le composant a son etat neuf avant reutilisation par le pool.
    /// </summary>
    public void Reset()
    {
        ImpactThreshold = SeuilParDefaut;
        Bounciness = RebondParDefaut;
        IsRagdollActive = false;
        RagdollBlendWeight = 0f;
    }
}
