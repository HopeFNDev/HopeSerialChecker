namespace HardwareSerialChecker.Controls;

public class TransparentTabPage : TabPage
{
    public TransparentTabPage(string text) : base(text)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        UseVisualStyleBackColor = false;
        BackColor = Color.Black;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Parent != null)
        {
            var state = e.Graphics.Save();
            try
            {
                var translate = this.Location;
                e.Graphics.TranslateTransform(-translate.X, -translate.Y);
                var pe = new PaintEventArgs(e.Graphics, Parent.DisplayRectangle);
                InvokePaintBackground(Parent, pe);
                InvokePaint(Parent, pe);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }
        else
        {
            base.OnPaintBackground(e);
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_ERASEBKGND = 0x0014;
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }
}
