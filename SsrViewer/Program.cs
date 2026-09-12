using OpenTK.Mathematics;
using System;
using System.IO;

namespace SsrViewer
{
    internal static class Program
    {
        private static string nextSkelPath = null!;
        private static string nextAtlasPath = null!;
        private static string? nextVoiceDir = null;
        private static Vector2i? nextLocation;

        private static bool available;

        [STAThread]
        static void Main()
        {
            OpenSsrWindow(@"C:\Users\tkdc\Downloads\arknightsPresets\sussurro", SsrWindow.GetDesktopSize() / 2 - new Vector2i(800, 800));
            while (available)
            {
                available = false;
                using var window = new SsrWindow(nextSkelPath, nextAtlasPath, nextVoiceDir);
                if (nextLocation != null)
                    window.Location = nextLocation.Value;
                window.Run();
            }
        }

        public static void OpenSsrWindow(string fileNameWithoutExt, Vector2i? location = null)
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

            nextSkelPath = skelPath;
            nextAtlasPath = atlasPath;
            nextVoiceDir = voiceDir;
            nextLocation = location;

            available = true;
        }
    }
}
