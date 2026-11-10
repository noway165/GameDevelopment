using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Metropolis.Game
{
    /// <summary>
    /// Launched with "-capture folder", takes menu and HUD screenshots and quits,
    /// so tools/check-player.ps1 can prove the built game renders. Otherwise does nothing.
    /// </summary>
    public sealed class LaunchCapture : MonoBehaviour
    {
        [SerializeField] GameUi ui;

        IEnumerator Start()
        {
            var folder = CaptureFolder();
            if (folder == null)
                yield break;

            yield return Frames(60);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "menu.png"));
            yield return Frames(10);

            ui.NewCity();
            yield return Frames(60);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "hud.png"));
            yield return Frames(30);

            // Application.Quit directly, so a capture run never touches the player's save.
            Application.Quit();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        static string CaptureFolder()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-capture")
                    return args[i + 1];
            return null;
        }
    }
}
