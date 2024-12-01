using System;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Windows.Forms;

public sealed class BitmapDisplayForm : Form
{
    public static void ShowBitmap(Bitmap bitmap)
    {
        BitmapDisplayForm bitmapDisplayForm = new BitmapDisplayForm(bitmap);
        bitmapDisplayForm.ShowDialog();
    }
    private Bitmap _bitmap;
    public BitmapDisplayForm(Bitmap bitmap)
    {
        _bitmap = bitmap;
        ResizeRedraw = true;
        StartPosition = FormStartPosition.Manual;
        Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
        ClientSize = new Size(bitmap.Width, bitmap.Height);
        int x = screenBounds.X + ((screenBounds.Width - ClientSize.Width) / 2);
        int y = screenBounds.Y + ((screenBounds.Height - ClientSize.Height) / 2);
        Location = new Point(x, y);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.Clear(Color.FromArgb(255, 0, 0));
        e.Graphics.DrawImage(_bitmap, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), new Rectangle(0, 0, _bitmap.Width, _bitmap.Height), GraphicsUnit.Pixel);
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}