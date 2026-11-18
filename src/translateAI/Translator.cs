using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace translateAI
{
    /// <summary>一个可选语言：代码 / 中文名 / 英文名。</summary>
    internal sealed class Lang
    {
        public readonly string Code;
        public readonly string Zh;
        public readonly string En;

        public Lang(string code, string zh, string en)
        {
            Code = code;
            Zh = zh;
            En = en;
        }

        public override string ToString()
        {
            return I18n.IsEn ? En : Zh;
        }
    }

    internal static class Languages
    {
        public const string Auto = "auto";

        public static readonly Lang[] All = new Lang[]
        {
            new Lang("auto", "自动检测", "Auto detect"),
            new Lang("zh", "中文",     "Chinese"),
            new Lang("en", "英文",     "English"),
            new Lang("ja", "日文",     "Japanese"),
            new Lang("ko", "韩文",     "Korean"),
            new Lang("fr", "法文",     "French"),
            new Lang("de", "德文",     "German"),
            new Lang("es", "西班牙文", "Spanish"),
            new Lang("ru", "俄文",     "Russian"),
            new Lang("pt", "葡萄牙文", "Portuguese"),
            new Lang("it", "意大利文", "Italian"),
            new Lang("ar", "阿拉伯文", "Arabic"),
            new Lang("th", "泰文",     "Thai"),
            new Lang("vi", "越南文",   "Vietnamese")
        };

        public static Lang Find(string code)
        {
            foreach (Lang l in All)
            {
                if (l.Code == code) return l;
            }
            return All[1];
        }

        /// <summary>自动检测：含汉字当中文，否则当英文。</summary>
        public static string Detect(string text)
        {
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) return "zh";
            }
            return "en";
        }
    }

    internal static class Translator
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        /// <summary>
        /// 混元官方建议：中↔外 用中文提示词，外↔外 用英文提示词。
        /// </summary>
        public static string BuildPrompt(string text, string from, string to)
        {
            Lang target = Languages.Find(to);

            if (from == "zh" || to == "zh")
            {
                return "将以下文本翻译为" + target.Zh +
                       "，注意只需要输出翻译后的结果，不要额外解释：\n\n" + text;
            }

            return "Translate the following segment into " + target.En +
                   ", without additional explanation.\n\n" + text;
        }

        /// <summary>解析出实际使用的源/目标语言（含自动检测与同语言纠正）。</summary>
        public static void Resolve(string text, string from, string to, out string src, out string dst)
        {
            src = from;
            dst = to;

            if (src == Languages.Auto)
            {
                src = Languages.Detect(text);
                if (src == dst)
                    dst = (src == "zh") ? "en" : "zh";
            }

            if (src == dst)
                throw new InvalidOperationException("源语言和目标语言相同，请调整或点反转。");
        }

        /// <summary>整段翻译（不分行）。</summary>
        public static async Task<string> TranslateAsync(
            HttpClient http, int port, string text, string from, string to, CancellationToken ct)
        {
            string src, dst;
            Resolve(text, from, to, out src, out dst);
            return await TranslateSegmentAsync(http, port, text, src, dst, ct);
        }

        /// <summary>
        /// 按行翻译：原文有换行的位置，译文也换行；空白行丢掉。
        /// 语言方向在整篇上解析一次，避免逐行检测造成方向漂移。
        /// </summary>
        public static async Task<string> TranslateTextAsync(
            HttpClient http, int port, string text, string from, string to,
            Action<int, int> progress, CancellationToken ct)
        {
            string src, dst;
            Resolve(text, from, to, out src, out dst);

            List<string> lines = SplitLines(text);

            if (lines.Count <= 1)
            {
                string single = lines.Count == 1 ? lines[0] : "";
                return await TranslateSegmentAsync(http, port, single, src, dst, ct);
            }

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < lines.Count; i++)
            {
                if (progress != null) progress(i + 1, lines.Count);

                string translated = await TranslateSegmentAsync(http, port, lines[i], src, dst, ct);

                if (i > 0) sb.Append("\r\n");
                sb.Append(NormalizeNewlines(translated));
            }

            return sb.ToString();
        }

        /// <summary>按行切分并去掉空白行；\r\n、\r、\n 都当换行。</summary>
        public static List<string> SplitLines(string text)
        {
            List<string> result = new List<string>();
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');

            foreach (string raw in normalized.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0) result.Add(line);
            }

            return result;
        }

        /// <summary>
        /// WinForms 多行 TextBox 只认 CRLF，单独的 LF 不会渲染成换行
        /// （但复制出去会被系统补成 CRLF，所以"拷出来有换行、界面里没有"）。
        /// </summary>
        public static string NormalizeNewlines(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        }

        private static async Task<string> TranslateSegmentAsync(
            HttpClient http, int port, string text, string src, string dst, CancellationToken ct)
        {
            Dictionary<string, object> message = new Dictionary<string, object>();
            message["role"] = "user";
            message["content"] = BuildPrompt(text, src, dst);

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["messages"] = new object[] { message };
            payload["temperature"] = 0.7;
            payload["top_k"] = 20;
            payload["top_p"] = 0.6;
            payload["max_tokens"] = 512;
            payload["stream"] = false;

            byte[] bytes = Encoding.UTF8.GetBytes(Json.Serialize(payload));
            string url = "http://127.0.0.1:" + port + "/v1/chat/completions";

            using (HttpContent content = new ByteArrayContent(bytes))
            {
                content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                using (HttpResponseMessage resp = await http.PostAsync(url, content, ct))
                {
                    string body = await resp.Content.ReadAsStringAsync();

                    if (!resp.IsSuccessStatusCode)
                        throw new InvalidOperationException(
                            "引擎返回 " + (int)resp.StatusCode + "：" + Shorten(body, 400));

                    return ExtractContent(body);
                }
            }
        }

        private static string ExtractContent(string responseJson)
        {
            Dictionary<string, object> root;
            try
            {
                root = Json.Deserialize<Dictionary<string, object>>(responseJson);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("引擎响应不是合法 JSON：" + Shorten(responseJson, 300), ex);
            }

            if (root == null) throw new InvalidOperationException("引擎响应为空。");

            object choicesObj;
            if (!root.TryGetValue("choices", out choicesObj))
                throw new InvalidOperationException("响应里没有 choices 字段：" + Shorten(responseJson, 300));

            List<object> choices = ToList(choicesObj);
            if (choices.Count == 0)
                throw new InvalidOperationException("引擎没有返回候选结果：" + Shorten(responseJson, 300));

            IDictionary<string, object> first = choices[0] as IDictionary<string, object>;
            if (first == null) throw new InvalidOperationException("choices[0] 结构异常。");

            object messageObj;
            if (!first.TryGetValue("message", out messageObj))
                throw new InvalidOperationException("choices[0] 里没有 message。");

            IDictionary<string, object> message = messageObj as IDictionary<string, object>;
            if (message == null) throw new InvalidOperationException("message 结构异常。");

            object contentObj;
            message.TryGetValue("content", out contentObj);

            string content = contentObj as string;
            return (content ?? "").Trim();
        }

        private static List<object> ToList(object value)
        {
            List<object> list = new List<object>();
            if (value == null) return list;

            object[] arr = value as object[];
            if (arr != null) { list.AddRange(arr); return list; }

            IEnumerable seq = value as IEnumerable;
            if (seq != null)
            {
                foreach (object item in seq) list.Add(item);
                return list;
            }

            list.Add(value);
            return list;
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
