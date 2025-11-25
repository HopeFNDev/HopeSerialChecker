namespace HardwareSerialChecker.Controls;

public class TransparentTabPage : TabPage
{
    public TransparentTabPage(string text) : base(text)
    {
        // Solid black tab page over the animated background. We no longer
        // paint the animated panel inside the page, so no frozen particles
        // appear inside the content area.
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer,
                 true);
        DoubleBuffered = true;
        UseVisualStyleBackColor = false;
        BackColor = Color.Black;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Just fill with the tab page's BackColor (black).
        using var brush = new SolidBrush(this.BackColor);
        e.Graphics.FillRectangle(brush, this.ClientRectangle);
    }
}
