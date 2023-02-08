using Newtonsoft.Json;
using SturfeeVPS.Core;
using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace Sturfee.DigitalTwin.HD
{
    public interface IDtHdProvider
    {
        bool IsCached(string dthdId);
        Task<DtHdLayout> DownloadDtHd(string dthdId);
        void DeleteCachedData(string dthdId);
    }

    public class DthdSceneDataProvider : IDtHdProvider
    {
        public DthdSceneDataProvider()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public bool IsCached(string dthdId)
        {
            // TODO: should set up some cache expiration strategy...

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            if(Directory.Exists(baseFolder))
            {
                var files = Directory.GetFiles(baseFolder);
                if (files.Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<DtHdLayout> DownloadDtHd(string dthdId)
        {
            try
            {
                var dtHdLayout = await FetchSceneData(dthdId);
                if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }
                await DownloadAllAssets(dthdId, dtHdLayout);
                return dtHdLayout;
            }
            catch (Exception e)
            {
                Debug.LogError("error downloading");
                throw;
            }
        }

        public void DeleteCachedData(string dthdId)
        {
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            if (Directory.Exists(baseFolder))
            {
                Directory.Delete(baseFolder, true);
            }
        }

        private async Task<DtHdLayout> FetchSceneData(string DthdId)
        {
            // get download URL
            string url = DTHDConstants.DTHD_API + "/" + DthdId + "?full_details=true";

            SturfeeDebug.Log($"Fetching HD DT => {url}");

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
                    var item = JsonConvert.DeserializeObject<DtHdLayout>(uwr.downloadHandler.text);
                    Debug.Log(uwr.downloadHandler.text);
                    return item;
                }

            }
            catch (Exception e)
            {
            }

            return null;
        }

        private async Task DownloadAllAssets(string DthdId, DtHdLayout layoutData)
        {
            // for building scan: /DTHD/{Hd Id}/Enhanced/SomeScan.glb
            // for all other assets: /DTHD/{Hd Id}/Assets/{dtHdAssetId}.glb

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", DthdId);
            var buildingFolder = Path.Combine(baseFolder, "Enhanced");
            var assetsFolder = Path.Combine(baseFolder, "Assets");
            if (!Directory.Exists(buildingFolder)) { Directory.CreateDirectory(buildingFolder); }
            if (!Directory.Exists(assetsFolder)) { Directory.CreateDirectory(assetsFolder); }

            // save the data file
            File.WriteAllText(Path.Combine(baseFolder, "data.json"), JsonConvert.SerializeObject(layoutData));

            var downloadTasks = new List<Task>();

            // download the main layout
            downloadTasks.Add(DownloadFile(layoutData.EnhancedMesh, $"{buildingFolder}/Enhanced.glb"));

            // download the additional assets
            if (layoutData.Assets != null)
            {
                foreach (var asset in layoutData.Assets)
                {
                    downloadTasks.Add(DownloadFile(asset.FileUrl, $"{assetsFolder}/{asset.DtHdAssetId}.glb"));
                }
            }

            // download the environment file
            downloadTasks.Add(DownloadFile(layoutData.DtEnvironmentUrl, $"{baseFolder}/environment.json"));

            await Task.WhenAll(downloadTasks);
        }


        private async Task DownloadFile(string url, string file)
        {
            try
            {
                if (File.Exists($"{file}")) throw new Exception($"File already downloaded ({file})");

                var uwr = new UnityWebRequest(url);

                uwr.method = UnityWebRequest.kHttpVerbGET;
                var dh = new DownloadHandlerFile($"{file}");
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
                throw;
            }
        }
    }
}
