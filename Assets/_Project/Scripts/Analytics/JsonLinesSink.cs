using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VoidProspector.Analytics
{
    /// <summary>
    /// Appends events to a local file, one JSON object per line. Buffered; written on <see cref="Flush"/>. When the
    /// file passes <see cref="MaxBytes"/> it is moved to <c>.old</c> (replacing the previous one) and a new file starts.
    /// Never throws: a failed write is reported once through <see cref="Error"/> and the events are dropped.
    /// </summary>
    public sealed class JsonLinesSink : IAnalyticsSink
    {
        public const long MaxBytes = 5 * 1024 * 1024;

        readonly List<string> _buffer = new List<string>();

        public string FilePath { get; }
        public string OldFilePath => FilePath + ".old";
        public int Written { get; private set; }
        /// <summary>The last write error, or null.</summary>
        public string Error { get; private set; }

        public JsonLinesSink(string filePath) => FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        public void Send(AnalyticsEvent e) => _buffer.Add(e.ToJson());

        public void Flush()
        {
            if (_buffer.Count == 0) return;
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                {
                    if (File.Exists(OldFilePath)) File.Delete(OldFilePath);
                    File.Move(FilePath, OldFilePath);
                }
                var sb = new StringBuilder();
                foreach (var line in _buffer) sb.Append(line).Append('\n');
                File.AppendAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
                Written += _buffer.Count;
                Error = null;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            _buffer.Clear();
        }
    }
}
