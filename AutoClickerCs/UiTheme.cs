using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AutoClickerCs;

public static class UiTheme
{
    public static readonly Color AppBackground = Color.FromArgb(24, 32, 51);
    public static readonly Color HeaderBackground = Color.FromArgb(31, 41, 64);
    public static readonly Color CardOuter = Color.FromArgb(46, 54, 88);
    public static readonly Color CardInner = Color.FromArgb(59, 67, 106);
    public static readonly Color Surface = Color.FromArgb(43, 49, 80);
    public static readonly Color SurfaceAlt = Color.FromArgb(38, 43, 67);
    public static readonly Color Accent = Color.FromArgb(91, 137, 243);
    public static readonly Color AccentSecondary = Color.FromArgb(76, 121, 218);
    public static readonly Color AccentBorder = Color.FromArgb(149, 182, 255);
    public static readonly Color Border = Color.FromArgb(57, 68, 94);
    public static readonly Color BorderSoft = Color.FromArgb(61, 72, 102);
    public static readonly Color TextPrimary = Color.FromArgb(243, 246, 255);
    public static readonly Color TextMuted = Color.FromArgb(158, 168, 198);
    public static readonly Color TextSoft = Color.FromArgb(181, 190, 215);
    public static readonly Font TitleFont = new("Segoe UI Semibold", 22f, FontStyle.Bold);
    public static readonly Font SectionFont = new("Segoe UI Semibold", 17f, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 14f, FontStyle.Regular);
    public static readonly Font SmallFont = new("Segoe UI", 13f, FontStyle.Regular);
    public static readonly Font StatusFont = new("Segoe UI Semibold", 14f, FontStyle.Bold);
    public static readonly Font HeaderTabFont = new("Segoe UI Semibold", 14f, FontStyle.Bold);

    public static void StyleInput(Control control)
    {
        control.BackColor = Surface;
        control.ForeColor = TextPrimary;
        control.Font = BodyFont;
        if (control is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is ComboBox comboBox)
        {
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.IntegralHeight = false;
            comboBox.DropDownHeight = 240;
        }
    }

    public static void StyleLabel(Label label, bool muted = false, bool section = false)
    {
        label.BackColor = Color.Transparent;
        label.ForeColor = section ? Color.FromArgb(185, 209, 255) : muted ? TextMuted : TextPrimary;
        label.Font = section ? SectionFont : (muted ? SmallFont : BodyFont);
    }

    public static void StyleCheckLike(ButtonBase control)
    {
        control.BackColor = Color.Transparent;
        control.ForeColor = TextPrimary;
        control.Font = BodyFont;
        if (control is RadioButton radio)
        {
            radio.FlatStyle = FlatStyle.Flat;
            radio.FlatAppearance.BorderColor = Border;
        }
        else if (control is CheckBox checkBox)
        {
            checkBox.FlatStyle = FlatStyle.Standard;
        }
    }

    public static void ConfigureGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public static void ConfigureFastGraphics(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.None;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.Low;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public static void DrawContinuousRoundedOutline(
        Graphics graphics,
        Rectangle bounds,
        int radius,
        Color color,
        float width = 1f)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        var inset = Math.Max(0.5f, width / 2f);
        using var path = CreateRoundedRectPath(
            new RectangleF(
                bounds.X + inset,
                bounds.Y + inset,
                Math.Max(1f, bounds.Width - (inset * 2f)),
                Math.Max(1f, bounds.Height - (inset * 2f))),
            Math.Max(0f, radius - inset));
        using var pen = new Pen(color, width)
        {
            Alignment = PenAlignment.Center
        };

        var previousSmoothing = graphics.SmoothingMode;
        var previousPixelOffset = graphics.PixelOffsetMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawPath(pen, path);
        graphics.SmoothingMode = previousSmoothing;
        graphics.PixelOffsetMode = previousPixelOffset;
    }

    public static GraphicsPath CreateRoundedRectPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var clampedRadius = Math.Max(0f, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f));
        var diameter = clampedRadius * 2f;

        if (diameter <= 0.01f)
        {
            path.AddRectangle(bounds);
            path.CloseFigure();
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

}

public sealed class ThemedTabControl : TabControl
{
    private const int TcmAdjustrect = 0x1328;

    public ThemedTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(126, 38);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = UiTheme.CardOuter;
        Padding = new Point(18, 10);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= TabPages.Count)
        {
            base.OnDrawItem(e);
            return;
        }

        var page = TabPages[e.Index];
        var rect = GetTabRect(e.Index);
        rect.Inflate(-4, -2);
        var selected = SelectedIndex == e.Index;
        using var textBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(215, 221, 240));

        UiTheme.ConfigureFastGraphics(e.Graphics);
        using var path = RoundedRect(rect, 16);
        using var brush = new SolidBrush(selected ? UiTheme.Accent : UiTheme.Surface);
        e.Graphics.FillPath(brush, path);
        UiTheme.DrawContinuousRoundedOutline(
            e.Graphics,
            rect,
            16,
            selected ? UiTheme.AccentBorder : UiTheme.Border);

        TextRenderer.DrawText(
            e.Graphics,
            page.Text,
            UiTheme.BodyFont,
            rect,
            textBrush.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is TabPage page)
        {
            page.BackColor = UiTheme.CardOuter;
            page.ForeColor = UiTheme.TextPrimary;
            page.BorderStyle = BorderStyle.None;
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == TcmAdjustrect && m.LParam != IntPtr.Zero)
        {
            var rect = Marshal.PtrToStructure<NativeRect>(m.LParam);
            rect.Left -= 6;
            rect.Top -= 4;
            rect.Right += 6;
            rect.Bottom += 6;
            Marshal.StructureToPtr(rect, m.LParam, true);
            m.Result = (IntPtr)1;
        }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 22;
    public Color FillColor { get; set; } = UiTheme.CardInner;
    public Color BorderColor { get; set; } = UiTheme.BorderSoft;
    public bool DrawShadow { get; set; } = true;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.CardInner;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        UiTheme.ConfigureGraphics(e.Graphics);

        var shadowOffset = DrawShadow ? 8 : 0;
        var bottomInset = DrawShadow ? 9 : 1;
        var shadowRect = new Rectangle(0, shadowOffset, Width - 1, Height - bottomInset);
        var fillRect = new Rectangle(0, 0, Width - 1, Height - bottomInset);

        if (DrawShadow)
        {
            using var shadowPath = RoundedRect(shadowRect, Radius);
            using var shadowBrush = new SolidBrush(Color.FromArgb(24, 16, 22, 31));
            e.Graphics.FillPath(shadowBrush, shadowPath);
        }

        using var path = RoundedRect(fillRect, Radius);
        using var brush = new SolidBrush(FillColor);
        using var pen = new Pen(BorderColor, 1f);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 1)
        {
            return;
        }

        var bottomInset = DrawShadow ? 9 : 1;
        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - bottomInset), Radius);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class AccentButton : Button
{
    public bool Primary { get; set; }

    public AccentButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
        BackColor = UiTheme.Surface;
        Height = 40;
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque,
            true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        UiTheme.ConfigureFastGraphics(pevent.Graphics);

        var fill = !Enabled
            ? Color.FromArgb(70, UiTheme.Surface)
            : Primary ? UiTheme.AccentSecondary : UiTheme.Surface;
        var border = !Enabled
            ? Color.FromArgb(70, UiTheme.Border)
            : Primary ? Color.FromArgb(92, 126, 198) : Color.FromArgb(76, 86, 118);

        using var path = RoundedRect(rect, 16);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);
        UiTheme.DrawContinuousRoundedOutline(pevent.Graphics, rect, 16, border);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            rect,
            Enabled ? Color.White : UiTheme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 16);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class InfoPill : Control
{
    public Color FillColor { get; set; } = UiTheme.Surface;
    public Color BorderColor { get; set; } = Color.FromArgb(76, 86, 118);
    public int Radius { get; set; } = 10;

    public InfoPill()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.CardInner;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI", 14f, FontStyle.Regular);
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque,
            true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        UiTheme.ConfigureFastGraphics(e.Graphics);

        using var path = RoundedRect(rect, Radius);
        using var brush = new SolidBrush(FillColor);
        e.Graphics.FillPath(brush, path);
        UiTheme.DrawContinuousRoundedOutline(e.Graphics, rect, Radius, BorderColor);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class PillValueEditor : Control
{
    private readonly TextBox _editor;
    private bool _committing;
    private string _displayText = string.Empty;

    public Color FillColor { get; set; } = UiTheme.Surface;
    public Color BorderColor { get; set; } = Color.FromArgb(76, 86, 118);
    public int Radius { get; set; } = 10;
    public event EventHandler? ValueCommitted;

    public PillValueEditor()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.CardInner;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI", 14f, FontStyle.Regular);
        Cursor = Cursors.IBeam;
        Size = new Size(86, 40);
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque,
            true);

        _editor = new TextBox
        {
            BorderStyle = BorderStyle.None,
            TextAlign = HorizontalAlignment.Center,
            BackColor = FillColor,
            ForeColor = ForeColor,
            Font = Font,
            Visible = false,
            ShortcutsEnabled = true
        };
        _editor.KeyDown += EditorOnKeyDown;
        _editor.Leave += EditorOnLeave;
        Controls.Add(_editor);
    }

    [AllowNull]
    public override string Text
    {
        get => _displayText;
        set
        {
            _displayText = value ?? string.Empty;
            if (_editor is not null)
            {
                _editor.Text = _displayText;
            }
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
        UpdateEditorBounds();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_editor is not null)
        {
            _editor.Font = Font;
        }
        UpdateEditorBounds();
        Invalidate();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        if (_editor is not null)
        {
            _editor.ForeColor = ForeColor;
        }
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.IBeam : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Enabled)
        {
            BeginEdit();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        UiTheme.ConfigureFastGraphics(e.Graphics);

        using var path = RoundedRect(rect, Radius);
        using var brush = new SolidBrush(Enabled ? FillColor : Color.FromArgb(58, 64, 92));
        e.Graphics.FillPath(brush, path);
        UiTheme.DrawContinuousRoundedOutline(e.Graphics, rect, Radius, BorderColor);
        using var textBrush = new SolidBrush(Enabled ? ForeColor : UiTheme.TextMuted);

        if (_editor.Visible)
        {
            return;
        }

        TextRenderer.DrawText(
            e.Graphics,
            _displayText,
            Font,
            rect,
            textBrush.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    private void BeginEdit()
    {
        if (_editor.Visible)
        {
            return;
        }

        _editor.Text = _displayText;
        _editor.BackColor = FillColor;
        _editor.ForeColor = ForeColor;
        _editor.Visible = true;
        UpdateEditorBounds();
        _editor.Focus();
        _editor.SelectAll();
        Invalidate();
    }

    private void EditorOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            CommitEdit();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            CancelEdit();
        }
    }

    private void EditorOnLeave(object? sender, EventArgs e)
    {
        if (_committing)
        {
            return;
        }

        CommitEdit();
    }

    private void CommitEdit()
    {
        if (!_editor.Visible)
        {
            return;
        }

        _committing = true;
        try
        {
            _displayText = _editor.Text;
            _editor.Visible = false;
            Invalidate();
            ValueCommitted?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _committing = false;
        }
    }

    private void CancelEdit()
    {
        _editor.Text = _displayText;
        _editor.Visible = false;
        Invalidate();
    }

    private void UpdateEditorBounds()
    {
        if (_editor is null)
        {
            return;
        }

        var editorHeight = Math.Max(Font.Height + 4, 22);
        _editor.Bounds = new Rectangle(12, Math.Max(2, (Height - editorHeight) / 2), Math.Max(8, Width - 24), editorHeight);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class PillDropdown : Control
{
    private readonly List<string> _items = [];
    private readonly ContextMenuStrip _menu;
    private int _selectedIndex = -1;

    public Color FillColor { get; set; } = UiTheme.Surface;
    public Color BorderColor { get; set; } = Color.FromArgb(76, 86, 118);
    public int Radius { get; set; } = 10;
    public event EventHandler? SelectedIndexChanged;

    public PillDropdown()
    {
        DoubleBuffered = true;
        BackColor = UiTheme.CardInner;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI", 14f, FontStyle.Regular);
        Cursor = Cursors.Hand;
        Size = new Size(240, 40);
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque,
            true);

        _menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Padding = Padding.Empty,
            Renderer = new PillMenuRenderer()
        };
        _menu.Font = Font;
        _menu.ForeColor = ForeColor;
        _menu.BackColor = UiTheme.Surface;
        _menu.Opened += (_, _) => UpdateMenuRegion();
        _menu.SizeChanged += (_, _) => UpdateMenuRegion();
    }

    public IReadOnlyList<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_items.Count == 0)
            {
                _selectedIndex = -1;
                Text = string.Empty;
                return;
            }

            var clamped = Math.Max(0, Math.Min(value, _items.Count - 1));
            if (_selectedIndex == clamped)
            {
                Text = _items[clamped];
                Invalidate();
                return;
            }

            _selectedIndex = clamped;
            base.Text = _items[clamped];
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
        set
        {
            var match = _items.FindIndex(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
            if (match >= 0)
            {
                SelectedIndex = match;
                return;
            }

            Text = value ?? string.Empty;
        }
    }

    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            var match = _items.FindIndex(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
            if (match >= 0)
            {
                _selectedIndex = match;
                base.Text = _items[match];
            }

            Invalidate();
        }
    }

    public void SetItems(IEnumerable<string> items)
    {
        _items.Clear();
        _items.AddRange(items);
        if (_items.Count > 0)
        {
            _selectedIndex = Math.Max(0, Math.Min(_selectedIndex, _items.Count - 1));
            base.Text = _selectedIndex >= 0 ? _items[_selectedIndex] : _items[0];
            if (_selectedIndex < 0)
            {
                _selectedIndex = 0;
            }
        }
        else
        {
            _selectedIndex = -1;
            base.Text = string.Empty;
        }

        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Enabled)
        {
            ShowMenu();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        UiTheme.ConfigureFastGraphics(e.Graphics);

        using var path = RoundedRect(rect, Radius);
        using var brush = new SolidBrush(Enabled ? FillColor : Color.FromArgb(58, 64, 92));
        e.Graphics.FillPath(brush, path);
        UiTheme.DrawContinuousRoundedOutline(e.Graphics, rect, Radius, BorderColor);
        using var textBrush = new SolidBrush(Enabled ? ForeColor : UiTheme.TextMuted);
        using var arrowPen = new Pen(textBrush.Color, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var displayText = Text ?? string.Empty;
        var textRect = new Rectangle(10, -1, Math.Max(0, Width - 38), Height);
        var selectedFont = Font;
        var availableWidth = Math.Max(20, textRect.Width - 8);
        const float maxSize = 14f;
        const float minSize = 10f;
        for (var size = maxSize; size >= minSize; size -= 0.5f)
        {
            using var testFont = new Font(Font.FontFamily, size, FontStyle.Regular);
            var measured = TextRenderer.MeasureText(
                e.Graphics,
                displayText,
                testFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding);

            if (measured.Width <= availableWidth)
            {
                selectedFont = new Font(Font.FontFamily, size, FontStyle.Regular);
                break;
            }
        }

        TextRenderer.DrawText(
            e.Graphics,
            displayText,
            selectedFont,
            textRect,
            textBrush.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if (!ReferenceEquals(selectedFont, Font))
        {
            selectedFont.Dispose();
        }

        var arrowCenterX = Width - 20;
        var arrowCenterY = Height / 2 + 1;
        e.Graphics.DrawLines(
            arrowPen,
            new Point[]
            {
                new Point(arrowCenterX - 5, arrowCenterY - 3),
                new Point(arrowCenterX, arrowCenterY + 2),
                new Point(arrowCenterX + 5, arrowCenterY - 3)
            });
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    private void ShowMenu()
    {
        _menu.Items.Clear();
        foreach (var item in _items)
        {
            var menuItem = new ToolStripMenuItem(item)
            {
                AutoSize = false,
                Checked = string.Equals(item, SelectedItem, StringComparison.OrdinalIgnoreCase),
                ForeColor = ForeColor,
                Height = 29,
                Padding = new Padding(12, 0, 12, 0),
                Width = Math.Max(120, Width)
            };
            menuItem.Click += (_, _) => SelectedItem = item;
            _menu.Items.Add(menuItem);
        }

        _menu.MinimumSize = new Size(Width, 0);
        _menu.Show(this, new Point(0, Height + 2));
    }

    private void UpdateMenuRegion()
    {
        if (_menu.Width <= 0 || _menu.Height <= 0)
        {
            return;
        }

        using var path = UiTheme.CreateRoundedRectPath(
            new RectangleF(0, 0, Math.Max(1, _menu.Width - 1), Math.Max(1, _menu.Height - 1)),
            Radius);
        _menu.Region?.Dispose();
        _menu.Region = new Region(path);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class PillMenuRenderer : ToolStripProfessionalRenderer
    {
        public PillMenuRenderer()
            : base(new ProfessionalColorTable())
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            UiTheme.ConfigureFastGraphics(e.Graphics);
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using var path = UiTheme.CreateRoundedRectPath(rect, 10);
            using var brush = new SolidBrush(UiTheme.Surface);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            UiTheme.DrawContinuousRoundedOutline(e.Graphics, rect, 10, Color.FromArgb(76, 86, 118));
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is not ToolStripMenuItem menuItem)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            var selected = menuItem.Selected;
            var checkedItem = menuItem.Checked;
            const int horizontalInset = 0;
            const int verticalInset = 0;
            var bounds = new Rectangle(
                horizontalInset,
                verticalInset,
                Math.Max(1, e.Item.Width - (horizontalInset * 2)),
                Math.Max(1, e.Item.Height - (verticalInset * 2)));
            var fill = selected
                ? UiTheme.AccentSecondary
                : checkedItem
                    ? Color.FromArgb(55, 67, 105)
                    : UiTheme.Surface;
            var border = selected
                ? UiTheme.AccentBorder
                : checkedItem
                    ? Color.FromArgb(92, 112, 155)
                    : UiTheme.Surface;

            UiTheme.ConfigureFastGraphics(e.Graphics);
            using var path = UiTheme.CreateRoundedRectPath(bounds, 10);
            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, path);

            if (selected || checkedItem)
            {
                UiTheme.DrawContinuousRoundedOutline(e.Graphics, bounds, 10, border);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = UiTheme.TextPrimary;
            e.TextRectangle = new Rectangle(
                e.TextRectangle.X + 4,
                e.TextRectangle.Y,
                Math.Max(1, e.TextRectangle.Width - 4),
                e.TextRectangle.Height);
            base.OnRenderItemText(e);
        }
    }
}

public sealed class ToggleSwitchCheckBox : CheckBox
{
    public bool UseSlidingKnob { get; set; } = true;
    public Color CheckedFillColor { get; set; } = UiTheme.Accent;
    public Color UncheckedFillColor { get; set; } = Color.FromArgb(41, 53, 77);
    public Color CheckedBorderColor { get; set; } = Color.FromArgb(123, 164, 255);
    public Color UncheckedBorderColor { get; set; } = Color.FromArgb(67, 80, 106);
    public Color CheckedLabelColor { get; set; } = Color.White;
    public Color UncheckedLabelColor { get; set; } = Color.White;

    public ToggleSwitchCheckBox()
    {
        Appearance = Appearance.Normal;
        AutoSize = false;
        Width = 112;
        Height = 40;
        Cursor = Cursors.Hand;
        BackColor = UiTheme.CardInner;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque,
            true);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        UiTheme.ConfigureFastGraphics(e.Graphics);
        var rect = new Rectangle(1, 0, Math.Max(1, Width - 3), Height - 1);
        var fill = Checked ? CheckedFillColor : UncheckedFillColor;
        var border = Checked ? CheckedBorderColor : UncheckedBorderColor;

        using var path = RoundedRect(rect, Height / 2);
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        UiTheme.DrawContinuousRoundedOutline(e.Graphics, rect, Height / 2, border);

        Rectangle textRect;
        if (UseSlidingKnob)
        {
            var knobX = Checked ? Width - Height + 1 : 1;
            using var knobBrush = new SolidBrush(Color.FromArgb(243, 247, 255));
            using var knobBorder = new Pen(Color.FromArgb(215, 226, 250), 1f);
            e.Graphics.FillEllipse(knobBrush, knobX, 1, Height - 3, Height - 3);
            e.Graphics.DrawEllipse(knobBorder, knobX, 1, Height - 3, Height - 3);
            textRect = new Rectangle(12, 0, Width - Height, Height);
        }
        else
        {
            textRect = new Rectangle(0, 0, Width, Height);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Checked ? "ON" : "OFF",
            Font,
            textRect,
            Checked ? CheckedLabelColor : UncheckedLabelColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedRect(new Rectangle(1, 0, Math.Max(1, Width - 3), Height - 1), Height / 2);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class SegmentRadioButton : RadioButton
{
    public bool PrimarySegment { get; set; }

    public SegmentRadioButton()
    {
        Appearance = Appearance.Button;
        AutoSize = false;
        Height = 40;
        Cursor = Cursors.Hand;
        BackColor = UiTheme.CardInner;
        ForeColor = UiTheme.TextPrimary;
        Font = new Font("Segoe UI", 14f, FontStyle.Regular);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Opaque,
            true);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        UiTheme.ConfigureFastGraphics(e.Graphics);
        var rect = new Rectangle(1, 0, Math.Max(1, Width - 3), Height - 1);
        var fill = Checked
            ? (PrimarySegment ? UiTheme.Accent : Color.FromArgb(82, 116, 188))
            : Color.FromArgb(41, 53, 77);
        var border = Checked ? Color.FromArgb(110, 149, 236) : Color.FromArgb(62, 74, 103);

        using var path = RoundedRect(rect, Height / 2);
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        UiTheme.DrawContinuousRoundedOutline(e.Graphics, rect, Height / 2, border);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            Enabled ? Color.White : UiTheme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = RoundedRect(new Rectangle(1, 0, Math.Max(1, Width - 3), Height - 1), Height / 2);
        Region?.Dispose();
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class ModernSlider : Control
{
    private bool _dragging;
    private int _value = 15;

    public int Minimum { get; set; } = 1;
    public int Maximum { get; set; } = 100;

    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            if (_value == clamped)
            {
                return;
            }

            _value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;
    public event EventHandler? DragCommit;

    public ModernSlider()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Height = 54;
        Width = 360;
        BackColor = UiTheme.CardInner;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(Parent?.BackColor ?? UiTheme.CardInner);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        UiTheme.ConfigureGraphics(g);
        using var backgroundBrush = new SolidBrush(Parent?.BackColor ?? UiTheme.CardInner);
        g.FillRectangle(backgroundBrush, ClientRectangle);
        var lineRect = new Rectangle(24, Height / 2 - 2, Width - 48, 4);
        var ratio = Maximum == Minimum ? 0f : (float)(Value - Minimum) / (Maximum - Minimum);
        var thumbCenterX = lineRect.Left + (int)Math.Round(lineRect.Width * ratio);

        using var baseBrush = new SolidBrush(Color.FromArgb(138, 148, 175));
        using var fillBrush = new SolidBrush(Color.FromArgb(231, 236, 250));
        using var ringBrush = new SolidBrush(Color.FromArgb(200, 213, 255));
        using var knobBrush = new SolidBrush(Color.White);
        using var centerBrush = new SolidBrush(Color.FromArgb(175, 197, 255));

        g.FillRectangle(baseBrush, lineRect);
        g.FillRectangle(fillBrush, lineRect.Left, lineRect.Top, Math.Max(10, thumbCenterX - lineRect.Left), lineRect.Height);
        g.FillEllipse(ringBrush, thumbCenterX - 18, lineRect.Top - 18, 36, 36);
        g.FillEllipse(knobBrush, thumbCenterX - 12, lineRect.Top - 12, 24, 24);
        g.FillEllipse(centerBrush, thumbCenterX - 4, lineRect.Top - 4, 8, 8);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _dragging = true;
        Capture = true;
        SetValueFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            SetValueFromX(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            SetValueFromX(e.X);
            DragCommit?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageDown or Keys.PageUp)
        {
            DragCommit?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetValueFromX(int x)
    {
        var trackLeft = 24;
        var trackWidth = Width - 48;
        var clampedX = Math.Max(trackLeft, Math.Min(trackLeft + trackWidth, x));
        var ratio = (double)(clampedX - trackLeft) / trackWidth;
        Value = Minimum + (int)Math.Round((Maximum - Minimum) * ratio);
    }
}
