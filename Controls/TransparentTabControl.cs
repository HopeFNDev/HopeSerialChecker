namespace HardwareSerialChecker.Controls;

public class TransparentTabControl : TabControl
{
    public TransparentTabControl()
    {
        // Rely on the base TabControl for all painting/layout, but enable
        // double-buffering and owner-draw headers so we can style them.
        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.OptimizedDoubleBuffer |
                      ControlStyles.UserPaint |
                      ControlStyles.ResizeRedraw,
                      true);
        this.DoubleBuffered = true;

        this.DrawMode = TabDrawMode.OwnerDrawFixed;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Use the standard background painting (no WS_EX_TRANSPARENT here).
        base.OnPaintBackground(e);

        using (var brush = new SolidBrush(Color.Black))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var backgroundBrush = new SolidBrush(Color.Black))
        {
            e.Graphics.FillRectangle(backgroundBrush, this.ClientRectangle);
        }

        Color blurple = Color.FromArgb(88, 101, 242);
        Color darkBackground = Color.Black;

        for (int i = 0; i < this.TabCount; i++)
        {
            var tabPage = this.TabPages[i];
            Rectangle tabRect = this.GetTabRect(i);

            bool selected = (this.SelectedIndex == i);

            using (var backBrush = new SolidBrush(selected ? blurple : darkBackground))
            {
                e.Graphics.FillRectangle(backBrush, tabRect);
            }

            using (var borderPen = new Pen(blurple, 1))
            {
                e.Graphics.DrawRectangle(borderPen, tabRect);
            }

            using (var textBrush = new SolidBrush(selected ? Color.White : blurple))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(tabPage.Text, this.Font, textBrush, tabRect, sf);
            }
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= this.TabPages.Count)
        {
            base.OnDrawItem(e);
            return;
        }

        var tabPage = this.TabPages[e.Index];
        Rectangle tabRect = this.GetTabRect(e.Index);

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                        || this.SelectedIndex == e.Index;

        Color blurple = Color.FromArgb(88, 101, 242);
        Color darkBackground = Color.Black;

        using (var backBrush = new SolidBrush(selected ? blurple : darkBackground))
        {
            e.Graphics.FillRectangle(backBrush, tabRect);
        }

        using (var borderPen = new Pen(blurple, 1))
        {
            e.Graphics.DrawRectangle(borderPen, tabRect);
        }

        using (var textBrush = new SolidBrush(selected ? Color.White : blurple))
        {
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString(tabPage.Text, this.Font, textBrush, tabRect, sf);
        }
    }
}
