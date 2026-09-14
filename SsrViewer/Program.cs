using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SsrViewer
{
    internal static class Program
    {
        private static string nextSkelPath = null!;
        private static string nextAtlasPath = null!;
        private static string? nextVoiceDir = null;
        private static Point? nextLocation;

        private static bool available;

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            OpenSsrWindow(SelectSpine()!);
            while (available)
            {
                available = false;
                var window = new SsrWindow(nextSkelPath, nextAtlasPath, nextVoiceDir);
                if (nextLocation != null) {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = nextLocation.Value;
                }
                Application.Run(window);
            }
        }

        public static string? SelectSpine()
        {
            var dialog = new OpenFileDialog { Filter = "Png|*.png" };
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK) return null;
            var file = dialog.FileName;
            if (Path.HasExtension(file))
                file = file[..^Path.GetExtension(file).Length];
            return file;
        }

        public static void OpenSsrWindow(string fileNameWithoutExt, Point? location = null)
        {
            string skelPath;
            if (File.Exists(fileNameWithoutExt + ".skel"))
                skelPath = fileNameWithoutExt + ".skel";
            else
                skelPath = fileNameWithoutExt + ".skel.bytes";

            string atlasPath;
            if (File.Exists(fileNameWithoutExt + ".atlas"))
                atlasPath = fileNameWithoutExt + ".atlas";
            else
                atlasPath = fileNameWithoutExt + ".atlas.txt";

            string? voiceDir = null;
            if (Directory.Exists(fileNameWithoutExt))
                voiceDir = fileNameWithoutExt;

            if (voiceDir == null && Path.GetFileName(fileNameWithoutExt).Contains('_'))
            {
                string withoutSkin = fileNameWithoutExt[0..fileNameWithoutExt.LastIndexOf('_')];

                if (Directory.Exists(withoutSkin))
                    voiceDir = withoutSkin;
            }

            OpenSsrWindow(skelPath, atlasPath, voiceDir, location);
        }

        public static void OpenSsrWindow(string skelPath, string atlasPath, string? voiceDir = null, Point? location = null)
        {
            nextSkelPath = skelPath;
            nextAtlasPath = atlasPath;
            nextVoiceDir = voiceDir;
            nextLocation = location;

            available = true;
        }
    }
}
