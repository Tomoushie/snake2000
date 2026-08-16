// /Engine/Rendering/IRenderEngine.cs
//
// Contrat générique du moteur de rendu.
// Ce fichier définit les interfaces, structures et événements nécessaires
// pour un moteur de rendu AAA : pipeline, batching, culling, post-process,
// shaders, LOD, instancing, effets VFX, feedback UI, integration GPU profiling,
// jobs parallèles, profiling et intégration EventBus.
//
// Règles :
// - Aucune logique spécifique à Snake2000 ici.
// - Ce fichier appartient uniquement à /Engine/Rendering.
// - Les composants ECS devront consommer ces contrats via services/interfaces.
// - Les interactions runtime passent par EventBus, services et messages.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// Ce fichier visait a l'origine une arborescence Engine.Events /
// Engine.Profiling / Engine.Utilities / Engine.Mathematics qui n'a jamais ete
// construite. Les directives pointent desormais vers ce qui existe :
//   Mathematics -> System.Numerics + System.Drawing
//   Events / Profiling / Utilities -> Snake2000.Engine.Core (EventBus,
//   Profiler, ResourceManager)
using System.Drawing;
using System.Numerics;
using Engine.Core;
using Engine.Jobsystem;
using Snake2000.Engine.Core;

// Vector2 existe dans System.Numerics ET dans Snake2000.Engine.Core : sans cet
// alias, chaque usage serait ambigu (CS0104). Le rendu travaille en flottants,
// donc c'est la version de System.Numerics qui fait foi ici.
using Vector2 = System.Numerics.Vector2;
using Rect = System.Drawing.RectangleF;

namespace Engine.Rendering
{
    #region Enums

    public enum RenderEngineState
    {
        Uninitialized,
        Initializing,
        Ready,
        Running,
        Paused,
        Degraded,
        Recovering,
        Error,
        ShuttingDown,
        Shutdown
    }

    public enum RenderQuality
    {
        Ultra,
        High,
        Medium,
        Low,
        Custom
    }

    public enum RenderResolution
    {
        Native,
        Half,
        Quarter,
        Eighth,
        Custom
    }

    public enum ShadowQuality
    {
        Off,
        Low,
        Medium,
        High,
        Ultra
    }

    public enum AntiAliasing
    {
        Off,
        FXAA,
        TAA,
        MSAA2x,
        MSAA4x,
        MSAA8x
    }

    public enum AnisotropicFiltering
    {
        Disabled,
        x2,
        x4,
        x8,
        x16
    }

    public enum LightingModel
    {
        Legacy,
        Deferred,
        ForwardPlus,
        RayTraced,
        Hybrid
    }

    public enum CullingMode
    {
        Frustum,
        Occlusion,
        Distance,
        Layer,
        Combined
    }

    public enum BatchMode
    {
        Static,
        Dynamic,
        Instanced,
        Skeletal
    }

    public enum ShaderType
    {
        Vertex,
        Fragment,
        Geometry,
        Hull,
        Domain,
        Compute,
        Amplification,
        Mesh
    }

    public enum MaterialPropertyType
    {
        Texture2D,
        TextureCube,
        Texture3D,
        Vector4,
        Vector3,
        Vector2,
        Float,
        Color,
        Int,
        Bool
    }

    public enum RenderTextureFormat
    {
        RGBA32,
        RGB24,
        ARGB32,
        Depth,
        ShadowMap,
        HDR,
        RG16,
        R8,
        R16
    }

    public enum BlendMode
    {
        Opaque,
        Cutout,
        Fade,
        Transparent,
        Additive,
        Multiply
    }

    public enum DepthWrite
    {
        On,
        Off
    }

    public enum ZTest
    {
        Less,
        Greater,
        LEqual,
        GEqual,
        Equal,
        NotEqual,
        Always
    }

    public enum CullMode
    {
        Back,
        Front,
        Off
    }

    public enum CompareFunction
    {
        Disabled,
        Never,
        Less,
        Equal,
        LEqual,
        Greater,
        NotEqual,
        GEqual,
        Always
    }

    public enum StencilOperation
    {
        Keep,
        Zero,
        Replace,
        IncrementSaturate,
        DecrementSaturate,
        Invert,
        IncrementWrap,
        DecrementWrap
    }

    public enum LightType
    {
        Directional,
        Point,
        Spot,
        Area,
        Capsule
    }

    public enum FogMode
    {
        None,
        Linear,
        Exponential,
        ExponentialSquared
    }

    public enum PostProcessEffect
    {
        AmbientOcclusion,
        ScreenSpaceReflections,
        Bloom,
        ChromaticAberration,
        Vignette,
        Grain,
        ColorGrading,
        MotionBlur,
        DepthOfField,
        Distortion,
        FilmGrain,
        LensDirt,
        LensFlare,
        HitstopFlash,
        ScreenShakeDistortion
    }

    public enum VFXType
    {
        ParticleSystem,
        Trail,
        LineRenderer,
        Billboard,
        Decal,
        LightProbe
    }

    public enum ParticleSimulationSpace
    {
        Local,
        World,
        Custom
    }

    public enum ParticleScalingMode
    {
        Local,
        Global,
        Hierarchy
    }

    public enum ParticleSortMode
    {
        None,
        OldestInFront,
        YoungestInFront
    }

    public enum ParticleStopAction
    {
        None,
        Disable,
        Destroy,
        Callback
    }

    public enum LODLevel
    {
        Level0, // Highest detail
        Level1,
        Level2,
        Level3,
        Level4, // Lowest detail
        Custom
    }

    public enum LODStrategy
    {
        DistanceBased,
        PerformanceBased,
        Manual
    }

    // [CORRECTION] Remplacement de la structure par un enum [Flags]
    [Flags]
    public enum RenderLayerMask : uint
    {
        Default = 1 << 0,
        Transparent = 1 << 1,
        UI = 1 << 2,
        // ... jusqu'à 32 couches possibles
    }

    public enum CameraClearFlags
    {
        Skybox,
        Color,
        Depth,
        Nothing
    }

    public enum RenderPassType
    {
        Opaque,
        Transparent,
        Overlay,
        UI
    }

    public enum ShaderKeyword
    {
        // Defined by the shader compiler or engine
        // Example: LIGHTMAP_ON, DYNAMICLIGHTMAP_ON, DIRLIGHTMAP_COMBINED, etc.
        CUSTOM_KEYWORD_1,
        CUSTOM_KEYWORD_2
    }

    // Nouveaux enums pour les corrections structurelles
    public enum FilterMode
    {
        Point,
        Bilinear,
        Trilinear
    }

    public enum TextureWrapMode
    {
        Repeat,
        Clamp,
        Mirror,
        MirrorOnce
    }

    public enum RenderSubsystemType
    {
        Device,
        Backend,
        Resource,
        Pipeline,
        Culling,
        Rendering,
        // [CORRECTION] Ajouts
        Streaming,
        PostProcess,
        VFX,
        Debug,
        Lighting,
        Shadows,
        UI,
        Capture,
        Accessibility
    }

    [Flags]
    public enum RenderFeatureFlags
    {
        None = 0,
        Shadows = 1 << 0,
        PostProcessing = 1 << 1,
        DynamicBatching = 1 << 2,
        Instancing = 1 << 3,
        OcclusionCulling = 1 << 4,
        VirtualTexturing = 1 << 5,
        RayTracing = 1 << 6,
        MotionBlur = 1 << 7,
        ScreenShake = 1 << 8,
        FeedbackEffects = 1 << 9,
        GPUSkinning = 1 << 10,
        Upscaling = 1 << 11,
        Denoiser = 1 << 12,
        MotionVectors = 1 << 13,
        VirtualGeometry = 1 << 14,
        VirtualShadowMaps = 1 << 15,
        RayTracedShadows = 1 << 16,
        RayTracedReflections = 1 << 17,
        RayTracedGI = 1 << 18,
        ScreenSpaceShadows = 1 << 19,
        ContactShadows = 1 << 20,
        VolumetricLighting = 1 << 21,
        VariableRateShading = 1 << 22,
        MeshShaders = 1 << 23,
        AsyncCompute = 1 << 24,
        DirectStorage = 1 << 25,
        DeterministicRendering = 1 << 26,
        // Ajoutez d'autres features ici
    }

    #endregion

    #region Handles

    // [CORRECTION] Interface commune
    public interface IRenderHandle
    {
        uint Id { get; }
        bool IsValid { get; }
    }

    // [CORRECTION] Type générique
    public readonly struct RenderResourceHandle<T> : IRenderHandle
    {
        public uint Id { get; }
        public RenderResourceHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(RenderResourceHandle<T> other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RenderResourceHandle<T> other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(RenderResourceHandle<T> left, RenderResourceHandle<T> right) => left.Equals(right);
        public static bool operator !=(RenderResourceHandle<T> left, RenderResourceHandle<T> right) => !left.Equals(right);
        public override string ToString() => $"RenderResourceHandle<{typeof(T).Name}>({Id})";
    }

    public readonly struct RenderTextureHandle : IRenderHandle
    {
        public uint Id { get; }

        public RenderTextureHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(RenderTextureHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RenderTextureHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(RenderTextureHandle left, RenderTextureHandle right) => left.Equals(right);
        public static bool operator !=(RenderTextureHandle left, RenderTextureHandle right) => !left.Equals(right);

        public override string ToString() => $"RenderTextureHandle({Id})";
    }

    public readonly struct MaterialHandle : IRenderHandle
    {
        public uint Id { get; }

        public MaterialHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(MaterialHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is MaterialHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(MaterialHandle left, MaterialHandle right) => left.Equals(right);
        public static bool operator !=(MaterialHandle left, MaterialHandle right) => !left.Equals(right);

        public override string ToString() => $"MaterialHandle({Id})";
    }

    public readonly struct ShaderHandle : IRenderHandle
    {
        public uint Id { get; }

        public ShaderHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(ShaderHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ShaderHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(ShaderHandle left, ShaderHandle right) => left.Equals(right);
        public static bool operator !=(ShaderHandle left, ShaderHandle right) => !left.Equals(right);

        public override string ToString() => $"ShaderHandle({Id})";
    }

    public readonly struct MeshHandle : IRenderHandle
    {
        public uint Id { get; }

        public MeshHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(MeshHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is MeshHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(MeshHandle left, MeshHandle right) => left.Equals(right);
        public static bool operator !=(MeshHandle left, MeshHandle right) => !left.Equals(right);

        public override string ToString() => $"MeshHandle({Id})";
    }

    public readonly struct TextureHandle : IRenderHandle
    {
        public uint Id { get; }

        public TextureHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(TextureHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is TextureHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(TextureHandle left, TextureHandle right) => left.Equals(right);
        public static bool operator !=(TextureHandle left, TextureHandle right) => !left.Equals(right);

        public override string ToString() => $"TextureHandle({Id})";
    }

    public readonly struct LightHandle : IRenderHandle
    {
        public uint Id { get; }

        public LightHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(LightHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is LightHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(LightHandle left, LightHandle right) => left.Equals(right);
        public static bool operator !=(LightHandle left, LightHandle right) => !left.Equals(right);

        public override string ToString() => $"LightHandle({Id})";
    }

    public readonly struct CameraHandle : IRenderHandle
    {
        public uint Id { get; }

        public CameraHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(CameraHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is CameraHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(CameraHandle left, CameraHandle right) => left.Equals(right);
        public static bool operator !=(CameraHandle left, CameraHandle right) => !left.Equals(right);

        public override string ToString() => $"CameraHandle({Id})";
    }

    public readonly struct VFXHandle : IRenderHandle
    {
        public uint Id { get; }

        public VFXHandle(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public bool Equals(VFXHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is VFXHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(VFXHandle left, VFXHandle right) => left.Equals(right);
        public static bool operator !=(VFXHandle left, VFXHandle right) => !left.Equals(right);

        public override string ToString() => $"VFXHandle({Id})";
    }

    // [CORRECTION] Nouveaux handles
    public readonly struct VertexBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public VertexBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(VertexBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is VertexBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(VertexBufferHandle left, VertexBufferHandle right) => left.Equals(right);
        public static bool operator !=(VertexBufferHandle left, VertexBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"VertexBufferHandle({Id})";
    }

    public readonly struct IndexBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public IndexBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(IndexBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is IndexBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(IndexBufferHandle left, IndexBufferHandle right) => left.Equals(right);
        public static bool operator !=(IndexBufferHandle left, IndexBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"IndexBufferHandle({Id})";
    }

    public readonly struct SkeletonPoseHandle : IRenderHandle
    {
        public uint Id { get; }
        public SkeletonPoseHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(SkeletonPoseHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is SkeletonPoseHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(SkeletonPoseHandle left, SkeletonPoseHandle right) => left.Equals(right);
        public static bool operator !=(SkeletonPoseHandle left, SkeletonPoseHandle right) => !left.Equals(right);
        public override string ToString() => $"SkeletonPoseHandle({Id})";
    }

    public readonly struct RenderInstanceHandle : IRenderHandle
    {
        public uint Id { get; }
        public RenderInstanceHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(RenderInstanceHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RenderInstanceHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(RenderInstanceHandle left, RenderInstanceHandle right) => left.Equals(right);
        public static bool operator !=(RenderInstanceHandle left, RenderInstanceHandle right) => !left.Equals(right);
        public override string ToString() => $"RenderInstanceHandle({Id})";
    }

    // [CORRECTION] Nouveaux handles pour Render Graph
    public readonly struct RenderPassHandle : IRenderHandle
    {
        public uint Id { get; }
        public RenderPassHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(RenderPassHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RenderPassHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(RenderPassHandle left, RenderPassHandle right) => left.Equals(right);
        public static bool operator !=(RenderPassHandle left, RenderPassHandle right) => !left.Equals(right);
        public override string ToString() => $"RenderPassHandle({Id})";
    }

    public readonly struct RenderGraphResourceHandle : IRenderHandle
    {
        public uint Id { get; }
        public RenderGraphResourceHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(RenderGraphResourceHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RenderGraphResourceHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(RenderGraphResourceHandle left, RenderGraphResourceHandle right) => left.Equals(right);
        public static bool operator !=(RenderGraphResourceHandle left, RenderGraphResourceHandle right) => !left.Equals(right);
        public override string ToString() => $"RenderGraphResourceHandle({Id})";
    }

    public readonly struct RenderGraphBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public RenderGraphBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(RenderGraphBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RenderGraphBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(RenderGraphBufferHandle left, RenderGraphBufferHandle right) => left.Equals(right);
        public static bool operator !=(RenderGraphBufferHandle left, RenderGraphBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"RenderGraphBufferHandle({Id})";
    }

    public readonly struct RenderGraphTextureHandle : IRenderHandle
    {
        public uint Id { get; }
        public RenderGraphTextureHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(RenderGraphTextureHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RenderGraphTextureHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(RenderGraphTextureHandle left, RenderGraphTextureHandle right) => left.Equals(right);
        public static bool operator !=(RenderGraphTextureHandle left, RenderGraphTextureHandle right) => !left.Equals(right);
        public override string ToString() => $"RenderGraphTextureHandle({Id})";
    }

    // [CORRECTION] Nouveaux handles pour GPU Resources
    public readonly struct GPUBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public GPUBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(GPUBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GPUBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(GPUBufferHandle left, GPUBufferHandle right) => left.Equals(right);
        public static bool operator !=(GPUBufferHandle left, GPUBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"GPUBufferHandle({Id})";
    }

    public readonly struct StructuredBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public StructuredBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(StructuredBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is StructuredBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(StructuredBufferHandle left, StructuredBufferHandle right) => left.Equals(right);
        public static bool operator !=(StructuredBufferHandle left, StructuredBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"StructuredBufferHandle({Id})";
    }

    public readonly struct ConstantBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public ConstantBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(ConstantBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ConstantBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(ConstantBufferHandle left, ConstantBufferHandle right) => left.Equals(right);
        public static bool operator !=(ConstantBufferHandle left, ConstantBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"ConstantBufferHandle({Id})";
    }

    public readonly struct IndirectArgumentBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public IndirectArgumentBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(IndirectArgumentBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is IndirectArgumentBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(IndirectArgumentBufferHandle left, IndirectArgumentBufferHandle right) => left.Equals(right);
        public static bool operator !=(IndirectArgumentBufferHandle left, IndirectArgumentBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"IndirectArgumentBufferHandle({Id})";
    }

    public readonly struct GPUQueryHandle : IRenderHandle
    {
        public uint Id { get; }
        public GPUQueryHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(GPUQueryHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GPUQueryHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(GPUQueryHandle left, GPUQueryHandle right) => left.Equals(right);
        public static bool operator !=(GPUQueryHandle left, GPUQueryHandle right) => !left.Equals(right);
        public override string ToString() => $"GPUQueryHandle({Id})";
    }

    public readonly struct GPUFenceHandle : IRenderHandle
    {
        public uint Id { get; }
        public GPUFenceHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(GPUFenceHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GPUFenceHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(GPUFenceHandle left, GPUFenceHandle right) => left.Equals(right);
        public static bool operator !=(GPUFenceHandle left, GPUFenceHandle right) => !left.Equals(right);
        public override string ToString() => $"GPUFenceHandle({Id})";
    }

    public readonly struct TimelineSemaphoreHandle : IRenderHandle
    {
        public uint Id { get; }
        public TimelineSemaphoreHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(TimelineSemaphoreHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is TimelineSemaphoreHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(TimelineSemaphoreHandle left, TimelineSemaphoreHandle right) => left.Equals(right);
        public static bool operator !=(TimelineSemaphoreHandle left, TimelineSemaphoreHandle right) => !left.Equals(right);
        public override string ToString() => $"TimelineSemaphoreHandle({Id})";
    }

    public readonly struct ShaderVariantHandle : IRenderHandle
    {
        public uint Id { get; }
        public ShaderVariantHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(ShaderVariantHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ShaderVariantHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(ShaderVariantHandle left, ShaderVariantHandle right) => left.Equals(right);
        public static bool operator !=(ShaderVariantHandle left, ShaderVariantHandle right) => !left.Equals(right);
        public override string ToString() => $"ShaderVariantHandle({Id})";
    }

    public readonly struct MaterialInstanceHandle : IRenderHandle
    {
        public uint Id { get; }
        public MaterialInstanceHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(MaterialInstanceHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is MaterialInstanceHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(MaterialInstanceHandle left, MaterialInstanceHandle right) => left.Equals(right);
        public static bool operator !=(MaterialInstanceHandle left, MaterialInstanceHandle right) => !left.Equals(right);
        public override string ToString() => $"MaterialInstanceHandle({Id})";
    }

    public readonly struct PipelineStateHandle : IRenderHandle
    {
        public uint Id { get; }
        public PipelineStateHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(PipelineStateHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is PipelineStateHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(PipelineStateHandle left, PipelineStateHandle right) => left.Equals(right);
        public static bool operator !=(PipelineStateHandle left, PipelineStateHandle right) => !left.Equals(right);
        public override string ToString() => $"PipelineStateHandle({Id})";
    }

    public readonly struct LightCookieHandle : IRenderHandle
    {
        public uint Id { get; }
        public LightCookieHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(LightCookieHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is LightCookieHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(LightCookieHandle left, LightCookieHandle right) => left.Equals(right);
        public static bool operator !=(LightCookieHandle left, LightCookieHandle right) => !left.Equals(right);
        public override string ToString() => $"LightCookieHandle({Id})";
    }

    public readonly struct ShadowAtlasHandle : IRenderHandle
    {
        public uint Id { get; }
        public ShadowAtlasHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(ShadowAtlasHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is ShadowAtlasHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(ShadowAtlasHandle left, ShadowAtlasHandle right) => left.Equals(right);
        public static bool operator !=(ShadowAtlasHandle left, ShadowAtlasHandle right) => !left.Equals(right);
        public override string ToString() => $"ShadowAtlasHandle({Id})";
    }

    public readonly struct SkinnedMeshHandle : IRenderHandle
    {
        public uint Id { get; }
        public SkinnedMeshHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(SkinnedMeshHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is SkinnedMeshHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(SkinnedMeshHandle left, SkinnedMeshHandle right) => left.Equals(right);
        public static bool operator !=(SkinnedMeshHandle left, SkinnedMeshHandle right) => !left.Equals(right);
        public override string ToString() => $"SkinnedMeshHandle({Id})";
    }

    public readonly struct GPUSkinningBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public GPUSkinningBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(GPUSkinningBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GPUSkinningBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(GPUSkinningBufferHandle left, GPUSkinningBufferHandle right) => left.Equals(right);
        public static bool operator !=(GPUSkinningBufferHandle left, GPUSkinningBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"GPUSkinningBufferHandle({Id})";
    }

    public readonly struct BlendShapeBufferHandle : IRenderHandle
    {
        public uint Id { get; }
        public BlendShapeBufferHandle(uint id) => Id = id;
        public bool IsValid => Id != 0;
        public bool Equals(BlendShapeBufferHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is BlendShapeBufferHandle h && Equals(h);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(BlendShapeBufferHandle left, BlendShapeBufferHandle right) => left.Equals(right);
        public static bool operator !=(BlendShapeBufferHandle left, BlendShapeBufferHandle right) => !left.Equals(right);
        public override string ToString() => $"BlendShapeBufferHandle({Id})";
    }

    #endregion

    #region Core Structures

    public struct RenderEngineConfig
    {
        public int Width;
        public int Height;
        public float RefreshRate;
        public bool FullScreen;
        public RenderResolution Resolution;
        public RenderQuality Quality;
        public AntiAliasing AntiAliasing;
        public AnisotropicFiltering AnisotropicFiltering;
        public LightingModel LightingModel;
        public ShadowQuality ShadowQuality;
        public float MaxShadowDistance;
        public int MaxLights;
        public int MaxDecals;
        public int MaxVFXInstances;
        public bool EnableVSync;
        public bool EnableHDR;
        public bool EnablePostProcessing;
        public bool EnableDynamicBatching;
        public bool EnableInstancing;
        public bool EnableOcclusionCulling;
        public float CullingDistance;
        public bool EnableLOD;
        public bool EnableSkinning;
        public bool EnableGPUSkinning;
        public bool EnableMotionBlur;
        public bool EnableScreenShake;
        public bool EnableFeedbackEffects; // Hitstop, Flash, etc.
        public string GraphicsAPI; // e.g., DirectX12, Vulkan, Metal, OpenGLCore
        public string BackendName; // e.g., CustomEngine, CustomRenderer
        public string Version; // [CORRECTION] Ajout d'une version

        public static RenderEngineConfig Default => new RenderEngineConfig
        {
            Width = 1920,
            Height = 1080,
            RefreshRate = 60f,
            FullScreen = false,
            Resolution = RenderResolution.Native,
            Quality = RenderQuality.High,
            AntiAliasing = AntiAliasing.TAA,
            AnisotropicFiltering = AnisotropicFiltering.x8,
            LightingModel = LightingModel.Deferred,
            ShadowQuality = ShadowQuality.High,
            MaxShadowDistance = 150f,
            MaxLights = 1024,
            MaxDecals = 256,
            MaxVFXInstances = 1000,
            EnableVSync = true,
            EnableHDR = true,
            EnablePostProcessing = true,
            EnableDynamicBatching = true,
            EnableInstancing = true,
            EnableOcclusionCulling = true,
            CullingDistance = 1000f,
            EnableLOD = true,
            EnableSkinning = true,
            EnableGPUSkinning = true,
            EnableMotionBlur = true,
            EnableScreenShake = true,
            EnableFeedbackEffects = true,
            GraphicsAPI = "DirectX12",
            BackendName = "CustomRenderer",
            Version = "1.0.0"
        };
    }

    // [CORRECTION] Nouvelle structure
    public struct RenderEngineOptions
    {
        public bool EnableValidationLayers;
        public bool EnableDeterministicRendering;
        public bool EnableHeadlessMode;
        public string LogPath;
        public int MaxShaderVariants;
        public int MaxPipelineStates;
    }

    // [CORRECTION] Nouvelle structure
    public struct RenderEngineConfigMigration
    {
        public string FromVersion;
        public string ToVersion;
        public Action<RenderEngineConfig> MigrationAction;
    }

    // [CORRECTION] Nouvelle structure
    public struct RenderEngineConfigValidator
    {
        public Func<RenderEngineConfig, bool> ValidationRule;
        public string ErrorMessage;
    }

    public struct RenderEngineCapabilities
    {
        public bool SupportsComputeShaders;
        public bool SupportsRayTracing;
        public bool SupportsVariableRateShading;
        public bool SupportsMeshShaders;
        public bool SupportsSamplerFeedback;
        public bool SupportsVulkan;
        public bool SupportsDX12;
        public bool SupportsMetal;
        public int MaxTextureSize;
        public int MaxRenderTextureSize;
        public int MaxSimultaneousRenderTargets;
        public int MaxVertexAttributes;
        public int MaxLightsPerObject;
        public float MaxAnisotropy;
        public string DeviceName;
        public string DriverVersion;
        public string APIVersion;
    }

    public struct RenderEngineMetrics
    {
        public int FrameIndex; // [CORRECTION] Ajout d'un index de frame
        public int DrawCalls;
        public int TrianglesDrawn;
        public int VerticesProcessed;
        public int BatchesDrawn;
        public int MaterialsDrawn;
        public int TexturesDrawn;
        public int ShadersCompiled;
        public int LightsActive;
        public int VFXActive;
        public int LODSwitches;
        public int GPUSkinningUpdates;
        public int RenderTexturesCreated;
        public int RenderTargetSwaps;
        public float CpuSubmitMs;
        public float GpuRenderMs;
        public float GpuSkinningMs;
        public float GpuPostProcessMs;
        public float MemoryUsedMB;
        public float VRAMUsedMB; // [CORRECTION] Ajout de budget VRAM
        public DateTime Timestamp;
    }

    public struct RenderEngineMetricsHistory
    {
        public List<RenderEngineMetrics> Metrics;
        public TimeSpan WindowDuration;
    }

    public struct RenderTargetDescriptor
    {
        public int Width;
        public int Height;
        public RenderTextureFormat Format;
        public int DepthStencilBits;
        public int MSAACount;
        public bool EnableRandomWrite;
        public bool UseMipMap;
        public bool AutoGenerateMips;
        public FilterMode FilterMode; // [CORRECTION] Ajout
        public TextureWrapMode WrapMode; // [CORRECTION] Ajout
        public Color ClearColor;
        public bool EnableDepthBuffer;
        public bool EnableStencilBuffer;
    }

    // [CORRECTION] Nouvelle structure pour Post-Process
    public struct PostProcessEffectDescriptor
    {
        public PostProcessEffect Type;
        public Dictionary<string, object> Parameters; // Ex: {"Intensity": 0.5f, "Threshold": 1.0f}
        public bool Enabled;
        public float Weight;
    }

    public struct MaterialProperty
    {
        public string Name;
        public MaterialPropertyType Type;
        // [CORRECTION] Remplacement de 'object Value' par un système typé
        public TextureHandle TextureValue;
        public Vector4 VectorValue;
        public float FloatValue;
        public Color ColorValue;
        public int IntValue;
        public bool BoolValue;

        public MaterialProperty(string name, MaterialPropertyType type, object value)
        {
            Name = name;
            Type = type;
            TextureValue = default;
            VectorValue = Vector4.Zero;
            FloatValue = 0f;
            ColorValue = Color.White;
            IntValue = 0;
            BoolValue = false;

            switch (type)
            {
                case MaterialPropertyType.Texture2D:
                case MaterialPropertyType.TextureCube:
                case MaterialPropertyType.Texture3D:
                    if (value is TextureHandle th) TextureValue = th;
                    break;
                case MaterialPropertyType.Vector4:
                    if (value is Vector4 v4) VectorValue = v4;
                    break;
                case MaterialPropertyType.Vector3:
                    if (value is Vector3 v3) VectorValue = new Vector4(v3, 1f);
                    break;
                case MaterialPropertyType.Vector2:
                    if (value is Vector2 v2) VectorValue = new Vector4(v2, 0f, 1f);
                    break;
                case MaterialPropertyType.Float:
                    if (value is float f) FloatValue = f;
                    break;
                case MaterialPropertyType.Color:
                    if (value is Color c) ColorValue = c;
                    break;
                case MaterialPropertyType.Int:
                    if (value is int i) IntValue = i;
                    break;
                case MaterialPropertyType.Bool:
                    if (value is bool b) BoolValue = b;
                    break;
            }
        }
    }

    public struct MaterialPropertyBlock
    {
        public Dictionary<string, MaterialProperty> Properties;
        public MaterialPropertyBlock(Dictionary<string, MaterialProperty> props)
        {
            Properties = props ?? new Dictionary<string, MaterialProperty>();
        }
    }

    public struct LightData
    {
        public LightHandle Handle;
        public LightType Type;
        public Vector3 Position;
        public Vector3 Direction;
        public Color Color;
        public float Range;
        public float SpotAngle;
        public float Intensity;
        public bool CastShadows;
        public bool Enabled;
        public RenderLayerMask LayerMask; // [CORRECTION] Utilisation de RenderLayerMask
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
    }

    public struct CameraData
    {
        public CameraHandle Handle;
        public Vector3 Position;
        public Quaternion Rotation;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 ViewProjectionMatrix;
        public float FieldOfView;
        public float NearClipPlane;
        public float FarClipPlane;
        public float AspectRatio;
        public Color BackgroundColor;
        public CameraClearFlags ClearFlags;
        public RenderLayerMask CullingMask; // [CORRECTION] Utilisation de RenderLayerMask
        public bool Enabled;
        public bool IsMainCamera;
        public bool IsOrthographic;
        public float OrthographicSize;
    }

    public struct MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector3[] Tangents;
        public Vector2[] UVs;
        public Color[] Colors;
        public int[] Indices;
        public int SubmeshCount;
        public int[] SubmeshStartIndex;
        public int[] SubmeshIndexCount;
        public bool CalculateNormals;
        public bool CalculateTangents;
        public bool CalculateBounds;
        public Bounds Bounds;
    }

    public struct Bounds
    {
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 Extents => Size * 0.5f;

        public Bounds(Vector3 center, Vector3 size)
        {
            Center = center;
            Size = size;
        }

        public static Bounds Empty => new Bounds(Vector3.Zero, Vector3.Zero);
    }

    public struct LODGroup
    {
        public LODLevel Level;
        public float ScreenRelativeTransitionHeight;
        public MeshHandle[] Renderers;
    }

    public struct PostProcessSettings
    {
        public bool Enabled;
        public float Weight; // For blending multiple settings
        // [CORRECTION] Remplacement de Dictionary<PostProcessEffect, object> par une pile typée
        public List<PostProcessEffectDescriptor> Effects;
    }

    public struct VFXData
    {
        public VFXType Type;
        public MaterialHandle Material;
        public MeshHandle Mesh; // For billboard or mesh-based VFX
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Duration;
        public bool Loop;
        public bool PlayOnAwake;
        public ParticleSystemData ParticleData; // If Type == ParticleSystem
        public TrailData TrailData;             // If Type == Trail
        // ... Other VFX-specific data structs
    }

    public struct ParticleSystemData
    {
        public int MaxParticles;
        public float StartLifetime;
        public float StartSpeed;
        public Vector3 StartSize;
        public Color StartColor;
        public float GravityModifier;
        public ParticleSimulationSpace SimulationSpace;
        public ParticleScalingMode ScalingMode;
        public ParticleSortMode SortMode;
        public ParticleStopAction StopAction;
        public float EmissionRate;
        public float Duration;
        public bool Loop;
        public bool PlayOnAwake;
    }

    public struct TrailData
    {
        public float StartWidth;
        public float EndWidth;
        public Color StartColor;
        public Color EndColor;
        public float Time;
        public int NumCornerVertices;
        public int NumCapVertices;
        public bool Autodestruct;
    }

    public struct ScreenShakeParameters
    {
        public float Amplitude;
        public float Frequency;
        public float Duration;
        public float FalloffTime;
        public Vector3 AxisMask; // e.g., (1,1,0) for X and Y only
        // [CORRECTION] Remplacement de AnimationCurve par un handle ou une structure
        public float[] IntensityCurveKeys; // Exemple simplifié
        public float[] IntensityCurveValues;
        // [CORRECTION] Ajout de priorité et decay
        public int Priority;
        public float DecayRate;
    }

    public struct MotionBlurSettings
    {
        public bool Enabled;
        public float Intensity;
        public float MaxBlurRadius;
        public float MinVelocity;
    }

    public struct FeedbackEffectParameters
    {
        public float HitstopDuration; // Time dilation or pause
        public float FlashIntensity;
        public Color FlashColor;
        public float ScreenShakeAmplitude;
        public float ScreenShakeFrequency;
        public float ScreenShakeDuration;
        // [CORRECTION] Ajout de priorité
        public int Priority;
        // [CORRECTION] Ajout de decay
        public float DecayRate;
    }

    // [CORRECTION] Nouvelles structures pour les corrections
    public struct RenderResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        public RenderResult(bool success, string errorMessage = null, Exception exception = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static RenderResult Ok() => new RenderResult(true);
        public static RenderResult Error(string message, Exception exception = null) => new RenderResult(false, message, exception);
    }

    public struct RenderEngineContext
    {
        public EventBus EventBus { get; set; }
        public Profiler Profiler { get; set; }
        public IJobSystem JobSystem { get; set; }
        public ResourceManager ResourceManager { get; set; }
        public IRenderDevice Device { get; set; }
        public IRenderBackend Backend { get; set; }
        public RenderEngineOptions Options { get; set; } // [CORRECTION] Ajout
        public RenderBudgetSettings Budget { get; set; } // [CORRECTION] Ajout
    }

    public struct RenderBudgetSettings
    {
        public float CPUMillisecondsPerFrame;
        public float GPUMillisecondsPerFrame;
        public float MemoryBudgetMB;
        public float VRAMBudgetMB;
    }

    public struct IndirectDrawCommand
    {
        public uint VertexCountPerInstance;
        public uint InstanceCount;
        public uint StartVertexLocation;
        public uint StartInstanceLocation;
    }

    public struct RenderInstance
    {
        public RenderInstanceHandle Handle;
        public MeshHandle Mesh;
        public MaterialHandle Material;
        public Matrix4x4 Transform;
        public RenderLayerMask LayerMask;
        public bool Visible;
        public bool CastShadows;
        public bool ReceiveShadows;
        public MaterialPropertyBlock PropertyBlock;
        public SkeletonPoseHandle AnimatedPose; // Pour intégration animation
    }

    // [CORRECTION] Structures pour Post-Process, VFX, etc.
    public struct RenderPassDescription
    {
        public string Name;
        public RenderPassType Type;
        public List<(RenderTextureHandle input, string slot)> Inputs;
        public List<(RenderTextureHandle output, string slot)> Outputs;
        public bool IsConditional;
        public string ConditionParameter;
        public bool ConditionValue;
    }

    // [CORRECTION] Nouvelles structures
    public struct RenderItemPriority
    {
        public int Priority;
        public float DistanceToCamera;
        public RenderItemPriority(int priority, float distance)
        {
            Priority = priority;
            DistanceToCamera = distance;
        }
    }

    public struct RenderItemSortingKey
    {
        public MaterialHandle Material;
        public RenderItemPriority Priority;
        public int SubmeshIndex;
        public RenderItemSortingKey(MaterialHandle material, RenderItemPriority priority, int submeshIndex)
        {
            Material = material;
            Priority = priority;
            SubmeshIndex = submeshIndex;
        }
    }

    public struct RenderInstanceLODData
    {
        public LODLevel CurrentLevel;
        public float LastTransitionTime;
        public RenderInstanceHandle[] LODHandles; // Handles des objets pour chaque niveau
    }

    public struct GPUBufferDescription
    {
        public int SizeInBytes;
        public string DebugName;
        public bool IsDynamic;
        public bool IsReadable;
        public bool IsWritable;
        public bool IsCpuWritable;
        public bool IsCpuReadable;
    }

    public struct LightClusterData
    {
        public List<LightHandle> LightsInTile;
        public int TileX;
        public int TileY;
    }

    public struct FacialExpressionWeights
    {
        public Dictionary<string, float> Weights; // Ex: {"Smile", 0.8f}, {"Anger", 0.2f}
    }

    public struct BloomSettings
    {
        public bool Enabled;
        public float Intensity;
        public float Threshold;
        public float SoftKnee;
        public float Radius;
        public float Clamp;
    }

    public struct DepthOfFieldSettings
    {
        public bool Enabled;
        public float FocusDistance;
        public float Aperture;
        public float FocalLength;
        public float MaxBlurSize;
    }

    public struct ColorGradingSettings
    {
        public bool Enabled;
        public float Temperature;
        public float Tint;
        public float Saturation;
        public float Contrast;
        public float Gain;
        public float Lift;
        public string LUTPath; // Chemin vers la LUT
    }

    public struct MotionBlurSettingsEx
    {
        public bool Enabled;
        public float Intensity;
        public float MaxBlurRadius;
        public float MinVelocity;
        public bool UseCameraVelocity;
        public bool UseObjectVelocity;
    }

    public struct ChromaticAberrationSettings
    {
        public bool Enabled;
        public float Intensity;
        public Vector2 Direction;
    }

    public struct DistortionSettings
    {
        public bool Enabled;
        public float Intensity;
        public Vector2 Center;
        public float Radius;
    }

    public struct GlitchSettings
    {
        public bool Enabled;
        public float Intensity;
        public float Frequency;
        public float BlockSize;
    }

    public struct VignetteSettings
    {
        public bool Enabled;
        public float Intensity;
        public Color Color;
        public Vector2 Center;
        public float Roundness;
        public float Smoothness;
        public float Rounded;
    }

    // [CORRECTION] Nouvelles structures pour sous-systèmes
    public struct RenderSubsystemDescriptor
    {
        public RenderSubsystemType Type;
        public string Name;
        public string Version;
        public bool Required;
        public Dictionary<string, object> Configuration;
    }

    public struct RenderSubsystemHealthReport
    {
        public RenderSubsystemType Type;
        public bool IsHealthy;
        public string StatusMessage;
        public DateTime Timestamp;
    }

    public struct RenderSubsystemTelemetry
    {
        public RenderSubsystemType Type;
        public Dictionary<string, float> Metrics;
        public DateTime Timestamp;
    }

    public struct RenderSubsystemBudget
    {
        public RenderSubsystemType Type;
        public float CPUMillisecondsPerFrame;
        public float GPUMillisecondsPerFrame;
        public float MemoryMB;
        public float VRAMMB;
    }

    // [CORRECTION] Nouvelles structures pour culling
    public struct FrustumCullingJobDescriptor
    {
        public ICamera Camera;
        public List<RenderInstanceHandle> Instances;
        public CullingMode Mode;
    }

    public struct OcclusionCullingJobDescriptor
    {
        public ICamera Camera;
        public List<RenderInstanceHandle> Instances;
        public bool UseHardwareQueries;
    }

    public struct DistanceCullingJobDescriptor
    {
        public ICamera Camera;
        public List<RenderInstanceHandle> Instances;
        public float MaxDistance;
    }

    public struct ScreenSizeCullingDescriptor
    {
        public float MinScreenSize;
        public float MaxScreenSize;
        public List<RenderInstanceHandle> Instances;
    }

    #endregion

    #region Interfaces

    public interface IRenderTexture
    {
        RenderTextureHandle Handle { get; }
        RenderTargetDescriptor Descriptor { get; }
        bool IsCreated { get; }
        void Create();
        void Release();
        void SetRenderTarget();
        void Blit(IRenderTexture source, IMaterial material = null);
    }

    public interface IMaterial
    {
        MaterialHandle Handle { get; }
        IShader Shader { get; set; }
        bool IsCreated { get; }
        void SetTexture(string name, ITexture texture);
        void SetVector(string name, Vector4 value);
        void SetFloat(string name, float value);
        void SetColor(string name, Color value);
        void SetInt(string name, int value);
        void SetMatrix(string name, Matrix4x4 value);
        void SetKeyword(ShaderKeyword keyword, bool enabled);
        IReadOnlyList<MaterialProperty> GetProperties();
        void SetPropertyBlock(MaterialPropertyBlock block); // [CORRECTION] Ajout
    }

    public interface IShader
    {
        ShaderHandle Handle { get; }
        string Name { get; }
        string Path { get; }
        bool IsLoaded { get; }
        Task<bool> LoadAsync();
        void Unload();
        void Reload();
        IReadOnlyList<ShaderKeyword> GetKeywords();
        // [CORRECTION] Ajout d'API de réflexion
        IReadOnlyList<string> GetProperties();
        IReadOnlyList<ShaderVariantHandle> GetVariants();
        void StripVariants(List<ShaderKeyword> keywordsToKeep);
    }

    public interface IMesh
    {
        MeshHandle Handle { get; }
        bool IsCreated { get; }
        void Create(MeshData data);
        void Release();
        void SetVertexBuffer(VertexBufferHandle vb); // [CORRECTION] Utilisation de handle
        void SetIndexBuffer(IndexBufferHandle ib); // [CORRECTION] Utilisation de handle
        Bounds GetBounds();
        int GetSubmeshCount();
    }

    public interface ITexture
    {
        TextureHandle Handle { get; }
        int Width { get; }
        int Height { get; }
        RenderTextureFormat Format { get; }
        bool IsLoaded { get; }
        Task<bool> LoadAsync(string path);
        void Unload();
        void SetData(byte[] data); // For dynamic updates
    }

    public interface ILight
    {
        LightHandle Handle { get; }
        LightData GetData();
        void SetData(LightData data);
        bool IsEnabled { get; set; }
    }

    public interface ICamera
    {
        CameraHandle Handle { get; }
        CameraData GetData();
        void SetData(CameraData data);
        void SetRenderTarget(IRenderTexture rt);
        IRenderTexture GetTargetTexture();
        bool IsEnabled { get; set; }
        bool IsMainCamera { get; set; }
    }

    public interface IVFX
    {
        VFXHandle Handle { get; }
        VFXData GetData();
        void SetData(VFXData data);
        void Play();
        void Stop();
        void Pause();
        bool IsAlive();
        void SetPosition(Vector3 position);
        void SetRotation(Quaternion rotation);
        void SetScale(Vector3 scale);
    }

    // [CORRECTION] Nouvelles interfaces
    // IRenderDevice est declare dans Engine/Rendering/IRenderPipeline.cs,
    // foyer de l'espace de noms Engine.Rendering.

    public interface IRenderBackend
    {
        string BackendName { get; }
        string APIVersion { get; }
        bool SupportsFeature(string feature);
        void Present();
    }

    // IRenderPipeline est declare dans Engine/Rendering/IRenderPipeline.cs,
    // ou la version complete porte deja Name et Execute.

    public interface IRenderGraphBuilder
    {
        RenderPassHandle AddPass(RenderPassDescription desc); // [CORRECTION] Retourne un handle
        void AddPassDependency(RenderPassHandle from, RenderPassHandle to); // [CORRECTION] Ajout
        void AddResource(RenderTextureHandle resource);
        void AddTransientResource(RenderTargetDescriptor desc);
        // [CORRECTION] Ajout de méthodes pour aliasing, barrières, etc.
        void AliasResource(RenderGraphResourceHandle resourceA, RenderGraphResourceHandle resourceB);
        void AddBarrier(RenderGraphResourceHandle resource);
    }

    public interface ICommandBuffer
    {
        void Begin();
        void End();
        void SetRenderTarget(IRenderTexture rt);
        void DrawMesh(IMesh mesh, IMaterial material, Matrix4x4 transform);
        void DispatchCompute(IShader computeShader, int x, int y, int z);
        void InsertDebugMarker(string name);
        void ResolveQueries();
    }

    public interface IPipelineState
    {
        void Apply();
    }

    public interface IPipelineStateDescriptor
    {
        BlendMode BlendMode { get; set; }
        DepthWrite DepthWrite { get; set; }
        ZTest ZTest { get; set; }
        CullMode CullMode { get; set; }
        CompareFunction StencilCompareFunction { get; set; }
        StencilOperation StencilFailOp { get; set; }
        StencilOperation StencilZFailOp { get; set; }
        StencilOperation StencilPassOp { get; set; }
        byte StencilReference { get; set; }
        uint StencilReadMask { get; set; }
        uint StencilWriteMask { get; set; }
    }

    public interface IVisibilitySystem
    {
        bool IsVisible(RenderInstanceHandle instance, ICamera camera);
        void PerformCulling(ICamera camera, CullingMode mode);
        IReadOnlyList<RenderInstanceHandle> GetVisibleInstances();
    }

    public interface IPostProcessStack
    {
        void PushSettings(PostProcessSettings settings);
        void PopSettings();
        void Apply(ICamera camera);
    }

    public interface IAnimationRenderBridge
    {
        void UpdateSkinning(SkeletonPoseHandle pose, IMesh mesh, IMaterial material);
        void ApplyFacialSync(SkeletonPoseHandle pose, Dictionary<string, float> blendShapeWeights);
        void RenderGhostSkeleton(SkeletonPoseHandle pose, Color color);
        void InspectBlendWeights(SkeletonPoseHandle pose);
        void CorrectPose(SkeletonPoseHandle pose, Matrix4x4 correctionMatrix);
    }

    public interface IRenderFeedbackSystem
    {
        void TriggerHitstop(FeedbackEffectParameters parameters);
        void TriggerFlash(Color color, float intensity);
        void TriggerScreenShake(ScreenShakeParameters parameters);
        void TriggerGlitch(float intensity);
    }

    public interface IRenderSubsystem
    {
        RenderSubsystemType Type { get; }
        bool Initialize(RenderEngineContext context);
        void Shutdown();
        void Update(float deltaTime);
        void ValidateIntegrity();
        RenderResult CheckHealth();
    }

    // [CORRECTION] Nouvelles interfaces
    public interface IPostProcessEffect
    {
        PostProcessEffect Type { get; }
        void Prepare(ICamera camera, IRenderTexture input, IRenderTexture output);
        void Execute(ICommandBuffer cmd);
        void Cleanup();
    }

    public interface IVisibilityCullingJob
    {
        void Execute(ICamera camera, IVisibilitySystem cullingSystem);
    }

    public interface IUIRenderer
    {
        void Begin();
        void End();
        void DrawRect(Rect rect, Color color);
        void DrawText(string text, Vector2 position, Color color);
        void SetScissor(Rect scissorRect);
        void SetViewport(Rect viewport);
    }

    // [CORRECTION] Nouvelle interface pour le helper
    public interface IRenderHandleValidator
    {
        bool IsValid<T>(T handle) where T : struct, IRenderHandle;
        void Validate<T>(T handle, string parameterName) where T : struct, IRenderHandle;
    }

    public interface IRenderEngine
    {
        // ============================================================
        // [CORRECTION] Architecture & Context
        // ============================================================

        void SetContext(RenderEngineContext context);
        RenderEngineContext GetContext();

        // ============================================================
        // Lifecycle
        // ============================================================

        void Initialize(
            RenderEngineConfig config,
            EventBus eventBus,
            Profiler profiler,
            IJobSystem jobSystem,
            ResourceManager resourceManager);

        Task InitializeAsync(
            RenderEngineConfig config,
            EventBus eventBus,
            Profiler profiler,
            IJobSystem jobSystem,
            ResourceManager resourceManager);

        void Shutdown();
        Task ShutdownAsync();

        void Restart(RenderEngineConfig config);
        void Reset();

        void WarmupPhase(); // [CORRECTION] Ajout
        void CooldownPhase(); // [CORRECTION] Ajout

        void Suspend();
        void Resume();

        RenderEngineState GetState();
        bool IsReady();

        // ============================================================
        // [CORRECTION] Subsystems
        // ============================================================

        void RegisterSubsystem(IRenderSubsystem subsystem);
        void RemoveSubsystem(RenderSubsystemType type);
        IRenderSubsystem GetSubsystem(RenderSubsystemType type);
        void ValidateSubsystemIntegrity(); // [CORRECTION] Ajout

        // ============================================================
        // [CORRECTION] Features & Config
        // ============================================================

        void SetFeatureEnabled(RenderFeatureFlags feature, bool enabled); // [CORRECTION] Ajout
        bool IsFeatureEnabled(RenderFeatureFlags feature); // [CORRECTION] Ajout
        void SetRenderBudget(RenderBudgetSettings budget); // [CORRECTION] Ajout
        RenderBudgetSettings GetRenderBudget(); // [CORRECTION] Ajout

        // ============================================================
        // Frame Update & Submission
        // ============================================================

        void BeginFrame();
        void RenderScene(ICamera camera);
        void Submit();
        void Present();
        void EndFrame();

        void SetTimeDilation(float dilation);
        float GetTimeDilation();

        // ============================================================
        // Configuration & Quality
        // ============================================================

        void ApplyConfig(RenderEngineConfig config);
        RenderEngineConfig GetCurrentConfig();

        void SetRenderQuality(RenderQuality quality);
        RenderQuality GetRenderQuality();

        void SetShadowQuality(ShadowQuality quality);
        ShadowQuality GetShadowQuality();

        void SetAntiAliasing(AntiAliasing aa);
        AntiAliasing GetAntiAliasing();

        void SetAnisotropicFiltering(AnisotropicFiltering af);
        AnisotropicFiltering GetAnisotropicFiltering();

        void SetEnableHDR(bool enable);
        bool GetEnableHDR();

        void SetEnablePostProcessing(bool enable);
        bool GetEnablePostProcessing();

        void SetEnableDynamicBatching(bool enable);
        bool GetEnableDynamicBatching();

        void SetEnableInstancing(bool enable);
        bool GetEnableInstancing();

        void SetEnableOcclusionCulling(bool enable);
        bool GetEnableOcclusionCulling();

        void SetEnableLOD(bool enable);
        bool GetEnableLOD();

        void SetEnableGPUSkinning(bool enable); // [CORRECTION] Maintenue
        bool GetEnableGPUSkinning(); // [CORRECTION] Maintenue
        void SetGPUSkinningEnabledForInstance(RenderInstanceHandle instance, bool enable); // [CORRECTION] Ajout
        bool IsGPUSkinningEnabledForInstance(RenderInstanceHandle instance); // [CORRECTION] Ajout

        // ============================================================
        // Resources Management
        // ============================================================

        Task<ITexture> LoadTextureAsync(string path);
        ITexture CreateTexture2D(int width, int height, RenderTextureFormat format);
        void UnloadTexture(ITexture texture);

        Task<IShader> LoadShaderAsync(string path);
        void UnloadShader(IShader shader);

        // [CORRECTION] Ajout de RenderResult
        RenderResult CreateMesh(out IMesh mesh);
        RenderResult CreateMaterial(IShader shader, out IMaterial material);
        RenderResult CreateRenderTexture(RenderTargetDescriptor descriptor, out IRenderTexture rt);

        void ReleaseMesh(IMesh mesh);
        void ReleaseMaterial(IMaterial material);
        void ReleaseRenderTexture(IRenderTexture rt);

        // ============================================================
        // [CORRECTION] GPU Resources
        // ============================================================

        GPUBufferHandle CreateGPUBuffer(GPUBufferDescription desc);
        void UpdateGPUBuffer(GPUBufferHandle handle, byte[] data);
        void ReleaseGPUBuffer(GPUBufferHandle handle);

        // [CORRECTION] Ajout de RenderResult
        RenderResult CreateVertexBuffer(int sizeInBytes, out VertexBufferHandle handle);
        void UpdateVertexBuffer(VertexBufferHandle handle, byte[] data);
        void ReleaseVertexBuffer(VertexBufferHandle handle);

        RenderResult CreateIndexBuffer(int sizeInBytes, out IndexBufferHandle handle);
        void UpdateIndexBuffer(IndexBufferHandle handle, byte[] data);
        void ReleaseIndexBuffer(IndexBufferHandle handle);

        // ============================================================
        // Cameras
        // ============================================================

        ICamera CreateCamera();
        void ReleaseCamera(ICamera camera);
        IReadOnlyList<ICamera> GetAllCameras();
        ICamera GetMainCamera();

        // ============================================================
        // Lights
        // ============================================================

        ILight CreateLight(LightType type);
        void ReleaseLight(ILight light);
        IReadOnlyList<ILight> GetAllLights();

        // ============================================================
        // Visual Effects (VFX)
        // ============================================================

        IVFX CreateVFX(VFXType type);
        void ReleaseVFX(IVFX vfx);
        void UpdateVFX(IVFX vfx, float deltaTime);
        void PlayVFX(IVFX vfx);
        void StopVFX(IVFX vfx);

        // ============================================================
        // [CORRECTION] Render Instances & Scene API
        // ============================================================

        // [CORRECTION] Ajout de RenderResult et validation
        RenderResult QueueRenderItem(IMesh mesh, IMaterial material, Matrix4x4 transform, RenderLayerMask layerMask, out RenderInstanceHandle handle);
        RenderResult UpdateRenderItemTransform(RenderInstanceHandle handle, Matrix4x4 newTransform);
        RenderResult UpdateRenderItemMaterial(RenderInstanceHandle handle, IMaterial newMaterial);
        RenderResult RemoveRenderItem(RenderInstanceHandle handle);
        void SetCullingMode(CullingMode mode); // [CORRECTION] Ajout
        void SetBatchMode(BatchMode mode); // [CORRECTION] Ajout

        // ============================================================
        // Animation Integration (for Skin, LOD, etc.)
        // ============================================================

        /// <summary>
        /// Applies a model-space pose to a skinned mesh instance.
        /// Used for Ghost Skeleton Mode, Pose Correction, Facial Sync.
        /// </summary>
        void ApplyPoseToSkinnedMesh(IMesh mesh, MaterialHandle material, Matrix4x4[] boneMatrices);

        /// <summary>
        /// Calculates and applies LOD level based on distance or performance.
        /// </summary>
        LODLevel CalculateLOD(Vector3 worldPosition, Vector3 cameraPosition, LODStrategy strategy = LODStrategy.DistanceBased);

        // ============================================================
        // Post-Processing & Feedback
        // ============================================================

        /// <summary>
        /// Sets the active post-process settings for the main camera.
        /// </summary>
        void SetPostProcessSettings(PostProcessSettings settings);

        /// <summary>
        /// Applies Motion Blur effect.
        /// </summary>
        void SetMotionBlurSettings(MotionBlurSettings settings);

        /// <summary>
        /// Applies a screen shake effect.
        /// </summary>
        void ApplyScreenShake(ScreenShakeParameters parameters);

        /// <summary>
        /// Triggers a hitstop-like effect (time dilation, flash, screen shake).
        /// </summary>
        void TriggerFeedbackEffect(FeedbackEffectParameters parameters);

        // ============================================================
        // Culling & Optimization
        // ============================================================

        void PerformFrustumCulling(ICamera camera, IEnumerable<IMesh> meshes);
        void PerformOcclusionCulling(ICamera camera);
        void PerformDistanceCulling(ICamera camera, float maxDistance);

        // ============================================================
        // [CORRECTION] Jobs & Profiling Hooks
        // ============================================================

        void ScheduleSkinningJobs(IJobSystem jobSystem, SkeletonPoseHandle pose, IMesh mesh);
        void ScheduleCullingJobs(IJobSystem jobSystem, ICamera camera, IVisibilitySystem cullingSystem);

        // ============================================================
        // Metrics & Observability
        // ============================================================

        RenderEngineCapabilities GetCapabilities();
        RenderEngineMetrics GetMetrics();
        RenderEngineMetricsHistory GetMetricsHistory(TimeSpan duration); // [CORRECTION] Ajout

        // ============================================================
        // Debug & Editor
        // ============================================================

        void SetDebugOverlayEnabled(bool enabled);
        void DrawDebugWireframe(IMesh mesh, Color color);
        void DrawDebugBounds(Bounds bounds, Color color);
        void DrawDebugLight(ILight light, Color color);
        void DrawDebugCamera(ICamera camera, Color color);
    }

    #endregion

    #region Events

    public class RenderEngineInitializedEvent
    {
        public IRenderEngine Source { get; }

        public RenderEngineInitializedEvent(IRenderEngine source)
        {
            Source = source;
        }
    }

    public class RenderEngineShutdownEvent
    {
        public IRenderEngine Source { get; }

        public RenderEngineShutdownEvent(IRenderEngine source)
        {
            Source = source;
        }
    }

    public class RenderEngineLifecycleEvent
    {
        public RenderEngineState PreviousState { get; }
        public RenderEngineState NewState { get; }
        public DateTime Timestamp { get; }

        public RenderEngineLifecycleEvent(
            RenderEngineState previousState,
            RenderEngineState newState)
        {
            PreviousState = previousState;
            NewState = newState;
            Timestamp = DateTime.UtcNow;
        }
    }

    // [CORRECTION] Nouveaux événements
    public class RenderFrameStartedEvent
    {
        public int FrameIndex { get; }
        public DateTime Timestamp { get; }
        public RenderFrameStartedEvent(int frameIndex)
        {
            FrameIndex = frameIndex;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class RenderFrameEndedEvent
    {
        public int FrameIndex { get; }
        public float ElapsedMs { get; }
        public DateTime Timestamp { get; }
        public RenderFrameEndedEvent(int frameIndex, float elapsedMs)
        {
            FrameIndex = frameIndex;
            ElapsedMs = elapsedMs;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class RenderResolutionChangedEvent
    {
        public int NewWidth { get; }
        public int NewHeight { get; }
        public RenderResolutionChangedEvent(int width, int height)
        {
            NewWidth = width;
            NewHeight = height;
        }
    }

    public class RenderQualityChangedEvent
    {
        public RenderQuality NewQuality { get; }
        public RenderQualityChangedEvent(RenderQuality quality)
        {
            NewQuality = quality;
        }
    }

    public class RenderDeviceLostEvent
    {
        public IRenderDevice Device { get; }
        public RenderDeviceLostEvent(IRenderDevice device)
        {
            Device = device;
        }
    }

    public class RenderDeviceResetEvent
    {
        public IRenderDevice Device { get; }
        public RenderDeviceResetEvent(IRenderDevice device)
        {
            Device = device;
        }
    }

    public class ShaderCompiledEvent
    {
        public IShader Shader { get; }
        public ShaderCompiledEvent(IShader shader)
        {
            Shader = shader;
        }
    }

    public class ShaderCompilationErrorEvent
    {
        public IShader Shader { get; }
        public string ErrorMessage { get; }
        public ShaderCompilationErrorEvent(IShader shader, string error)
        {
            Shader = shader;
            ErrorMessage = error;
        }
    }

    public class MaterialCreatedEvent
    {
        public MaterialHandle Handle { get; }
        public MaterialCreatedEvent(MaterialHandle handle)
        {
            Handle = handle;
        }
    }

    public class MaterialDestroyedEvent
    {
        public MaterialHandle Handle { get; }
        public MaterialDestroyedEvent(MaterialHandle handle)
        {
            Handle = handle;
        }
    }

    public class MeshCreatedEvent
    {
        public MeshHandle Handle { get; }
        public MeshCreatedEvent(MeshHandle handle)
        {
            Handle = handle;
        }
    }

    public class MeshDestroyedEvent
    {
        public MeshHandle Handle { get; }
        public MeshDestroyedEvent(MeshHandle handle)
        {
            Handle = handle;
        }
    }

    public class TextureStreamingStatusEvent
    {
        public TextureHandle Handle { get; }
        public bool Loaded { get; }
        public TextureStreamingStatusEvent(TextureHandle handle, bool loaded)
        {
            Handle = handle;
            Loaded = loaded;
        }
    }

    public class LODChangedEvent
    {
        public RenderInstanceHandle Instance { get; }
        public LODLevel OldLevel { get; }
        public LODLevel NewLevel { get; }
        public LODChangedEvent(RenderInstanceHandle instance, LODLevel oldLevel, LODLevel newLevel)
        {
            Instance = instance;
            OldLevel = oldLevel;
            NewLevel = newLevel;
        }
    }

    public class CameraStackChangedEvent
    {
        public List<CameraHandle> ActiveCameras { get; }
        public CameraStackChangedEvent(List<CameraHandle> cameras)
        {
            ActiveCameras = cameras;
        }
    }

    public class LightChangedEvent
    {
        public LightHandle Handle { get; }
        public LightChangedEvent(LightHandle handle)
        {
            Handle = handle;
        }
    }

    public class VFXSpawnedEvent
    {
        public VFXHandle Handle { get; }
        public VFXSpawnedEvent(VFXHandle handle)
        {
            Handle = handle;
        }
    }

    public class VFXCompletedEvent
    {
        public VFXHandle Handle { get; }
        public VFXCompletedEvent(VFXHandle handle)
        {
            Handle = handle;
        }
    }

    public class PostProcessSettingsChangedEvent
    {
        public PostProcessSettings Settings { get; }
        public PostProcessSettingsChangedEvent(PostProcessSettings settings)
        {
            Settings = settings;
        }
    }

    public class ScreenShakeStartedEvent
    {
        public ScreenShakeParameters Parameters { get; }
        public ScreenShakeStartedEvent(ScreenShakeParameters parameters)
        {
            Parameters = parameters;
        }
    }

    public class ScreenShakeEndedEvent
    {
        public ScreenShakeParameters Parameters { get; } // Peut-être utile pour comparer
        public ScreenShakeEndedEvent(ScreenShakeParameters parameters)
        {
            Parameters = parameters;
        }
    }

    public class FeedbackEffectCompletedEvent
    {
        public string EffectType { get; } // e.g., "Hitstop", "Flash", "ScreenShake"
        public object Parameters { get; }

        public FeedbackEffectCompletedEvent(string effectType, object parameters)
        {
            EffectType = effectType;
            Parameters = parameters;
        }
    }

    public class RenderTargetCreatedEvent
    {
        public RenderTextureHandle Handle { get; }
        public RenderTargetDescriptor Descriptor { get; }

        public RenderTargetCreatedEvent(RenderTextureHandle handle, RenderTargetDescriptor descriptor)
        {
            Handle = handle;
            Descriptor = descriptor;
        }
    }

    public class RenderTargetDestroyedEvent
    {
        public RenderTextureHandle Handle { get; }

        public RenderTargetDestroyedEvent(RenderTextureHandle handle)
        {
            Handle = handle;
        }
    }

    public class RenderErrorEvent
    {
        public string Message { get; }
        public Exception Exception { get; }

        public RenderErrorEvent(string message, Exception exception = null)
        {
            Message = message;
            Exception = exception;
        }
    }

    public class FeedbackEffectTriggeredEvent
    {
        public string EffectType { get; } // e.g., "Hitstop", "Flash", "ScreenShake"
        public object Parameters { get; }

        public FeedbackEffectTriggeredEvent(string effectType, object parameters)
        {
            EffectType = effectType;
            Parameters = parameters;
        }
    }

    #endregion
}