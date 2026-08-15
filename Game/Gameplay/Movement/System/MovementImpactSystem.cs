// Game/Gameplay/Movement/System/MovementImpactSystem.cs

public class MovementImpactSystem : ISystem
{
    private EntityManager _entityManager;
    private PhysicsSystem _physicsSystem;

    public MovementImpactSystem(EntityManager entityManager, PhysicsSystem physicsSystem)
    {
        _entityManager = entityManager;
        _physicsSystem = physicsSystem;
    }

    public void Initialize() { }
    public void Shutdown() { }

    public void Update(float deltaTime)
    {
        // Ce système est typiquement appelé par le PhysicsSystem après une collision
        // On suppose qu'une méthode HandleCollision est appelée avec les détails de la collision
        // Ex: HandleCollision(Entity entityA, Entity entityB, CollisionDetails details);
    }

    public void HandleCollision(Entity entityA, Entity entityB, Vector2 impulse, float relativeSpeed)
    {
        ProcessImpact(entityA, impulse, relativeSpeed);
        ProcessImpact(entityB, -impulse, relativeSpeed); // Impulsion opposée pour l'autre entité
    }

    private void ProcessImpact(Entity entity, Vector2 impulse, float relativeSpeed)
    {
        if (!_entityManager.HasComponent<MovementImpactComponent>(entity) ||
            !_entityManager.HasComponent<MovementComponent>(entity) ||
            !_entityManager.HasComponent<RigidBodyComponent>(entity)) return;

        var impactComp = _entityManager.GetComponent<MovementImpactComponent>(entity);
        var moveComp = _entityManager.GetComponent<MovementComponent>(entity);
        var rb = _entityManager.GetComponent<RigidBodyComponent>(entity);

        if (relativeSpeed < impactComp.ImpactThreshold) return; // Pas d'impact notable

        // Calcul de la réaction physique
        Vector2 reaction = CalculateReaction(impulse, rb.Mass, impactComp.Bounciness);
        _physicsSystem.ApplyImpulse(rb, reaction);

        // Mise à jour de l'état de mouvement (ex: transition vers Slip ou Injured)
        UpdateMovementStateAfterImpact(ref moveComp, relativeSpeed, impulse);

        // Gestion de la santé et des blessures
        ApplyDamageFromImpact(ref moveComp, relativeSpeed, impulse);

        // Activation du ragdoll si impact violent
        if (relativeSpeed > impactComp.ImpactThreshold * 2.0f) // Seuil plus élevé pour ragdoll
        {
            ActivateRagdoll(entity, ref impactComp);
        }

        // Mise à jour des composants modifiés
        _entityManager.SetComponent(entity, moveComp);
        _entityManager.SetComponent(entity, impactComp);
    }

    private Vector2 CalculateReaction(Vector2 impulse, float mass, float bounciness)
    {
        Vector2 normalizedImpulse = impulse.Normalized();
        float magnitude = impulse.Length();
        float reactionMagnitude = magnitude * bounciness;
        return normalizedImpulse * reactionMagnitude / mass;
    }

    private void UpdateMovementStateAfterImpact(ref MovementComponent moveComp, float speed, Vector2 impulse)
    {
        // Exemple : Si impact de côté, potentiellement glisser ou perdre l'équilibre
        float lateralForce = Math.Abs(impulse.X); // Exemple simple
        if (lateralForce > 10.0f && moveComp.Balance < 0.8f)
        {
            moveComp.Balance -= 0.2f; // Perte d'équilibre
            // Trigger Slip ou Fall state
        }
    }

    private void ApplyDamageFromImpact(ref MovementComponent moveComp, float speed, Vector2 impulse)
    {
        float damage = Math.Max(0.0f, (speed - 2.0f) * 2.0f); // Exemple simple
        moveComp.Health -= damage;
        moveComp.Health = Math.Max(0.0f, moveComp.Health);

        // Exemple : Impact violent aux jambes -> blessure
        if (Math.Abs(impulse.Y) > Math.Abs(impulse.X) && speed > 8.0f)
        {
            moveComp.LegInjurySeverity = Math.Min(1.0f, moveComp.LegInjurySeverity + 0.1f);
        }
    }

    private void ActivateRagdoll(Entity entity, ref MovementImpactComponent impactComp)
    {
        impactComp.IsRagdollActive = true;
        impactComp.RagdollBlendWeight = 1.0f;
        // Peut-être désactiver le contrôle de mouvement normal ici
    }
}