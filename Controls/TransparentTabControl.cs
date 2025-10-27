namespace HardwareSerialChecker.Controls;

public class TransparentTabControl : TabControl
{
    public TransparentTabControl()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | 
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        this.DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Parent != null)
        {
            var state = e.Graphics.Save();
            try
            {
                var selfOnScreen = this.PointToScreen(Point.Empty);
                var pOnScreen = Parent.PointToScreen(Point.Empty);
                e.Graphics.TranslateTransform(pOnScreen.X - selfOnScreen.X, pOnScreen.Y - selfOnScreen.Y);
                var pe = new PaintEventArgs(e.Graphics, Parent.DisplayRectangle);
                Parent.InvokePaintBackground(Parent, pe);
                Parent.InvokePaint(Parent, pe);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        
        
        // Draw tabs
        for (int i = 0; i < this.TabCount; i++)
        {
            DrawTab(e.Graphics, this.TabPages[i], i);
        }
    }

    private void DrawTab(Graphics g, TabPage tabPage, int index)
    {
        Rectangle tabRect = this.GetTabRect(index);
        bool selected = (this.SelectedIndex == index);
        
        
        using (var brush = new SolidBrush(selected ? Color.Maroon : Color.Black))
        {
            g.FillRectangle(brush, tabRect);
        }
        
        // Draw tab border
        using (var pen = new Pen(Color.Maroon, 1))
        {
            g.DrawRectangle(pen, tabRect);
        }
        
        // Draw tab text
        using (var brush = new SolidBrush(selected ? Color.White : Color.Maroon))
        {
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(tabPage.Text, this.Font, brush, tabRect, sf);
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        this.Invalidate();
    }
}
