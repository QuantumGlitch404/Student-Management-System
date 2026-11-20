using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Ai
{
    public class AiTurn
    {
        public string Role;
        public string Content;
        public AiTurn(string role, string content) { Role = role; Content = content ?? ""; }
    }

    public class AiResult
    {
        public bool Ok;
        public string Text = "";
        public string Error = "";
        public int PromptTokens;
        public int CompletionTokens;
        public int ElapsedMs;
    }

    public class AiStatusInfo
    {
        public bool Enabled = true;
        public bool ModelPresent;
        public bool RuntimePresent;
        public bool Ready;
        public string State = "stopped";   // stopped | starting | busy | ready | error
        public string ModelName = "";
        public string ModelPath = "";
        public long ModelBytes;
        public string RuntimeVersion = "";
        public string Error = "";
        public int Port;
    }

    /// <summary>
    /// Supervises the bundled llama.cpp server and the Q2_K model.
    ///
    /// The process starts lazily on the first question (not at application
    /// start), stays warm for quick follow-ups, and stops after 30 idle
    /// minutes. All inference is local: the only network calls are to
    /// 127.0.0.1. If the model or runtime is missing, the rest of the
    /// application is untouched and the chat shows a clear message.
    /// </summary>
    public static class AiService
    {
        public const string ModelFileName = "Ektome-Qwen1.5-0.5B-Chat-PristinelyUncensored.Q2_K.gguf";
        private const int DefaultPort = 8137;
        private const int IdleStopMinutes = 30;

        private static readonly object Sync = new object();
        private static readonly SemaphoreSlim GenerateLock = new SemaphoreSlim(1, 1);
        private static Process _proc;
        private static DateTime _lastUseUtc = DateTime.UtcNow;
        private static DateTime _lastStartUtc = DateTime.MinValue;
        private static string _lastError = "";
        private static Timer _idleTimer;

        // ---------------------------------------------------------------- paths

        public static string AiRoot()
        {
            string overrideDir = AppConfig.Get("Ai:Directory", "");
            if (!string.IsNullOrWhiteSpace(overrideDir) && Directory.Exists(overrideDir)) return overrideDir;

            string appPath = HttpRuntime.AppDomainAppPath;
            string[] candidates =
            {
                Path.Combine(appPath, "AI"),                                  // bundled inside the site
                Path.GetFullPath(Path.Combine(appPath, "..", "AI")),          // installer layout {app}\AI
                Path.GetFullPath(Path.Combine(appPath, "..", "..", "AI"))     // dev layout repo\AI
            };
            foreach (string c in candidates)
                if (Directory.Exists(c) && File.Exists(Path.Combine(c, "model", ModelFileName))) return c;
            foreach (string c in candidates) if (Directory.Exists(c)) return c;
            return candidates[0];
        }

        public static string ModelPath()
        {
            string configured = AppConfig.Get("Ai:ModelPath", "");
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
            return Path.Combine(AiRoot(), "model", ModelFileName);
        }

        public static string RuntimeExe()
        {
            string configured = AppConfig.Get("Ai:RuntimePath", "");
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
            return Path.Combine(AiRoot(), "runtime", "llama-server.exe");
        }

        public static int Port()
        {
            return AppConfig.GetInt("Ai:Port", DefaultPort);
        }

        public static bool MasterEnabled()
        {
            return AppConfig.GetBool("Ai:Enabled", true);
        }

        // --------------------------------------------------------------- status

        public static AiStatusInfo Status()
        {
            var s = new AiStatusInfo
            {
                Enabled = MasterEnabled(),
                Port = Port(),
                ModelPath = ModelPath()
            };
            try
            {
                var fi = new FileInfo(s.ModelPath);
                s.ModelPresent = fi.Exists && fi.Length > 50L * 1024 * 1024;
                s.ModelBytes = fi.Exists ? fi.Length : 0;
                s.ModelName = "Local model";
            }
            catch (Exception) { s.ModelPresent = false; }
            s.RuntimePresent = File.Exists(RuntimeExe());
            s.Ready = IsHealthy(800);
            s.State = GenerateLock.CurrentCount == 0 ? "busy" : (s.Ready ? "ready" : "stopped");
            if (!s.Enabled) { s.State = "disabled"; s.Error = "The assistant is disabled in server settings."; }
            else if (!s.ModelPresent) { s.State = "error"; s.Error = "Model file not found. Reinstall the application with the AI component."; }
            else if (!s.RuntimePresent) { s.State = "error"; s.Error = "llama.cpp runtime not found. Reinstall the application with the AI component."; }
            else if (!string.IsNullOrEmpty(_lastError)) s.Error = _lastError;
            s.RuntimeVersion = ReadRuntimeVersion();
            return s;
        }

        private static string ReadRuntimeVersion()
        {
            try
            {
                string p = RuntimeExe();
                if (!File.Exists(p)) return "";
                var vi = FileVersionInfo.GetVersionInfo(p);
                return string.IsNullOrWhiteSpace(vi.FileVersion) ? "llama.cpp (bundled)" : vi.FileVersion;
            }
            catch (Exception) { return "llama.cpp (bundled)"; }
        }

        // ----------------------------------------------------------- lifecycle

        public static bool EnsureReady(int waitMs, out string error)
        {
            error = "";
            if (!MasterEnabled()) { error = "The assistant is disabled in server settings."; return false; }
            if (!File.Exists(ModelPath())) { error = "The local model file is missing. Reinstall the AI component."; return false; }
            if (!File.Exists(RuntimeExe())) { error = "The llama.cpp runtime is missing. Reinstall the AI component."; return false; }
            if (IsHealthy(700)) { _lastError = ""; return true; }

            int port = Port();
            lock (Sync)
            {
                if (IsHealthy(400)) return true;

                // Avoid restart storms (e.g. a broken binary) - at most once per 8s.
                if ((DateTime.UtcNow - _lastStartUtc).TotalSeconds < 8 && _proc != null && !_proc.HasExited)
                {
                    // still starting below
                }
                else
                {
                    KillOwnProcess();
                    try
                    {
                        string log = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "logs", "ai-runtime.log");
                        Directory.CreateDirectory(Path.GetDirectoryName(log));
                        var psi = new ProcessStartInfo
                        {
                            FileName = RuntimeExe(),
                            WorkingDirectory = Path.GetDirectoryName(RuntimeExe()),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        psi.Arguments = "-m \"" + ModelPath() + "\" --host 127.0.0.1 --port " + port +
                                        " -c 2048 -t " + Threads() + " --no-webui --log-file \"" + log + "\" --log-verbosity 2";
                        _proc = Process.Start(psi);
                        _lastStartUtc = DateTime.UtcNow;
                        _lastError = "";
                    }
                    catch (Exception ex)
                    {
                        _lastError = "Could not start the local model runtime: " + ex.Message;
                        error = _lastError;
                        return false;
                    }
                }

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(waitMs);
                while (DateTime.UtcNow < deadline)
                {
                    if (IsHealthy(700)) { _lastError = ""; return true; }
                    if (_proc != null && _proc.HasExited)
                    {
                        _lastError = "The local model runtime stopped unexpectedly (exit code " + _proc.ExitCode + ").";
                        error = _lastError;
                        return false;
                    }
                    Thread.Sleep(350);
                }
            }

            error = "The local model is still loading. Try again in a moment.";
            return false;
        }

        private static int Threads()
        {
            int configured = AppConfig.GetInt("Ai:Threads", 0);
            if (configured > 0) return Math.Min(configured, 16);
            int cores = Environment.ProcessorCount;
            return Math.Max(2, Math.Min(6, cores / 2));
        }

        private static void KillOwnProcess()
        {
            try
            {
                if (_proc != null && !_proc.HasExited) _proc.Kill();
            }
            catch (Exception) { /* already gone */ }
            _proc = null;
        }

        public static void StopServer()
        {
            lock (Sync) { KillOwnProcess(); }
        }

        private static bool IsHealthy(int timeoutMs)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + Port() + "/health");
                req.Method = "GET";
                req.Proxy = null;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                using (var resp = (HttpWebResponse)req.GetResponse())
                    return resp.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception) { return false; }
        }

        // ------------------------------------------------------------- complete

        /// <summary>
        /// Runs one chat completion. Callers are serialised because CPU
        /// inference on a 0.5B model is fast but not free; a second browser
        /// tab waits rather than starting a competing generation.
        /// </summary>
        public static AiResult Complete(List<AiTurn> turns, int maxTokens, int timeoutSeconds)
        {
            var result = new AiResult();
            var sw = Stopwatch.StartNew();
            _lastUseUtc = DateTime.UtcNow;

            string err;
            if (!EnsureReady(90000, out err))
            {
                result.Error = err;
                return result;
            }
            if (!GenerateLock.Wait(TimeSpan.FromSeconds(20)))
            {
                result.Error = "The assistant is busy with another question. Try again in a moment.";
                return result;
            }

            try
            {
                _lastUseUtc = DateTime.UtcNow;
                var payload = new Dictionary<string, object>
                {
                    { "messages", BuildMessages(turns) },
                    { "max_tokens", Math.Max(64, Math.Min(maxTokens, 700)) },
                    { "temperature", 0.35 },
                    { "top_p", 0.9 },
                    { "repeat_penalty", 1.08 },
                    { "stream", false }
                };
                string json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(payload);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + Port() + "/v1/chat/completions");
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.Proxy = null;
                req.Timeout = timeoutSeconds * 1000;
                req.ReadWriteTimeout = timeoutSeconds * 1000;
                using (var stream = req.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                {
                    string body = reader.ReadToEnd();
                    var ser = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
                    var root = ser.Deserialize<Dictionary<string, object>>(body);
                    object choicesObj;
                    if (root == null || !root.TryGetValue("choices", out choicesObj)) { result.Error = "The model returned an unreadable response."; return result; }
                    var choices = choicesObj as System.Collections.IList;
                    if (choices == null || choices.Count == 0) { result.Error = "The model returned no answer."; return result; }
                    var first = choices[0] as Dictionary<string, object>;
                    object msgObj;
                    if (first != null && first.TryGetValue("message", out msgObj))
                    {
                        var msg = msgObj as Dictionary<string, object>;
                        object content;
                        if (msg != null && msg.TryGetValue("content", out content))
                            result.Text = Convert.ToString(content).Trim();
                    }
                    object usageObj;
                    if (root.TryGetValue("usage", out usageObj))
                    {
                        var usage = usageObj as Dictionary<string, object>;
                        if (usage != null)
                        {
                            object pt, ct;
                            if (usage.TryGetValue("prompt_tokens", out pt)) int.TryParse(Convert.ToString(pt), out result.PromptTokens);
                            if (usage.TryGetValue("completion_tokens", out ct)) int.TryParse(Convert.ToString(ct), out result.CompletionTokens);
                        }
                    }
                }

                result.Ok = !string.IsNullOrWhiteSpace(result.Text);
                if (!result.Ok) result.Error = "The local model produced an empty answer. Try rephrasing the question.";
                return result;
            }
            catch (WebException ex)
            {
                string detail = "";
                try
                {
                    if (ex.Response != null)
                        using (var r = new StreamReader(ex.Response.GetResponseStream())) detail = r.ReadToEnd();
                }
                catch (Exception) { /* ignore */ }
                _lastError = "Local inference failed: " + ex.Message + (detail.Length > 0 ? " " + Truncate(detail, 200) : "");
                result.Error = "The local model could not answer this time. " + Truncate(ex.Message, 140);
                return result;
            }
            catch (Exception ex)
            {
                _lastError = "Local inference failed: " + ex.Message;
                result.Error = "The local model could not answer this time. " + Truncate(ex.Message, 140);
                return result;
            }
            finally
            {
                result.ElapsedMs = (int)sw.ElapsedMilliseconds;
                _lastUseUtc = DateTime.UtcNow;
                GenerateLock.Release();
            }
        }

        private static List<Dictionary<string, string>> BuildMessages(List<AiTurn> turns)
        {
            var list = new List<Dictionary<string, string>>();
            foreach (var t in turns)
                list.Add(new Dictionary<string, string> { { "role", t.Role }, { "content", t.Content } });
            return list;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
        }

        // ---------------------------------------------------------- idle timer

        /// <summary>Called from the status endpoint so the timer exists only when used.</summary>
        public static void Touch()
        {
            _lastUseUtc = DateTime.UtcNow;
            if (_idleTimer != null) return;
            lock (Sync)
            {
                if (_idleTimer != null) return;
                _idleTimer = new Timer(_ =>
                {
                    try
                    {
                        if (GenerateLock.CurrentCount == 0) return;
                        if ((DateTime.UtcNow - _lastUseUtc).TotalMinutes < IdleStopMinutes) return;
                        if (IsHealthy(400))
                        {
                            // Only stop the process we started.
                            lock (Sync) { KillOwnProcess(); }
                        }
                    }
                    catch (Exception) { /* the timer must never take the app down */ }
                }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            }
        }
    }
}