using Newtonsoft.Json;
using Sturfee.XRCS.Utils;
using SturfeeVPS.Core;
using SturfeeVPS.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Loader;

namespace Sturfee.DigitalTwin.HD
{

    public enum DtHdErrorCode
    {
        NO_DT_HD_DATA,
        NO_MESH_DATA,
        DATA_NOT_DOWNLOADED,
        LOAD_ERROR
    }

    public class DtHdSceneLoader : SimpleSingleton<DtHdSceneLoader>
    {
        private GameObject _parent;
        private GameObject Enhanced;
        private Dictionary<string, GameObject> LoadedAssets;


        public async Task LoadDtHdAsync(string dthdId)
        {
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var dataFilePath = Path.Combine(baseFolder, "data.json");

            if (File.Exists(dataFilePath))
            {
                var dataJson = File.ReadAllText(dataFilePath);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: No DT HD data found in file for {dthdId}"); return; }
                var layoutData = JsonConvert.DeserializeObject<DtHdLayout>(dataJson);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: Cannot read data file for {dthdId}"); return; }

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
                if (File.Exists($"{baseFolder}/environment.json"))
                {
                    await LoadLightingAndReflections($"{baseFolder}/environment.json");
                }
                else if (File.Exists($"{baseFolder}/dt_environment.json"))
                {
                    await LoadLightingAndReflections($"{baseFolder}/dt_environment.json");
                }

                // set the position
                Enhanced.transform.position = Converters.GeoToUnityPosition(layoutData.Location);
            }
            else
            {
                // TODO: we should go download this data and still load everything...
                Debug.LogError($"Error :: No local DT HD data file for {dthdId}");
            }
        }

        public async Task LoadScanMeshAsync(string dthdId, string scanMeshId = null)
        {
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var dataFilePath = Path.Combine(baseFolder, "data.json");

            if (File.Exists(dataFilePath))
            {
                var dataJson = File.ReadAllText(dataFilePath);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: No DT HD data found in file for {dthdId}"); return; }
                var layoutData = JsonConvert.DeserializeObject<DtHdLayout>(dataJson);
                if (string.IsNullOrEmpty(dataJson)) { Debug.LogError($"Error :: Cannot read data file for {dthdId}"); return; }

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
            LoadAssetItems(layoutData.Assets);
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
                if (!string.IsNullOrEmpty(scanmesh.ScanMeshUrl))
                {
                    await ImportDtMesh($"{scanMeshFolder}/{scanmesh.DtHdScanId}.glb", scanmesh, "DtHdScanMesh", _parent);
                }
            }
        }

        private async Task ImportDtMesh(string filePath, object data, string dataType, GameObject parent)
        {
            var _importOptions = new ImportOptions
            {
                DataLoader = new FileLoader(Path.GetDirectoryName(filePath)),
                AsyncCoroutineHelper = _parent.AddComponent<AsyncCoroutineHelper>(),
            };

            Debug.Log($"DtHdSceneLoader :: Khronos :: Loading file = {filePath}");

            try
            {
                var _importer = new GLTFSceneImporter(filePath, _importOptions);

                _importer.Collider = GLTFSceneImporter.ColliderType.Mesh;
                _importer.SceneParent = parent.transform;

                //await _importer.LoadSceneAsync(-1, true, (go, err) => { OnFinishAsync(data, dataType, filePath, go, err); });
                await _importer.LoadSceneAsync(-1, true, (go, err) => { OnMeshLoaded(data, dataType, filePath, go, err); });
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
                Debug.Log($"MR HYDE: {mr}");
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

        private async Task LoadLightingAndReflections(string file)
        {
            var envDataJson = await File.ReadAllTextAsync(file);

            if (!string.IsNullOrEmpty(envDataJson))
            {
                var envData = JsonConvert.DeserializeObject<DtEnvironment>(envDataJson);

                if (envData != null && envData.Unity != null)
                {
                    var parent = new GameObject($"Lighting-and-Reflections");
                    parent.transform.SetParent(Enhanced.transform);
                    parent.transform.localPosition = Vector3.zero;

                    foreach (var dtReflection in envData.Unity.ReflectionProbes)
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

                    foreach (var light in envData.Unity.Lights)
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

        private UnityEngine.LightType GetLightType(UnityLight uLight)
        {
            switch (uLight.LightType)
            {
                case Sturfee.DigitalTwin.HD.LightType.Directional:
                    return UnityEngine.LightType.Directional;
                case Sturfee.DigitalTwin.HD.LightType.Spot:
                    return UnityEngine.LightType.Spot;
                case Sturfee.DigitalTwin.HD.LightType.Point:
                    return UnityEngine.LightType.Point;

                default:
                    return UnityEngine.LightType.Point;
            }
        }

        private UnityEngine.LightShadows GetShadowType(UnityLight uLight)
        {
            switch (uLight.ShadowType)
            {
                case Sturfee.DigitalTwin.HD.ShadowType.NoShadows:
                    return UnityEngine.LightShadows.None;
                case Sturfee.DigitalTwin.HD.ShadowType.HardSadows:
                    return UnityEngine.LightShadows.Hard;
                case Sturfee.DigitalTwin.HD.ShadowType.SoftShadows:
                    return UnityEngine.LightShadows.Soft;

                default:
                    return UnityEngine.LightShadows.None;
            }
        }

        private UnityEngine.LightmapBakeType GetLightMode(UnityLight uLight)
        {
            switch (uLight.LightMode)
            {
                case Sturfee.DigitalTwin.HD.LightMode.RealTime:
                    return UnityEngine.LightmapBakeType.Realtime;
                case Sturfee.DigitalTwin.HD.LightMode.Baked:
                    return UnityEngine.LightmapBakeType.Baked;
                case Sturfee.DigitalTwin.HD.LightMode.Mixed:
                    return UnityEngine.LightmapBakeType.Mixed;

                default:
                    return UnityEngine.LightmapBakeType.Realtime;
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
