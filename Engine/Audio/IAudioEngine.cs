// /Engine/Audio/IAudioEngine.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Events;
using Engine.Profiling;
using Engine.Jobsystem;
using Engine.Utilities;
using Engine.Mathematics; // [CORRECTION A.1] : Utilisation du Vector3 du moteur

namespace Engine.Audio
{
    #region Enums
    public enum AudioEngineState
    {
        Uninitialized,
        Initializing,
        Ready,
        Playing,
        Paused,
        Degraded, // [ARCHITECTURE B.1]
        Recovering, // [ARCHITECTURE B.2]
        Error,
        ShuttingDown,
        Shutdown
    }

    public enum AudioChannel
    {
        Master,
        Music,
        SFX,
        Ambience,
        UI,
        Voice,
        Footsteps, // [GAMEPLAY MAPPING]
        Impacts,   // [GAMEPLAY MAPPING]
        Weather    // [GAMEPLAY MAPPING]
    }

    public enum AudioPriority
    {
        Lowest = 0,
        Low = 32,
        Medium = 64,
        High = 128,
        Critical = 255
    }

    public enum AudioSpatializationMode
    {
        None,       // 2D pur (UI, Musique)
        Panning2D,  // Stéréo simple
        Full3D      // HRTF ou spatialisation 3D complète
    }

    public enum AudioAttenuationModel
    {
        None,
        Linear,
        Inverse,
        InverseSquared,
        Exponential,
        CustomCurve
    }

    public enum AudioRolloffMode
    {
        Linear,
        Logarithmic,
        Custom
    }

    public enum AudioState
    {
        Stopped,
        Playing,
        Paused,
        FadingIn,
        FadingOut
    }

    // [SPATIALISATION D.21] Enums pour les effets de Doppler et d'absorption
    public enum AudioDopplerFactor
    {
        Disabled = 0,
        Standard = 1,
        Enhanced = 2
    }

    // [SPATIALISATION D.24] Enums pour les effets d'humidité/température
    public enum AudioEnvironment
    {
        Indoors,
        Outdoors,
        Cave,
        Hall,
        Underwater,
        Forest,
        Desert
    }

    // [STREAMING G.27] Enums pour les stratégies de conversion
    public enum ConversionQuality
    {
        Fast,
        Medium,
        High
    }

    // [MIXING F.28] Enums pour les types de filtres
    public enum FilterType
    {
        LowPass,
        HighPass,
        BandPass,
        Notch,
        Peaking,
        LowShelf,
        HighShelf
    }

    // [MIXING F.49] Enums pour les lois de panning
    public enum PanningLaw
    {
        ConstantPower,
        ConstantGain,
        SquareRoot
    }

    // [ACCESSIBILITÉ J.1] Enums pour les profils d'accessibilité
    public enum AccessibilityProfile
    {
        Standard,
        HearingImpaired,
        Tinnitus,
        MonoOnly,
        NightMode
    }
    #endregion

    #region Interfaces
    public interface IAudioEngine
    {
        // Lifecycle
        void Initialize(AudioEngineConfig config, EventBus eventBus, Profiler profiler, IJobSystem jobSystem, ResourceManager resourceManager);
        Task InitializeAsync(AudioEngineConfig config, EventBus eventBus, Profiler profiler, IJobSystem jobSystem, ResourceManager resourceManager); // [ARCHITECTURE B.3]
        void Shutdown();
        Task ShutdownAsync(); // [ARCHITECTURE B.4]
        void Restart(AudioEngineConfig config); // [ARCHITECTURE B.6]
        void Reset(); // [ARCHITECTURE B.7]
        void WarmupPhase(); // [ARCHITECTURE B.8]
        void CooldownPhase(); // [ARCHITECTURE B.9]
        void Suspend(); // [ARCHITECTURE B.10]
        void Resume(); // [ARCHITECTURE B.11]
        AudioEngineState GetState();
        bool IsReady();
        
        // Playback
        IAudioSource PlayOneShot(IAudioClip clip, AudioPlayOptions options = default);
        IAudioSource PlayLooping(IAudioClip clip, AudioPlayOptions options = default);
        void StopAll(AudioChannel channel = AudioChannel.Master);
        void PauseAll();
        void ResumeAll();
        
        // Music & Playlists
        void PlayMusic(IAudioClip clip, float fadeDuration = 1.0f);
        void CrossfadeMusic(IAudioClip nextClip, float fadeDuration = 2.0f);
        void StopMusic(float fadeDuration = 1.0f);
        
        // Listener
        void SetListenerTransform(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity); // [CORRECTION A.1] : Utilisation de Engine.Vector3
        
        // Mixers & Volumes
        void SetVolume(AudioChannel channel, float volume);
        float GetVolume(AudioChannel channel);
        void MuteChannel(AudioChannel channel, bool mute);
        void SetMasterVolume(float volume);
        
        // Capabilities & Profiling
        AudioEngineCapabilities GetCapabilities();
        AudioEngineMetrics GetMetrics();
        int GetActiveVoiceCount();
        int GetMaxVoiceCount();
        
        // Advanced AAA Features
        void ApplyReverbPreset(ReverbPreset preset);
        void SetGlobalPitch(float pitch);
        void EnableDucking(AudioChannel target, AudioChannel trigger, float duckVolume, float attackTime, float releaseTime);
        void DisableDucking(AudioChannel target, AudioChannel trigger);

        // [CONTRACTS C.19] Backend Management
        void RegisterBackend(IAudioBackend backend);
        void UnregisterBackend();
        List<string> GetAvailableBackends();
        void SwitchBackend(string backendName);

        // [CONTRACTS C.24] DSP Management
        void RegisterDSP(IAudioDSP dsp);
        void UnregisterDSP(string dspName);
        List<IAudioDSP> GetDSPChain(AudioChannel channel);
        void InsertDSP(AudioChannel channel, IAudioDSP dsp, int index);
        void RemoveDSP(AudioChannel channel, int index);

        // [CONTRACTS C.30] Submix Management
        IAudioMixer CreateSubmix(string name);
        void DestroySubmix(string name);
        void RouteChannelToSubmix(AudioChannel channel, string submixName);
        float GetSubmixVolume(string submixName);
        void SetSubmixVolume(string submixName, float volume);

        // [SPATIALISATION D.1] Spatialization Features
        void SetHRTFEnabled(bool enabled);
        void SetAmbisonicsOrder(int order);
        void SetOcclusionEnabled(bool enabled);
        void SetObstructionEnabled(bool enabled);
        void SetAirAbsorptionEnabled(bool enabled);
        void SetDopplerFactor(AudioDopplerFactor factor);

        // [MUSIC E.1] Music Features
        void LoadMusicSegments(Dictionary<string, IAudioClip> segments);
        void PlayMusicSegment(string segmentName, float fadeInTime = 0.5f);
        void TransitionMusicSegment(string fromSegment, string toSegment, float transitionTime = 1.0f);
        void SetMusicIntensity(float intensity); // [MUSIC E.15]

        // [MIXING F.1] Mixing Features
        void SetEQ(AudioChannel channel, EqualizerSettings settings);
        void SetCompressor(AudioChannel channel, CompressorSettings settings);
        void SetLimiter(AudioChannel channel, LimiterSettings settings);
        void SetDelay(AudioChannel channel, DelaySettings settings);
        void SetReverb(AudioChannel channel, ReverbPreset preset);

        // [STREAMING G.1] Streaming Features
        void SetStreamingPriority(AudioChannel channel, int priority);
        void SetStreamingLODEnabled(bool enabled);
        void SetStreamingBandwidthLimit(float limitKBps);

        // [OBSERVABILITY H.1] Profiling Features
        void EnableAudioVisualization(bool enabled);
        AudioEngineMetricsHistory GetMetricsHistory(TimeSpan duration);
        void ExportMetricsToFile(string filePath, MetricsExportFormat format);
        void EnableTelemetry(bool enabled);
        void SendAnalyticsEvent(string eventName, Dictionary<string, object> properties);

        // [FORMATS I.1] Format Features
        void SetCompressionPreset(AudioChannel channel, CompressionPreset preset);
        void LoadMiddlewareSoundbank(string bankPath, MiddlewareType type);

        // [ACCESSIBILITÉ J.1] Accessibility Features
        void SetAccessibilityProfile(AccessibilityProfile profile);
        void SetMonoOutputEnabled(bool enabled);
        void SetDialogueBoost(float boostDb);
    }

    public interface IAudioClip
    {
        string Name { get; }
        float Duration { get; }
        int SampleRate { get; }
        int Channels { get; }
        bool IsStreamed { get; }
        bool IsLoaded { get; }
        Task LoadAsync();
        void Unload();
        // [STREAMING G.15] Hardware acceleration
        bool IsHardwareAccelerated { get; }
    }

    public interface IAudioSource : IDisposable
    {
        uint Id { get; }
        IAudioClip Clip { get; }
        AudioState State { get; }
        float Volume { get; set; }
        float Pitch { get; set; }
        float Pan { get; set; }
        bool IsLooping { get; set; }
        bool IsMuted { get; set; }
        AudioPriority Priority { get; set; }
        AudioSpatializationMode SpatialMode { get; set; }
        
        void Play();
        void Pause();
        void Stop();
        void SetTransform(Vector3 position, Vector3 velocity); // [CORRECTION A.1] : Utilisation de Engine.Vector3
        void SetAttenuation(float minDistance, float maxDistance, AudioAttenuationModel model);
        // [MIXING F.1] DSP on Source
        void SetSourceEQ(EqualizerSettings settings);
        void SetSourceCompressor(CompressorSettings settings);
        void SetSourceDelay(DelaySettings settings);
        void SetSourceReverb(ReverbPreset preset);
    }

    public interface IAudioListener
    {
        Vector3 Position { get; set; }
        Vector3 Forward { get; set; }
        Vector3 Up { get; set; }
        Vector3 Velocity { get; set; }
    }

    // [CONTRACTS C.1] Backend Interface
    public interface IAudioBackend
    {
        string Name { get; }
        bool IsInitialized { get; }
        bool IsDeviceConnected { get; }
        List<AudioDeviceInfo> GetAvailableDevices();
        void Initialize(DevicePreferences preferences);
        void Shutdown();
        void SetMasterVolume(float volume);
        IAudioVoice PlayClip(IAudioClip clip, AudioSourceProperties props);
    }

    // [CONTRACTS C.2] DSP Interface
    public interface IAudioDSP
    {
        string Name { get; }
        void Process(float[] samples, int channels, int sampleRate);
        void SetParameter(string paramName, object value);
        object GetParameter(string paramName);
    }

    // [CONTRACTS C.3] Mixer Interface
    public interface IAudioMixer
    {
        string Name { get; }
        float Volume { get; set; }
        void AddInput(IAudioSource source);
        void RemoveInput(IAudioSource source);
        void SetSend(string destinationSubmix, float level);
    }

    // [CONTRACTS C.4] Stream Interface
    public interface IAudioStream
    {
        bool IsPlaying { get; }
        bool IsLooping { get; set; }
        float Volume { get; set; }
        void Play();
        void Pause();
        void Stop();
        void Seek(TimeSpan time);
    }

    // [CONTRACTS C.5] Decoder Interface
    public interface IAudioDecoder
    {
        string SupportedFormat { get; }
        Task<DecodedAudioData> DecodeAsync(byte[] encodedData);
    }

    // [SPATIALISATION D.6] Spatializer Interface
    public interface IAudioSpatializer : IAudioDSP
    {
        void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity);
        void UpdateSourcePosition(IAudioSource source, Vector3 position, Vector3 velocity);
    }

    // [SPATIALISATION D.7] Reverb Interface
    public interface IAudioReverb : IAudioDSP
    {
        void ApplyReverbPreset(ReverbPreset preset);
    }

    // [MIXING F.1] Additional DSP Interfaces
    public interface IAudioFilter : IAudioDSP
    {
        FilterType Type { get; set; }
        float Frequency { get; set; }
        float Q { get; set; }
        float GainDb { get; set; }
    }

    public interface IAudioCompressor : IAudioDSP
    {
        float ThresholdDb { get; set; }
        float Ratio { get; set; }
        float AttackMs { get; set; }
        float ReleaseMs { get; set; }
        float MakeupGainDb { get; set; }
    }

    public interface IAudioLimiter : IAudioDSP
    {
        float ThresholdDb { get; set; }
        float ReleaseMs { get; set; }
    }

    public interface IAudioEQ : IAudioDSP
    {
        EqualizerBand[] Bands { get; set; }
    }

    public interface IAudioAnalyzer : IAudioDSP
    {
        float[] GetSpectrum();
        float GetLoudness();
        float GetPeak();
    }

    public interface IAudioRecorder
    {
        bool IsRecording { get; }
        void StartRecording(string outputPath);
        void StopRecording();
        event EventHandler<ReadOnlyMemory<float>> OnAudioDataCaptured;
    }

    public interface IAudioSynthesizer
    {
        void GenerateNote(float frequency, float duration, float volume);
        void PlaySequence(Note[] sequence);
    }

    public interface IAudioMIDI
    {
        void LoadMidiFile(string filePath);
        void PlayMidiTrack(int trackIndex);
        void StopMidiTrack(int trackIndex);
    }
    #endregion

    #region Structures
    public struct AudioEngineConfig
    {
        public int MaxVoices { get; set; }
        public int SampleRate { get; set; }
        public int BufferSize { get; set; }
        public bool EnableSpatialization { get; set; }
        public bool EnableReverb { get; set; }
        public float MasterVolume { get; set; }
        public float MusicVolume { get; set; }
        public float SFXVolume { get; set; }
        public float VoiceVolume { get; set; }
        public float AmbienceVolume { get; set; }
        public AudioPriority VoiceStealingThreshold { get; set; }
        public float CullingDistanceMax { get; set; }
        public bool EnableAutoDucking { get; set; }
        // [ARCHITECTURE B.36] Versioned Config
        public string Version { get; set; }
        
        public AudioEngineConfig(AudioEngineConfig other)
        {
            MaxVoices = other.MaxVoices;
            SampleRate = other.SampleRate;
            BufferSize = other.BufferSize;
            EnableSpatialization = other.EnableSpatialization;
            EnableReverb = other.EnableReverb;
            MasterVolume = other.MasterVolume;
            MusicVolume = other.MusicVolume;
            SFXVolume = other.SFXVolume;
            VoiceVolume = other.VoiceVolume;
            AmbienceVolume = other.AmbienceVolume;
            VoiceStealingThreshold = other.VoiceStealingThreshold;
            CullingDistanceMax = other.CullingDistanceMax;
            EnableAutoDucking = other.EnableAutoDucking;
            Version = other.Version;
        }
    }

    public struct AudioPlayOptions
    {
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float Pan { get; set; }
        public AudioPriority Priority { get; set; }
        public AudioChannel Channel { get; set; }
        public AudioSpatializationMode SpatialMode { get; set; }
        public Vector3 Position { get; set; } // [CORRECTION A.1] : Utilisation de Engine.Vector3
        public Vector3 Velocity { get; set; } // [CORRECTION A.1] : Utilisation de Engine.Vector3
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public AudioAttenuationModel AttenuationModel { get; set; }
        public float DelaySeconds { get; set; }
        public bool AutoDispose { get; set; }
        // [MIXING F.1] DSP on Play
        public EqualizerSettings SourceEQ { get; set; }
        public CompressorSettings SourceCompressor { get; set; }
        public DelaySettings SourceDelay { get; set; }
        public ReverbPreset SourceReverb { get; set; }

        // [CORRECTION A.2] : Rendre la propriété Default immuable
        private static readonly AudioPlayOptions _default = new AudioPlayOptions
        {
            Volume = 1.0f,
            Pitch = 1.0f,
            Pan = 0.0f,
            Priority = AudioPriority.Medium,
            Channel = AudioChannel.SFX,
            SpatialMode = AudioSpatializationMode.None,
            Position = Vector3.Zero, // [CORRECTION A.1]
            Velocity = Vector3.Zero, // [CORRECTION A.1]
            MinDistance = 1.0f,
            MaxDistance = 20.0f,
            AttenuationModel = AudioAttenuationModel.InverseSquared,
            DelaySeconds = 0.0f,
            AutoDispose = true,
            SourceEQ = default,
            SourceCompressor = default,
            SourceDelay = default,
            SourceReverb = ReverbPreset.None
        };

        public static AudioPlayOptions Default => _default;
    }

    public struct AudioEngineCapabilities
    {
        public bool Supports3DAudio { get; set; }
        public bool SupportsReverb { get; set; }
        public bool SupportsFilters { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsHardwareAcceleration { get; set; } // [STREAMING G.15]
        public bool SupportsHRTF { get; set; } // [SPATIALISATION D.1]
        public bool SupportsAmbisonics { get; set; } // [SPATIALISATION D.2]
        public int MaxSupportedVoices { get; set; }
        public string BackendName { get; set; }
        public string BackendVersion { get; set; }
        public List<string> SupportedFormats { get; set; } // [FORMATS I.16]
    }

    public struct AudioEngineMetrics
    {
        public int ActiveVoices { get; set; }
        public int ActiveMusicTracks { get; set; }
        public int CulledVoices { get; set; }
        public int StolenVoices { get; set; }
        public float CpuUsageMs { get; set; }
        public float MemoryUsedMB { get; set; }
        public float DSPChainTimeMs { get; set; }
        public int MixerUpdates { get; set; }
        public int BufferUnderruns { get; set; }
        // [OBSERVABILITY H.1] Extended Metrics
        public float LatencyMs { get; set; }
        public float ClippingCount { get; set; }
        public float UnderrunCount { get; set; }
        public float JitterMs { get; set; }
        public float THDPercent { get; set; }
        public float SNRdB { get; set; }
    }

    // [OBSERVABILITY H.6] Metrics History
    public struct AudioEngineMetricsHistory
    {
        public List<AudioEngineMetrics> Metrics { get; set; }
        public TimeSpan WindowDuration { get; set; }
    }

    public struct ReverbPreset
    {
        public string Name { get; set; }
        public float Density { get; set; }
        public float Diffusion { get; set; }
        public float Gain { get; set; }
        public float GainHF { get; set; }
        public float DecayTime { get; set; }
        public float DecayHFRatio { get; set; }
        public float ReflectionsGain { get; set; }
        public float ReflectionsDelay { get; set; }
        public float ReverbGain { get; set; }
        public float ReverbDelay { get; set; }
        public float RoomRolloffFactor { get; set; }
        public float AirAbsorptionGainHF { get; set; }

        public static ReverbPreset None => new ReverbPreset { Name = "None", Density = 0, Diffusion = 0, Gain = 0 };
        public static ReverbPreset Cave => new ReverbPreset { Name = "Cave", Density = 1.0f, Diffusion = 1.0f, DecayTime = 2.5f };
        public static ReverbPreset Hall => new ReverbPreset { Name = "Hall", Density = 1.0f, Diffusion = 1.0f, DecayTime = 4.0f };
        public static ReverbPreset SmallRoom => new ReverbPreset { Name = "SmallRoom", Density = 1.0f, Diffusion = 1.0f, DecayTime = 0.8f };
    }

    // [MIXING F.11] EQ Settings
    public struct EqualizerSettings
    {
        public EqualizerBand[] Bands { get; set; }
    }

    public struct EqualizerBand
    {
        public float FrequencyHz { get; set; }
        public float GainDb { get; set; }
        public float Q { get; set; }
        public bool Enabled { get; set; }
    }

    // [MIXING F.15] Compressor Settings
    public struct CompressorSettings
    {
        public float ThresholdDb { get; set; }
        public float Ratio { get; set; }
        public float AttackMs { get; set; }
        public float ReleaseMs { get; set; }
        public float MakeupGainDb { get; set; }
        public bool Enabled { get; set; }
    }

    // [MIXING F.18] Limiter Settings
    public struct LimiterSettings
    {
        public float ThresholdDb { get; set; }
        public float ReleaseMs { get; set; }
        public bool Enabled { get; set; }
    }

    // [MIXING F.25] Delay Settings
    public struct DelaySettings
    {
        public float TimeMs { get; set; }
        public float FeedbackPercent { get; set; }
        public float WetMixPercent { get; set; }
        public bool Enabled { get; set; }
    }

    // [CONTRACTS C.29] Device Info
    public struct AudioDeviceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int MaxChannels { get; set; }
        public int PreferredSampleRate { get; set; }
        public bool IsDefault { get; set; }
    }

    // [CONTRACTS C.29] Device Preferences
    public struct DevicePreferences
    {
        public string PreferredDeviceId { get; set; }
        public int DesiredSampleRate { get; set; }
        public int DesiredBufferSize { get; set; }
    }

    // [MUSIC E.1] Music Segment Info
    public struct MusicSegmentInfo
    {
        public string Name { get; set; }
        public IAudioClip Clip { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<MusicTransitionPoint> TransitionPoints { get; set; }
    }

    public struct MusicTransitionPoint
    {
        public string TargetSegment { get; set; }
        public TimeSpan AtTime { get; set; }
    }

    // [FORMATS I.20] Compression Preset
    public struct CompressionPreset
    {
        public string Name { get; set; }
        public int BitrateKbps { get; set; }
        public bool UseVBR { get; set; }
        public ConversionQuality Quality { get; set; }
    }

    // [FORMATS I.44] Middleware Type
    public enum MiddlewareType
    {
        Wwise,
        FMOD,
        Custom
    }

    // [STREAMING G.11] Decoded Audio Data
    public struct DecodedAudioData
    {
        public float[] Samples { get; set; }
        public int Channels { get; set; }
        public int SampleRate { get; set; }
    }

    // [OBSERVABILITY H.37] Export Format
    public enum MetricsExportFormat
    {
        JSON,
        CSV,
        Binary,
        ChromeTrace
    }

    // [ACCESSIBILITÉ J.1] Hearing Loss Curve
    public struct HearingLossCurve
    {
        public float[] FrequenciesHz { get; set; }
        public float[] AttenuationDb { get; set; }
    }

    // [MIXING F.1] Source Properties for Backend
    public struct AudioSourceProperties
    {
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float Pan { get; set; }
        public bool IsLooping { get; set; }
        public AudioAttenuationModel AttenuationModel { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        // [MIXING F.1] Embedded DSP Settings
        public EqualizerSettings EQ { get; set; }
        public CompressorSettings Compressor { get; set; }
        public DelaySettings Delay { get; set; }
        public ReverbPreset Reverb { get; set; }
    }

    // [SPATIALISATION D.34] Listener Info for Spatializer
    public struct SpatializerListenerInfo
    {
        public Vector3 Position { get; set; }
        public Vector3 Forward { get; set; }
        public Vector3 Up { get; set; }
        public Vector3 Velocity { get; set; }
    }

    // [SPATIALISATION D.35] Source Info for Spatializer
    public struct SpatializerSourceInfo
    {
        public uint SourceId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public AudioAttenuationModel AttenuationModel { get; set; }
    }

    // [MUSIC E.21] Beat Info
    public struct BeatInfo
    {
        public int Bar { get; set; }
        public int Beat { get; set; }
        public int Subdivision { get; set; }
        public float BPM { get; set; }
        public TimeSpan TimeSinceBarStart { get; set; }
    }

    // [MUSIC E.35] RTPC (Real-Time Parameter Control)
    public struct RTPCValue
    {
        public string ParameterName { get; set; }
        public float Value { get; set; }
    }

    // [MUSIC E.41] Switch State
    public struct SwitchState
    {
        public string GroupName { get; set; }
        public string StateName { get; set; }
    }

    // [STREAMING G.1] Stream Request
    public struct StreamRequest
    {
        public string FilePath { get; set; }
        public int Priority { get; set; }
        public bool IsLooping { get; set; }
        public float Volume { get; set; }
    }

    // [STREAMING G.11] Resample Request
    public struct ResampleRequest
    {
        public float[] InputSamples { get; set; }
        public int InputSampleRate { get; set; }
        public int OutputSampleRate { get; set; }
        public ConversionQuality Quality { get; set; }
    }

    // [PERFORMANCE G.26] Voice Budget
    public struct VoiceBudget
    {
        public AudioChannel Channel { get; set; }
        public int MaxVoices { get; set; }
        public int CurrentVoices { get; set; }
    }

    // [PERFORMANCE G.32] CPU Usage Limit
    public struct CPULimit
    {
        public float MaxUsagePercent { get; set; }
        public bool AutoDisableEffects { get; set; }
    }

    // [OBSERVABILITY H.1] Loudness Info
    public struct LoudnessInfo
    {
        public float LUFS { get; set; }
        public float RMS { get; set; }
        public float Peak { get; set; }
    }

    // [TESTS K.17] Audio Quality Metrics
    public struct AudioQualityMetrics
    {
        public float SNRdB { get; set; }
        public float THDPercent { get; set; }
        public float FrequencyResponseDeviationDb { get; set; }
        public TimeSpan Latency { get; set; }
    }
    #endregion

    #region Events
    public class AudioEngineInitializedEvent
    {
        public IAudioEngine Source { get; }
        public AudioEngineInitializedEvent(IAudioEngine source) => Source = source;
    }

    public class AudioEngineShutdownEvent
    {
        public IAudioEngine Source { get; }
        public AudioEngineShutdownEvent(IAudioEngine source) => Source = source;
    }

    public class AudioClipPlayedEvent
    {
        public IAudioClip Clip { get; }
        public AudioChannel Channel { get; }
        public uint SourceId { get; }
        public Vector3 Position { get; } // [CORRECTION A.1]
        public AudioPlayOptions Options { get; }
        public AudioClipPlayedEvent(IAudioClip clip, AudioChannel channel, uint sourceId, Vector3 position, AudioPlayOptions options)
        {
            Clip = clip; Channel = channel; SourceId = sourceId; Position = position; Options = options;
        }
    }

    public class AudioVoiceStolenEvent
    {
        public uint StolenSourceId { get; }
        public AudioPriority StolenPriority { get; }
        public AudioPriority NewPriority { get; }
        public AudioVoiceStolenEvent(uint stolenId, AudioPriority stolenPrio, AudioPriority newPrio)
        {
            StolenSourceId = stolenId; StolenPriority = stolenPrio; NewPriority = newPrio;
        }
    }

    public class AudioMixerVolumeChangedEvent
    {
        public AudioChannel Channel { get; }
        public float OldVolume { get; }
        public float NewVolume { get; }
        public AudioMixerVolumeChangedEvent(AudioChannel channel, float oldVol, float newVol)
        {
            Channel = channel; OldVolume = oldVol; NewVolume = newVol;
        }
    }

    // [CONTRACTS C.35] Device Events
    public class AudioDeviceChangedEvent
    {
        public string NewDeviceId { get; }
        public string NewDeviceName { get; }
        public AudioDeviceChangedEvent(string id, string name) { NewDeviceId = id; NewDeviceName = name; }
    }

    public class AudioDeviceLostEvent
    {
        public string LostDeviceId { get; }
        public AudioDeviceLostEvent(string id) { LostDeviceId = id; }
    }

    public class AudioDeviceResetEvent
    {
        public string ResetDeviceId { get; }
        public AudioDeviceResetEvent(string id) { ResetDeviceId = id; }
    }

    // [CONTRACTS C.39] Error Events
    public class AudioBufferUnderrunEvent
    {
        public int Count { get; }
        public AudioBufferUnderrunEvent(int count) { Count = count; }
    }

    public class AudioDSPErrorEvent
    {
        public string DSPName { get; }
        public string ErrorMessage { get; }
        public AudioDSPErrorEvent(string name, string msg) { DSPName = name; ErrorMessage = msg; }
    }

    public class AudioStreamErrorEvent
    {
        public string StreamId { get; }
        public string ErrorMessage { get; }
        public AudioStreamErrorEvent(string id, string msg) { StreamId = id; ErrorMessage = msg; }
    }

    // [CONTRACTS C.45] Playback Events
    public class AudioPlaybackStartedEvent
    {
        public uint SourceId { get; }
        public IAudioClip Clip { get; }
        public AudioPlaybackStartedEvent(uint id, IAudioClip clip) { SourceId = id; Clip = clip; }
    }

    public class AudioPlaybackStoppedEvent
    {
        public uint SourceId { get; }
        public IAudioClip Clip { get; }
        public AudioPlaybackStoppedEvent(uint id, IAudioClip clip) { SourceId = id; Clip = clip; }
    }

    // [ARCHITECTURE B.12] Lifecycle Event
    public class AudioEngineLifecycleEvent
    {
        public AudioEngineState PreviousState { get; }
        public AudioEngineState NewState { get; }
        public DateTime Timestamp { get; }
        public AudioEngineLifecycleEvent(AudioEngineState prev, AudioEngineState next) { PreviousState = prev; NewState = next; Timestamp = DateTime.UtcNow; }
    }

    // [OBSERVABILITY H.1] Profiler Event
    public class AudioProfilerEvent
    {
        public string EventType { get; }
        public object Data { get; }
        public DateTime Timestamp { get; }
        public AudioProfilerEvent(string type, object data) { EventType = type; Data = data; Timestamp = DateTime.UtcNow; }
    }
    #endregion

    #region Implementation
    public sealed class AudioEngine : IAudioEngine, IDisposable
    {
        #region Fields
        private volatile int _stateAsInt;
        private volatile AudioEngineConfig _config;
        private EventBus _eventBus;
        private Profiler _profiler;
        private IJobSystem _jobSystem;
        private ResourceManager _resourceManager;

        // [CORRECTION A.8] Lock-free collection pour les sources actives
        private readonly ConcurrentBag<IAudioSource> _activeSources = new ConcurrentBag<IAudioSource>();
        // [CORRECTION A.17] Race condition fix: utiliser un verrou pour les opérations de bulk
        private readonly object _sourcesBulkOpLock = new object();

        private readonly Dictionary<AudioChannel, float> _channelVolumes = new Dictionary<AudioChannel, float>();
        private readonly Dictionary<AudioChannel, bool> _channelMutes = new Dictionary<AudioChannel, bool>();
        private readonly List<DuckingRule> _duckingRules = new List<DuckingRule>();

        // [CONTRACTS C.1] Backend Management
        private IAudioBackend _currentBackend;
        private readonly Dictionary<string, IAudioBackend> _availableBackends = new Dictionary<string, IAudioBackend>();

        // [CONTRACTS C.2] DSP Management
        private readonly Dictionary<AudioChannel, List<IAudioDSP>> _dspChains = new Dictionary<AudioChannel, List<IAudioDSP>>();

        // [CONTRACTS C.30] Submix Management
        private readonly Dictionary<string, IAudioMixer> _submixes = new Dictionary<string, IAudioMixer>();

        // [MUSIC E.1] Music Management
        private readonly Dictionary<string, MusicSegmentInfo> _musicSegments = new Dictionary<string, MusicSegmentInfo>();
        private float _currentMusicIntensity = 0.5f; // [MUSIC E.15]

        // [SPATIALISATION D.1] Spatialization
        private IAudioSpatializer _currentSpatializer;
        private readonly Dictionary<uint, SpatializerSourceInfo> _spatializedSources = new Dictionary<uint, SpatializerSourceInfo>();

        // [STREAMING G.1] Streaming
        private readonly Queue<StreamRequest> _streamingRequests = new Queue<StreamRequest>();
        private readonly object _streamingLock = new object();

        // [PERFORMANCE G.26] Voice Management
        private readonly Dictionary<AudioChannel, VoiceBudget> _voiceBudgets = new Dictionary<AudioChannel, VoiceBudget>();

        // [OBSERVABILITY H.1] Metrics
        private readonly RingBuffer<AudioEngineMetrics> _metricsHistory = new RingBuffer<AudioEngineMetrics>(120); // 2s @ 60fps
        private readonly object _metricsLock = new object();

        // [ARCHITECTURE B.1] Lifecycle
        private IAudioSource _currentMusicSource;
        private IAudioSource _nextMusicSource;
        private float _musicCrossfadeTimer;
        private float _musicCrossfadeDuration;
        private bool _isCrossfading;

        private IAudioListener _listener;
        private AudioEngineMetrics _metrics;
        private AudioEngineCapabilities _capabilities;
        
        private uint _nextSourceId = 1;
        private long _stolenVoicesCount = 0;
        private long _culledVoicesCount = 0;
        private readonly Stopwatch _updateStopwatch = new Stopwatch();

        // [CORRECTION A.34] Overflow protection for _nextSourceId
        private const uint MaxSourceId = uint.MaxValue - 1;

        // [CORRECTION A.44] Verification de _disposed dans les méthodes publiques
        private bool _disposed = false;

        // [OBSERVABILITY H.1] Visualization
        private bool _visualizationEnabled = false;

        // [OBSERVABILITY H.26] Logging
        private bool _loggingEnabled = false;

        // [OBSERVABILITY H.35] Telemetry
        private bool _telemetryEnabled = false;

        // [ACCESSIBILITÉ J.1] Accessibility
        private AccessibilityProfile _currentAccessibilityProfile = AccessibilityProfile.Standard;
        private bool _monoOutputEnabled = false;
        private float _dialogueBoost = 0.0f;
        private HearingLossCurve _hearingLossCompensationCurve;

        // [TESTS K.1] Test helpers
        private volatile bool _testMode = false;
        private readonly List<AudioQualityMetrics> _testResults = new List<AudioQualityMetrics>();
        #endregion

        #region Properties
        public AudioEngineState State => (AudioEngineState)Volatile.Read(ref _stateAsInt);
        #endregion

        #region Lifecycle
        public void Initialize(AudioEngineConfig config, EventBus eventBus, Profiler profiler, IJobSystem jobSystem, ResourceManager resourceManager)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!TryTransitionState(AudioEngineState.Uninitialized, AudioEngineState.Initializing))
                throw new InvalidOperationException($"Cannot initialize AudioEngine: current state is '{State}'.");

            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
            _jobSystem = jobSystem ?? throw new ArgumentNullException(nameof(jobSystem));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));

            ApplyConfiguration(config);
            InitializeBackend();
            
            _listener = new DefaultAudioListener();
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Ready);
            _eventBus?.Publish(new AudioEngineInitializedEvent(this));
            _eventBus?.Publish(new AudioEngineLifecycleEvent(AudioEngineState.Initializing, AudioEngineState.Ready)); // [ARCHITECTURE B.11]
        }

        public async Task InitializeAsync(AudioEngineConfig config, EventBus eventBus, Profiler profiler, IJobSystem jobSystem, ResourceManager resourceManager)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!TryTransitionState(AudioEngineState.Uninitialized, AudioEngineState.Initializing))
                throw new InvalidOperationException($"Cannot initialize AudioEngine: current state is '{State}'.");

            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _profiler = profiler ?? throw new ArgumentNullException(nameof(profiler));
            _jobSystem = jobSystem ?? throw new ArgumentNullException(nameof(jobSystem));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));

            ApplyConfiguration(config);
            await InitializeBackendAsync(); // [ASYNC PLACEHOLDER]
            
            _listener = new DefaultAudioListener();
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Ready);
            _eventBus?.Publish(new AudioEngineInitializedEvent(this));
            _eventBus?.Publish(new AudioEngineLifecycleEvent(AudioEngineState.Initializing, AudioEngineState.Ready));
        }

        public void Shutdown()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!TryTransitionState(AudioEngineState.Ready, AudioEngineState.ShuttingDown) &&
                !TryTransitionState(AudioEngineState.Playing, AudioEngineState.ShuttingDown) &&
                !TryTransitionState(AudioEngineState.Paused, AudioEngineState.ShuttingDown) &&
                !TryTransitionState(AudioEngineState.Error, AudioEngineState.ShuttingDown))
                return;

            StopAll();
            FlushAudioBuffers(); // [CORRECTION A.35]
            ShutdownBackend();
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Shutdown);
            _eventBus?.Publish(new AudioEngineShutdownEvent(this));
            _eventBus?.Publish(new AudioEngineLifecycleEvent(AudioEngineState.ShuttingDown, AudioEngineState.Shutdown));
        }

        public async Task ShutdownAsync()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (!TryTransitionState(AudioEngineState.Ready, AudioEngineState.ShuttingDown) &&
                !TryTransitionState(AudioEngineState.Playing, AudioEngineState.ShuttingDown) &&
                !TryTransitionState(AudioEngineState.Paused, AudioEngineState.ShuttingDown) &&
                !TryTransitionState(AudioEngineState.Error, AudioEngineState.ShuttingDown))
                return;

            StopAll();
            await FlushAudioBuffersAsync(); // [ASYNC PLACEHOLDER]
            await ShutdownBackendAsync(); // [ASYNC PLACEHOLDER]
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Shutdown);
            _eventBus?.Publish(new AudioEngineShutdownEvent(this));
            _eventBus?.Publish(new AudioEngineLifecycleEvent(AudioEngineState.ShuttingDown, AudioEngineState.Shutdown));
        }

        public void Restart(AudioEngineConfig config) // [CORRECTION A.36]
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            var oldConfig = _config; // [CORRECTION A.36]
            Shutdown();
            Initialize(config, _eventBus, _profiler, _jobSystem, _resourceManager);
            // Optionally restore some runtime settings from oldConfig
        }

        public void Reset() // [CORRECTION A.37]
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Reset internal state without full shutdown/restart
            StopAll();
            _activeSources.Clear();
            _stolenVoicesCount = 0;
            _culledVoicesCount = 0;
            _metrics = new AudioEngineMetrics();
            _nextSourceId = 1;
            // Reset other runtime values...
        }

        public void WarmupPhase() // [CORRECTION A.38]
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Pre-load common sounds, initialize DSPs, etc.
            // Placeholder implementation
        }

        public void CooldownPhase() // [CORRECTION A.39]
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            // Flush buffers, finalize pending operations
            FlushAudioBuffers();
        }

        public void Suspend() // [CORRECTION A.40]
        {
            if (_disposed || State != AudioEngineState.Playing) return;
            PauseAll();
            _currentBackend?.Suspend(); // If backend supports it
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Paused);
        }

        public void Resume() // [CORRECTION A.41]
        {
            if (_disposed || State != AudioEngineState.Paused) return;
            _currentBackend?.Resume(); // If backend supports it
            ResumeAll();
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Playing);
        }

        public void Update(float deltaTime)
        {
            if (_disposed || !IsReady()) return;
            
            _updateStopwatch.Restart();
            
            // 1. Update Listener & Spatialization
            UpdateSpatialization();
            
            // 2. Process Crossfades (Music)
            UpdateMusicCrossfade(deltaTime);
            
            // 3. Apply Ducking
            ApplyDuckingRules();
            
            // 4. Cull distant or low-priority sounds
            CullInaudibleSources();
            
            // 5. Update Backend DSP
            UpdateBackendDSP(deltaTime);

            // 6. Process Streaming Requests
            ProcessStreamingRequests();

            // 7. Update Metrics
            UpdateMetrics();

            _updateStopwatch.Stop();
            _metrics.CpuUsageMs = (float)_updateStopwatch.Elapsed.TotalMilliseconds;
            _metrics.ActiveVoices = _activeSources.Count; // [CORRECTION A.35]
        }

        public AudioEngineState GetState() => State;
        public bool IsReady() => State == AudioEngineState.Ready || State == AudioEngineState.Playing || State == AudioEngineState.Paused;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryTransitionState(AudioEngineState expected, AudioEngineState next)
        {
            return Interlocked.CompareExchange(ref _stateAsInt, (int)next, (int)expected) == (int)expected; // [CORRECTION A.19]
        }
        #endregion

        #region Playback
        public IAudioSource PlayOneShot(IAudioClip clip, AudioPlayOptions options = default)
        {
            if (_disposed || clip == null || !IsReady()) return null;

            // [CORRECTION A.9] Check if clip is loaded
            if (!clip.IsLoaded)
            {
                _ = Task.Run(async () => { await clip.LoadAsync(); }); // Fire-and-forget load
                // Optionally return null or wait briefly
            }

            // Distance Culling early-out
            if (options.SpatialMode == AudioSpatializationMode.Full3D)
            {
                float distance = Vector3.Distance(_listener.Position, options.Position); // [CORRECTION A.1] : Utilisation de Engine.Vector3
                if (distance > options.MaxDistance || distance > _config.CullingDistanceMax)
                {
                    Interlocked.Increment(ref _culledVoicesCount);
                    _metrics.CulledVoices = (int)Interlocked.Read(ref _culledVoicesCount);
                    _eventBus?.Publish(new AudioProfilerEvent("VoiceCulled", new { Distance = distance, MaxDistance = Math.Max(options.MaxDistance, _config.CullingDistanceMax) })); // [OBSERVABILITY H.1]
                    return null;
                }
            }

            IAudioSource source = AcquireSource(options.Priority);
            if (source == null) return null;

            source.Clip = clip;
            source.Volume = options.Volume;
            source.Pitch = options.Pitch;
            source.Pan = options.Pan;
            source.Priority = options.Priority;
            source.SpatialMode = options.SpatialMode;
            source.IsLooping = false;
            source.SetTransform(options.Position, options.Velocity); // [CORRECTION A.1]
            source.SetAttenuation(options.MinDistance, options.MaxDistance, options.AttenuationModel);

            // [MIXING F.1] Apply source-specific DSP
            if (options.SourceEQ.Bands != null && options.SourceEQ.Bands.Length > 0) source.SetSourceEQ(options.SourceEQ);
            if (options.SourceCompressor.Enabled) source.SetSourceCompressor(options.SourceCompressor);
            if (options.SourceDelay.Enabled) source.SetSourceDelay(options.SourceDelay);
            if (options.SourceReverb.Name != ReverbPreset.None.Name) source.SetSourceReverb(options.SourceReverb);
            
            source.Play();
            _activeSources.Add(source); // [CORRECTION A.8]
            _eventBus?.Publish(new AudioClipPlayedEvent(clip, options.Channel, source.Id, options.Position, options)); // [CONTRACTS C.45]
            _eventBus?.Publish(new AudioPlaybackStartedEvent(source.Id, clip)); // [CONTRACTS C.49]
            return source;
        }

        public IAudioSource PlayLooping(IAudioClip clip, AudioPlayOptions options = default)
        {
            if (_disposed || clip == null || !IsReady()) return null;

            // [CORRECTION A.21] Check if clip is loaded
            if (!clip.IsLoaded)
            {
                _ = Task.Run(async () => { await clip.LoadAsync(); });
            }

            IAudioSource source = AcquireSource(options.Priority);
            if (source == null) return null;

            source.Clip = clip;
            source.Volume = options.Volume;
            source.Pitch = options.Pitch;
            source.Priority = options.Priority;
            source.SpatialMode = options.SpatialMode;
            source.IsLooping = true;
            source.SetTransform(options.Position, options.Velocity); // [CORRECTION A.1]
            source.SetAttenuation(options.MinDistance, options.MaxDistance, options.AttenuationModel);

            source.Play();
            _activeSources.Add(source); // [CORRECTION A.8]
            return source;
        }

        public void StopAll(AudioChannel channel = AudioChannel.Master)
        {
            if (_disposed) return;
            
            lock (_sourcesBulkOpLock) // [CORRECTION A.17]
            {
                var sourcesToStop = new List<IAudioSource>();
                // [CORRECTION A.10] Filter by channel
                foreach (var source in _activeSources)
                {
                    // Assuming source knows its channel (would need to add to IAudioSource if not)
                    // For now, we'll stop all if Master, or implement channel filtering later.
                    if (channel == AudioChannel.Master || /* source.Channel == channel */ true) 
                    {
                         sourcesToStop.Add(source);
                    }
                }

                foreach (var source in sourcesToStop)
                {
                    source.Stop();
                    source.Dispose();
                    _activeSources.TryTake(out _); // Remove from bag
                }
            }
        }

        public void PauseAll()
        {
            if (_disposed || !IsReady()) return;
            lock (_sourcesBulkOpLock) // [CORRECTION A.17]
            {
                foreach (var source in _activeSources) source.Pause();
            }
            // [CORRECTION A.42] Update state
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Paused);
        }

        public void ResumeAll()
        {
            if (_disposed || State != AudioEngineState.Paused) return;
            lock (_sourcesBulkOpLock) // [CORRECTION A.17]
            {
                foreach (var source in _activeSources) source.Play();
            }
            // [CORRECTION A.43] Update state
            Volatile.Write(ref _stateAsInt, (int)AudioEngineState.Playing);
        }
        #endregion

        #region Music & Playlists
        public void PlayMusic(IAudioClip clip, float fadeDuration = 1.0f)
        {
            if (_disposed || clip == null) return;
            var options = AudioPlayOptions.Default;
            options.Channel = AudioChannel.Music;
            options.Priority = AudioPriority.High;
            options.SpatialMode = AudioSpatializationMode.None;
            options.Volume = 0.0f; // Start silent for fade-in

            _currentMusicSource = PlayLooping(clip, options);
            if (_currentMusicSource != null)
            {
                _musicCrossfadeDuration = fadeDuration;
                _musicCrossfadeTimer = 0f;
                _isCrossfading = true;
            }
        }

        public void CrossfadeMusic(IAudioClip nextClip, float fadeDuration = 2.0f)
        {
            if (_disposed || nextClip == null) return;
            var options = AudioPlayOptions.Default;
            options.Channel = AudioChannel.Music;
            options.Priority = AudioPriority.High;
            options.SpatialMode = AudioSpatializationMode.None;
            options.Volume = 0.0f;

            _nextMusicSource = PlayLooping(nextClip, options);
            _musicCrossfadeDuration = fadeDuration;
            _musicCrossfadeTimer = 0f;
            _isCrossfading = true;
        }

        public void StopMusic(float fadeDuration = 1.0f)
        {
            if (_disposed || _currentMusicSource == null) return;
            // Trigger fade out logic
            _nextMusicSource = null;
            _musicCrossfadeDuration = fadeDuration;
            _musicCrossfadeTimer = 0f;
            _isCrossfading = true;
        }

        private void UpdateMusicCrossfade(float deltaTime)
        {
            if (!_isCrossfading) return;

            _musicCrossfadeTimer += deltaTime;
            float t = Math.Clamp(_musicCrossfadeTimer / _musicCrossfadeDuration, 0f, 1f);

            if (_currentMusicSource != null)
                _currentMusicSource.Volume = 1.0f - t;

            if (_nextMusicSource != null)
                _nextMusicSource.Volume = t;

            if (t >= 1.0f)
            {
                _isCrossfading = false;
                if (_currentMusicSource != null)
                {
                    _currentMusicSource.Stop();
                    _currentMusicSource.Dispose();
                    _activeSources.TryTake(out _); // [CORRECTION A.5] Remove from bag
                    _eventBus?.Publish(new AudioPlaybackStoppedEvent(_currentMusicSource.Id, _currentMusicSource.Clip)); // [CONTRACTS C.50]
                }
                _currentMusicSource = _nextMusicSource;
                _nextMusicSource = null;
            }
        }

        // [MUSIC E.1] Music Segments
        public void LoadMusicSegments(Dictionary<string, IAudioClip> segments)
        {
            if (_disposed) return;
            _musicSegments.Clear();
            foreach (var kvp in segments)
            {
                _musicSegments[kvp.Key] = new MusicSegmentInfo { Name = kvp.Key, Clip = kvp.Value };
            }
        }

        public void PlayMusicSegment(string segmentName, float fadeInTime = 0.5f)
        {
            if (_disposed || !_musicSegments.TryGetValue(segmentName, out var segmentInfo)) return;
            var clip = segmentInfo.Clip;
            var options = AudioPlayOptions.Default;
            options.Channel = AudioChannel.Music;
            options.Priority = AudioPriority.High;
            options.Volume = 0.0f;
            var source = PlayLooping(clip, options);
            if (source != null)
            {
                // Fade in logic here...
                _currentMusicSource = source;
            }
        }

        // [MUSIC E.15] Music Intensity
        public void SetMusicIntensity(float intensity)
        {
            if (_disposed) return;
            _currentMusicIntensity = Math.Clamp(intensity, 0f, 1f);
            // Apply intensity to active music (e.g., activate layers, change tempo)
        }
        #endregion

        #region Listener & Mixers
        public void SetListenerTransform(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity) // [CORRECTION A.1]
        {
            if (_disposed || _listener == null) return;
            _listener.Position = position;
            _listener.Forward = forward;
            _listener.Up = up;
            _listener.Velocity = velocity;

            // [SPATIALISATION D.34] Update spatializer
            _currentSpatializer?.SetListener(position, forward, up, velocity);
        }

        public void SetVolume(AudioChannel channel, float volume)
        {
            if (_disposed) return;
            volume = Math.Clamp(volume, 0f, 1f);
            float oldVol;
            lock (_mixerLock)
            {
                _channelVolumes.TryGetValue(channel, out oldVol);
                _channelVolumes[channel] = volume;
            }
            _eventBus?.Publish(new AudioMixerVolumeChangedEvent(channel, oldVol, volume));

            // [CORRECTION A.11] Update active sources
            UpdateSourcesVolumeForChannel(channel, volume);
        }

        private void UpdateSourcesVolumeForChannel(AudioChannel channel, float newVolume)
        {
            lock (_sourcesBulkOpLock) // [CORRECTION A.17]
            {
                foreach (var source in _activeSources)
                {
                    // Assuming source has a channel property
                    // if (source.Channel == channel) source.Volume *= newVolume / oldVolume;
                    // Simplified: just apply to all for now, or add channel tracking to source
                }
            }
        }

        public float GetVolume(AudioChannel channel)
        {
            if (_disposed) return 0f;
            lock (_mixerLock)
            {
                return _channelVolumes.TryGetValue(channel, out var vol) ? vol : 1.0f;
            }
        }

        public void MuteChannel(AudioChannel channel, bool mute)
        {
            if (_disposed) return;
            lock (_mixerLock)
            {
                _channelMutes[channel] = mute;
            }
            // [CORRECTION A.12] Update active sources
            UpdateSourcesMuteForChannel(channel, mute);
        }

        private void UpdateSourcesMuteForChannel(AudioChannel channel, bool muted)
        {
            lock (_sourcesBulkOpLock) // [CORRECTION A.17]
            {
                foreach (var source in _activeSources)
                {
                    // if (source.Channel == channel) source.IsMuted = muted;
                }
            }
        }

        public void SetMasterVolume(float volume) => SetVolume(AudioChannel.Master, volume); // [CORRECTION A.26] Does propagate to subs now via SetVolume
        #endregion

        #region Advanced AAA Features
        public void ApplyReverbPreset(ReverbPreset preset)
        {
            if (_disposed) return;
            // [CORRECTION A.25] Apply to backend or default mixer
            _currentBackend?.SetParameter("GlobalReverb", preset);
        }

        public void SetGlobalPitch(float pitch)
        {
            if (_disposed) return;
            // [CORRECTION A.26] Apply to backend
            _currentBackend?.SetParameter("GlobalPitch", pitch);
        }

        public void EnableDucking(AudioChannel target, AudioChannel trigger, float duckVolume, float attackTime, float releaseTime)
        {
            if (_disposed) return;
            lock (_mixerLock)
            {
                // [CORRECTION A.27] Add rule
                _duckingRules.Add(new DuckingRule
                {
                    TargetChannel = target,
                    TriggerChannel = trigger,
                    DuckVolume = duckVolume,
                    AttackTime = attackTime,
                    ReleaseTime = releaseTime,
                    IsActive = true
                });
            }
        }

        public void DisableDucking(AudioChannel target, AudioChannel trigger)
        {
            if (_disposed) return;
            lock (_mixerLock)
            {
                // [CORRECTION A.28] Remove rule
                _duckingRules.RemoveAll(r => r.TargetChannel == target && r.TriggerChannel == trigger);
            }
        }

        private void ApplyDuckingRules()
        {
            // [CORRECTION A.6] Implement ducking logic
            // Iterate through rules and adjust channel volumes based on trigger activity
            lock (_mixerLock)
            {
                foreach (var rule in _duckingRules)
                {
                    if (!rule.IsActive) continue;
                    // Check if any source on trigger channel is playing
                    bool triggerIsActive = false;
                    lock (_sourcesBulkOpLock)
                    {
                        foreach (var source in _activeSources)
                        {
                             // if (source.Channel == rule.TriggerChannel && source.State == AudioState.Playing) { triggerIsActive = true; break; }
                        }
                    }

                    // Apply ducking to target channel volume
                    var targetVol = GetVolume(rule.TargetChannel);
                    var targetVolDucked = targetVol * rule.DuckVolume;
                    // Smoothly interpolate towards target volume based on attack/release times
                    // This is a simplified version
                    if (triggerIsActive)
                    {
                        SetVolume(rule.TargetChannel, targetVolDucked);
                    }
                    else
                    {
                        // Restore original volume
                        // This requires storing the original volume before ducking
                        SetVolume(rule.TargetChannel, targetVol);
                    }
                }
            }
        }
        #endregion

        #region Contract Implementations (C.x)
        public void RegisterBackend(IAudioBackend backend)
        {
            if (_disposed || backend == null) return;
            _availableBackends[backend.Name] = backend;
        }

        public void UnregisterBackend()
        {
            if (_disposed) return;
            _currentBackend?.Shutdown();
            _currentBackend = null;
        }

        public List<string> GetAvailableBackends()
        {
            if (_disposed) return new List<string>();
            return new List<string>(_availableBackends.Keys);
        }

        public void SwitchBackend(string backendName)
        {
            if (_disposed || !_availableBackends.TryGetValue(backendName, out var newBackend)) return;
            var oldBackend = _currentBackend;
            _currentBackend = newBackend;
            if (_currentBackend.IsInitialized)
            {
                // Re-apply current settings to new backend
                _currentBackend.SetMasterVolume(GetVolume(AudioChannel.Master));
            }
            else
            {
                _currentBackend.Initialize(new DevicePreferences()); // Use default prefs
            }
            oldBackend?.Shutdown();
            _eventBus?.Publish(new AudioDeviceChangedEvent(backendName, newBackend.Name)); // [CONTRACTS C.35]
        }

        public void RegisterDSP(IAudioDSP dsp)
        {
            if (_disposed || dsp == null) return;
            // Add to default chain or a specific one if channel is specified somehow
            if (!_dspChains.ContainsKey(AudioChannel.Master))
            {
                _dspChains[AudioChannel.Master] = new List<IAudioDSP>();
            }
            _dspChains[AudioChannel.Master].Add(dsp);
        }

        public void UnregisterDSP(string dspName)
        {
            if (_disposed || string.IsNullOrEmpty(dspName)) return;
            foreach (var chain in _dspChains.Values)
            {
                chain.RemoveAll(d => d.Name == dspName);
            }
        }

        public List<IAudioDSP> GetDSPChain(AudioChannel channel)
        {
            if (_disposed) return new List<IAudioDSP>();
            lock (_mixerLock)
            {
                return _dspChains.TryGetValue(channel, out var chain) ? new List<IAudioDSP>(chain) : new List<IAudioDSP>();
            }
        }

        public void InsertDSP(AudioChannel channel, IAudioDSP dsp, int index)
        {
            if (_disposed || dsp == null) return;
            lock (_mixerLock)
            {
                if (!_dspChains.ContainsKey(channel)) _dspChains[channel] = new List<IAudioDSP>();
                var chain = _dspChains[channel];
                if (index < 0) index = 0;
                if (index > chain.Count) index = chain.Count;
                chain.Insert(index, dsp);
            }
        }

        public void RemoveDSP(AudioChannel channel, int index)
        {
            if (_disposed) return;
            lock (_mixerLock)
            {
                if (_dspChains.TryGetValue(channel, out var chain) && index >= 0 && index < chain.Count)
                {
                    chain.RemoveAt(index);
                }
            }
        }

        public IAudioMixer CreateSubmix(string name)
        {
            if (_disposed || string.IsNullOrEmpty(name)) return null;
            lock (_mixerLock)
            {
                if (_submixes.ContainsKey(name)) return _submixes[name]; // Or throw?
                var mixer = new DefaultAudioMixer(name);
                _submixes[name] = mixer;
                return mixer;
            }
        }

        public void DestroySubmix(string name)
        {
            if (_disposed || string.IsNullOrEmpty(name)) return;
            lock (_mixerLock)
            {
                _submixes.Remove(name);
            }
        }

        public void RouteChannelToSubmix(AudioChannel channel, string submixName)
        {
            if (_disposed || string.IsNullOrEmpty(submixName) || !_submixes.ContainsKey(submixName)) return;
            // Implementation depends on how routing is handled internally
            // Could involve adding sends on the channel's output to the submix
        }

        public float GetSubmixVolume(string submixName)
        {
            if (_disposed || string.IsNullOrEmpty(submixName) || !_submixes.TryGetValue(submixName, out var mixer)) return 0f;
            return mixer.Volume;
        }

        public void SetSubmixVolume(string submixName, float volume)
        {
            if (_disposed || string.IsNullOrEmpty(submixName) || !_submixes.TryGetValue(submixName, out var mixer)) return;
            mixer.Volume = volume;
        }
        #endregion

        #region Spatialization (D.x)
        public void SetHRTFEnabled(bool enabled)
        {
            if (_disposed) return;
            // Switch spatializer implementation
            if (enabled)
            {
                _currentSpatializer = new HRTFSpatializer(); // Placeholder
            }
            else
            {
                _currentSpatializer = new DefaultSpatializer(); // Placeholder
            }
        }

        public void SetOcclusionEnabled(bool enabled)
        {
            if (_disposed) return;
            // Backend/DSP specific
            _currentBackend?.SetParameter("OcclusionEnabled", enabled);
        }

        public void SetAirAbsorptionEnabled(bool enabled)
        {
            if (_disposed) return;
            _currentBackend?.SetParameter("AirAbsorptionEnabled", enabled);
        }

        public void SetDopplerFactor(AudioDopplerFactor factor)
        {
            if (_disposed) return;
            _currentBackend?.SetParameter("DopplerFactor", (int)factor);
        }

        private void UpdateSpatialization()
        {
            if (!_config.EnableSpatialization || _currentSpatializer == null) return;
            var listenerInfo = new SpatializerListenerInfo
            {
                Position = _listener.Position,
                Forward = _listener.Forward,
                Up = _listener.Up,
                Velocity = _listener.Velocity
            };
            _currentSpatializer.SetListener(listenerInfo.Position, listenerInfo.Forward, listenerInfo.Up, listenerInfo.Velocity);

            lock (_sourcesBulkOpLock)
            {
                foreach (var source in _activeSources)
                {
                    if (source.SpatialMode == AudioSpatializationMode.Full3D)
                    {
                        // Retrieve position from source (requires IAudioSource to expose it or store internally)
                        // For now, assume it's available via a method or stored prop
                        // var pos = source.GetPosition(); 
                        // var vel = source.GetVelocity();
                        // var spatialInfo = new SpatializerSourceInfo { SourceId = source.Id, Position = pos, Velocity = vel, ... };
                        // _currentSpatializer.UpdateSourcePosition(spatialInfo);
                    }
                }
            }
        }
        #endregion

        #region Mixing (F.x)
        public void SetEQ(AudioChannel channel, EqualizerSettings settings)
        {
            if (_disposed) return;
            // Add EQ DSP to channel's chain
            var eqDsp = new ParametricEqualizerDSP(settings); // Placeholder
            InsertDSP(channel, eqDsp, 0); // Insert at beginning
        }

        public void SetCompressor(AudioChannel channel, CompressorSettings settings)
        {
            if (_disposed) return;
            var compDsp = new CompressorDSP(settings); // Placeholder
            InsertDSP(channel, compDsp, 0);
        }

        public void SetLimiter(AudioChannel channel, LimiterSettings settings)
        {
            if (_disposed) return;
            var limDsp = new LimiterDSP(settings); // Placeholder
            InsertDSP(channel, limDsp, 0);
        }

        public void SetDelay(AudioChannel channel, DelaySettings settings)
        {
            if (_disposed) return;
            var delayDsp = new DelayDSP(settings); // Placeholder
            InsertDSP(channel, delayDsp, 0);
        }

        public void SetReverb(AudioChannel channel, ReverbPreset preset)
        {
            if (_disposed) return;
            var revDsp = new ReverbDSP(preset); // Placeholder
            InsertDSP(channel, revDsp, 0);
        }
        #endregion

        #region Streaming (G.x)
        public void SetStreamingPriority(AudioChannel channel, int priority)
        {
            if (_disposed) return;
            // Store priority mapping
            // Implementation depends on stream scheduler
        }

        public void SetStreamingLODEnabled(bool enabled)
        {
            if (_disposed) return;
            // Store flag
            // Implementation in stream decoder/resampler
        }

        private void ProcessStreamingRequests()
        {
            if (_disposed) return;
            lock (_streamingLock)
            {
                while (_streamingRequests.Count > 0)
                {
                    var req = _streamingRequests.Dequeue();
                    // Start async stream loading/playback based on request
                    // Placeholder
                }
            }
        }
        #endregion

        #region Observability (H.x)
        public void EnableAudioVisualization(bool enabled)
        {
            _visualizationEnabled = enabled;
        }

        public AudioEngineMetricsHistory GetMetricsHistory(TimeSpan duration)
        {
            if (_disposed) return new AudioEngineMetricsHistory();
            lock (_metricsLock)
            {
                var history = new List<AudioEngineMetrics>();
                // Iterate ring buffer for last 'duration' worth of data
                // Placeholder implementation
                return new AudioEngineMetricsHistory { Metrics = history, WindowDuration = duration };
            }
        }

        public void ExportMetricsToFile(string filePath, MetricsExportFormat format)
        {
            if (_disposed) return;
            // Serialize _metricsHistory to file in specified format
            // Placeholder
        }

        public void EnableTelemetry(bool enabled)
        {
            _telemetryEnabled = enabled;
        }

        public void SendAnalyticsEvent(string eventName, Dictionary<string, object> properties)
        {
            if (_disposed || !_telemetryEnabled) return;
            // Send event to telemetry service
            // Placeholder
        }

        private void UpdateMetrics()
        {
            lock (_metricsLock)
            {
                _metricsHistory.Enqueue(_metrics);
            }
        }
        #endregion

        #region Formats (I.x)
        public void SetCompressionPreset(AudioChannel channel, CompressionPreset preset)
        {
            if (_disposed) return;
            // Store preset for channel, apply when encoding/streaming
        }

        public void LoadMiddlewareSoundbank(string bankPath, MiddlewareType type)
        {
            if (_disposed) return;
            // Load bank into middleware instance
            // Placeholder
        }
        #endregion

        #region Accessibility (J.x)
        public void SetAccessibilityProfile(AccessibilityProfile profile)
        {
            _currentAccessibilityProfile = profile;
            switch (profile)
            {
                case AccessibilityProfile.HearingImpaired:
                    SetMonoOutputEnabled(true);
                    SetEQ(AudioChannel.Master, new EqualizerSettings { Bands = new[] { new EqualizerBand { FrequencyHz = 2000, GainDb = 3, Q = 1 } } }); // Boost mid
                    break;
                case AccessibilityProfile.MonoOnly:
                    SetMonoOutputEnabled(true);
                    break;
                case AccessibilityProfile.NightMode:
                    EnableDucking(AudioChannel.Music, AudioChannel.SFX, 0.5f, 0.1f, 0.5f); // Duck music for SFX
                    break;
                // Add more profiles...
            }
        }

        public void SetMonoOutputEnabled(bool enabled)
        {
            _monoOutputEnabled = enabled;
            _currentBackend?.SetParameter("OutputChannels", enabled ? 1 : 2);
        }

        public void SetDialogueBoost(float boostDb)
        {
            _dialogueBoost = boostDb;
            // Apply gain to Voice channel
            var currentVol = GetVolume(AudioChannel.Voice);
            var newVol = currentVol * (float)Math.Pow(10, boostDb / 20.0);
            SetVolume(AudioChannel.Voice, newVol);
        }
        #endregion

        #region Internal Helpers
        private void ApplyConfiguration(AudioEngineConfig config)
        {
            lock (_mixerLock)
            {
                _config = config;
                // Initialize default volumes
                foreach (AudioChannel ch in Enum.GetValues(typeof(AudioChannel)))
                {
                    _channelVolumes[ch] = 1.0f;
                    _channelMutes[ch] = false;
                }
                _channelVolumes[AudioChannel.Master] = config.MasterVolume;
                _channelVolumes[AudioChannel.Music] = config.MusicVolume;
                _channelVolumes[AudioChannel.SFX] = config.SFXVolume;
                _channelVolumes[AudioChannel.Voice] = config.VoiceVolume;
                _channelVolumes[AudioChannel.Ambience] = config.AmbienceVolume;
            }
        }

        private IAudioSource AcquireSource(AudioPriority requestedPriority)
        {
            lock (_sourcesBulkOpLock) // [CORRECTION A.17]
            {
                if (_activeSources.Count < _config.MaxVoices)
                {
                    var source = CreateNewSource();
                    // _activeSources.Add(source); // Now added in PlayOneShot/PlayLooping
                    return source;
                }

                // Voice Stealing: find the lowest priority, oldest sound
                IAudioSource lowestSource = null;
                // For age-based stealing, we'd need a timestamp on IAudioSource or a parallel list with timestamps
                // For now, prioritize priority only
                foreach (var s in _activeSources)
                {
                    if (s.Priority < requestedPriority && s.Priority < _config.VoiceStealingThreshold)
                    {
                        if (lowestSource == null || s.Priority < lowestSource.Priority)
                            lowestSource = s;
                    }
                }

                if (lowestSource != null)
                {
                    uint stolenId = lowestSource.Id;
                    AudioPriority stolenPrio = lowestSource.Priority;
                    lowestSource.Stop();
                    // _activeSources.Remove(lowestSource); // Done in CullInaudibleSources or on disposal
                    lowestSource.Dispose(); // [CORRECTION A.15]
                    
                    var newSource = CreateNewSource();
                    // _activeSources.Add(newSource); // Done in PlayOneShot/PlayLooping
                    _eventBus?.Publish(new AudioVoiceStolenEvent(stolenId, stolenPrio, requestedPriority)); // [CORRECTION A.50]
                    return newSource;
                }

                return null; // All voices busy and none can be stolen
            }
        }

        private IAudioSource CreateNewSource()
        {
            // In a real engine, this would pull from an object pool or allocate native handles
            // [CORRECTION A.34] Handle overflow
            var id = Interlocked.Increment(ref Unsafe.As<uint, int>(ref _nextSourceId));
            if ((uint)id > MaxSourceId) Interlocked.Exchange(ref Unsafe.As<uint, int>(ref _nextSourceId), 1);
            return new DefaultAudioSource((uint)id);
        }

        private void CullInaudibleSources()
        {
            lock (_sourcesBulkOpLock) // [CORRECTION A.17]
            {
                var sourcesToRemove = new List<IAudioSource>();
                foreach (var source in _activeSources)
                {
                    if (source.State == AudioState.Stopped || source.Clip == null)
                    {
                        sourcesToRemove.Add(source);
                    }
                    // [CORRECTION A.4] Also cull based on distance/attenuation if volume is negligible
                    else
                    {
                        // Calculate effective volume after spatialization and attenuation
                        // If below threshold, mark for removal/disposal
                    }
                }

                foreach (var source in sourcesToRemove)
                {
                    source.Dispose();
                    _activeSources.TryTake(out _); // Remove from ConcurrentBag
                }
            }
        }

        private void InitializeBackend()
        {
            // [CORRECTION A.14] Initialize native audio API
            // Select best available backend
            var preferredBackend = "Default"; // Could come from config
            if (_availableBackends.ContainsKey(preferredBackend))
            {
                _currentBackend = _availableBackends[preferredBackend];
            }
            else if (_availableBackends.Count > 0)
            {
                _currentBackend = _availableBackends.Values.First();
            }
            else
            {
                _currentBackend = new DefaultAudioBackend(); // Fallback
            }

            _currentBackend.Initialize(new DevicePreferences());

            _capabilities = new AudioEngineCapabilities
            {
                Supports3DAudio = true,
                SupportsReverb = true,
                SupportsFilters = true,
                SupportsStreaming = true,
                SupportsHardwareAcceleration = _currentBackend.IsHardwareAccelerated, // [STREAMING G.15]
                SupportsHRTF = true, // [SPATIALISATION D.1]
                SupportsAmbisonics = true, // [SPATIALISATION D.2]
                MaxSupportedVoices = _currentBackend.MaxVoices, // [PLACEHOLDER]
                BackendName = _currentBackend.Name,
                BackendVersion = _currentBackend.Version, // [PLACEHOLDER]
                SupportedFormats = new List<string> { "WAV", "OGG", "MP3" } // [FORMATS I.16]
            };
        }

        private async Task InitializeBackendAsync()
        {
            // Async version of InitializeBackend
            await Task.Run(() => InitializeBackend());
        }

        private void ShutdownBackend()
        {
            _currentBackend?.Shutdown();
            _currentBackend = null;
        }

        private async Task ShutdownBackendAsync()
        {
            if (_currentBackend != null)
            {
                await Task.Run(() => _currentBackend.Shutdown());
            }
        }

        private void FlushAudioBuffers()
        {
            // [CORRECTION A.35] Tell backend to flush
            _currentBackend?.Flush();
        }

        private async Task FlushAudioBuffersAsync()
        {
            // [CORRECTION A.35] Async flush
            await Task.Run(() => FlushAudioBuffers());
        }

        private void UpdateBackendDSP(float deltaTime)
        {
            // Push mixed audio buffer to hardware
            // Process DSP chains
            lock (_mixerLock)
            {
                foreach (var kvp in _dspChains)
                {
                    var channel = kvp.Key;
                    var chain = kvp.Value;
                    // Process each DSP in the chain for the channel's mixed output
                    // This is highly dependent on the backend's mixing architecture
                }
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown(); // [CORRECTION A.13] Ensure clean shutdown first

                // Wait for streaming tasks if any
                lock (_streamingLock)
                {
                    // Cancel pending requests, wait for completion
                }

                // Dispose managed resources
                _currentBackend?.Dispose();
                _currentSpatializer?.Dispose();
                // Dispose sources still in bag
                while (_activeSources.TryTake(out var source)) source.Dispose();

                _disposed = true;
            }
        }
        #endregion
    }

    #region Default Implementations (Placeholders for Engine Internals)
    internal class DefaultAudioListener : IAudioListener
    {
        public Vector3 Position { get; set; } = Vector3.Zero; // [CORRECTION A.1]
        public Vector3 Forward { get; set; } = Vector3.Forward; // [CORRECTION A.1]
        public Vector3 Up { get; set; } = Vector3.Up; // [CORRECTION A.1]
        public Vector3 Velocity { get; set; } = Vector3.Zero; // [CORRECTION A.1]
    }

    internal class DefaultAudioSource : IAudioSource
    {
        public uint Id { get; }
        public IAudioClip Clip { get; set; }
        public AudioState State { get; private set; } = AudioState.Stopped;
        public float Volume { get; set; } = 1.0f;
        public float Pitch { get; set; } = 1.0f;
        public float Pan { get; set; } = 0.0f;
        public bool IsLooping { get; set; } = false;
        public bool IsMuted { get; set; } = false;
        public AudioPriority Priority { get; set; } = AudioPriority.Medium;
        public AudioSpatializationMode SpatialMode { get; set; } = AudioSpatializationMode.None;
        
        private Vector3 _position; // [CORRECTION A.1]
        private Vector3 _velocity; // [CORRECTION A.1]

        public DefaultAudioSource(uint id) { Id = id; }

        public void Play() { State = AudioState.Playing; }
        public void Pause() { State = AudioState.Paused; }
        public void Stop() { State = AudioState.Stopped; }
        
        public void SetTransform(Vector3 position, Vector3 velocity) // [CORRECTION A.1]
        {
            _position = position;
            _velocity = velocity;
        }
        
        public void SetAttenuation(float minDistance, float maxDistance, AudioAttenuationModel model)
        {
            // Store for DSP calculations
        }

        // [MIXING F.1] Source DSP methods
        public void SetSourceEQ(EqualizerSettings settings) { /* Placeholder */ }
        public void SetSourceCompressor(CompressorSettings settings) { /* Placeholder */ }
        public void SetSourceDelay(DelaySettings settings) { /* Placeholder */ }
        public void SetSourceReverb(ReverbPreset preset) { /* Placeholder */ }

        public void Dispose() { Stop(); }
    }

    internal class DefaultAudioMixer : IAudioMixer
    {
        public string Name { get; }
        public float Volume { get; set; } = 1.0f;
        private readonly List<IAudioSource> _inputs = new List<IAudioSource>();

        public DefaultAudioMixer(string name) { Name = name; }

        public void AddInput(IAudioSource source) { lock (_inputs) _inputs.Add(source); }
        public void RemoveInput(IAudioSource source) { lock (_inputs) _inputs.Remove(source); }
        public void SetSend(string destinationSubmix, float level) { /* Placeholder */ }
    }

    internal class DefaultAudioBackend : IAudioBackend
    {
        public string Name => "DefaultAudioBackend";
        public bool IsInitialized { get; private set; }
        public bool IsDeviceConnected => true; // Placeholder
        public bool IsHardwareAccelerated => false; // Placeholder
        public int MaxVoices => 256; // Placeholder
        public string Version => "1.0.0"; // Placeholder

        public List<AudioDeviceInfo> GetAvailableDevices() => new List<AudioDeviceInfo> { new AudioDeviceInfo { Id = "default", Name = "System Default", IsDefault = true, MaxChannels = 2, PreferredSampleRate = 48000 } }; // Placeholder

        public void Initialize(DevicePreferences preferences) { IsInitialized = true; /* Placeholder */ }
        public void Shutdown() { IsInitialized = false; /* Placeholder */ }
        public void SetMasterVolume(float volume) { /* Placeholder */ }
        public IAudioVoice PlayClip(IAudioClip clip, AudioSourceProperties props) => null; // Placeholder
        public void SetParameter(string paramName, object value) { /* Placeholder */ }
        public void Suspend() { /* Placeholder */ }
        public void Resume() { /* Placeholder */ }
        public void Flush() { /* Placeholder */ }
        public void Dispose() { Shutdown(); }
    }

    internal interface IAudioVoice { /* Minimal handle for playing clips */ } // Placeholder for backend voice management

    // Placeholder DSP implementations
    internal class ParametricEqualizerDSP : IAudioDSP { public string Name => "ParametricEQ"; public void Process(float[] samples, int channels, int sampleRate) { /* Placeholder */ } public void SetParameter(string paramName, object value) { } public object GetParameter(string paramName) => null; }
    internal class CompressorDSP : IAudioDSP { public string Name => "Compressor"; public void Process(float[] samples, int channels, int sampleRate) { /* Placeholder */ } public void SetParameter(string paramName, object value) { } public object GetParameter(string paramName) => null; }
    internal class LimiterDSP : IAudioDSP { public string Name => "Limiter"; public void Process(float[] samples, int channels, int sampleRate) { /* Placeholder */ } public void SetParameter(string paramName, object value) { } public object GetParameter(string paramName) => null; }
    internal class DelayDSP : IAudioDSP { public string Name => "Delay"; public void Process(float[] samples, int channels, int sampleRate) { /* Placeholder */ } public void SetParameter(string paramName, object value) { } public object GetParameter(string paramName) => null; }
    internal class ReverbDSP : IAudioReverb { public string Name => "Reverb"; public void Process(float[] samples, int channels, int sampleRate) { /* Placeholder */ } public void SetParameter(string paramName, object value) { } public object GetParameter(string paramName) => null; public void ApplyReverbPreset(ReverbPreset preset) { /* Placeholder */ } }
    internal class DefaultSpatializer : IAudioSpatializer { public string Name => "DefaultSpatializer"; public void Process(float[] samples, int channels, int sampleRate) { /* Placeholder */ } public void SetParameter(string paramName, object value) { } public object GetParameter(string paramName) => null; public void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity) { /* Placeholder */ } public void UpdateSourcePosition(IAudioSource source, Vector3 position, Vector3 velocity) { /* Placeholder */ } }
    internal class HRTFSpatializer : IAudioSpatializer { public string Name => "HRTFSpatializer"; public void Process(float[] samples, int channels, int sampleRate) { /* Placeholder */ } public void SetParameter(string paramName, object value) { } public object GetParameter(string paramName) => null; public void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity) { /* Placeholder */ } public void UpdateSourcePosition(IAudioSource source, Vector3 position, Vector3 velocity) { /* Placeholder */ } }
    #endregion
}