using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SturfeeVPS.Core;

namespace Sturfee.DigitalTwin.HD
{
    public class ScanMesh
    {
        public string DtHdScanId { get; set; }
        public string Status { get; set; }
        public string SiteName { get; set; }
        public string Thumbnail { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public GeoLocation ScanLocation { get; set; }
        public double RefX { get; set; }
        public double RefY { get; set; }
        public double RefZ { get; set; }
        public int Floor { get; set; }
        public string ScanMeshUrl { get; set; }
    }
}
