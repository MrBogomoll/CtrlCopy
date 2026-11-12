using System.Drawing;
using System.Windows.Forms;

namespace AutoCopy.Tests
{
    internal static class SelectionFixture
    {
        [System.STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            var form = new Form { Text = "AutoCopy Selection Fixture", ClientSize = new Size(650, 380), StartPosition = FormStartPosition.CenterScreen, Font = new Font("Segoe UI", 12), TopMost = true };
            form.Controls.Add(new Label { Text = "Тестовое выделение текста", Location = new Point(24, 20), AutoSize = true });
            var text = new TextBox { Name = "SelectionText", AccessibleName = "Selection text", Multiline = true, Text = "AutoCopy проверка выделения.\r\nВторая строка: 12345.", Location = new Point(24, 60), Size = new Size(600, 150), Font = new Font("Segoe UI", 14) };
            form.Controls.Add(text);
            form.Controls.Add(new Label { Text = "Поле пароля", Location = new Point(24, 245), AutoSize = true });
            form.Controls.Add(new TextBox { Name = "PasswordText", AccessibleName = "Password text", UseSystemPasswordChar = true, Text = "private-password-value", Location = new Point(24, 280), Size = new Size(600, 34) });
            form.Shown += (s, e) => { text.Focus(); text.Select(0, 0); };
            Application.Run(form);
        }
    }
}
