namespace KofgeClicker;

public sealed class PromptDialog : Form
{
    private readonly TextBox _input;

    public string InputText => _input.Text;

    private PromptDialog(string title, string prompt, string initialValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(340, 140);
        BackColor = Color.FromArgb(32, 36, 44);
        ForeColor = Color.White;

        var label = new Label
        {
            Left = 16,
            Top = 16,
            Width = 308,
            Text = prompt,
            ForeColor = Color.White,
            BackColor = Color.Transparent
        };

        _input = new TextBox
        {
            Left = 16,
            Top = 46,
            Width = 308,
            Text = initialValue,
            BackColor = Color.FromArgb(48, 52, 60),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var okButton = new Button
        {
            Text = "OK",
            Left = 152,
            Top = 92,
            Width = 80,
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = 244,
            Top = 92,
            Width = 80,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(label);
        Controls.Add(_input);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public static (DialogResult Result, string Value) Show(IWin32Window owner, string title, string prompt, string initialValue)
    {
        using var dialog = new PromptDialog(title, prompt, initialValue);
        var result = dialog.ShowDialog(owner);
        return (result, dialog.InputText);
    }
}
