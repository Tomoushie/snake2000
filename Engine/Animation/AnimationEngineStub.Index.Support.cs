// Ces onze methodes sont appelees par AnimationEngineStub.Index.cs sans y etre
// definies. Chacune est nommee par le compilateur et typee par son site d'appel.
// Corps vides : aucun appelant ne dicte encore de comportement.

using System;
using System.Collections.Generic;

namespace Engine.Animation
{
    public enum SubsystemSecurityLevel
    {
        Low,
        Medium,
        High
    }

    public partial class AnimationEngineStub
    {
        public List<string> ListSubsystems()
        {
            return new List<string>();
        }

        public SubsystemDescriptor GetSubsystemDescriptor(string name)
        {
            return default(SubsystemDescriptor);
        }

        public SubsystemHealthStatus GetSubsystemHealth(string name)
        {
            return default(SubsystemHealthStatus);
        }

        public T GetSubsystem<T>(string name) where T : class
        {
            return default(T);
        }

        public SubsystemSecurityLevel GetSubsystemSecurityLevel(string name)
        {
            return default(SubsystemSecurityLevel);
        }

        public List<string> CalculateLoadOrder()
        {
            return new List<string>();
        }

        public bool ValidateExternalSubsystemCompatibility(IExternalSubsystem instance)
        {
            return false;
        }

        public List<string> GetSubsystemDependencies(string name)
        {
            return new List<string>();
        }

        public bool IsSubsystemHealthy(string name)
        {
            return false;
        }

        public AnimationEngineMetrics GetSubsystemMetrics(string name)
        {
            return default(AnimationEngineMetrics);
        }

        public string GetSubsystemVersion(string name)
        {
            return string.Empty;
        }
    }
}