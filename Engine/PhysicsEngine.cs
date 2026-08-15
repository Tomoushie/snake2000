using System;
using System.Collections.Generic;
using System.Drawing; // Pour RectangleF, Point, etc. (ou utiliser une structure personnalisée)
using System.Numerics; // Pour Vector2, Vector3 si disponible, sinon on utilise notre Vector2 de Engine.cs

// --- 137. CORE ENGINE (Concepts repris) ---
// IManager est défini dans Engine.cs
// IMessage et EventBus sont définis dans Engine.cs

// --- 139. PHYSICS ENGINE (Physique): Physique 2D/3D complète, collisions avancées, triggers, raycasts, overlap checks, rigidbodies, forces, constraints

// Structures de base (Vector2 est déjà défini dans Engine.cs)
// Ajoutons RectangleF et d'autres primitives si nécessaire
public struct RectangleF
{
    public float X, Y, Width, Height;
    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;

    public RectangleF(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    public static RectangleF Empty => new RectangleF(0, 0, 0, 0);
}

// --- NOUVEAU FICHIER : PhysicsEngine.cs ---

// Types de formes physiques
public enum ShapeType
{
    Circle,
    Rectangle,
    Polygon // Pourrait nécessiter une structure supplémentaire
}

// Composant de forme physique (collider)
public struct ColliderComponent : IComponent
{
    public ShapeType Type { get; set; }
    public RectangleF Bounds { get; set; } // Pour simplifier, on utilise un rectangle englobant
    public bool IsTrigger { get; set; } // Si vrai, génère des événements mais n'applique pas de réponse physique

    public ColliderComponent(ShapeType type, RectangleF bounds, bool isTrigger = false)
    {
        Type = type;
        Bounds = bounds;
        IsTrigger = isTrigger;
    }
}

// Composant de corps rigide
public struct RigidBodyComponent : IComponent
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Acceleration { get; set; }
    public float Mass { get; set; }
    public float InverseMass => Mass > 0 ? 1.0f / Mass : 0.0f; // Utile pour les calculs
    public float Restitution { get; set; } // Rebond
    public float Friction { get; set; } // Frottement
    public bool IsStatic { get; set; } // Si vrai, ne subit pas les forces
    public bool IsKinematic { get; set; } // Si vrai, contrôlé par l'utilisateur, pas par la physique

    public RigidBodyComponent(Vector2 position, float mass = 1.0f, bool isStatic = false, bool isKinematic = false)
    {
        Position = position;
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
        Mass = mass;
        InverseMass = Mass > 0 ? 1.0f / Mass : 0.0f;
        Restitution = 0.2f; // Valeur par défaut
        Friction = 0.1f; // Valeur par défaut
        IsStatic = isStatic;
        IsKinematic = isKinematic;
    }
}

// Composant de contrainte (liaison entre deux corps)
public struct ConstraintComponent : IComponent
{
    public Entity EntityA { get; set; }
    public Entity EntityB { get; set; }
    public Vector2 AnchorA { get; set; } // Point d'ancrage sur A
    public Vector2 AnchorB { get; set; } // Point d'ancrage sur B
    public float MaxDistance { get; set; } // Pour une liaison de distance
    // Ajouter d'autres propriétés selon le type de contrainte (pivot, charnière, etc.)

    public ConstraintComponent(Entity entityA, Entity entityB, Vector2 anchorA, Vector2 anchorB, float maxDist)
    {
        EntityA = entityA;
        EntityB = entityB;
        AnchorA = anchorA;
        AnchorB = anchorB;
        MaxDistance = maxDist;
    }
}

// Messages de collision
public class CollisionEnterMessage : IMessage
{
    public Entity EntityA { get; }
    public Entity EntityB { get; }
    public CollisionEnterMessage(Entity a, Entity b) { EntityA = a; EntityB = b; }
}

public class CollisionExitMessage : IMessage
{
    public Entity EntityA { get; }
    public Entity EntityB { get; }
    public CollisionExitMessage(Entity a, Entity b) { EntityA = a; EntityB = b; }
}

public class TriggerEnterMessage : IMessage
{
    public Entity EntityA { get; }
    public Entity EntityB { get; }
    public TriggerEnterMessage(Entity a, Entity b) { EntityA = a; EntityB = b; }
}

public class TriggerExitMessage : IMessage
{
    public Entity EntityA { get; }
    public Entity EntityB { get; }
    public TriggerExitMessage(Entity a, Entity b) { EntityA = a; EntityB = b; }
}

// Système de physique
public class PhysicsSystem : ISystem
{
    private EntityManager _entityManager;
    private List<Entity> _colliders = new(); // Pour accès rapide aux entités avec Collider
    private List<Entity> _rigidBodies = new(); // Pour accès rapide aux entités avec RigidBody
    private List<Entity> _constraints = new(); // Pour accès rapide aux entités avec Constraint

    // Paramètres physiques globaux
    public Vector2 Gravity { get; set; } = new Vector2(0, 9.81f); // Gravité par défaut (vers le bas)
    private float _timeStep = 1.0f / 60.0f; // Pas de temps pour l'intégration (60 FPS)

    public PhysicsSystem(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public void Initialize()
    {
        // Récupérer les entités pertinentes au démarrage
        RefreshEntityLists();
    }

    public void Update(float deltaTime)
    {
        // Intégration physique (mise à jour des positions/vitesses)
        IntegrateForces(deltaTime);
        IntegrateVelocity(deltaTime);

        // Détection de collision
        DetectCollisions();

        // Résolution de collision (impulsions, contraintes)
        ResolveCollisions();

        // Résolution des contraintes
        ResolveConstraints();
    }

    public void Shutdown()
    {
        // Nettoyage si nécessaire
    }

    private void RefreshEntityLists()
    {
        // Cette méthode devrait être appelée si des entités sont ajoutées/supprimées dynamiquement
        // ou si des composants sont ajoutés/supprimés pendant l'exécution.
        // Pour simplifier, on suppose que la liste est mise à jour à chaque frame ou via un événement ECS.
        _colliders.Clear();
        _rigidBodies.Clear();
        _constraints.Clear();

        // Exemple de récupération (nécessite une méthode dans EntityManager pour filtrer efficacement)
        // Pour l'instant, on suppose une méthode qui retourne toutes les entités avec un type de composant spécifique
        // _colliders = _entityManager.GetEntitiesWith<ColliderComponent>();
        // _rigidBodies = _entityManager.GetEntitiesWith<RigidBodyComponent>();
        // _constraints = _entityManager.GetEntitiesWith<ConstraintComponent>();
    }

    private void IntegrateForces(float deltaTime)
    {
        foreach (var entity in _rigidBodies)
        {
            var rb = _entityManager.GetComponent<RigidBodyComponent>(entity);
            if (rb.IsStatic || rb.IsKinematic) continue; // Les objets statiques/kine ne sont pas affectés par les forces

            // Appliquer la gravité
            rb.Acceleration = Gravity;

            // Appliquer d'autres forces ici (vent, magnétisme, etc.)

            // Intégration de la vitesse (Euler simple)
            rb.Velocity += rb.Acceleration * deltaTime;

            _entityManager.AddComponent(entity, rb); // Mettre à jour le composant
        }
    }

    private void IntegrateVelocity(float deltaTime)
    {
        foreach (var entity in _rigidBodies)
        {
            var rb = _entityManager.GetComponent<RigidBodyComponent>(entity);
            if (rb.IsStatic || rb.IsKinematic) continue; // Les objets statiques/kine ne sont pas affectés par la vitesse

            // Intégration de la position (Euler simple)
            rb.Position += rb.Velocity * deltaTime;

            // Mettre à jour le collider en conséquence (si le collider est attaché au RigidBody)
            var collider = _entityManager.GetComponent<ColliderComponent>(entity);
            if (collider != null)
            {
                // Ajuster les bounds du collider en fonction de la nouvelle position du RigidBody
                // Cela dépend de la structure exacte du collider
                // Exemple pour un rectangle centré sur la position du RigidBody :
                collider.Bounds = new RectangleF(
                    rb.Position.X - collider.Bounds.Width / 2,
                    rb.Position.Y - collider.Bounds.Height / 2,
                    collider.Bounds.Width,
                    collider.Bounds.Height
                );
                _entityManager.AddComponent(entity, collider);
            }

            _entityManager.AddComponent(entity, rb); // Mettre à jour le composant
        }
    }

    private void DetectCollisions()
    {
        // Algorithme de détection de collision simplifié (Broadphase + Narrowphase)
        // Broadphase : Check bounding boxes (RectangleF) pour réduire le nombre de paires à tester
        for (int i = 0; i < _colliders.Count; i++)
        {
            for (int j = i + 1; j < _colliders.Count; j++)
            {
                Entity entityA = _colliders[i];
                Entity entityB = _colliders[j];

                var colliderA = _entityManager.GetComponent<ColliderComponent>(entityA);
                var colliderB = _entityManager.GetComponent<ColliderComponent>(entityB);

                // Vérifier l'intersection des rectangles englobants (Broadphase)
                if (Intersect(colliderA.Bounds, colliderB.Bounds))
                {
                    // Narrowphase : Vérifier la collision précise selon les types de shapes
                    // Pour l'instant, on suppose que l'intersection de RectangleF signifie une collision
                    // et on publie un message.
                    if (colliderA.IsTrigger || colliderB.IsTrigger)
                    {
                        EventBus.Instance.Publish(new TriggerEnterMessage(entityA, entityB));
                    }
                    else
                    {
                        EventBus.Instance.Publish(new CollisionEnterMessage(entityA, entityB));
                    }
                }
            }
        }
    }

    private void ResolveCollisions()
    {
        // Résoudre les collisions détectées (ex: séparer les objets, appliquer des impulsions)
        // Cela nécessite des algorithmes de résolution d'impulsion ou de position.
        // Pour simplifier, on se contente de publier les messages de collision dans DetectCollisions.
        // Une implémentation réelle gérerait les contacts, les normales, les profondeurs de pénétration, etc.
    }

    private void ResolveConstraints()
    {
        // Résoudre les contraintes (ex: maintenir la distance entre deux objets)
        // Algorithme de relaxation ou de projection.
        foreach (var entity in _constraints)
        {
            var constraint = _entityManager.GetComponent<ConstraintComponent>(entity);
            var rbA = _entityManager.GetComponent<RigidBodyComponent>(constraint.EntityA);
            var rbB = _entityManager.GetComponent<RigidBodyComponent>(constraint.EntityB);

            if (rbA == null || rbB == null) continue; // L'un des deux corps n'existe plus

            // Calculer la distance actuelle
            Vector2 worldAnchorA = rbA.Position + constraint.AnchorA;
            Vector2 worldAnchorB = rbB.Position + constraint.AnchorB;
            Vector2 delta = worldAnchorB - worldAnchorA;
            float distance = delta.Length();

            // Vérifier si la contrainte est violée
            if (distance > constraint.MaxDistance)
            {
                // Appliquer une correction (simplifiée ici)
                // Cela impliquerait de calculer une impulsion et de la distribuer entre A et B
                // en fonction de leur masse inverse.
                float correctionMagnitude = (distance - constraint.MaxDistance) / (rbA.InverseMass + rbB.InverseMass);
                Vector2 correction = delta.Normalized() * correctionMagnitude;

                if (!rbA.IsStatic)
                {
                    rbA.Position += correction * rbA.InverseMass;
                }
                if (!rbB.IsStatic)
                {
                    rbB.Position -= correction * rbB.InverseMass;
                }

                // Mettre à jour les composants
                _entityManager.AddComponent(constraint.EntityA, rbA);
                _entityManager.AddComponent(constraint.EntityB, rbB);

                // Mettre à jour les colliders si nécessaire
                UpdateColliderFromRigidBody(constraint.EntityA);
                UpdateColliderFromRigidBody(constraint.EntityB);
            }
        }
    }

    // Helper pour mettre à jour le collider en fonction du RigidBody
    private void UpdateColliderFromRigidBody(Entity entity)
    {
        var rb = _entityManager.GetComponent<RigidBodyComponent>(entity);
        var collider = _entityManager.GetComponent<ColliderComponent>(entity);
        if (rb != null && collider != null)
        {
             collider.Bounds = new RectangleF(
                rb.Position.X - collider.Bounds.Width / 2,
                rb.Position.Y - collider.Bounds.Height / 2,
                collider.Bounds.Width,
                collider.Bounds.Height
            );
            _entityManager.AddComponent(entity, collider);
        }
    }

    // Helper pour intersection de rectangles
    private bool Intersect(RectangleF a, RectangleF b)
    {
        return !(a.Right < b.Left || a.Left > b.Right || a.Bottom < b.Top || a.Top > b.Bottom);
    }

    // --- 145. RAYCASTING & OVERLAP CHECKS (Ajoutées ici) ---
    public bool Raycast(Vector2 origin, Vector2 direction, float maxDistance, out RaycastHit hit)
    {
        hit = new RaycastHit(); // Valeur par défaut
        Vector2 endpoint = origin + direction * maxDistance;

        // Algorithme de raycasting (ex: Ray-AABB)
        // Parcourir les colliders et tester l'intersection
        foreach (var entity in _colliders)
        {
            var collider = _entityManager.GetComponent<ColliderComponent>(entity);
            // Tester l'intersection du rayon avec le Bounds du collider
            // Si intersection, vérifier si c'est le plus proche
            // Exemple simplifié pour AABB
            if (RayIntersectsAABB(origin, direction, collider.Bounds, out float t))
            {
                if (t > 0 && t < hit.Distance) // Trouvé une intersection plus proche
                {
                    hit.Entity = entity;
                    hit.Point = origin + direction * t;
                    hit.Distance = t;
                    // Calculer la normale (simplifié)
                    // Ex: si le rayon entre par la gauche/droite -> normale X, etc.
                    // On suppose une normale arbitraire pour l'exemple
                    hit.Normal = new Vector2(0, -1); // Normale vers le haut pour simplifier
                    return true;
                }
            }
        }
        return false; // Aucune intersection trouvée
    }

    // Helper pour l'intersection Ray-AABB
    private bool RayIntersectsAABB(Vector2 rayOrigin, Vector2 rayDir, RectangleF aabb, out float t)
    {
        t = 0;
        float tMin = 0.0f;
        float tMax = float.MaxValue;

        Vector2 invDir = new Vector2(1.0f / rayDir.X, 1.0f / rayDir.Y);

        Vector2 near = (new Vector2(aabb.Left, aabb.Top) - rayOrigin) * invDir;
        Vector2 far = (new Vector2(aabb.Right, aabb.Bottom) - rayOrigin) * invDir;

        Vector2 min = new Vector2(Math.Min(near.X, far.X), Math.Min(near.Y, far.Y));
        Vector2 max = new Vector2(Math.Max(near.X, far.X), Math.Max(near.Y, far.Y));

        tMin = Math.Max(tMin, min.X);
        tMax = Math.Min(tMax, max.X);
        tMin = Math.Max(tMin, min.Y);
        tMax = Math.Min(tMax, max.Y);

        if (tMax >= tMin && tMax >= 0.0f)
        {
            t = tMin;
            return true;
        }
        return false;
    }

    public List<Entity> OverlapCheck(RectangleF area)
    {
        List<Entity> overlappingEntities = new List<Entity>();
        // Parcourir les colliders et tester l'overlap avec la zone
        foreach (var entity in _colliders)
        {
            var collider = _entityManager.GetComponent<ColliderComponent>(entity);
            if (Intersect(collider.Bounds, area))
            {
                overlappingEntities.Add(entity);
            }
        }
        return overlappingEntities;
    }
}

// Structure pour les résultats d'un raycast
public struct RaycastHit
{
    public Entity Entity { get; set; }
    public Vector2 Point { get; set; }
    public Vector2 Normal { get; set; }
    public float Distance { get; set; }

    public RaycastHit(Entity entity, Vector2 point, Vector2 normal, float distance)
    {
        Entity = entity;
        Point = point;
        Normal = normal;
        Distance = distance;
    }
}