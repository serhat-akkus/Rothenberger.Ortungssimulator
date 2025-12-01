using Rothenberger.Ortungssimulator.V2.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rothenberger.Ortungssimulator.V2.Graphics
{
    public static class ICanvasExtensions
    {
        public static void DrawCanvasBackground(this ICanvas canvas, RectF dirtyRect)
        {
            // Hintergrund
            canvas.FillColor = Colors.LightGray;
            canvas.FillRectangle(dirtyRect);

            // Schwarzer Rand
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 2;
            canvas.DrawRectangle(dirtyRect);
        }

        public static void DrawClickMarker(this ICanvas canvas, Point clickPointPosition)
        {
            var p = clickPointPosition;

            // Mausklick: rotes Kreuz
            canvas.StrokeColor = Colors.Red;
            canvas.StrokeSize = 4;
            canvas.DrawLine((float)p.X - 10, (float)p.Y - 10, (float)p.X + 10, (float)p.Y + 10);
            canvas.DrawLine((float)p.X + 10, (float)p.Y - 10, (float)p.X - 10, (float)p.Y + 10);
        }

        public static void DrawProbeCircle(this ICanvas canvas, Point probePosition)
        {
            var p = probePosition;

            // Probe: blauer Kreis
            canvas.FillColor = Colors.Blue;
            canvas.FillCircle((float)p.X, (float)p.Y, 5);
        }
    }
}
