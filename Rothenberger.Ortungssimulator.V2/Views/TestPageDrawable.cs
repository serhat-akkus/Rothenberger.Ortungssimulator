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
                    canvas.DrawProbeCircle(vm.ProbePosition, vm.ProbeRadiusPixels);
                }

                if (vm.IsClickPointVisible)
                {
                    canvas.DrawClickMarker(vm.ClickPointPosition);
                }
            }
        }
    }
}