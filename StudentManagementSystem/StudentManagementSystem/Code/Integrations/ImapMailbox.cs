using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace StudentManagementSystem.Integrations
{
    /// <summary>
    /// Small IMAP reader for the mail centre. It fetches recent messages over
    /// SSL, decodes the common MIME shapes (plain, HTML, multipart, base64 and
    /// quoted-printable) and hands each message to a callback for storage.
    /// No credentials are stored here; the caller supplies the decrypted password.
    /// </summary>
    public static class ImapMailbox
    {
        public static int Sync(string host, int port, bool ssl, string user, string password,
            Action<string, string, string, string, string, string, string> store, int limit)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("IMAP host, user and app password are required.");

            using (var conn = new ImapConnection(host, port <= 0 ? 993 : port, ssl))
            {
                conn.Login(user, password);
                conn.SelectInbox();
                List<string> uids = conn.SearchRecent(Math.Max(1, limit));
                int count = 0;
                foreach (string uid in uids)
                {
                    byte[] raw = conn.Fetch(uid);
                    if (raw == null || raw.Length == 0) continue;
                    ParsedMail mail = Parse(raw);
                    string key = "IMAP:" + uid;
                    // store(from, to, subject, html, text, folder, uid)
                    store(mail.From, mail.To, mail.Subject, mail.Html, mail.Text, "INBOX", key);
                    count++;
                }
                return count;
            }
        }

        private class ParsedMail
        {
            public string From, To, Subject, Html, Text;
        }

        private static ParsedMail Parse(byte[] raw)
        {
            string all = Encoding.UTF8.GetString(raw);
            int split = all.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (split < 0) split = all.IndexOf("\n\n", StringComparison.Ordinal);
            string headerText = split < 0 ? all : all.Substring(0, split);
            string body = split < 0 ? "" : all.Substring(split + (all.Substring(split, 2) == "\r\n" ? 4 : 2));

            var headers = ParseHeaders(headerText);
            var mail = new ParsedMail
            {
                From = Header(headers, "From"),
                To = Header(headers, "To"),
                Subject = DecodeWords(Header(headers, "Subject"))
            };
            string contentType = Header(headers, "Content-Type");
            string transfer = Header(headers, "Content-Transfer-Encoding");
            ExtractPart(contentType, transfer, body, mail);
            if (mail.Html == null && mail.Text == null) mail.Text = body;
            return mail;
        }

        private static Dictionary<string, string> ParseHeaders(string text)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string unfolded = Regex.Replace(text ?? "", "\r?\n[ \t]+", " ");
            foreach (string line in Regex.Split(unfolded, "\r?\n"))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                if (!headers.ContainsKey(key)) headers[key] = line.Substring(colon + 1).Trim();
            }
            return headers;
        }

        private static string Header(Dictionary<string, string> h, string key)
        {
            string value;
            return h.TryGetValue(key, out value) ? value : "";
        }

        private static void ExtractPart(string contentType, string transfer, string body, ParsedMail mail)
        {
            string ct = (contentType ?? "").ToLowerInvariant();
            string boundary = MatchParam(contentType, "boundary");
            if (ct.StartsWith("multipart/") && !string.IsNullOrEmpty(boundary))
            {
                string[] parts = Regex.Split(body ?? "", "--" + Regex.Escape(boundary));
                foreach (string part in parts)
                {
                    int split = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (split < 0) split = part.IndexOf("\n\n", StringComparison.Ordinal);
                    if (split < 0) continue;
                    string head = part.Substring(0, split);
                    string content = part.Substring(split + (part.Substring(split, 2) == "\r\n" ? 4 : 2));
                    var h = ParseHeaders(head);
                    ExtractPart(Header(h, "Content-Type"), Header(h, "Content-Transfer-Encoding"), content, mail);
                }
                return;
            }

            string decoded = DecodeTransfer(body, transfer);
            if (ct.StartsWith("text/html"))
            {
                if (mail.Html == null) mail.Html = decoded;
            }
            else if (ct.StartsWith("text/plain") || ct.Length == 0)
            {
                if (mail.Text == null) mail.Text = decoded;
            }
        }

        private static string MatchParam(string header, string name)
        {
            if (string.IsNullOrEmpty(header)) return null;
            Match m = Regex.Match(header, name + "\\s*=\\s*\"?([^\";]+)\"?", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string DecodeTransfer(string body, string transfer)
        {
            if (string.IsNullOrEmpty(body)) return "";
            string t = (transfer ?? "").ToLowerInvariant();
            try
            {
                if (t.Contains("base64"))
                {
                    string clean = Regex.Replace(body, "\\s", "");
                    return Encoding.UTF8.GetString(Convert.FromBase64String(clean));
                }
                if (t.Contains("quoted-printable"))
                    return DecodeQuotedPrintable(body);
            }
            catch (Exception) { /* keep the raw body */ }
            return body;
        }

        private static string DecodeQuotedPrintable(string value)
        {
            value = Regex.Replace(value, "=\r?\n", "");
            return Regex.Replace(value, "=([0-9A-Fa-f]{2})", m =>
                ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        }

        private static string DecodeWords(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return Regex.Replace(value, "=\\?([^?]+)\\?([BbQq])\\?([^?]*)\\?=", m =>
            {
                try
                {
                    string charset = m.Groups[1].Value;
                    string enc = m.Groups[2].Value.ToUpperInvariant();
                    string data = m.Groups[3].Value;
                    byte[] bytes = enc == "B"
                        ? Convert.FromBase64String(data)
                        : Encoding.ASCII.GetBytes(DecodeQuotedPrintable(data.Replace('_', ' ')));
                    return Encoding.GetEncoding(charset).GetString(bytes);
                }
                catch (Exception) { return m.Value; }
            });
        }

        private sealed class ImapConnection : IDisposable
        {
            private readonly TcpClient _client;
            private readonly Stream _stream;
            private readonly List<byte> _buffer = new List<byte>();
            private int _tag;

            public ImapConnection(string host, int port, bool ssl)
            {
                _client = new TcpClient();
                _client.Connect(host, port);
                _client.ReceiveTimeout = 20000;
                _client.SendTimeout = 20000;
                Stream stream = _client.GetStream();
                if (ssl)
                {
                    var secure = new SslStream(stream, false);
                    secure.AuthenticateAsClient(host);
                    stream = secure;
                }
                _stream = stream;
                ReadLine();
            }

            public void Login(string user, string password)
            {
                string tag = NextTag();
                Send(tag + " LOGIN " + Quote(user) + " " + Quote(password));
                ReadUntilTag(tag);
            }

            public void SelectInbox()
            {
                string tag = NextTag();
                Send(tag + " SELECT \"INBOX\"");
                ReadUntilTag(tag);
            }

            public List<string> SearchRecent(int limit)
            {
                string tag = NextTag();
                Send(tag + " UID SEARCH ALL");
                List<string> uids = new List<string>();
                while (true)
                {
                    string line = ReadLine();
                    if (line == null) break;
                    if (line.StartsWith("* SEARCH", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (string part in line.Substring(8).Trim().Split(' '))
                            if (part.Length > 0) uids.Add(part);
                    }
                    if (line.StartsWith(tag + " ", StringComparison.Ordinal)) break;
                }
                if (uids.Count > limit) uids = uids.GetRange(uids.Count - limit, limit);
                return uids;
            }

            public byte[] Fetch(string uid)
            {
                string tag = NextTag();
                Send(tag + " UID FETCH " + uid + " (UID BODY.PEEK[])");
                MemoryStream data = null;
                while (true)
                {
                    string line = ReadLine();
                    if (line == null) break;
                    Match lit = Regex.Match(line, "\\{(\\d+)\\}$");
                    if (lit.Success)
                    {
                        int size = int.Parse(lit.Groups[1].Value, CultureInfo.InvariantCulture);
                        data = new MemoryStream();
                        byte[] literal = ReadBytes(size);
                        data.Write(literal, 0, literal.Length);
                        ReadLine();
                    }
                    if (line.StartsWith(tag + " ", StringComparison.Ordinal)) break;
                }
                return data == null ? null : data.ToArray();
            }

            private string NextTag() { return "A" + (++_tag).ToString(CultureInfo.InvariantCulture); }

            private void ReadUntilTag(string tag)
            {
                while (true)
                {
                    string line = ReadLine();
                    if (line == null) throw new IOException("IMAP server closed the connection.");
                    if (line.StartsWith(tag + " ", StringComparison.Ordinal))
                    {
                        if (line.IndexOf(" OK", StringComparison.OrdinalIgnoreCase) < 0)
                            throw new InvalidOperationException("IMAP: " + line);
                        return;
                    }
                }
            }

            private void Send(string line)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(line + "\r\n");
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }

            private static string Quote(string value)
            {
                return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }

            private string ReadLine()
            {
                var bytes = new List<byte>();
                while (true)
                {
                    int b = ReadByte();
                    if (b < 0) return bytes.Count == 0 ? null : Encoding.UTF8.GetString(bytes.ToArray());
                    if (b == '\n')
                    {
                        if (bytes.Count > 0 && bytes[bytes.Count - 1] == '\r') bytes.RemoveAt(bytes.Count - 1);
                        return Encoding.UTF8.GetString(bytes.ToArray());
                    }
                    bytes.Add((byte)b);
                }
            }

            private byte[] ReadBytes(int count)
            {
                while (_buffer.Count < count)
                {
                    int b = _stream.ReadByte();
                    if (b < 0) break;
                    _buffer.Add((byte)b);
                }
                int take = Math.Min(count, _buffer.Count);
                byte[] result = _buffer.GetRange(0, take).ToArray();
                _buffer.RemoveRange(0, take);
                return result;
            }

            private int ReadByte()
            {
                if (_buffer.Count > 0)
                {
                    int b = _buffer[0];
                    _buffer.RemoveAt(0);
                    return b;
                }
                return _stream.ReadByte();
            }

            public void Dispose()
            {
                try { Send("A999 LOGOUT"); } catch (Exception) { }
                try { _stream.Dispose(); } catch (Exception) { }
                try { _client.Close(); } catch (Exception) { }
            }
        }
    }
}
