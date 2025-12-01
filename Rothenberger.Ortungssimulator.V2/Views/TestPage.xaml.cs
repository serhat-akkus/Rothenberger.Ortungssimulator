using Rothenberger.Ortungssimulator.V2.ViewModels;

namespace Rothenberger.Ortungssimulator.V2.Views
{
    public partial class TestPage : ContentPage
    {
        private readonly TestPageViewModel _viewModel;

        public TestPage()
        {
            InitializeComponent();
            _viewModel = new TestPageViewModel();
            BindingContext = _viewModel;
            CanvasView.StartInteraction += CanvasView_StartInteraction;
            CanvasView.MoveHoverInteraction += CanvasView_MoveHoverInteraction;
            CanvasView.Drawable = new TestPageDrawable(this);
            ArrowView.Drawable = new ArrowBoxDrawable(_viewModel);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.CurrentMousePosition = new Point(500, 500); // Canvas-Mitte
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (BindingContext is TestPageViewModel vm)
            {
                var position = e.GetPosition((Element?)sender);
                vm.UpdateCurrentMousePosition(new Point(position.Value.X, position.Value.Y));
            }
        }

        private void CanvasView_StartInteraction(object sender, TouchEventArgs e)
        {
            var point = e.Touches.First();

            // Rotes X an der Klickstelle setzen
            _viewModel.OnCanvasClicked(point);
            _viewModel.IsClickPointVisible = true;

            // Blauen Punkt an der (bestehenden) Sondenposition anzeigen
            // WICHTIG: Keine Änderung an ProbePosition
            _viewModel.IsProbeVisible = true;

            CanvasView.Invalidate();
            ArrowView.Invalidate();
        }

        private void CanvasView_MoveHoverInteraction(object sender, TouchEventArgs e)
        {
            var point = e.Touches.First();

            // Keine Änderung der Sondenposition beim Hover/Move
            _viewModel.CurrentMousePosition = point;
            _viewModel.UpdateArrowToProbe();
            ArrowView.Invalidate();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TestPageViewModel.IsProbeVisible) ||
                e.PropertyName == nameof(TestPageViewModel.IsClickPointVisible))
            {
                CanvasView.Invalidate();
            }
            if (e.PropertyName == nameof(TestPageViewModel.IsArrowBoxVisible) ||
                e.PropertyName == nameof(TestPageViewModel.ArrowRotation))
            {
                ArrowView.Invalidate();
            }
        }
    }
}