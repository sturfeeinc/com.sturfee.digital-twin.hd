using Newtonsoft.Json;
using SturfeeVPS.Core;
using SturfeeVPS.Core.Models;
using SturfeeVPS.Core.Constants;
using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;


namespace Sturfee.DigitalTwin.HD
{
    /// <summary>
    /// Interface for DTHD scene data providers
    /// </summary>
    public interface IDtHdProvider
    {
        bool IsCached(string dthdId);
        bool AreAllScansCached(string dthdId);
        bool IsScanCached(string dthdId, string scanId);
        Task<DtHdLayout> DownloadDtHd(string dthdId);
        Task DownloadAllScanMeshes(string dthdId);
        Task DownloadScanMesh(string dthdId, string scanId);
        void DeleteCachedData(string dthdId);

        Task<bool> IsEnchanceMeshCachedAsync(string dthdId);
        Task<bool> AreAllScansCachedAsync(string dthdId);

        Task RefreshCacheInfoAsync(string dtHdId);
        Task<DtHdLayout> GetDtHdLayout(string dtHdId, bool skipCache = false);
        Task<ScanMesh> GetScanMesh(string dtHdId, string scanId, bool skipCache = false);
    }

    /// <summary>
    /// Provides methods for obtaining non-meta scene data in ".glb" format. Contains other helper functions.
    /// </summary>
    public class DthdSceneDataProvider : IDtHdProvider
    {
        // Directories
        private string baseFolder, buildingFolder, scanMeshFolder, assetsFolder;
        public DthdSceneDataProvider()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        /// <summary>
        /// Gets the DTHD Layout data from local cache, or fetches it from the server
        /// </summary>
        /// <param name="dtHdId">DTHD ID</param>
        /// <param name="skipCache">Skip the cache and go to the server</param>
        public async Task<DtHdLayout> GetDtHdLayout(string dtHdId, bool skipCache = false)
        {
            var dtHdLayout = await FetchSceneData(dtHdId, skipCache);
            if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dtHdId);
            File.WriteAllText(Path.Combine(baseFolder, "data.json"), JsonConvert.SerializeObject(dtHdLayout));
            return dtHdLayout;
        }

        /// <summary>
        /// Gets the DT Scan Mesh data from local cache, or fetches it from the server
        /// </summary>
        /// <param name="dtHdId">DT HD ID of the layout that the scan belongs to</param>
        /// <param name="scanId">Scan ID that you want data for</param>
        /// <param name="skipCache">Skip the cache and go to the server</param>
        public async Task<ScanMesh> GetScanMesh(string dtHdId, string scanId, bool skipCache = false)
        {
            var dtHdLayout = await FetchSceneData(dtHdId, skipCache);
            if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }

            if (skipCache)
            {
                var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dtHdId);
                File.WriteAllText(Path.Combine(baseFolder, "data.json"), JsonConvert.SerializeObject(dtHdLayout));
            }

            var scanMeshData = dtHdLayout.ScanMeshes.FirstOrDefault(x => x.DtHdScanId == scanId);
            if (scanMeshData != null)
            {
                return scanMeshData;
            }
            return null;

            // var scanData = await FetchScanData(dtHdId, scanId, skipCache);
            // if (scanData == null) { throw new ArgumentException($"Invalid DT HD ID"); }
            // var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dtHdId);
            // File.WriteAllText(Path.Combine(baseFolder, "data.json"), JsonConvert.SerializeObject(scanData));
        }


        /// <summary>
        /// Downloads DTHD scene data even if it exists in local cache.
        /// </summary>
        /// <param name="dtHdId">DTHD ID</param>
        public async Task RefreshCacheInfoAsync(string dtHdId)
        {
            var dtHdLayout = await FetchSceneData(dtHdId, true);
            if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dtHdId);
            File.WriteAllText(Path.Combine(baseFolder, "data.json"), JsonConvert.SerializeObject(dtHdLayout));
        }

        /// <summary>
        /// Checks if DTHD data is cached
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <returns>boolean response</returns>
        public bool IsCached(string dthdId)
        {
            // TODO: should set up some cache expiration strategy...

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            if (Directory.Exists(baseFolder))
            {
                var files = Directory.GetFiles(baseFolder);
                if (files.Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if enhanced building mesh for associated DTHD ID is locally cached.
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <returns>boolean response</returns>
        public async Task<bool> IsEnchanceMeshCachedAsync(string dthdId)
        {
            // this checks against the server
            var dtHdLayout = await FetchSceneData(dthdId);
            if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }

            if (string.IsNullOrEmpty(dtHdLayout.EnhancedMesh)) { return true; }

            var filePath = Path.Combine(Application.persistentDataPath, "DTHD", dthdId, "Enhanced", "Enhanced.glb");
            if (File.Exists(filePath))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a scan mesh for associated DTHD ID is locally cached.
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <param name="scanId">Scan mesh ID</param>
        /// <returns>boolean response</returns>
        public bool IsScanCached(string dthdId, string scanId)
        {
            // TODO: should set up some cache expiration strategy...

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var scanMeshesFolder = Path.Combine(baseFolder, "ScanMeshes");
            if (Directory.Exists(scanMeshesFolder))
            {
                if (File.Exists(Path.Combine(scanMeshesFolder, $"{scanId}.glb")))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if all associated scan meshes are locally cached.
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <returns>boolean response</returns>
        public bool AreAllScansCached(string dthdId)
        {
            // this only checks local files
            // TODO: should set up some cache expiration strategy...

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);

            var dataFilePath = Path.Combine(baseFolder, "data.json");
            if (File.Exists(dataFilePath))
            {
                var dataJson = File.ReadAllText(dataFilePath);
                var layoutData = JsonConvert.DeserializeObject<DtHdLayout>(dataJson);

                var scanIds = layoutData.ScanMeshes.Where(x => !string.IsNullOrEmpty(x.ScanMeshUrl)).Select(x => x.DtHdScanId);

                var scanMeshesFolder = Path.Combine(baseFolder, "ScanMeshes");
                if (Directory.Exists(scanMeshesFolder))
                {
                    var files = Directory.GetFiles(scanMeshesFolder);
                    var ids = files.Select(x => Path.GetFileNameWithoutExtension(x));
                    if (scanIds.ToHashSet().SetEquals(ids))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Asynchronously checks if all associated scan meshes are locally cached.
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <returns>boolean response</returns>
        public async Task<bool> AreAllScansCachedAsync(string dthdId)
        {
            // this checks against the server
            var dtHdLayout = await FetchSceneData(dthdId);
            if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }

            var scanIds = dtHdLayout.ScanMeshes.Where(x => !string.IsNullOrEmpty(x.ScanMeshUrl)).Select(x => x.DtHdScanId);

            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            var scanMeshesFolder = Path.Combine(baseFolder, "ScanMeshes");
            if (Directory.Exists(scanMeshesFolder))
            {
                var files = Directory.GetFiles(scanMeshesFolder);
                var ids = files.Select(x => Path.GetFileNameWithoutExtension(x));
                if (scanIds.ToHashSet().SetEquals(ids))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Downloader for enhanced building mesh and associated assets
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <returns>DTHD layout (scene data)</returns>
        public async Task<DtHdLayout> DownloadDtHd(string dthdId)
        {
            try
            {
                var dtHdLayout = await FetchSceneData(dthdId);
                if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }
                await DownloadAllAssets(dthdId, dtHdLayout);

                // FOR DEBUG
                // await AllScanMesh(dthdId);
                // Debug.Log($"[DthdSceneDataProvider] :: DTHD ID: {dthdId}");

                return dtHdLayout;
            }
            catch (Exception e)
            {
                Debug.LogError("error downloading");
                throw;
            }
        }

        /// <summary>
        /// Downloader for all scan meshes associated with a DTHD ID
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        public async Task DownloadAllScanMeshes(string dthdId)
        {
            try
            {
                SetUpDirectories(dthdId);
                var dtHdLayout = await FetchSceneData(dthdId);
                if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }
                await DownloadAllScanMeshes(dthdId, dtHdLayout);
                // return dtHdLayout;
            }
            catch (Exception e)
            {
                Debug.LogError("error downloading");
                throw;
            }
        }

        /// <summary>
        /// Downloader for a single scan mesh
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        /// <param name="scanId">Scan mesh ID</param>
        public async Task DownloadScanMesh(string dthdId, string scanId)
        {
            try
            {
                SetUpDirectories(dthdId);
                var dtHdLayout = await FetchSceneData(dthdId);
                if (dtHdLayout == null) { throw new ArgumentException($"Invalid DT HD ID"); }
                await DownloadScanMesh(dthdId, scanId, dtHdLayout);
                // return dtHdLayout;
            }
            catch (Exception e)
            {
                Debug.LogError("error downloading");
                throw;
            }
        }

        /// <summary>
        /// Deletes entire cached DTHD data.
        /// </summary>
        /// <param name="dthdId">DTHD ID</param>
        public void DeleteCachedData(string dthdId)
        {
            var baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", dthdId);
            if (Directory.Exists(baseFolder))
            {
                Directory.Delete(baseFolder, true);
            }
        }

        private async Task<DtHdLayout> FetchSceneData(string DthdId, bool forceRefresh = false)
        {
            SetUpDirectories(DthdId);
            Debug.Log($"[DthdSceneDataProvider] dthd id: {DthdId}, path: {baseFolder}");

            // if layout already exists, load the layout file
            var dataFilePath = Path.Combine(baseFolder, "data.json");
            if (File.Exists(dataFilePath) && !forceRefresh)
            {
                var dataJson = File.ReadAllText(dataFilePath);
                var layoutData = JsonConvert.DeserializeObject<DtHdLayout>(dataJson);
                return layoutData;
            }

            // get download URL
            string url = DtConstants.DTHD_LAYOUT + "/" + DthdId + "?full_details=true";

            SturfeeDebug.Log($"Fetching HD DT => {url}");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/json; charset=utf-8";
            // TODO: auth

            try
            {
                var response = await request.GetResponseAsync() as HttpWebResponse;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Debug.LogError($"ERROR:: API => {response.StatusCode} - {response.StatusDescription}");
                    Debug.LogError(response);
                    throw new Exception(response.StatusDescription);
                }

                StreamReader reader = new StreamReader(response.GetResponseStream());
                string jsonResponse = reader.ReadToEnd();

                Debug.Log(jsonResponse);

                var result = JsonConvert.DeserializeObject<DtHdLayout>(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting metadata for DT HD with ID = {DthdId}");
                Debug.LogError(ex);
                throw;
            }

            return null;
        }

        private async Task<ScanMesh> FetchScanData(string DthdId, string scanId, bool forceRefresh = false)
        {
            SetUpDirectories(DthdId);
            Debug.Log($"[DthdSceneDataProvider] dthd id: {DthdId}, scan id: {scanId}, path: {baseFolder}");

            // if layout already exists, load the layout file
            var dataFilePath = Path.Combine(baseFolder, "data.json");
            if (File.Exists(dataFilePath) && !forceRefresh)
            {
                var dataJson = File.ReadAllText(dataFilePath);
                var layoutData = JsonConvert.DeserializeObject<DtHdLayout>(dataJson);
                var scanMeshData = layoutData.ScanMeshes.FirstOrDefault(x => x.DtHdScanId == scanId);
                if (scanMeshData != null)
                {
                    return scanMeshData;
                }
            }

            // get download URL
            string url = $"{DtConstants.DTHD_LAYOUT}/{DthdId}/scan/{scanId}";

            SturfeeDebug.Log($"Fetching Scan Mesh data => {url}");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/json; charset=utf-8";
            // TODO: auth

            try
            {
                var response = await request.GetResponseAsync() as HttpWebResponse;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Debug.LogError($"ERROR:: API => {response.StatusCode} - {response.StatusDescription}");
                    Debug.LogError(response);
                    throw new Exception(response.StatusDescription);
                }

                StreamReader reader = new StreamReader(response.GetResponseStream());
                string jsonResponse = reader.ReadToEnd();

                Debug.Log(jsonResponse);

                var result = JsonConvert.DeserializeObject<ScanMesh>(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting metadata for DT HD with ID = {DthdId}");
                Debug.LogError(ex);
                throw;
            }

            return null;
        }

        private async Task DownloadAllAssets(string DthdId, DtHdLayout layoutData)
        {
            SetUpDirectories(DthdId);
            // save the data file
            if (!File.Exists(Path.Combine(baseFolder, "data.json")))
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


        private async Task DownloadAllScanMeshes(string DthdId, DtHdLayout layoutData)
        {
            // save the data file
            if (!File.Exists(Path.Combine(baseFolder, "data.json")))
                File.WriteAllText(Path.Combine(baseFolder, "data.json"), JsonConvert.SerializeObject(layoutData));

            var downloadTasks = new List<Task>();

            // download all scan meshes
            if (layoutData.ScanMeshes != null)
            {
                foreach (var scanmesh in layoutData.ScanMeshes)
                {
                    if (scanmesh.Status != "ARCHIVED")
                    {
                        downloadTasks.Add(DownloadFile(scanmesh.ScanMeshUrl, $"{scanMeshFolder}/{scanmesh.DtHdScanId}.glb"));
                    }
                }
            }

            await Task.WhenAll(downloadTasks);
        }

        private async Task DownloadScanMesh(string DthdId, string ScanId, DtHdLayout layoutData)
        {
            // save the data file
            if (!File.Exists(Path.Combine(baseFolder, "data.json")))
                File.WriteAllText(Path.Combine(baseFolder, "data.json"), JsonConvert.SerializeObject(layoutData));

            // download target scan mesh
            var fileUrl = layoutData.ScanMeshes.FirstOrDefault(a => a.DtHdScanId == ScanId).ScanMeshUrl;
            await DownloadFile(fileUrl, $"{scanMeshFolder}/{ScanId}.glb");
        }

        private void SetUpDirectories(string DthdId)
        {
            // for building scan: /DTHD/{Hd Id}/Enhanced/SomeScan.glb
            // for scan mesh: /DTHD/{Hd Id}/ScanMeshes/{Scan Id}.glb
            // for all other assets: /DTHD/{Hd Id}/Assets/{dtHdAssetId}.glb

            baseFolder = Path.Combine(Application.persistentDataPath, "DTHD", DthdId);
            buildingFolder = Path.Combine(baseFolder, "Enhanced");
            scanMeshFolder = Path.Combine(baseFolder, "ScanMeshes");
            assetsFolder = Path.Combine(baseFolder, "Assets");
            if (!Directory.Exists(buildingFolder)) { Directory.CreateDirectory(buildingFolder); }
            if (!Directory.Exists(scanMeshFolder)) { Directory.CreateDirectory(scanMeshFolder); }
            if (!Directory.Exists(assetsFolder)) { Directory.CreateDirectory(assetsFolder); }
        }

        private async Task DownloadFile(string url, string file)
        {
            try
            {
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
