// Surface que Engine/Animation/Tests/AnimationEngineStubOrchestrator.cs attend du
// stub. Chacun de ces quatorze membres vient d'un site d'appel reel, nomme par le
// compilateur. Corps vides : aucun appelant ne dicte encore de comportement.

using System;
using System.Collections.Generic;
using Engine.Animation.Test;

namespace Engine.Animation
{
    public partial class AnimationEngineStub
    {
        public bool IsShutdown => false;
        public HealthStatus HealthStatus => HealthStatus.Healthy;
        public int LoggedCallCount => 0;

        public void StartRecordingSession() { }
        public RecordedSession StopRecordingSession() => new RecordedSession();
        public void ReplaySession(RecordedSession session) { }

        public void PlayScenario(StubScenario scenario) { }

        public bool AssertHealthy() => true;
        public string GetStateSnapshot() => string.Empty;
        public List<string> GetStateHistory() => new List<string>();

        public List<string> GetCallRecords() => new List<string>();
        public DetailedMetrics GetDetailedMetrics() => new DetailedMetrics();
        public DiagnosticReport GenerateDiagnosticReport() => new DiagnosticReport();
        public void EnableTelemetryExport(bool enabled) { }
    }

    public static class AnimationEngineStubFactory
    {
        public static AnimationEngineStub CreateDeterministic() => new AnimationEngineStub();
    }
}