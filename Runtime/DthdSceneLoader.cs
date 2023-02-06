using Newtonsoft.Json;
using Sturfee.XRCS.Utils;
using SturfeeVPS.Core;
using SturfeeVPS.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Loader;

namespace Sturfee.DigitalTwin.HD
{
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
                await _LoadDtHdAsync(dthdId, layoutData);

                _parent.transform.Rotate(-90, 0, 180);
            }
            else
            {
                // TODO: we should go download this data and still load everything...
                Debug.LogError($"Error :: No local DT HD data file for {dthdId}");
            }            
        }

        public async Task LoadAssetsAsync(string dthdId, DtHdLayout layoutData)
        {
            // for building scan: /DTHD/{Hd Id}/Enhanced/SomeScan.glb
            // for all other assets: /DTHD/{Hd Id}/Assets/{dtHdAssetId}.glb

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var buildingFolder = Path.Combine(baseFolder, "Enhanced");
            var assetsFolder = Path.Combine(baseFolder, "Assets");
            if (!Directory.Exists(buildingFolder) || !Directory.Exists(assetsFolder)) { throw new Exception("Assets not downloaded!"); }

            _parent = new GameObject("DTHDScene"); 
            _parent.transform.position = Vector3.zero;

            // await LoadBuildingAsync($"{BuildingFolder}/Enhanced.glb");
            await LoadAssetAsync($"{buildingFolder}/Enhanced.glb", (go, err) =>
            {
                if (go != null)
                {
                }
                else
                {
                }
            });

            var building = _parent.transform.GetChild(0);
            var utmRef = new UtmPosition();
            utmRef.X = layoutData.RefX;
            utmRef.Y = layoutData.RefY;
            utmRef.Z = layoutData.RefZ;
            var gps = GeoCoordinateConverter.UtmToGps(utmRef);
            building.transform.position = Converters.GeoToUnityPosition(gps);
            // Building.transform.position = Converters.GeoToUnityPosition(_SceneData.Location);

            LoadedAssets = new Dictionary<string, GameObject>();

            foreach (DtHdAsset i in layoutData.Assets)
            {
                var _filePath = $"{assetsFolder}/{i.DtHdAssetId}.glb";
                await LoadAssetAsync(_filePath, (go, err) =>
                {
                    if (go != null)
                    {
                    }
                    else
                    {
                    }
                });
            }

            foreach (DtHdAsset i in layoutData.Assets)
            {
                foreach (DtHdAssetItem j in i.Items)
                {
                    var _obj = Instantiate(LoadedAssets[i.DtHdAssetId]);
                    _obj.transform.SetParent(Enhanced.transform);
                    _obj.transform.localPosition = new Vector3((float)j.LocalX, (float)j.LocalY, (float)j.LocalZ);
                }
                // Destroy(LoadedAssets[i.DtHdAssetId]);
                LoadedAssets[i.DtHdAssetId].SetActive(false);
            }
        }

        private async Task LoadAssetAsync(string filePath, Action<GameObject, ExceptionDispatchInfo> onComplete = null)
        {
            var _importOptions = new ImportOptions
            {
                DataLoader = new FileLoader(Path.GetDirectoryName(filePath)),
                AsyncCoroutineHelper = gameObject.AddOrGetComponent<AsyncCoroutineHelper>(),
            };

            try
            {
                var _importer = new GLTFSceneImporter(filePath, _importOptions);

                _importer.Collider = GLTFSceneImporter.ColliderType.Mesh;
                _importer.SceneParent = _parent.transform;
                UnityEngine.Debug.Log(_parent.transform.name + " parent name");

                await _importer.LoadSceneAsync(
                    -1,
                    true, 
                    (go, err) => 
                    {
                        onComplete?.Invoke(go, err);
                        OnFinishAsync(filePath, go, err);
                    }
                );
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private void OnFinishAsync(string filePath, GameObject result, ExceptionDispatchInfo info)
        {
            if (result == null)
            {
                return;
            }

            var separators = new char[] {
              Path.DirectorySeparatorChar,
              Path.AltDirectorySeparatorChar
            };
            var parts = filePath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            var tileId = parts[parts.Length - 2];

            var filename = Path.GetFileNameWithoutExtension(filePath);
            result.name = filename;
            //var obj = new GameObject($"{filename}");

            result.transform.localScale = Vector3.one;
            result.transform.Rotate(-90, 180, 0);


            var tileObj = GameObject.Find(tileId);
            if (tileObj == null)
            {
                tileObj = new GameObject(tileId);
                tileObj.transform.SetParent(_parent.transform);
                result.transform.SetParent(tileObj.transform);
                // _loadedTiles.Add(new LoadedTile
                // {
                //     Tile = tileObj,
                //     Geohash = tileId
                // });
            }
            else 
            {
                result.transform.SetParent(tileObj.transform);
            }

            // handles to enhanced mesh GameObject and Asset GameObjects
            if (LoadedAssets != null)
            {
                LoadedAssets[filename] = result;
            }
            else
            {
                Enhanced = result;
            }

            foreach (MeshRenderer mr in result.transform.GetComponentsInChildren<MeshRenderer>())
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

            }

        }



        public async Task _LoadDtHdAsync(string dthdId, DtHdLayout layoutData)
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

            // load the environment (reflection probes, lighting, etc)
            if (File.Exists($"{baseFolder}/environment.json"))
            {
                await LoadLightingAndReflections($"{baseFolder}/environment.json");
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
                    var helper = result.AddComponent<DtHadLayoutHelper>();
                    helper.DtHdId = $"{layoutData.DtHdId}";

                    result.gameObject.name = $"DtHdLayout_{layoutData.DtHdId}";

                    helper.SpawnPoint = new GameObject($"DtHdSpawnPoint");
                    helper.SpawnPoint.transform.SetParent(result.transform);

                    // only for testing
                    if (layoutData.DtHdId == "3745b04f-7465-4533-b84f-406690685845")
                    {
                        helper.SpawnPoint.transform.localPosition = new Vector3(3.31999993f, 5.73000002f, 0.693000019f);
                        helper.SpawnPoint.transform.Rotate(new Vector3(180 - 90, 90, 90));
                    }
                    else
                    {
                        helper.SpawnPoint.transform.localPosition = new Vector3(layoutData.SpawnPositionX, layoutData.SpawnPositionY, layoutData.SpawnPositionZ);
                        helper.SpawnPoint.transform.Rotate(new Vector3(layoutData.SpawnHeading - 90, 90, 90));// -90 points north
                    }

                    // set the position
                    var utmRef = new UtmPosition();
                    utmRef.X = layoutData.RefX;
                    utmRef.Y = layoutData.RefY;
                    utmRef.Z = layoutData.RefZ;
                    var gps = GeoCoordinateConverter.UtmToGps(utmRef);
                    result.transform.position = Converters.GeoToUnityPosition(gps);

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
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            foreach (MeshRenderer mr in result.transform.GetComponentsInChildren<MeshRenderer>())
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
                    foreach (var dtReflection in envData.Unity.ReflectionProbes)
                    {
                        LoadReflectionProbe(dtReflection, Enhanced);
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
            reflectionProbe.size = Vector3.one * data.BoxSize;

            reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            reflectionProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;

            reflectionProbe.resolution = 256;

            reflectionProbe.RenderProbe();
        }
    }
}
