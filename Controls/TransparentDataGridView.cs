using System.Text;

namespace HardwareSerialChecker.Controls;

public class TransparentDataGridView : DataGridView
{
    public TransparentDataGridView()
    {
        // Behave like a normal DataGridView, but with double-buffering
        // and black styling. All painting is handled by the base class,
        // so the animated background never appears inside the table.
        DoubleBuffered = true;
        BackgroundColor = Color.Black;
        BackColor = Color.Black;
        EnableHeadersVisualStyles = false;
        BorderStyle = BorderStyle.None;
    }
}
