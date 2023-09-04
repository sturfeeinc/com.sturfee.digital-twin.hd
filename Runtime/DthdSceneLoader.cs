using Newtonsoft.Json;
using Sturfee.XRCS.Utils;
using SturfeeVPS.Core;
using SturfeeVPS.Core.Models;
using SturfeeVPS.Core.Constants;
using SturfeeVPS.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
//using UnityGLTF;
//using UnityGLTF.Loader;
using GLTFast;
using CesiumForUnity;

namespace Sturfee.DigitalTwin.HD
{
    /// <summary>
    /// API error codes
    /// </summary>
    public enum DtHdErrorCode
    {
        NO_DT_HD_DATA,
        NO_MESH_DATA,
        DATA_NOT_DOWNLOADED,
        LOAD_ERROR
    }


    /// <summary>
    /// A loader for DTHD Scenes in the form of scene-change persistent singleton. Contains methods for loading DTHD Scene and ScanMesh asynchronously.
    /// </summary>
    public class DtHdSceneLoader : SimpleSingleton<DtHdSceneLoader>
    {
        public static int maximumScreenSpaceError = 32;
        public static uint maximumSimultaneousTileLoads = 8;
        public static uint loadingDescendantLimit = 8;
        public static int maximumCachedBytes = 256 * 1024 * 1024; // 256 MB;
        public static int culledScreenSpaceError = 32;
        public static bool createPhysicsMeshes = true;

        private GameObject _parent;
        private GameObject Enhanced;
        private Dictionary<string, GameObject> LoadedAssets;

        /// <summary>
        /// Loads DTHD scene including enhanced building mesh (artist generated), reflection probes and related assets.
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <returns>Parent transform of instantiated scene</returns>
        public async Task<GameObject> LoadDtHdAsync(string dthdId, int version = -1)
        {
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var dataFilePath = Path.Combine(baseFolder, "data.json");

            if (File.Exists(dataFilePath))
            {
                var dataJson = File.ReadAllText(dataFilePath);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: No DT HD data found in file for {dthdId}"); return null; }
                var layoutData = JsonConvert.DeserializeObject<DtHdLayout>(dataJson);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: Cannot read data file for {dthdId}"); return null; }

                var cesiumId = layoutData.CesiumAssetId;
                if (version != -1)
                {
                    var versionData = layoutData.Versions.FirstOrDefault(x => x.Version == version);
                    if (versionData != null)
                    {
                        cesiumId = versionData.CesiumAssetId;
                    }
                }

                if (!string.IsNullOrEmpty(cesiumId))
                {
                    Debug.Log($"[STURFEE] :: Using Cesium ({cesiumId}) | {layoutData.Location.Latitude}, {layoutData.Location.Longitude}");
                    _parent = new GameObject($"DTHDScene_{dthdId}");
                    _parent.transform.position = Vector3.zero;

                    var cesiumGeo = _parent.AddComponent<CesiumGeoreference>();
                    cesiumGeo.latitude = layoutData.Location.Latitude;
                    cesiumGeo.longitude = layoutData.Location.Longitude;
                    cesiumGeo.height = layoutData.Location.Altitude;

                    var asset = new GameObject("CesiumAsset");
                    asset.transform.parent = _parent.transform;
                    var cesiumAsset = asset.AddComponent<Cesium3DTileset>();
                    cesiumAsset.ionAccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiJkNzhmN2E0YS05ZmU2LTQwZDAtYTU2OS03YjlmMGZkOGYxYmUiLCJpZCI6MTI5MDg0LCJpYXQiOjE2ODczNDg0MjF9.uwHxAhuoNqSoFdIJUp5IgFA-MLtBG23WTfQKXrt6fmY";
                    cesiumAsset.maximumScreenSpaceError = DtHdSceneLoader.maximumScreenSpaceError; // 32;
                    cesiumAsset.maximumSimultaneousTileLoads = DtHdSceneLoader.maximumSimultaneousTileLoads; // 8;
                    cesiumAsset.loadingDescendantLimit = DtHdSceneLoader.loadingDescendantLimit; // 8;
                    cesiumAsset.maximumCachedBytes = DtHdSceneLoader.maximumCachedBytes; // 256 * 1024 * 1024; // 256 MB
                    cesiumAsset.culledScreenSpaceError = DtHdSceneLoader.culledScreenSpaceError; // 32;
                    cesiumAsset.createPhysicsMeshes = DtHdSceneLoader.createPhysicsMeshes;
                    cesiumAsset.ionAssetID = int.Parse(cesiumId);

                    // create helper for spawn points, etc
                    var helper = asset.AddComponent<DtHdLayoutHelper>();
                    helper.DtHdId = $"{layoutData.DtHdId}";
                    asset.gameObject.name = $"DtHdLayout_{layoutData.DtHdId}";

                    // add dummy spawnpoint
                    helper.SpawnPoint = new GameObject($"DtHdSpawnPoint");
                    helper.SpawnPoint.transform.SetParent(asset.transform);
                    helper.SpawnPoint.transform.localPosition = new Vector3(layoutData.SpawnPositionX, layoutData.SpawnPositionY, layoutData.SpawnPositionZ);
                    //helper.SpawnPoint.transform.Rotate(new Vector3(layoutData.SpawnHeading - 90, 90, 90));// -90 points north
                    helper.SpawnPoint.transform.Rotate(new Vector3(0, layoutData.SpawnHeading, 0));

                    // load the environment (reflection probes, lighting, etc)
                    if (File.Exists($"{baseFolder}/environment.json"))
                    {
                        await LoadLightingAndReflections($"{baseFolder}/environment.json", _parent.transform, "Cesium");
                    }
                    else if (File.Exists($"{baseFolder}/dt_environment.json"))
                    {
                        await LoadLightingAndReflections($"{baseFolder}/dt_environment.json", _parent.transform, "Cesium");
                    }
                }
                else
                {
                    if (layoutData.EnhancedMesh != null)
                    {
                        await _LoadDtHdAsync(dthdId, layoutData);
                    }
                    else
                    {
                        throw CreateException(DtHdErrorCode.LOAD_ERROR, "Error loading layout mesh");
                    }

                    _parent.transform.Rotate(-90, 0, 180);

                    // load the environment (reflection probes, lighting, etc)
                    try
                    {
                        if (File.Exists($"{baseFolder}/environment.json"))
                        {
                            await LoadLightingAndReflections($"{baseFolder}/environment.json", Enhanced.transform, "Unity");
                        }
                        else if (File.Exists($"{baseFolder}/dt_environment.json"))
                        {
                            await LoadLightingAndReflections($"{baseFolder}/dt_environment.json", Enhanced.transform, "Unity");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[Sturfee.DigitalTwin.HD]:DtHdSceneLoader :: ERROR Loading Lighting data");
                        Debug.Log(ex);
                    }

                    // set the position
                    Enhanced.transform.position = Converters.GeoToUnityPosition(layoutData.Location);
                }
            }
            else
            {
                // TODO: we should go download this data and still load everything...
                Debug.LogError($"Error :: No local DT HD data file for {dthdId}");
            }

            return _parent;
        }

        /// <summary>
        /// Loads scan mesh for associated DTHD ID
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <param name="scanMeshId">Scan mesh ID</param>
        /// <returns>Parent transform of the instantiated scan mesh</returns>
        public async Task<GameObject> LoadScanMeshAsync(string dthdId, string scanMeshId = null)
        {
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var dataFilePath = Path.Combine(baseFolder, "data.json");

            if (File.Exists(dataFilePath))
            {
                var dataJson = File.ReadAllText(dataFilePath);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: No DT HD data found in file for {dthdId}"); return null; }
                var layoutData = JsonConvert.DeserializeObject<DtHdLayout>(dataJson);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: Cannot read data file for {dthdId}"); return null; }

                if (layoutData.ScanMeshes == null) { throw CreateException(DtHdErrorCode.NO_MESH_DATA, "No meshes to load"); }
                if (!layoutData.ScanMeshes.Any()) { throw CreateException(DtHdErrorCode.NO_MESH_DATA, "No meshes to load"); }

                var scanMeshesToLoad = layoutData.ScanMeshes;
                if (!string.IsNullOrEmpty(scanMeshId))
                {
                    var scanMesh = layoutData.ScanMeshes.FirstOrDefault(x => x.DtHdScanId == scanMeshId);
                    if (scanMesh != null) { scanMeshesToLoad = new List<ScanMesh> { scanMesh }; }
                }

                if (scanMeshesToLoad.Any())
                {
                    await _LoadScanMeshes(dthdId, scanMeshesToLoad);
                }
                else
                {
                    throw CreateException(DtHdErrorCode.NO_MESH_DATA, "No meshes to load");
                }

                _parent.transform.Rotate(-90, 0, 180);
            }
            else
            {
                // TODO: we should go download this data and still load everything...
                Debug.LogError($"Error :: No local DT HD data file for {dthdId}");
            }

            return _parent;
        }

        private async Task _LoadDtHdAsync(string dthdId, DtHdLayout layoutData)
        {
            // for building scan: /DTHD/{Hd Id}/Enhanced/SomeScan.glb
            // for all other assets: /DTHD/{Hd Id}/Assets/{dtHdAssetId}.glb

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var buildingFolder = Path.Combine(baseFolder, "Enhanced");
            var assetsFolder = Path.Combine(baseFolder, "Assets");
            if (!Directory.Exists(buildingFolder) || !Directory.Exists(assetsFolder)) { throw new Exception("Assets not downloaded!"); }

            _parent = new GameObject($"DTHDScene_{dthdId}");
            _parent.transform.position = Vector3.zero;

            // load the main layout
            await ImportDtMesh($"{buildingFolder}/Enhanced.glb", layoutData, "DtHdLayout", _parent);

            LoadedAssets = new Dictionary<string, GameObject>();

            // load the assets
            foreach (var asset in layoutData.Assets)
            {
                await ImportDtMesh($"{assetsFolder}/{asset.DtHdAssetId}.glb", asset, "DtHdAsset", _parent);
            }

            // load the asset items (instances)
            Debug.Log($"[Sturfee.DigitalTwin.HD]:DtHdSceneLoader :: Loading Asset Instances...");
            try
            {
                LoadAssetItems(layoutData.Assets);
            }
            catch (Exception ex)
            {
                Debug.Log($"[Sturfee.DigitalTwin.HD]:DtHdSceneLoader :: ERROR Loading Asset Instances");
                Debug.Log(ex);
            }            
        }

        private async Task _LoadScanMeshes(string dthdId, List<ScanMesh> scanMeshes)
        {
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var scanMeshFolder = Path.Combine(baseFolder, "ScanMeshes");
            if (!Directory.Exists(scanMeshFolder))
            {
                throw CreateException(DtHdErrorCode.DATA_NOT_DOWNLOADED, "Scan meshes not downloaded!");
            }

            _parent = new GameObject($"DTHDScans_{scanMeshes.Count}_{dthdId}");
            _parent.transform.position = Vector3.zero;

            // load all scan meshes
            foreach (var scanmesh in scanMeshes)
            {
                if (!string.IsNullOrEmpty(scanmesh.ScanMeshUrl) && scanmesh.Status != "ARCHIVED")
                {
                    await ImportDtMesh($"{scanMeshFolder}/{scanmesh.DtHdScanId}.glb", scanmesh, "DtHdScanMesh", _parent);
                }
            }
        }

        private async Task ImportDtMesh(string filePath, object data, string dataType, GameObject parent)
        {
            //var _importOptions = new ImportOptions
            //{
            //    DataLoader = new FileLoader(Path.GetDirectoryName(filePath)),
            //    AsyncCoroutineHelper = _parent.AddComponent<AsyncCoroutineHelper>(),
            //};

            Debug.Log($"DtHdSceneLoader :: Khronos :: Loading file = {filePath}");

            try
            {
                //var _importer = new GLTFSceneImporter(filePath, _importOptions);

                //_importer.Collider = GLTFSceneImporter.ColliderType.Mesh;
                //_importer.SceneParent = parent.transform;

                ////await _importer.LoadSceneAsync(-1, true, (go, err) => { OnFinishAsync(data, dataType, filePath, go, err); });
                //await _importer.LoadSceneAsync(-1, true, (go, err) => { OnMeshLoaded(data, dataType, filePath, go, err); });

                byte[] glbData = File.ReadAllBytes(filePath);
                var gltf = new GltfImport();
                bool success = await gltf.LoadGltfBinary(
                    glbData,
                    // The URI of the original data is important for resolving relative URIs within the glTF
                    new Uri(filePath)
                    );
                if (success)
                {
                    var go = new GameObject($"GLTF_SCENE");
                    go.transform.SetParent(parent.transform);
                    success = await gltf.InstantiateMainSceneAsync(go.transform);
                    foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                    {
                        mf.gameObject.AddComponent<MeshCollider>();
                    }
                    OnMeshLoaded(data, dataType, filePath, go, null);
                }

            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private void OnMeshLoaded(object data, string objectType, string filePath, GameObject result, ExceptionDispatchInfo info)
        {
            if (result == null)
            {
                Debug.LogError($"DtHdSceneLoader :: ERROR loading GLTF => {filePath}\nERR: {info.SourceException}");
                return;
            }

            Debug.Log($"DtHdSceneLoader :: loaded GLTF ({filePath})");

            var separators = new char[] {
              Path.DirectorySeparatorChar,
              Path.AltDirectorySeparatorChar
            };

            var filename = Path.GetFileNameWithoutExtension(filePath);
            result.name = filename;

            result.transform.localScale = Vector3.one;
            //result.transform.Rotate(-90, 0, 180);

            try
            {
                if (objectType == "DtHdLayout")
                {
                    // create a spawn point dummy
                    var layoutData = data as DtHdLayout;
                    var helper = result.AddComponent<DtHdLayoutHelper>();
                    helper.DtHdId = $"{layoutData.DtHdId}";

                    result.gameObject.name = $"DtHdLayout_{layoutData.DtHdId}";

                    helper.SpawnPoint = new GameObject($"DtHdSpawnPoint");
                    helper.SpawnPoint.transform.SetParent(result.transform);
                    helper.SpawnPoint.transform.localPosition = new Vector3(layoutData.SpawnPositionX, layoutData.SpawnPositionY, layoutData.SpawnPositionZ);
                    helper.SpawnPoint.transform.Rotate(new Vector3(layoutData.SpawnHeading - 90, 90, 90));// -90 points north

                    // // set the position
                    // result.transform.position = Converters.GeoToUnityPosition(layoutData.Location);

                    Enhanced = result;
                }

                if (objectType == "DtHdAsset")
                {
                    var assetData = data as DtHdAsset;

                    if (Enhanced != null)
                    {
                        result.transform.SetParent(Enhanced.transform);
                    }
                    else
                    {
                        result.transform.SetParent(_parent.transform);
                    }

                    var prefab = result.AddComponent<DtHdAssetPrefab>();
                    prefab.PrefabId = $"{assetData.DtHdAssetId}";
                    result.gameObject.SetActive(false);

                    if (LoadedAssets != null)
                    {
                        LoadedAssets[assetData.DtHdAssetId] = result;
                    }
                }

                if (objectType == "DtHdScanMesh")
                {
                    var scanMeshData = data as ScanMesh;

                    result.transform.position = Converters.GeoToUnityPosition(scanMeshData.ScanLocation);
                    //Debug.Log("POSFADSFFF");

                    // FOR TEST SCAN MESH
                    //result.transform.Rotate(-90, 0, 180);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            foreach (MeshRenderer mr in result.transform.GetComponentsInChildren<MeshRenderer>())
            {
                try
                {
                    // force white base color and non-metallic
                    if (mr.material.mainTexture != null)
                    {
                        mr.material.color = Color.white;
                    }
                    if (mr.material.HasProperty("_Metallic"))
                    {
                        mr.material.SetFloat("_Metallic", 0);
                    }
                    if (mr.material.name.ToLower().Contains("mirror"))
                    {
                        mr.material.SetFloat("_Metallic", 0.9f);
                    }
                    if (mr.material.name.ToLower().Contains("metal"))
                    {
                        mr.material.SetFloat("_Metallic", 0.8f);
                    }

                    //if (mr.material.HasProperty("_Glossiness")) // _MetallicGlossMap
                    //{
                    //    mr.material.SetFloat("_Glossiness", 1);
                    //}

                    if (mr.material.HasProperty("_MetallicGlossMap")) // _MetallicGlossMap
                    {
                        var metaalicRoughnessMap = mr.material.GetTexture("_MetallicGlossMap");
                        if (metaalicRoughnessMap != null)
                        {
                            if (mr.material.HasProperty("_Glossiness")) // _MetallicGlossMap
                            {
                                mr.material.SetFloat("_Glossiness", 1);
                            }
                        }
                    }

                    if (mr.material.HasProperty("roughnessFactor"))
                    {
                        if (!mr.material.name.ToLower().Contains("mirror"))
                        {
                            mr.material.SetFloat("roughnessFactor", 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log($"[Sturfee.DigitalTwin.HD]:DtHdSceneLoader :: ERROR overriding GLTF materials");
                    Debug.Log(ex);
                }
            }

        }

        private void LoadAssetItems(List<DtHdAsset> assets)
        {
            foreach (var asset in assets)
            {
                foreach (var assetItem in asset.Items)
                {
                    if (LoadedAssets.ContainsKey(asset.DtHdAssetId))
                    {
                        var obj = Instantiate(LoadedAssets[asset.DtHdAssetId], Enhanced.transform);

                        var assetData = obj.AddComponent<DtHdAssetInstance>();
                        assetData.AssetId = $"{asset.DtHdAssetId}";
                        assetData.AssetItemId = $"{assetItem.DtHdAssetItemId}";

                        Destroy(obj.GetComponent<DtHdAssetPrefab>());

                        obj.SetActive(true);
                        obj.transform.localPosition = new Vector3((float)assetItem.LocalX, (float)assetItem.LocalY, (float)assetItem.LocalZ);
                    }
                    else
                    {
                        Debug.LogError($"No Asset Prefab found with ID = {asset.DtHdAssetId} (for instance {assetItem.DtHdAssetItemId})");
                    }
                }
                //LoadedAssets[asset.DtHdAssetId].SetActive(false);
            }
        }

        private async Task LoadLightingAndReflections(string file, Transform baseParent, string type)
        {
            var envDataJson = await File.ReadAllTextAsync(file);

            if (!string.IsNullOrEmpty(envDataJson))
            {
                var envData = JsonConvert.DeserializeObject<DtEnvironment>(envDataJson);

                if (envData != null)
                {
                    if (type == "Cesium" && envData.Cesium == null) { return; }
                    if (type == "Unity" && envData.Unity == null) { return; }

                    var parent = new GameObject($"Lighting-and-Reflections");
                    parent.transform.SetParent(baseParent);//Enhanced.transform);
                    parent.transform.localPosition = Vector3.zero;

                    UnityReflectionProbe[] reflectionProbes = new UnityReflectionProbe[] { };
                    if (type == "Cesium")
                    {
                        reflectionProbes = envData.Cesium.ReflectionProbes;
                    }
                    if (type == "Unity")
                    {
                        reflectionProbes = envData.Unity.ReflectionProbes;
                    }

                    foreach (var dtReflection in reflectionProbes)
                    {
                        try
                        {
                            LoadReflectionProbe(dtReflection, parent);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex);
                        }
                    }

                    UnityLight[] lights = new UnityLight[] { };
                    if (type == "Cesium")
                    {
                        lights = envData.Cesium.Lights;
                    }
                    if (type == "Unity")
                    {
                        lights = envData.Unity.Lights;
                    }

                    foreach (var light in lights)
                    {
                        try
                        {
                            LoadLights(light, parent);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex);
                        }
                    }
                }
            }
        }

        private void LoadReflectionProbe(UnityReflectionProbe data, GameObject parent)
        {
            var reflectionProbe = new GameObject($"{data.Name}").AddComponent<ReflectionProbe>();
            reflectionProbe.gameObject.transform.SetParent(parent.transform);
            reflectionProbe.gameObject.transform.localPosition = new Vector3(data.LocalX, data.LocalY, data.LocalZ);
            reflectionProbe.importance = data.Importance;
            reflectionProbe.intensity = data.Intensity;
            reflectionProbe.boxProjection = data.BoxProjection;
            reflectionProbe.size = new Vector3(data.BoxSizeX, data.BoxSizeY, data.BoxSizeZ);

            reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            reflectionProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;

            reflectionProbe.resolution = 256;

            reflectionProbe.RenderProbe();
        }

        private void LoadLights(UnityLight data, GameObject parent)
        {
            var light = new GameObject($"{data.Name}").AddComponent<Light>();
            light.gameObject.transform.SetParent(parent.transform);
            light.gameObject.transform.localPosition = new Vector3(data.LocalX, data.LocalY, data.LocalZ);
            light.gameObject.transform.localRotation = new Quaternion(data.RotationX, data.RotationY, data.RotationZ, data.RotationW);

            light.type = GetLightType(data);
            light.range = data.Range;
            light.spotAngle = data.SpotAngle;
            light.color = new Color(data.ColorR, data.ColorG, data.ColorB);
            light.intensity = data.Intensity;
            light.shadows = GetShadowType(data);
            light.shadowStrength = data.ShadowStrength;
            // light.lightmapBakeType = GetLightMode(data); // editor only
        }

        private LightType GetLightType(UnityLight uLight)
        {
            switch (uLight.LightType)
            {
                case SturfeeLightType.Directional:
                    return LightType.Directional;
                case SturfeeLightType.Spot:
                    return LightType.Spot;
                case SturfeeLightType.Point:
                    return LightType.Point;

                default:
                    return LightType.Point;
            }
        }

        private LightShadows GetShadowType(UnityLight uLight)
        {
            switch (uLight.ShadowType)
            {
                case ShadowType.NoShadows:
                    return LightShadows.None;
                case ShadowType.HardSadows:
                    return LightShadows.Hard;
                case ShadowType.SoftShadows:
                    return LightShadows.Soft;

                default:
                    return LightShadows.None;
            }
        }

        private LightmapBakeType GetLightMode(UnityLight uLight)
        {
            switch (uLight.LightMode)
            {
                case LightMode.RealTime:
                    return LightmapBakeType.Realtime;
                case LightMode.Baked:
                    return LightmapBakeType.Baked;
                case LightMode.Mixed:
                    return LightmapBakeType.Mixed;

                default:
                    return LightmapBakeType.Realtime;
            }
        }

        private Exception CreateException(DtHdErrorCode statusCode, string statusMessage)
        {
            var ex = new Exception(string.Format("{0} - {1}", statusMessage, statusCode));
            ex.Data.Add(statusCode, statusMessage);  // store "3" and "Invalid Parameters"
            return ex;
        }
    }
}
