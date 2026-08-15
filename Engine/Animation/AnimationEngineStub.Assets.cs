// /Engine/Animation/AnimationEngineStub.Assets.cs
//
// Responsabilités : Gestion des assets, streaming, intégrité.
// Dépendances : AnimationEngineStub.Core, AnimationEngineStub.Metrics, AnimationEngineStub.Security.
// Intègre : AssetCatalog, AssetCache, IntegrityChecker.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.Security.Cryptography;

namespace Engine.Animation
{
    // [AJOUT] Interfaces pour les idées 398-597
    public interface IAssetCatalog
    {
        void RegisterAsset(AssetInfo info);
        AssetInfo GetAssetInfo(string name);
        List<AssetInfo> GetAllAssets();
        bool IsAssetLoaded(string name);
        void MarkAssetAsUsed(string name);
        void MarkAssetAsUnused(string name);
    }

    // ... autres interfaces et implémentations ...
}