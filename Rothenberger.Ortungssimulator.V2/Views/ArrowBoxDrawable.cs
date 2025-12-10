using Rothenberger.Ortungssimulator.V2.ViewModels;

namespace Rothenberger.Ortungssimulator.V2.Views
{
    public partial class TestPage
    {
        private class ArrowBoxDrawable : IDrawable
        {
            private readonly TestPageViewModel _vm;
            public ArrowBoxDrawable(TestPageViewModel vm) { _vm = vm; }
            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                if (!_vm.IsArrowBoxVisible) return;
                canvas.SaveState();

                // Mittelpunkt der Box
                float centerX = dirtyRect.Width / 2;
                float centerY = dirtyRect.Height / 2;
                canvas.Translate(centerX, centerY);
                canvas.Rotate((float)_vm.ArrowRotation);

                // Schaft
                float shaftLength = 28;
                float arrowHeadLength = 12;
                float arrowHeadWidth = 16;

                // Linie endet direkt vor dem Dreieck
                canvas.StrokeColor = Colors.Black;
                canvas.StrokeSize = 6;
                canvas.DrawLine(-shaftLength / 2, 0, shaftLength / 2, 0);

                // Dreieck: Spitze auf der Linie, Basis hinter dem Linienende
                canvas.FillColor = Colors.Black;
                var arrowHead = new PathF();
                arrowHead.MoveTo(shaftLength / 2 + arrowHeadLength, 0); // Spitze des Dreiecks (vorne)
                arrowHead.LineTo(shaftLength / 2, -arrowHeadWidth / 2); // links hinten
                arrowHead.LineTo(shaftLength / 2, arrowHeadWidth / 2);  // rechts hinten
                arrowHead.Close();
                canvas.FillPath(arrowHead);

                canvas.RestoreState();
            }
        }
    }
}