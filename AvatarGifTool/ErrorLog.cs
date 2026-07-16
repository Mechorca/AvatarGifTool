using System;
using System.IO;
using System.Text;

namespace AvatarGifTool
{
    internal static class ErrorLog
    {
        private static readonly object SyncRoot = new object();

        public static void Write(Exception ex, string context = null)
        {
            if (ex == null)
            {
                return;
            }

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
                if (!string.IsNullOrWhiteSpace(context))
                {
                    builder.AppendLine($"Context: {context}");
                }
                builder.AppendLine(ex.ToString());
                builder.AppendLine();

                string path = Path.Combine(AppContext.BaseDirectory, "error.log");
                lock (SyncRoot)
                {
                    File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
