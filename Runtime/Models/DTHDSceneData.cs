using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SturfeeVPS.Core;

namespace Sturfee.DigitalTwin.HD
{
    public class DTHDConstants
    {
        public static readonly string DTHD_API = "https://digitaltwin.devsturfee.com/hd/layout";
        public static readonly string TestID = "3745b04f-7465-4533-b84f-406690685845";
    }

    public class DtHdAssetItem
    {
        public string DtHdAssetId { get; set; }
        public string DtHdAssetItemId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public GeoLocation Location { get; set; }
        public float LocalX { get; set; }
        public float LocalY { get; set; }
        public float LocalZ { get; set; }
        public float RotationX { get; set; }
        public float RotationY { get; set; }
        public float RotationZ { get; set; }
        public float RotationW { get; set; }
        public float Scale { get; set; }
    }

    public class DtHdAsset
    {
        public string DtHdAssetId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<DtHdAssetItem> Items { get; set; }
        public string FileUrl { get; set; }
        public int FileSizeBytes { get; set; }
        public string Format { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public string AssetType { get; set; }
        public string ExternalRefId { get; set; }
        public string EditMode { get; set; }
        public string EditRole { get; set; }
        public string PhysicsMode { get; set; }
    }

    public class DtHdLayout
    {
        public string DtHdId { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public GeoLocation Location { get; set; }
        public double RefX { get; set; }
        public double RefY { get; set; }
        public double RefZ { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public int FileSizeBytes { get; set; }
        public float SpawnPositionX { get; set; }
        public float SpawnPositionY { get; set; }
        public float SpawnPositionZ { get; set; }
        public float SpawnHeading { get; set; }
        public bool IsIndoor { get; set; }
        public bool IsPublic { get; set; }
        public string ScanMeshes { get; set; }
        public string EnhancedMesh { get; set; }
        public List<DtHdAsset> Assets { get; set; }
        public string DtEnvironmentUrl { get; set; }
    }

    // data for DtHd Environment.json
    [Serializable]
    public class DtEnvironment
    {
        public UnityEnvironment Unity { get; set; }
    }

    [Serializable]
    public class UnityEnvironment
    {
        public UnityReflectionProbe[] ReflectionProbes { get; set; }
        public UnityLight[] Lights { get; set; }
    }

    public enum ReflectionProbeType
    {
        Baked,
        Custom,
        Realtime
    }

    [Serializable]
    public class UnityReflectionProbe
    {
        public Guid ReflectionProbeId { get; set; }
        public Guid? DtHdId { get; set; }
        public Guid UserId { get; set; }

        public string Name { get; set; }
        public int Importance { get; set; }
        public float Intensity { get; set; }
        public bool BoxProjection { get; set; }
        public float BoxSize { get; set; }
        public ReflectionProbeType Type { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }

        public double Lat { get; set; }
        public double Lon { get; set; }
        public double Alt { get; set; }

        public float LocalX { get; set; }
        public float LocalY { get; set; }
        public float LocalZ { get; set; }
    }

    public enum LightType
    {
        Point,
        Spot,
        Directional
    }

    public enum ShadowType
    {
        NoShadows,
        HardSadows,
        SoftShadows
    }

    public enum LightMode
    {
        RealTime,
        Mixed,
        Baked
    }

    [Serializable]
    public class UnityLight
    {
        public Guid LightId { get; set; }
        public Guid? DtHdId { get; set; }
        public Guid UserId { get; set; }

        public string Name { get; set; }
        public LightType LightType { get; set; }

        public bool IsMainLight { get; set; }

        public float Range { get; set; }
        public float SpotAngle { get; set; }

        public float ColorR { get; set; }
        public float ColorG { get; set; }
        public float ColorB { get; set; }

        public float Intensity { get; set; }
        public ShadowType ShadowType { get; set; }
        public LightMode LightMode { get; set; }

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }

        public double Lat { get; set; }
        public double Lon { get; set; }
        public double Alt { get; set; }

        public float LocalX { get; set; }
        public float LocalY { get; set; }
        public float LocalZ { get; set; }

        public float RotationX { get; set; }
        public float RotationY { get; set; }
        public float RotationZ { get; set; }
        public float RotationW { get; set; }
    }
}