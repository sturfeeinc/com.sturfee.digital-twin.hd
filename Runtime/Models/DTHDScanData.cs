using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SturfeeVPS.Core;

namespace Sturfee.DigitalTwin.HD
{
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
    }
}
