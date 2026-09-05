using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using Netch.Forms;

namespace Netch.Ui;

internal enum MdButtonKind
{
    Filled,
    Tonal,
    Outlined
}

/// <summary>
///     Applies Material Design 3 color, shape, and type tokens to existing WinForms controls
///     without replacing the designer-generated control tree.
/// </summary>
internal static class MdTheme
{
    private static readonly ConditionalWeakTable<Control, object> Applied = new();
    private static readonly ConditionalWeakTable<Button, ButtonVisual> ButtonState = new();
    private static readonly MdMenuRenderer MenuRenderer = new();

    public static void Apply(Control root)
    {
        ApplyControl(root);
    }

    public static void ApplyControl(Control control)
    {
        if (!Applied.TryAdd(control, TrueSentinel.Instance))
            return;

        EnableDoubleBuffer(control);

        switch (control)
        {
            case Form form:
                StyleForm(form);
                break;
            case Button button:
                StyleButton(button);
                break;
            case GroupBox groupBox:
                StyleGroupBox(groupBox);
                break;
            case TabControl tabControl:
                StyleTabControl(tabControl);
                break;
            case TabPage tabPage:
                StyleTabPage(tabPage);
                break;
            case TextBoxBase text:
                StyleTextBox(text);
                break;
            case ComboBox combo:
                StyleComboBox(combo);
                break;
            case ListView listView:
                StyleListView(listView);
                break;
            case ListBox listBox:
                StyleListBox(listBox);
                break;
            case SyncGlobalCheckBox sync:
                sync.RefreshTheme();
                break;
            case CheckBox checkBox:
                StyleCheckBox(checkBox);
                break;
            case RadioButton radio:
                StyleRadioButton(radio);
                break;
            case LinkLabel link:
                StyleLinkLabel(link);
                break;
            case Label label:
                StyleLabel(label);
                break;
            case MenuStrip menu:
                StyleMenuStrip(menu);
                break;
            case StatusStrip status:
                StyleStatusStrip(status);
                break;
            case ContextMenuStrip context:
                StyleContextMenu(context);
                break;
            case PictureBox picture:
                picture.BackColor = Color.Transparent;
                break;
            case Panel or TableLayoutPanel or FlowLayoutPanel or SplitContainer:
                StyleSurface(control, control.Parent is GroupBox ? MdColors.SurfaceContainerLow : MdColors.Background);
                break;
            case ContainerControl:
                if (control is not Form && control is not TabControl)
                    StyleSurface(control, MdColors.Background);
                break;
        }

        control.ControlAdded += (_, e) => ApplyControl(e.Control);

        foreach (Control child in control.Controls)
            ApplyControl(child);

        if (control.ContextMenuStrip != null)
            ApplyControl(control.ContextMenuStrip);
    }

    private static void StyleForm(Form form)
    {
        form.BackColor = MdColors.Background;
        form.ForeColor = MdColors.OnSurface;
        form.Font = MdFonts.Body;
        if (form.Padding.All < 8)
            form.Padding = new Padding(Math.Max(form.Padding.Left, 4), form.Padding.Top, Math.Max(form.Padding.Right, 4), form.Padding.Bottom);
    }

    private static void StyleSurface(Control control, Color back)
    {
        control.BackColor = back;
        control.ForeColor = MdColors.OnSurface;
        control.Font = MdFonts.Body;
    }

    private static void StyleButton(Button button)
    {
        var kind = ResolveButtonKind(button);
        var visual = new ButtonVisual { Kind = kind };
        ButtonState.Add(button, visual);

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.Transparent;
        button.FlatAppearance.MouseDownBackColor = Color.Transparent;
        button.BackColor = Color.Transparent;
        button.ForeColor = ForeColorFor(kind);
        button.Font = MdFonts.Label;
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        button.TextAlign = ContentAlignment.MiddleCenter;

        button.MouseEnter += (_, _) =>
        {
            visual.Hover = true;
            button.Invalidate();
        };
        button.MouseLeave += (_, _) =>
        {
            visual.Hover = false;
            visual.Pressed = false;
            button.Invalidate();
        };
        button.MouseDown += (_, _) =>
        {
            visual.Pressed = true;
            button.Invalidate();
        };
        button.MouseUp += (_, _) =>
        {
            visual.Pressed = false;
            button.Invalidate();
        };
        button.Paint += ButtonOnPaint;
        button.Resize += (_, _) => button.Invalidate();
    }

    private static MdButtonKind ResolveButtonKind(Button button)
    {
        if (button.Name is "ControlButton")
            return MdButtonKind.Filled;

        if (button.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
            button.Name.Contains("Unselect", StringComparison.OrdinalIgnoreCase))
            return MdButtonKind.Tonal;

        return MdButtonKind.Outlined;
    }

    private static Color ForeColorFor(MdButtonKind kind) =>
        kind switch
        {
            MdButtonKind.Filled => MdColors.OnPrimary,
            MdButtonKind.Tonal => MdColors.OnSecondaryContainer,
            _ => MdColors.Primary
        };

    private static Color BackColorFor(MdButtonKind kind) =>
        kind switch
        {
            MdButtonKind.Filled => MdColors.Primary,
            MdButtonKind.Tonal => MdColors.SecondaryContainer,
            _ => Color.Transparent
        };

    private static void ButtonOnPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button || !ButtonState.TryGetValue(button, out var visual))
            return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bounds = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
        var radius = Math.Max(8, button.Height / 2);
        using var path = MdShapes.RoundedRect(bounds, radius);
        using var fill = new SolidBrush(BackColorFor(visual.Kind));
        g.FillPath(fill, path);

        if (visual.Kind == MdButtonKind.Outlined)
        {
            using var pen = new Pen(MdColors.Outline, 1f);
            g.DrawPath(pen, path);
        }

        if (visual.Pressed)
        {
            using var overlay = new SolidBrush(MdColors.StatePressed);
            g.FillPath(overlay, path);
        }
        else if (visual.Hover)
        {
            using var overlay = new SolidBrush(MdColors.StateHover);
            g.FillPath(overlay, path);
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(g, button.Text, button.Font, button.ClientRectangle, ForeColorFor(visual.Kind), flags);
    }

    private static void StyleGroupBox(GroupBox groupBox)
    {
        groupBox.BackColor = MdColors.SurfaceContainerLow;
        groupBox.ForeColor = MdColors.OnSurfaceVariant;
        groupBox.Font = MdFonts.Label;
        groupBox.Paint += GroupBoxOnPaint;
    }

    private static void GroupBoxOnPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not GroupBox box)
            return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var parentBack = box.Parent?.BackColor ?? MdColors.Background;
        g.Clear(parentBack);

        var header = box.Font.Height;
        var bounds = new Rectangle(0, header / 2, box.Width - 1, box.Height - header / 2 - 1);
        using var path = MdShapes.RoundedRect(bounds, MdShapes.CardRadius);
        using var fill = new SolidBrush(MdColors.SurfaceContainerLow);
        using var pen = new Pen(MdColors.OutlineVariant);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);

        if (string.IsNullOrEmpty(box.Text))
            return;

        var textSize = TextRenderer.MeasureText(box.Text, box.Font);
        var textRect = new Rectangle(16, 0, textSize.Width + 8, header + 2);
        using var titleFill = new SolidBrush(parentBack);
        g.FillRectangle(titleFill, textRect);
        TextRenderer.DrawText(g, box.Text, box.Font, textRect, MdColors.OnSurfaceVariant, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private static void StyleTabControl(TabControl tab)
    {
        tab.DrawMode = TabDrawMode.OwnerDrawFixed;
        tab.SizeMode = tab.TabCount > 4 ? TabSizeMode.FillToRight : TabSizeMode.Normal;
        tab.Padding = new Point(12, 6);
        tab.BackColor = MdColors.Background;
        tab.ForeColor = MdColors.OnSurface;
        tab.Font = MdFonts.Label;
        tab.DrawItem += TabControlOnDrawItem;
        tab.Paint += TabControlOnPaint;
    }

    private static void TabControlOnPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not TabControl tab)
            return;

        e.Graphics.Clear(MdColors.Background);
        if (tab.TabPages.Count == 0)
            return;

        var first = tab.GetTabRect(0);
        using var pen = new Pen(MdColors.OutlineVariant);
        e.Graphics.DrawLine(pen, 0, first.Bottom, tab.Width, first.Bottom);
    }

    private static void TabControlOnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tab || e.Index < 0 || e.Index >= tab.TabPages.Count)
            return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = tab.GetTabRect(e.Index);
        var selected = e.Index == tab.SelectedIndex;
        var page = tab.TabPages[e.Index];

        using (var fill = new SolidBrush(selected ? MdColors.SecondaryContainer : MdColors.Background))
        using (var path = MdShapes.RoundedRect(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height), 10))
        {
            g.FillPath(fill, path);
        }

        if (selected)
        {
            using var indicator = new SolidBrush(MdColors.Primary);
            g.FillRectangle(indicator, bounds.X + 10, bounds.Bottom - 3, bounds.Width - 20, 3);
        }

        var color = selected ? MdColors.OnSecondaryContainer : MdColors.OnSurfaceVariant;
        TextRenderer.DrawText(g, page.Text, tab.Font, bounds, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static void StyleTabPage(TabPage page)
    {
        page.BackColor = MdColors.Background;
        page.ForeColor = MdColors.OnSurface;
        page.Font = MdFonts.Body;
        page.UseVisualStyleBackColor = false;
    }

    private static void StyleTextBox(TextBoxBase text)
    {
        text.BorderStyle = BorderStyle.FixedSingle;
        text.BackColor = MdColors.SurfaceContainerLowest;
        text.ForeColor = MdColors.OnSurface;
        text.Font = MdFonts.Body;
    }

    private static void StyleComboBox(ComboBox combo)
    {
        combo.BackColor = MdColors.SurfaceContainerLowest;
        combo.ForeColor = MdColors.OnSurface;
        combo.Font = MdFonts.Body;
        combo.FlatStyle = FlatStyle.Flat;
        combo.ItemHeight = Math.Max(combo.ItemHeight, 22);
        if (combo.DrawMode == DrawMode.Normal)
        {
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem += ComboBoxOnDrawItem;
        }
    }

    private static void ComboBoxOnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0)
            return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var back = new SolidBrush(selected ? MdColors.SecondaryContainer : MdColors.SurfaceContainerLowest);
        e.Graphics.FillRectangle(back, e.Bounds);
        var color = selected ? MdColors.OnSecondaryContainer : MdColors.OnSurface;
        TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString() ?? "", combo.Font, e.Bounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    public static void DrawComboItemBackground(Graphics graphics, Rectangle bounds, bool selected)
    {
        using var back = new SolidBrush(selected ? MdColors.SecondaryContainer : MdColors.SurfaceContainerLowest);
        graphics.FillRectangle(back, bounds);
    }

    private static void StyleListView(ListView listView)
    {
        listView.BackColor = MdColors.SurfaceContainerLowest;
        listView.ForeColor = MdColors.OnSurface;
        listView.Font = MdFonts.Body;
        listView.BorderStyle = BorderStyle.FixedSingle;
        listView.OwnerDraw = false;
    }

    private static void StyleListBox(ListBox listBox)
    {
        listBox.BackColor = MdColors.SurfaceContainerLowest;
        listBox.ForeColor = MdColors.OnSurface;
        listBox.Font = MdFonts.Body;
        listBox.BorderStyle = BorderStyle.FixedSingle;
        listBox.IntegralHeight = false;
    }

    private static void StyleCheckBox(CheckBox checkBox)
    {
        if (checkBox.BackColor == Color.Yellow)
            return;

        checkBox.BackColor = Color.Transparent;
        checkBox.ForeColor = MdColors.OnSurface;
        checkBox.Font = MdFonts.Body;
        checkBox.FlatStyle = FlatStyle.System;
    }

    private static void StyleRadioButton(RadioButton radio)
    {
        radio.BackColor = Color.Transparent;
        radio.ForeColor = MdColors.OnSurface;
        radio.Font = MdFonts.Body;
        radio.FlatStyle = FlatStyle.System;
    }

    private static void StyleLabel(Label label)
    {
        if (label.ForeColor == Color.Red)
            return;

        label.ForeColor = MdColors.OnSurfaceVariant;
        label.Font = MdFonts.Body;
        if (label.BackColor == SystemColors.Control)
            label.BackColor = Color.Transparent;
    }

    private static void StyleLinkLabel(LinkLabel link)
    {
        link.LinkColor = MdColors.Primary;
        link.ActiveLinkColor = MdColors.PrimaryContainer;
        link.VisitedLinkColor = MdColors.Primary;
        link.ForeColor = MdColors.OnSurface;
        link.BackColor = Color.Transparent;
    }

    private static void StyleMenuStrip(MenuStrip menu)
    {
        menu.Renderer = MenuRenderer;
        menu.BackColor = MdColors.SurfaceContainerLow;
        menu.ForeColor = MdColors.OnSurface;
        menu.Font = MdFonts.Body;
        menu.Padding = new Padding(8, 4, 8, 4);
        foreach (ToolStripItem item in menu.Items)
            StyleToolStripItem(item);
    }

    private static void StyleStatusStrip(StatusStrip status)
    {
        status.Renderer = MenuRenderer;
        status.BackColor = MdColors.SurfaceContainerHigh;
        status.ForeColor = MdColors.OnSurfaceVariant;
        status.Font = MdFonts.Body;
        status.SizingGrip = false;
        foreach (ToolStripItem item in status.Items)
        {
            if (item.Name is "NatTypeStatusLightLabel")
                continue;

            item.ForeColor = MdColors.OnSurfaceVariant;
            item.Font = MdFonts.Body;
        }
    }

    private static void StyleContextMenu(ContextMenuStrip menu)
    {
        menu.Renderer = MenuRenderer;
        menu.BackColor = MdColors.SurfaceContainerLowest;
        menu.ForeColor = MdColors.OnSurface;
        menu.Font = MdFonts.Body;
        foreach (ToolStripItem item in menu.Items)
            StyleToolStripItem(item);
    }

    private static void StyleToolStripItem(ToolStripItem item)
    {
        if (item.Name is "NewVersionLabel" or "VersionLabel")
            return;

        item.ForeColor = MdColors.OnSurface;
        item.Font = MdFonts.Body;
        if (item is ToolStripDropDownItem drop)
        {
            foreach (ToolStripItem child in drop.DropDownItems)
                StyleToolStripItem(child);
        }
    }

    private static void EnableDoubleBuffer(Control control)
    {
        typeof(Control)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(control, true, null);
    }

    private sealed class ButtonVisual
    {
        public MdButtonKind Kind;
        public bool Hover;
        public bool Pressed;
    }

    private sealed class TrueSentinel
    {
        public static readonly TrueSentinel Instance = new();
    }
}
