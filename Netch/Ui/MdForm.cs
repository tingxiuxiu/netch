namespace Netch.Ui;

public class MdForm : Form
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MdTheme.Apply(this);
    }
}
