using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace GDEmuSdCardManager
{
    /// <summary>
    /// Interaction logic for LogsWindow.xaml
    /// </summary>
    public partial class LogsWindow : Window
    {
        public LogsWindow(string logsContent)
        {
            InitializeComponent();

            FlowDocument fd = new FlowDocument();
            MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(logsContent));
            TextRange textRange = new TextRange(fd.ContentStart, fd.ContentEnd);
            textRange.Load(ms, DataFormats.Rtf);
            InfoRichTextBox.Document = fd;

            var warning = new Paragraph(new Run("WARNING: THIS WINDOW ISN'T AUTOMATICALLY REFRESHED. CLOSE AND REOPEN IT TO SEE THE LAST LOGS."))
            {
                Foreground = Brushes.Green
            };
            InfoRichTextBox.Document.Blocks.Add(warning);
            InfoRichTextBox.Document.Blocks.Add(new Paragraph());
            InfoRichTextBox.Focus();
            InfoRichTextBox.CaretPosition = InfoRichTextBox.CaretPosition.DocumentEnd;
            InfoRichTextBox.ScrollToEnd();
        }
    }
}