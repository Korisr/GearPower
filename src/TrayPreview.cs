using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace GearPower
{
    public static class TrayPreview
    {
        public static void Run()
        {
            int[] levels = new int[] { 82, 23, 12 };
            string[] labels = new string[] { "82%  正常", "23%  偏低", "12%  低电量" };
            using (var canvas = new Bitmap(480, 190, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(canvas))
            using (var labelFont = new Font("Microsoft YaHei UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var labelBrush = new SolidBrush(Color.FromArgb(230, 240, 245, 250)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.FromArgb(255, 10, 15, 22));
                for (int i = 0; i < levels.Length; i++)
                {
                    var readings = new List<DeviceReading>();
                    readings.Add(Sample("preview:" + i, "Preview", levels[i]));
                    using (Bitmap icon = TrayIconRenderer.RenderBitmap(readings, 64))
                    {
                        int x = 32 + i * 148;
                        graphics.DrawImage(icon, x, 22, 112, 112);
                        var size = graphics.MeasureString(labels[i], labelFont);
                        graphics.DrawString(labels[i], labelFont, labelBrush,
                            x + (112 - size.Width) / 2, 142);
                    }
                }
                canvas.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tray-preview.png"), ImageFormat.Png);
            }
            Log.Info("Tray preview written");
        }

        private static DeviceReading Sample(string id, string name, int percent)
        {
            var reading = new DeviceReading();
            reading.Id = id;
            reading.Name = name;
            reading.Vendor = "Preview";
            reading.Kind = DeviceKind.Mouse;
            reading.Transport = "2.4G 接收器";
            reading.Protocol = "Preview";
            reading.Percent = percent;
            reading.Updated = DateTime.Now;
            reading.IsDemo = true;
            return reading;
        }
    }
}
