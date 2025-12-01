using Rothenberger.Ortungssimulator.V2.ViewModels;
using System;

namespace Rothenberger.Ortungssimulator.V2.Graphics
{
    public static class ICanvasExtensions
    {
        public static void DrawCanvasBackground(this ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.LightGray;
            canvas.FillRectangle(dirtyRect);

            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 2;
            canvas.DrawRectangle(dirtyRect);
        }

        public static void DrawClickMarker(this ICanvas canvas, Point clickPointPosition)
        {
            var p = clickPointPosition;
            canvas.StrokeColor = Colors.Red;
            canvas.StrokeSize = 4;
            canvas.DrawLine((float)p.X - 10, (float)p.Y - 10, (float)p.X + 10, (float)p.Y + 10);
            canvas.DrawLine((float)p.X + 10, (float)p.Y - 10, (float)p.X - 10, (float)p.Y + 10);
        }

        public static void DrawProbeCircle(this ICanvas canvas, Point probePosition, double radiusPixels)
        {
            var p = probePosition;

            // Transparenter Bereich (Detektionsradius)
            canvas.FillColor = Colors.Blue.WithAlpha(0.25f);
            canvas.StrokeColor = Colors.Blue.WithAlpha(0.6f);
            canvas.StrokeSize = 2;
            canvas.FillCircle((float)p.X, (float)p.Y, (float)radiusPixels);
            canvas.DrawCircle((float)p.X, (float)p.Y, (float)radiusPixels);

            // Mittelpunkt (Sonde selbst)
            canvas.FillColor = Colors.Blue;
            canvas.FillCircle((float)p.X, (float)p.Y, 6);
        }
    }
}
