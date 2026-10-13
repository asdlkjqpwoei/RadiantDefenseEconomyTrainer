using System;
using System.IO;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        TrainerLocalization localization = null;
        try
        {
            localization = TrainerLocalization.LoadDefault();
            Application.Run(new TrainerForm(localization));
        }
        catch (Exception ex)
        {
            string title = localization == null
                ? Path.GetFileNameWithoutExtension(Application.ExecutablePath)
                : localization.Get("app.title");
            string message = localization == null
                ? ex.ToString()
                : localization.Format("error.unhandled_exception", ex.Message);
            MessageBox.Show(message, title);
        }
    }
}
