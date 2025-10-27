using System.Text;

namespace HardwareSerialChecker.Controls;

public class TransparentDataGridView : DataGridView
{
    public TransparentDataGridView()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        BackgroundColor = Color.Black;
        BackColor = Color.Black;
        EnableHeadersVisualStyles = false;
        BorderStyle = BorderStyle.None;
    }

    private Control? GetBackgroundSourceControl()
    {
        Control? c = this.Parent;
        Control? candidate = null;
        while (c != null)
        {
            if (c is AnimatedBackgroundPanel)
            {
                candidate = c;
                break;
            }
            candidate = c; // fallback to highest ancestor if panel not found
            c = c.Parent;
        }
        return candidate;
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
        var bg = GetBackgroundSourceControl();
        if (bg != null)
        {
            var state = e.Graphics.Save();
            try
            {
                var selfOnScreen = this.PointToScreen(Point.Empty);
                var bgOnScreen = bg.PointToScreen(Point.Empty);
                e.Graphics.TranslateTransform(bgOnScreen.X - selfOnScreen.X, bgOnScreen.Y - selfOnScreen.Y);
                var pe = new PaintEventArgs(e.Graphics, bg.DisplayRectangle);
                bg.InvokePaintBackground(bg, pe);
                bg.InvokePaint(bg, pe);
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
        var bg = GetBackgroundSourceControl();
        if (bg != null)
        {
            var state = e.Graphics.Save();
            try
            {
                var selfOnScreen = this.PointToScreen(Point.Empty);
                var bgOnScreen = bg.PointToScreen(Point.Empty);
                e.Graphics.TranslateTransform(bgOnScreen.X - selfOnScreen.X, bgOnScreen.Y - selfOnScreen.Y);
                var pe = new PaintEventArgs(e.Graphics, bg.DisplayRectangle);
                bg.InvokePaintBackground(bg, pe);
                bg.InvokePaint(bg, pe);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }
        base.OnPaint(e);

        // After base painting, repaint parent's background over any unused area
        // to avoid solid background showing (right/bottom beyond cells area).
        var bg2 = GetBackgroundSourceControl();
        if (bg2 != null)
        {
            var cellsRect = this.DisplayRectangle;
            var client = this.ClientRectangle;

            // Right area
            if (cellsRect.Right < client.Right)
            {
                var rightRect = new Rectangle(cellsRect.Right, client.Top, client.Right - cellsRect.Right, client.Height);
                PaintAncestorRegion(e.Graphics, bg2, rightRect);
            }

            // Bottom area
            if (cellsRect.Bottom < client.Bottom)
            {
                var bottomRect = new Rectangle(client.Left, cellsRect.Bottom, client.Width, client.Bottom - cellsRect.Bottom);
                PaintAncestorRegion(e.Graphics, bg2, bottomRect);
            }
        }
    }

    private void PaintAncestorRegion(Graphics g, Control bg, Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        var state = g.Save();
        try
        {
            var selfOnScreen = this.PointToScreen(Point.Empty);
            var bgOnScreen = bg.PointToScreen(Point.Empty);
            g.TranslateTransform(bgOnScreen.X - selfOnScreen.X, bgOnScreen.Y - selfOnScreen.Y);
            using var reg = new Region(r);
            g.Clip = reg;
            var pe = new PaintEventArgs(g, bg.DisplayRectangle);
            bg.InvokePaintBackground(bg, pe);
            bg.InvokePaint(bg, pe);
        }
        finally
        {
            g.Restore(state);
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

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        // Only modify data cells, not header/row header
        bool isHeader = e.RowIndex < 0 || e.ColumnIndex < 0;
        if (isHeader)
        {
            base.OnCellPainting(e);
            return;
        }

        // Paint background with parent's content for non-selected cells
        // and draw border/content on top.
        var parts = DataGridViewPaintParts.Border | DataGridViewPaintParts.ContentForeground;

        // Selection highlight
        bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
        if (selected)
        {
            using var selBrush = new SolidBrush(Color.Maroon);
            e.Graphics.FillRectangle(selBrush, e.CellBounds);
            // Draw foreground after our background
            parts |= DataGridViewPaintParts.ContentForeground;
        }
        else
        {
            var bg = GetBackgroundSourceControl();
            if (bg != null)
                PaintAncestorRegion(e.Graphics, bg, e.CellBounds);
        }

        e.Paint(e.ClipBounds, parts);
        e.Handled = true;
    }
}
