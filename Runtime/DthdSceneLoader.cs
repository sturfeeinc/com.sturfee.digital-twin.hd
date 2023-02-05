//using GLTFast;
using Newtonsoft.Json;
using NGeoHash;
using Sturfee.XRCS.Config;
using Sturfee.XRCS.Utils;
using SturfeeVPS.Core;
using SturfeeVPS.SDK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityGLTF;
using UnityGLTF.Loader;
using DebugWatch = System.Diagnostics.Stopwatch;

namespace Sturfee.DigitalTwin.HD
{
    public class DthdSceneLoader : SimpleSingleton<DthdSceneLoader>
    {
        private GameObject _parent;
        private GameObject Enhanced;
        private Dictionary<string, GameObject> LoadedAssets;

        public async Task LoadAssetsAsync(string DthdId, SceneData _SceneData)
        {
            // for building scan: /DTHD/{Hd Id}/Enhanced/SomeScan.glb
            // for all other assets: /DTHD/{Hd Id}/Assets/{dtHdAssetId}.glb

            var BuildingFolder = Path.Combine(Application.persistentDataPath, "DTHD", DthdId,"Enhanced");
            var AssetsFolder = Path.Combine(Application.persistentDataPath, "DTHD", DthdId, "Assets");
            if (!Directory.Exists(BuildingFolder) || !Directory.Exists(AssetsFolder)) { throw new Exception("Assets not downloaded!"); }

            _parent = new GameObject("DTHDScene"); 
            _parent.transform.position = Vector3.zero;

            // await LoadBuildingAsync($"{BuildingFolder}/Enhanced.glb");
            await LoadAssetAsync($"{BuildingFolder}/Enhanced.glb", (go, err) =>
            {
                if (go != null)
                {
                }
                else
                {
                }
            });

            var Building = _parent.transform.GetChild(0);
            var UtmRef = new UtmPosition();
            UtmRef.X = _SceneData.RefX;
            UtmRef.Y = _SceneData.RefY;
            UtmRef.Z = _SceneData.RefZ;
            var GPS = GeoCoordinateConverter.UtmToGps(UtmRef);
            Building.transform.position = Converters.GeoToUnityPosition(GPS);
            // Building.transform.position = Converters.GeoToUnityPosition(_SceneData.Location);

            LoadedAssets = new Dictionary<string, GameObject>();

            foreach (Asset i in _SceneData.Assets)
            {
                var _filePath = $"{AssetsFolder}/{i.DtHdAssetId}.glb";
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

            foreach (Asset i in _SceneData.Assets)
            {
                foreach (AssetItem j in i.Items)
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
    }
}
