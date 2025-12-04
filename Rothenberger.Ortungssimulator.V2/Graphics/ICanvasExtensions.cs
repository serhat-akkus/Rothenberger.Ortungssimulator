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

            // Mittelpunkt (Sonde selbst)
            canvas.FillColor = Colors.Blue;
            canvas.FillCircle((float)p.X, (float)p.Y, 6);

            // Einklinkradius
            var fillColor = Colors.Blue.WithAlpha(0.5f);
            var strokeColor = Colors.Blue.WithAlpha(0.8f);
            var strokeSize = 1f;
            canvas.DrawProbeRadius(probePosition, radiusPixels, fillColor, strokeColor, strokeSize);
        }

        public static void DrawProbeRadius(this ICanvas canvas, Point probePosition, double radiusPixels, Color? fillColor, Color? strokeColor, float strokeSize)
        {
            var p = probePosition;

            // Transparenter Bereich (Detektionsradius)
            canvas.FillColor = fillColor;
            canvas.StrokeColor = strokeColor;
            canvas.StrokeSize = strokeSize;
            canvas.FillCircle((float)p.X, (float)p.Y, (float)radiusPixels);
            canvas.DrawCircle((float)p.X, (float)p.Y, (float)radiusPixels);
        }
    }
}
