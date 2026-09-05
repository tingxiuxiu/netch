namespace Netch.Ui;

internal sealed class MdMenuRenderer : ToolStripProfessionalRenderer
{
    public MdMenuRenderer() : base(new MdColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(MdColors.OutlineVariant);
        e.Graphics.DrawLine(pen, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected ? MdColors.OnSecondaryContainer : MdColors.OnSurface;
        e.TextFont = MdFonts.Body;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        bounds.Inflate(-4, -2);
        using var path = MdShapes.RoundedRect(bounds, 8);
        using var brush = new SolidBrush(e.Item.Selected ? MdColors.SecondaryContainer : Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.FillPath(brush, path);
    }

    private sealed class MdColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => MdColors.SurfaceContainerLow;
        public override Color MenuStripGradientEnd => MdColors.SurfaceContainerLow;
        public override Color MenuBorder => MdColors.OutlineVariant;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => MdColors.SecondaryContainer;
        public override Color MenuItemSelectedGradientBegin => MdColors.SecondaryContainer;
        public override Color MenuItemSelectedGradientEnd => MdColors.SecondaryContainer;
        public override Color ImageMarginGradientBegin => MdColors.SurfaceContainerLowest;
        public override Color ImageMarginGradientMiddle => MdColors.SurfaceContainerLowest;
        public override Color ImageMarginGradientEnd => MdColors.SurfaceContainerLowest;
        public override Color ToolStripDropDownBackground => MdColors.SurfaceContainerLowest;
        public override Color SeparatorDark => MdColors.OutlineVariant;
        public override Color SeparatorLight => MdColors.SurfaceContainer;
        public override Color StatusStripGradientBegin => MdColors.SurfaceContainerHigh;
        public override Color StatusStripGradientEnd => MdColors.SurfaceContainerHigh;
        public override Color StatusStripBorder => MdColors.OutlineVariant;
    }
}
