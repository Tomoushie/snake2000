// Game/Gameplay/Movement/Components/MovementComponent.cs
using Snake2000.Engine.AI;
using Snake2000.Engine.Core;

public enum MovementType
{
    PhysicsBased,
    Steering,
    RootMotion
}

public struct MovementComponent : IComponent, IReusableComponent
{
    public float BaseSpeed { get; set; } = 1.0f;
    public float CurrentSpeed { get; set; } = 1.0f;
    public float Acceleration { get; set; } = 10.0f;
    public float BaseDeceleration { get; set; } = 10.0f;
    public float Deceleration { get; set; } = 10.0f;
    public float MaxSlopeAngle { get; set; } = 45.0f;
    public bool IsGrounded { get; set; } = false;
    public Vector2 GroundNormal { get; set; } = Vector2.Up;
    public float Stamina { get; set; } = 100.0f;
    public float MaxStamina { get; set; } = 100.0f;
    public float StaminaRegenRate { get; set; } = 5.0f;
    public float StaminaDrainRate { get; set; } = 10.0f;
    public MovementType Type { get; set; } = MovementType.PhysicsBased;
    public float LODUpdateFrequency { get; set; } = 1.0f;
    public float EffectiveFrictionFactor { get; set; } = 1.0f;
    public float BodyTemperature { get; set; } = 37.0f;
    public float StressLevel { get; set; } = 0.0f;
    public float BreathLevel { get; set; } = 100.0f;
    public float BreathDrainRate { get; set; } = 1.0f;
    public float BreathRecoveryRate { get; set; } = 2.0f;
    public float Health { get; set; } = 100.0f;
    public float LegInjurySeverity { get; set; } = 0.0f;
    public float Balance { get; set; } = 1.0f;
    public float Traction { get; set; } = 1.0f;
    public float TractionLoss { get; set; } = 0.0f;
}

public struct MovementStateComponent : IComponent, IReusableComponent
{
    public MovementState State { get; set; } = MovementState.Idle;
    public Vector2 DesiredVelocity { get; set; } = Vector2.Zero;
    public bool IsSlipping { get; set; } = false;
    public float SlipRecoveryTimer { get; set; } = 0.0f;
}

public enum MovementState
{
    Idle, Walking, Running, Sprinting, Jumping, Falling, Slipping, Recovering, Crouching, Injured
}

// Autres composants dans leurs propres fichiers...