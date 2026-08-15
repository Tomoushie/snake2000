// /Engine/Animation/Test/CommonTypes.cs
//
// Types partagés utilisés par l'AnimationEngineStub, l'Orchestrator et le Dashboard.
// Contient les enums, structs et types de base nécessaires pour éviter les dépendances circulaires.

using System.Drawing; // Pour System.Drawing.Point

namespace Engine.Animation.Test
{
    #region Enums
    /// <summary>
    /// Niveau de gravité pour les logs et les événements.
    /// </summary>
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// Niveau de gravité pour les alertes.
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Priorité pour les alertes.
    /// </summary>
    public enum AlertPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }
    #endregion

    #region Structs
    // Note: System.Drawing.Point est utilisé, mais si vous ne voulez pas de dépendance à System.Drawing,
    // vous pouvez définir votre propre Point comme ceci :
    /*
    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Point(int x, int y) { X = x; Y = y; }
    }
    */
    // Cependant, pour l'instant, on utilise System.Drawing.Point car c'était implicite dans le code précédent.
    #endregion
}