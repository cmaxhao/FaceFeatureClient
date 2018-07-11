using System.Runtime.InteropServices;

namespace DF_FaceTracking.cs
{
    class FPSTimer
    {
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long data);
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long data);

        private MainForm form;
        private long freq, last;
        private int fps;

        public FPSTimer(MainForm mf)
        {
            form = mf;
            QueryPerformanceFrequency(out freq);
            fps = 0;
            QueryPerformanceCounter(out last);
        }

        public void Tick(string text)
        {
            long now;
            QueryPerformanceCounter(out now);
            fps++;
            if (now - last > freq) // update every second
            {
                last = now;
                form.UpdateStatus(text+" FPS=" + fps, MainForm.Label.StatusLabel);
                fps = 0;
            }
        }
    }
}
