using System;
using Microsoft.Maui.Controls;

namespace Rothenberger.Ortungssimulator.V2.Views
{
    public partial class RunsCompletedDialog : ContentView
    {
        double _startX;
        double _startY;

        public RunsCompletedDialog()
        {
            InitializeComponent();
        }

        void OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startX = Card.TranslationX;
                    _startY = Card.TranslationY;
                    break;
                case GestureStatus.Running:
                    Card.TranslationX = _startX + e.TotalX;
                    Card.TranslationY = _startY + e.TotalY;
                    break;
                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    ClampInsideParent();
                    break;
            }
        }

        void ClampInsideParent()
        {
            if (Parent is not VisualElement parent)
                return;

            var w = Card.Width;
            var h = Card.Height;
            if (w <= 0 || h <= 0 || parent.Width <= 0 || parent.Height <= 0)
                return;

            var maxX = (parent.Width / 2) - (w / 2);
            var minX = -maxX;
            var maxY = (parent.Height / 2) - (h / 2);
            var minY = -maxY;

            Card.TranslationX = Math.Max(minX, Math.Min(maxX, Card.TranslationX));
            Card.TranslationY = Math.Max(minY, Math.Min(maxY, Card.TranslationY));
        }
    }
}