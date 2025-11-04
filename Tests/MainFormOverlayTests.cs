using System.Drawing;
using System.Threading;
using GlobalTextHelper;
using NUnit.Framework;

namespace WritingHelper.Tests
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MainFormOverlayTests
    {
        [Test]
        public void OverlayAppearsWhenSelectionIsPresent()
        {
            using var form = new MainForm();
            form.CreateControl();
            form.Editor.Text = "This is a sample paragraph for testing.";
            form.Editor.Select(0, 4);

            form.ForceOverlayRefreshForTest();

            Assert.That(form.SelectionOverlayVisible, Is.True);
        }

        [Test]
        public void OverlayHidesWhenSelectionClears()
        {
            using var form = new MainForm();
            form.CreateControl();
            form.Editor.Text = "Testing text selection overlay.";
            form.Editor.Select(0, 7);
            form.ForceOverlayRefreshForTest();
            Assert.That(form.SelectionOverlayVisible, Is.True);

            form.Editor.Select(0, 0);
            form.ForceOverlayRefreshForTest();

            Assert.That(form.SelectionOverlayVisible, Is.False);
        }

        [Test]
        public void OverlayRepositionsWhenHostMoves()
        {
            using var form = new MainForm();
            form.CreateControl();
            form.Location = new Point(50, 50);
            form.Editor.Text = "Reposition test with enough content to span lines.\nSecond line of text.";
            form.Editor.Select(0, form.Editor.Text.Length / 2);
            form.ForceOverlayRefreshForTest();
            var firstLocation = form.OverlayForm.Location;

            form.Location = new Point(400, 300);
            form.ForceOverlayRepositionForTest();
            var secondLocation = form.OverlayForm.Location;

            Assert.That(secondLocation, Is.Not.EqualTo(firstLocation));
        }
    }
}
