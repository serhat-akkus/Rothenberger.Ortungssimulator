using Rothenberger.Ortungssimulator.V2.Graphics;

namespace Rothenberger.Ortungssimulator.V2.Views
{
    public partial class TestPage
    {
        private class TestPageDrawable : IDrawable
        {
            private readonly TestPage _page;
            public TestPageDrawable(TestPage page) { _page = page; }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                canvas.DrawCanvasBackground(dirtyRect);

                var vm = _page._viewModel;
                if (vm == null) return;

                if (vm.IsProbeVisible)
                {
                    canvas.DrawProbeCircle(vm.ProbePosition, vm.EinklinkRadiusPixels);
                    
                    if (vm.IsProberadiusEnabled)
                    {
                        var fillColor = Colors.Blue.WithAlpha(0.25f);
                        var strokeColor = Colors.Blue.WithAlpha(0.6f);
                        var strokeSize = 2f;
                        canvas.DrawProbeRadius(vm.ProbePosition, vm.ProbeRadiusPixelsIn2d, fillColor, strokeColor, strokeSize);
                    }
                }

                if (vm.IsClickPointVisible)
                {
                    canvas.DrawClickMarker(vm.ClickPointPosition);
                }
            }
        }
    }
}