using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace RedEden.ARES
{
    public enum ARESProvisionPhase
    {
        Idle,
        StartingRuntime,
        CheckingModel,
        DownloadingModel,
        DownloadingSpeechModel,
        LoadingModel,
        Ready,
        Failed
    }

    /// <summary>
    /// Launcher logic. On startup: make sure a local Ollama server answers (starting the bundled one
    /// if needed), make sure the LLM exists (pull it if not), make sure the offline speech model file
    /// exists (download it if not), then warm the LLM into memory. After the first run the game is
    /// fully offline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ARESModelProvisioner : MonoBehaviour
    {
        [SerializeField] private ARESProvisioningConfiguration configuration;
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool persistAcrossScenes = true;

        public static bool IsReady { get; private set; }
        public static event Action Ready;

        public ARESProvisionPhase Phase { get; private set; }
        public float Progress { get; private set; }
        public string StatusText { get; private set; } = string.Empty;
        public string Error { get; private set; }
        public event Action Changed;

        private Coroutine run;
        private Process ollamaProcess;

#pragma warning disable 0649
        [Serializable] private sealed class TagsResponse { public TagModel[] models; }
        [Serializable] private sealed class TagModel { public string name; public string model; }
        [Serializable] private sealed class PullBody { public string model; public bool stream; }
        [Serializable]
        private sealed class WarmBody
        {
            public string model;
            public string prompt;
            public string keep_alive;
            public bool stream;
            public WarmOptions options;
        }
        [Serializable] private sealed class WarmOptions { public int num_predict; }
        [Serializable]
        private sealed class PullLine
        {
            public string status;
            public string digest;
            public long total;
            public long completed;
            public string error;
        }
#pragma warning restore 0649

        private void Start()
        {
            if (persistAcrossScenes && transform.parent == null)
                DontDestroyOnLoad(gameObject);
            if (runOnStart)
                Begin();
        }

        public void Begin()
        {
            if (run != null)
                return;
            run = StartCoroutine(Provision());
        }

        public void Retry()
        {
            if (Phase == ARESProvisionPhase.Failed)
                Begin();
        }

        private void OnApplicationQuit()
        {
            StopOwnedOllama();
        }

        private void OnDestroy()
        {
            if (!persistAcrossScenes)
                StopOwnedOllama();
        }

        // ------------------------------------------------------------------ main flow

        private IEnumerator Provision()
        {
            IsReady = false;
            Error = null;

            if (configuration == null || configuration.BridgeConfiguration == null)
            {
                Fail("ARES provisioning configuration or its Local LLM configuration is not assigned.");
                yield break;
            }

            if (!Uri.TryCreate(configuration.BridgeConfiguration.Endpoint, UriKind.Absolute, out Uri endpointUri))
            {
                Fail("ARES Ollama endpoint is not a valid absolute URL.");
                yield break;
            }

            string baseUrl = endpointUri.GetLeftPart(UriPartial.Authority);
            string model = configuration.BridgeConfiguration.Model;

            // 1. Runtime
            SetPhase(ARESProvisionPhase.StartingRuntime, "Starting local AI engine...", 0f);
            bool up = false;
            yield return Ping(baseUrl, value => up = value);
            if (!up)
            {
                if (!configuration.UseBundledOllama)
                {
                    Fail("No Ollama server is running at " + baseUrl + ".");
                    yield break;
                }

                string startError = StartOllama(endpointUri);
                if (startError != null)
                {
                    Fail(startError);
                    yield break;
                }

                float deadline = Time.realtimeSinceStartup + configuration.ServerStartTimeoutSeconds;
                while (!up && Time.realtimeSinceStartup < deadline)
                {
                    if (ollamaProcess != null && ollamaProcess.HasExited)
                    {
                        Fail("The local AI engine exited unexpectedly (code " + ollamaProcess.ExitCode + ").");
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return Ping(baseUrl, value => up = value);
                }
                if (!up)
                {
                    Fail("The local AI engine did not start in time.");
                    yield break;
                }
            }

            // 2. LLM present?
            SetPhase(ARESProvisionPhase.CheckingModel, "Checking AI model '" + model + "'...", 0f);
            bool installed = false;
            string checkError = null;
            yield return IsModelInstalled(baseUrl, model, (found, error) => { installed = found; checkError = error; });
            if (checkError != null)
            {
                Fail(checkError);
                yield break;
            }

            // 3. Pull if missing
            if (!installed)
            {
                string pullError = null;
                yield return PullModel(baseUrl, model, error => pullError = error);
                if (pullError != null)
                {
                    Fail(pullError);
                    yield break;
                }
            }

            // 4. Speech model files
            foreach (ARESRequiredFile file in configuration.RequiredFiles)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.fileName) || string.IsNullOrWhiteSpace(file.url))
                    continue;

                string path = Path.Combine(ARESPaths.WhisperModelsDirectory, file.fileName);
                if (File.Exists(path) && new FileInfo(path).Length >= file.minimumBytes)
                    continue;

                string downloadError = null;
                yield return DownloadFile(file, path, error => downloadError = error);
                if (downloadError != null)
                {
                    Fail(downloadError);
                    yield break;
                }
            }

            // 5. Warm the LLM into memory (non-fatal)
            SetPhase(ARESProvisionPhase.LoadingModel, "Loading AI model into memory...", 0.5f);
            yield return WarmModel(baseUrl, model);

            IsReady = true;
            run = null;
            SetPhase(ARESProvisionPhase.Ready, "ARES is ready.", 1f);
            Ready?.Invoke();
        }

        // ------------------------------------------------------------------ Ollama process

        private string StartOllama(Uri endpointUri)
        {
            string bundled = ARESPaths.BundledOllamaExecutable;
            string executable = File.Exists(bundled) ? bundled : "ollama"; // fallback: PATH (dev machines)

            try
            {
                Directory.CreateDirectory(ARESPaths.OllamaModelsDirectory);
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = File.Exists(bundled) ? Path.GetDirectoryName(bundled) : Application.dataPath
                };
                info.EnvironmentVariables["OLLAMA_MODELS"] = ARESPaths.OllamaModelsDirectory;
                info.EnvironmentVariables["OLLAMA_HOST"] = endpointUri.Host + ":" + endpointUri.Port;
                info.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = configuration.KeepAlive;
                ollamaProcess = Process.Start(info);
                return ollamaProcess == null ? "Could not start the local AI engine." : null;
            }
            catch (Win32Exception exception)
            {
                return "Could not start the local AI engine (" + executable + "): " + exception.Message +
                    ". Place the standalone Ollama files in " + Path.GetDirectoryName(bundled) + ".";
            }
            catch (Exception exception)
            {
                return "Could not start the local AI engine: " + exception.Message;
            }
        }

        private void StopOwnedOllama()
        {
            if (ollamaProcess == null)
                return;
            try
            {
                if (!ollamaProcess.HasExited)
                {
                    // /T also kills Ollama's runner child processes.
                    try
                    {
                        Process.Start(new ProcessStartInfo("taskkill", "/PID " + ollamaProcess.Id + " /T /F")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    }
                    catch (Exception)
                    {
                        ollamaProcess.Kill();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("ARES could not stop the local AI engine cleanly: " + exception.Message);
            }
            ollamaProcess = null;
        }

        // ------------------------------------------------------------------ HTTP steps

        private static IEnumerator Ping(string baseUrl, Action<bool> done)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(baseUrl + "/api/version"))
            {
                request.timeout = 2;
                yield return request.SendWebRequest();
                done(request.result == UnityWebRequest.Result.Success);
            }
        }

        private static IEnumerator IsModelInstalled(string baseUrl, string model, Action<bool, string> done)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(baseUrl + "/api/tags"))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    done(false, "Could not query installed models: " + request.error);
                    yield break;
                }

                TagsResponse response;
                try
                {
                    response = JsonUtility.FromJson<TagsResponse>(request.downloadHandler.text);
                }
                catch (ArgumentException exception)
                {
                    done(false, "Could not parse the installed model list: " + exception.Message);
                    yield break;
                }

                string wanted = NormalizeModelName(model);
                if (response != null && response.models != null)
                {
                    foreach (TagModel tag in response.models)
                    {
                        if (tag != null &&
                            (string.Equals(NormalizeModelName(tag.name), wanted, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(NormalizeModelName(tag.model), wanted, StringComparison.OrdinalIgnoreCase)))
                        {
                            done(true, null);
                            yield break;
                        }
                    }
                }
                done(false, null);
            }
        }

        private static string NormalizeModelName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            name = name.Trim();
            return name.IndexOf(':') >= 0 ? name : name + ":latest";
        }

        private IEnumerator PullModel(string baseUrl, string model, Action<string> done)
        {
            SetPhase(ARESProvisionPhase.DownloadingModel, "Downloading AI model '" + model + "'...", 0f);
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new PullBody { model = model, stream = true }));
            Dictionary<string, long> totals = new Dictionary<string, long>();
            Dictionary<string, long> completed = new Dictionary<string, long>();
            string pullError = null;
            bool sawSuccess = false;

            NdjsonDownloadHandler handler = new NdjsonDownloadHandler();
            using (UnityWebRequest request = new UnityWebRequest(baseUrl + "/api/pull", UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = handler;
                request.timeout = 0; // large download: no overall timeout
                request.SetRequestHeader("Content-Type", "application/json");
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    ConsumePullLines(handler, totals, completed, ref pullError, ref sawSuccess);
                    yield return null;
                }
                ConsumePullLines(handler, totals, completed, ref pullError, ref sawSuccess);

                if (pullError == null && request.result != UnityWebRequest.Result.Success)
                    pullError = "Model download failed: " + request.error;
            }

            if (pullError != null)
            {
                done(pullError);
                yield break;
            }
            if (!sawSuccess)
            {
                done("Model download ended before completion. Check your internet connection and retry.");
                yield break;
            }
            done(null);
        }

        private void ConsumePullLines(NdjsonDownloadHandler handler, Dictionary<string, long> totals,
            Dictionary<string, long> completed, ref string pullError, ref bool sawSuccess)
        {
            while (handler.TryDequeue(out string line))
            {
                PullLine parsed;
                try
                {
                    parsed = JsonUtility.FromJson<PullLine>(line);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (parsed == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(parsed.error))
                {
                    pullError = "Model download failed: " + parsed.error;
                    continue;
                }
                if (string.Equals(parsed.status, "success", StringComparison.OrdinalIgnoreCase))
                    sawSuccess = true;

                if (!string.IsNullOrEmpty(parsed.digest) && parsed.total > 0)
                {
                    totals[parsed.digest] = parsed.total;
                    completed[parsed.digest] = parsed.completed;
                }

                long sumTotal = 0, sumDone = 0;
                foreach (KeyValuePair<string, long> pair in totals)
                {
                    sumTotal += pair.Value;
                    completed.TryGetValue(pair.Key, out long done);
                    sumDone += done;
                }

                if (sumTotal > 0)
                    SetPhase(ARESProvisionPhase.DownloadingModel,
                        "Downloading AI model... " + (sumDone / 1048576) + " / " + (sumTotal / 1048576) + " MB",
                        Mathf.Clamp01(sumDone / (float)sumTotal));
                else if (!string.IsNullOrWhiteSpace(parsed.status))
                    SetPhase(ARESProvisionPhase.DownloadingModel, "AI model: " + parsed.status, Progress);
            }
        }

        private IEnumerator DownloadFile(ARESRequiredFile file, string finalPath, Action<string> done)
        {
            string label = string.IsNullOrWhiteSpace(file.displayName) ? file.fileName : file.displayName;
            SetPhase(ARESProvisionPhase.DownloadingSpeechModel, "Downloading " + label + "...", 0f);

            string partPath = finalPath + ".part";
            string prepareError = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath));
                if (File.Exists(partPath))
                    File.Delete(partPath);
            }
            catch (Exception exception)
            {
                prepareError = "Could not prepare the download folder: " + exception.Message;
            }
            if (prepareError != null)
            {
                done(prepareError);
                yield break;
            }

            string error = null;
            using (UnityWebRequest request = new UnityWebRequest(file.url, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(partPath) { removeFileOnAbort = true };
                request.timeout = 0;
                request.redirectLimit = 10;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    SetPhase(ARESProvisionPhase.DownloadingSpeechModel,
                        "Downloading " + label + "... " + ((long)request.downloadedBytes / 1048576) + " MB",
                        request.downloadProgress);
                    yield return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                    error = "Download of " + label + " failed: " + request.error;
            }

            if (error == null)
            {
                try
                {
                    long size = new FileInfo(partPath).Length;
                    if (size < file.minimumBytes)
                    {
                        error = label + " downloaded incompletely (" + size + " bytes).";
                        File.Delete(partPath);
                    }
                    else
                    {
                        if (File.Exists(finalPath))
                            File.Delete(finalPath);
                        File.Move(partPath, finalPath);
                    }
                }
                catch (Exception exception)
                {
                    error = "Could not finalize " + label + ": " + exception.Message;
                }
            }
            done(error);
        }

        private IEnumerator WarmModel(string baseUrl, string model)
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                new WarmBody
                {
                    model = model,
                    prompt = "Reply with one period.",
                    keep_alive = configuration.KeepAlive,
                    stream = false,
                    options = new WarmOptions { num_predict = 1 }
                }));
            using (UnityWebRequest request = new UnityWebRequest(baseUrl + "/api/generate", UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 180;
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("ARES could not pre-load the AI model (it will load on first use): " + request.error);
            }
        }

        // ------------------------------------------------------------------ state

        private void SetPhase(ARESProvisionPhase phase, string text, float progress)
        {
            Phase = phase;
            StatusText = text;
            Progress = Mathf.Clamp01(progress);
            Changed?.Invoke();
        }

        private void Fail(string message)
        {
            run = null;
            Error = message;
            Phase = ARESProvisionPhase.Failed;
            StatusText = message;
            Debug.LogError("ARES setup failed: " + message, this);
            Changed?.Invoke();
        }

        /// <summary>Splits a streamed HTTP body into NDJSON lines as chunks arrive.</summary>
        private sealed class NdjsonDownloadHandler : DownloadHandlerScript
        {
            private readonly List<byte> pending = new List<byte>();
            private readonly Queue<string> lines = new Queue<string>();
            private readonly object gate = new object();

            public NdjsonDownloadHandler() : base(new byte[16 * 1024]) { }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength < 1)
                    return false;
                lock (gate)
                {
                    for (int i = 0; i < dataLength; i++)
                    {
                        if (data[i] == (byte)'\n')
                            Flush();
                        else
                            pending.Add(data[i]);
                    }
                }
                return true;
            }

            protected override void CompleteContent()
            {
                lock (gate)
                    Flush();
            }

            public bool TryDequeue(out string line)
            {
                lock (gate)
                {
                    if (lines.Count > 0)
                    {
                        line = lines.Dequeue();
                        return true;
                    }
                }
                line = null;
                return false;
            }

            private void Flush()
            {
                if (pending.Count == 0)
                    return;
                lines.Enqueue(Encoding.UTF8.GetString(pending.ToArray()));
                pending.Clear();
            }
        }
    }
}
