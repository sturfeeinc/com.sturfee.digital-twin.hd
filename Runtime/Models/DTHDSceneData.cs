using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SturfeeVPS.Core;
using Newtonsoft.Json;

namespace Sturfee.DigitalTwin.HD
{
    public class DTHDConstants
    {
        public static readonly string DTHD_API = "https://digitaltwin.sturfee.com/hd/layout";
        public static readonly string TestID = "3745b04f-7465-4533-b84f-406690685845";
    }

    [Serializable]
    public class DtHdAssetItem
    {
        public string DtHdAssetId;
        public string DtHdAssetItemId;
        public string Name;
        public DateTime CreatedDate;
        public DateTime UpdatedDate;
        public GeoLocation Location;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public float RotationX;
        public float RotationY;
        public float RotationZ;
        public float RotationW;
        public float Scale;
    }

    [Serializable]
    public enum AssetType
    {
        Prop,
        ProductGroup,
        EditableSurface
    }

    [Serializable]
    public class DtHdAsset
    {
        public string DtHdAssetId;
        public string Name;
        public string Description;
        public List<DtHdAssetItem> Items;
        public string FileUrl;
        public int FileSizeBytes;
        public string Format;
        public DateTime CreatedDate;
        public DateTime UpdatedDate;
        public AssetType AssetType;
        public string ExternalRefId;
        public string EditMode;
        public string EditRole;
        public string PhysicsMode;
    }

    [Serializable]
    public class DtHdLayout
    {
        public string DtHdId;
        public string UserId;
        public string Name;
        public GeoLocation Location;
        public double RefX;
        public double RefY;
        public double RefZ;
        public DateTime CreatedDate;
        public DateTime UpdatedDate;
        public int FileSizeBytes;
        public float SpawnPositionX;
        public float SpawnPositionY;
        public float SpawnPositionZ;
        public float SpawnHeading;
        public bool IsIndoor;
        public bool IsPublic;
        public List<ScanMesh> ScanMeshes;
        public string EnhancedMesh;
        public List<DtHdAsset> Assets;
        public string DtEnvironmentUrl;
    }

    [Serializable]
    public class ScanMesh
    {
        public string DtHdScanId;
        public string Status;
        public string SiteName;
        public string Thumbnail;
        public DateTime CreatedDate;
        public DateTime UpdatedDate;
        public GeoLocation ScanLocation;
        public double RefX;
        public double RefY;
        public double RefZ;
        public int Floor;
        public string ScanMeshUrl;
        public VpsHdSite VpsHdSite;

    }

    // [Serializable]
    // public class VpsHdSite
    // {
    //     [JsonProperty("site_id")]
    //     public string SiteId;
    //     public string Name;
    //     [JsonProperty("dthd_id")]
    //     public string DtHdId;
    //     [JsonProperty("dtscan_id")]
    //     public string DtScanId;

    // }

    [Serializable]
    public class VpsHdSite
    {
        public string thumbnailUrl;
        public SiteInfo siteInfo;
        public string anchorMesh;
    }

    [Serializable]
    public class SiteInfo
    {
        public string site_id;
        public string name;
        public string dthd_id;
        public string dtscan_id;
        public string thumbnail_id;
        public DateTime createdDate;
        public DateTime updatedDate;
        public int floor;
        public bool isIndoor;
        public double refX;
        public double refY;
        public double refZ;
        public string source;
        public string platform;
        public string s3_key;
        public double longitude;
        public double latitude;
        public int utm_lon_zone;
        public string utm_lat_zone;
        public float radius;
        public bool active;
        public float terrainAdjustment;
        public float projectionErrorThreshold;
    }

    // data for DtHd Environment.json
    [Serializable]
    public class DtEnvironment
    {
        public UnityEnvironment Unity;
    }

    [Serializable]
    public class UnityEnvironment
    {
        public UnityReflectionProbe[] ReflectionProbes;
        public UnityLight[] Lights;
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
        public Guid ReflectionProbeId;
        public Guid? DtHdId;
        public Guid UserId;

        public string Name;
        public int Importance;
        public float Intensity;
        public bool BoxProjection;
        public float BoxSize;
        public ReflectionProbeType Type;
        public DateTime CreatedDate;
        public DateTime UpdatedDate;

        public double Lat;
        public double Lon;
        public double Alt;

        public float LocalX;
        public float LocalY;
        public float LocalZ;
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
        public Guid LightId;
        public Guid? DtHdId;
        public Guid UserId;

        public string Name;
        public LightType LightType;

        public bool IsMainLight;

        public float Range;
        public float SpotAngle;

        public float ColorR;
        public float ColorG;
        public float ColorB;

        public float Intensity;
        public ShadowType ShadowType;
        public LightMode LightMode;

        public DateTime CreatedDate;
        public DateTime UpdatedDate;

        public double Lat;
        public double Lon;
        public double Alt;

        public float LocalX;
        public float LocalY;
        public float LocalZ;

        public float RotationX;
        public float RotationY;
        public float RotationZ;
        public float RotationW;
    }
}
