using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace JellyGate
{
    public sealed class QaScreenshotRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            if (string.IsNullOrWhiteSpace(ReadArgument("-qaScreenshot"))) return;
            Application.runInBackground = true;
            var runner = new GameObject("QA Screenshot Runner").AddComponent<QaScreenshotRunner>();
            DontDestroyOnLoad(runner.gameObject);
            Debug.Log("QA screenshot runner created.");
        }

        private IEnumerator Start()
        {
            var outputPath = ReadArgument("-qaScreenshot");
            var delayArgument = ReadArgument("-qaDelay");
            var delay = float.TryParse(delayArgument, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedDelay)
                ? Mathf.Clamp(parsedDelay, 1f, 30f)
                : 4.5f;
            // The production boot loader performs atlas and boss-presentation prewarming.
            // An absolute timer could fire at its 29% stage and falsely certify the loading
            // illustration instead of the requested UI. Start the capture delay only after
            // the loader has completed and removed itself.
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null)
                yield return null;
            yield return new WaitForSecondsRealtime(delay);
            Debug.Log($"Capturing QA screenshot to {outputPath}");
            yield return new WaitForEndOfFrame();
            // ScreenCapture includes the final IMGUI/UI composition. Reading the camera
            // framebuffer directly can capture only the world layer in standalone QA builds.
            var captureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var captureMethod = captureType?.GetMethod("CaptureScreenshotAsTexture",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, Type.EmptyTypes, null);
            var image = captureMethod?.Invoke(null, null) as Texture2D;
            if (image == null)
            {
                image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                image.Apply();
            }
            var sampled = image.GetPixels32();
            var visibleSamples = 0;
            for (var i = 0; i < sampled.Length; i += Mathf.Max(1, sampled.Length / 128))
                if (sampled[i].r + sampled[i].g + sampled[i].b > 24) visibleSamples++;
            if (visibleSamples < 4)
            {
                Destroy(image);
                image = CaptureCameraFrame();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            SaveBmp(outputPath, image.GetPixels32(), image.width, image.height);
            Destroy(image);
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit();
        }

        private static Texture2D CaptureCameraFrame()
        {
            var camera = Camera.main;
            var renderTarget = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = renderTarget;
            camera.Render();
            RenderTexture.active = renderTarget;
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTarget.Release();
            Destroy(renderTarget);
            return image;
        }

        private static void SaveBmp(string path, Color32[] pixels, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            const int headerSize = 54;
            int rowSize = width * 4;
            int pixelBytes = rowSize * height;
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);

            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(headerSize + pixelBytes);
            writer.Write(0);
            writer.Write(headerSize);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(0);
            writer.Write(pixelBytes);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 color = pixels[row + x];
                    writer.Write(color.b);
                    writer.Write(color.g);
                    writer.Write(color.r);
                    writer.Write((byte)255);
                }
            }
        }

        private static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
