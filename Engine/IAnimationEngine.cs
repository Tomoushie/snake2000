// Contrat du moteur d'animation, taille sur ce que le jeu appelle reellement.
//
// La declaration d'origine — 3 268 lignes, 448 methodes et 207 proprietes —
// est conservee dans Docs/Intention/IAnimationEngine.cs.txt. Elle reste un
// repere de ce que le moteur vise a terme ; elle n'etait implementee par
// personne et referencait 200 types jamais ecrits.
//
// Les trois membres ci-dessous sont ceux qu'appelle
// Game/Gameplay/Movement/System/MovementAnimationBridgeSystem.cs. Tout ajout
// ici doit venir d'un appelant reel, pas d'une intention.

// Entity est qualifie plutot qu'importe : Snake2000.Engine.Core declare son
// propre Vector2, qui entrerait en conflit avec celui de System.Numerics.
using System.Numerics;

namespace Engine.Animation
{
    /// <summary>
    /// Marqueur d'un systeme de jeu qui se lie au moteur d'animation.
    /// Permet au moteur de recevoir le pont sans dependre de Game/.
    /// </summary>
    public interface IAnimationBridge
    {
    }

    /// <summary>
    /// Abstraction du moteur d'animation vue depuis le jeu.
    /// </summary>
    public interface IAnimationEngine
    {
        /// <summary>
        /// Lie un pont d'animation au moteur. Appele a l'initialisation du pont.
        /// </summary>
        void InitializeBridge(IAnimationBridge bridge);

        /// <summary>
        /// Delie un pont d'animation. Appele a l'extinction du pont.
        /// </summary>
        void ShutdownBridge(IAnimationBridge bridge);

        /// <summary>
        /// Deplacement racine produit par l'animation depuis la derniere frame,
        /// a appliquer au corps physique de l'entite.
        /// </summary>
        Vector2 ExtractRootMotionDelta(Snake2000.Engine.Core.Entity entity);
    }
}
