using Newtonsoft.Json;
using SturfeeVPS.Core;
using System;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace Sturfee.DigitalTwin.HD
{
    public interface IDthdSceneDataProvider
    {
        Task<SceneData> FetchSceneData(string DthdId);
        Task DownloadAllAssets(string DthdId, SceneData _SceneData);
    }

    public class DthdSceneDataProvider : IDthdSceneDataProvider
    {
        public DthdSceneDataProvider()
        {
            ServicePointManager.DefaultConnectionLimit = 1000; 
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public async Task<SceneData> FetchSceneData(string DthdId)
        {
            // get download URL
            string url = DTHDConstants.DTHD_API + "/" + DthdId + "?full_details=true";

            try
            {               
                var uwr = new UnityWebRequest(url);

                var dh = new DownloadHandlerBuffer();
                uwr.downloadHandler = dh;

                uwr.method = UnityWebRequest.kHttpVerbGET;
                await uwr.SendWebRequest();
                
                

                if (uwr.result == UnityWebRequest.Result.ConnectionError) //uwr.isNetworkError || uwr.isHttpError)
                {
                    Debug.Log("error downloading");
                }
                else
                {
                    var item = JsonConvert.DeserializeObject<SceneData>(uwr.downloadHandler.text);
                    Debug.Log(uwr.downloadHandler.text);
                    return item;
                }

            }
            catch (Exception e)
            {           
            }

            return null;
        }
    
        public async Task DownloadAllAssets(string DthdId, SceneData _SceneData)
        {
            // for building scan: /DTHD/{Hd Id}/Enhanced/SomeScan.glb
            // for all other assets: /DTHD/{Hd Id}/Assets/{dtHdAssetId}.glb

            var BuildingFolder = Path.Combine(Application.persistentDataPath, "DTHD", DthdId,"Enhanced");
            var AssetsFolder = Path.Combine(Application.persistentDataPath, "DTHD", DthdId, "Assets");
            if (!Directory.Exists(BuildingFolder)) { Directory.CreateDirectory(BuildingFolder); }
            if (!Directory.Exists(AssetsFolder)) { Directory.CreateDirectory(AssetsFolder); }

            // get download URL
            string url = _SceneData.EnhancedMesh;


            // download building mesh
            try
            {
                if (File.Exists($"{BuildingFolder}/Enhanced.glb")) throw new Exception("Building Scan already downloaded");

                var uwr = new UnityWebRequest(url);

                uwr.method = UnityWebRequest.kHttpVerbGET;
                var dh = new DownloadHandlerFile($"{BuildingFolder}/Enhanced.glb");
                dh.removeFileOnAbort = true;
                uwr.downloadHandler = dh;
                await uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success) //(uwr.result == UnityWebRequest.Result.ConnectionError) //uwr.isNetworkError || uwr.isHttpError)
                {
                }
                else
                {
                }

            }
            catch (Exception e)
            {               
            }

            // download assets
            if (_SceneData.Assets == null) return;
            foreach (Asset i in _SceneData.Assets)
            {
                try
                {
                    if (File.Exists($"{AssetsFolder}/{i.DtHdAssetId}.glb")) throw new Exception("Asset already downloaded");;

                    var uwr = new UnityWebRequest(i.FileUrl);

                    uwr.method = UnityWebRequest.kHttpVerbGET;
                    var dh = new DownloadHandlerFile($"{AssetsFolder}/{i.DtHdAssetId}.glb");
                    dh.removeFileOnAbort = true;
                    uwr.downloadHandler = dh;
                    await uwr.SendWebRequest();

                    if (uwr.result != UnityWebRequest.Result.Success) //(uwr.result == UnityWebRequest.Result.ConnectionError) //uwr.isNetworkError || uwr.isHttpError)
                    {
                    }
                    else
                    {
                    }

                }
                catch (Exception e)
                {               
                }
            }
            
        }
    }
}
