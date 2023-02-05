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

    public class AssetItem
    {
        public string DtHdAssetItemId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public GeoLocation Location { get; set; }
        public double LocalX { get; set; }
        public double LocalY { get; set; }
        public double LocalZ { get; set; }
        public double RotationX { get; set; }
        public double RotationY { get; set; }
        public double RotationZ { get; set; }
        public double RotationW { get; set; }
        public double Scale { get; set; }
    }

    public class Asset
    {
        public string DtHdAssetId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<AssetItem> Items { get; set; }
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

    public class SceneData
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
        public double SpawnPositionX { get; set; }
        public double SpawnPositionY { get; set; }
        public double SpawnPositionZ { get; set; }
        public double SpawnHeading { get; set; }
        public bool IsIndoor { get; set; }
        public bool IsPublic { get; set; }
        public string ScanMeshes { get; set; }
        public string EnhancedMesh { get; set; }
        public List<Asset> Assets { get; set; }
        public string ReflectionProbeInfoUrl { get; set; }
    }
}