using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using StudentManagementSystem.Data;
using StudentManagementSystem.Lib;

namespace StudentManagementSystem.Handlers
{
    /// <summary>
    /// Chat assistant: FAQ database first (always on), Groq LLM second (only when a key exists).
    /// POST message=... -> {"answer": "..."}.
    /// </summary>
    public class ChatHandler : IHttpHandler
    {
        public bool IsReusable { get { return false; } }

        public void ProcessRequest(HttpContext ctx)
        {
            var res = ctx.Response;
            res.ContentType = "application/json; charset=utf-8";
            if (ctx.Request.HttpMethod != "POST")
            {
                res.StatusCode = 405;
                res.Write("{\"answer\":\"Post your question as message=...\"}");
                return;
            }
            string question = (ctx.Request.Form["message"] ?? "").Trim();
            if (question.Length == 0 || question.Length > 500)
            {
                res.Write("{\"answer\":\"Ask a short question about fees, attendance, results, exams or the library.\"}");
                return;
            }

            string answer, source;
            string faq = CommsRepo.FindFaq(question, CurrentUser.IsAuthenticated ? CurrentUser.RoleName : "");
            if (!string.IsNullOrEmpty(faq))
            {
                answer = faq;
                source = "faq";
            }
            else
            {
                string groqKey = AppConfig.Get("Chatbot:GroqApiKey", "");
                string groqModel = AppConfig.Get("Chatbot:GroqModel", "llama-3.3-70b-versatile");
                if (groqKey.Length > 0)
                {
                    answer = AskGroq(groqKey, groqModel, question);
                    source = "llm";
                }
                else
                {
                    answer = "I do not have that in the FAQ library yet. Please raise a complaint and the office will answer.";
                    source = "none";
                }
            }

            try { CommsRepo.LogChat(CurrentUser.IsAuthenticated ? (int?)CurrentUser.UserId : null,
                CurrentUser.RoleName, question, answer, source); }
            catch (Exception) { /* chat log must never break chat */ }

            if (answer.Length > AppConfig.GetInt("Chatbot:MaxChars", 240))
                answer = answer.Substring(0, AppConfig.GetInt("Chatbot:MaxChars", 240)) + "...";
            res.Write("{\"answer\":" + J(answer) + "}");
        }

        private static string AskGroq(string apiKey, string model, string question)
        {
            try
            {
                string body = "{\"model\":" + J(model) + ",\"messages\":[" +
                    "{\"role\":\"system\",\"content\":" + J("You are the SMS Student Information System assistant. Answer briefly (max 3 sentences) about college matters: fees, attendance, exams, results, library routine. If unsure, tell the student to raise a complaint.") + "}," +
                    "{\"role\":\"user\",\"content\":" + J(question) + "}]," +
                    "\"max_tokens\":150,\"temperature\":0.4}";
                byte[] payload = Encoding.UTF8.GetBytes(body);
                var req = (HttpWebRequest)WebRequest.Create("https://api.groq.com/openai/v1/chat/completions");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Headers[HttpRequestHeader.Authorization] = "Bearer " + apiKey;
                req.Timeout = 15000;
                using (var s = req.GetRequestStream()) s.Write(payload, 0, payload.Length);
                using (var resp = req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();
                    Match m = Regex.Match(json, "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    string content = "";
                    while (m.Success) { content = m.Groups[1].Value; m = m.NextMatch(); }
                    return Unescape(content.Length > 0 ? content : "The assistant could not answer right now.");
                }
            }
            catch (Exception)
            {
                return "The LLM helper is unreachable; rely on the FAQ or raise a complaint.";
            }
        }

        private static string Unescape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\n", " ").Replace("\\r", " ").Replace("\\t", " ")
                .Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static string J(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c < ' ') sb.Append(" ");
                else sb.Append(c);
            }
            return sb.Append("\"").ToString();
        }
    }
}
