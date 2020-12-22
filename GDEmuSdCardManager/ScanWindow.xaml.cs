using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.DTO;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GDEmuSdCardManager
{
    /// <summary>
    /// Interaction logic for ScanWindow.xaml
    /// </summary>
    public partial class ScanWindow : Window
    {
        private ScanViewModel viewModel;

        public event EventHandler OnScanFinished;
        public ScanWindow(ScanViewModel viewModel)
        {
            InitializeComponent();
            this.viewModel = viewModel;
        }

        public void LoadGamesOnPc()
        {
            WriteInfo("Scanning PC folder...");
            IEnumerable<string> paths = viewModel.PcFolder.Split(viewModel.PathSplitter);
            viewModel.IsScanSuccessful = true;

            if (string.IsNullOrEmpty(viewModel.PcFolder))
            {
                WriteError("PC path must not be empty");
                viewModel.IsScanSuccessful = false;
                CloseButton.IsEnabled = true;
                return;
            }

            foreach (string path in paths)
            {
                if (!Directory.Exists(path))
                {
                    WriteError($"PC path {path} is invalid");
                    viewModel.IsScanSuccessful = false;
                }
            }

            if (!viewModel.IsScanSuccessful)
            {
                CloseButton.IsEnabled = true;
                return;
            }

            List<string> subFolders = new List<string>();
            List<string> compressedFiles = new List<string>();
            foreach (string path in paths)
            {
                subFolders.AddRange(FileManager.EnumerateFolders(path));
                compressedFiles.AddRange(FileManager.EnumerateArchives(path));
            }


            CopyProgressBar.Maximum = subFolders.Count + compressedFiles.Count;
            CopyProgressBar.Value = 0;

            var games = new List<GameOnPc>();

            foreach (var subFolder in subFolders)
            {
                if (Directory
                    .EnumerateFiles(
                    subFolder,
                    "*",
                    new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false,
                        ReturnSpecialDirectories = false
                    })
                    .Count(f => Path.GetExtension(f) == ".gdi" || Path.GetExtension(f) == ".cdi") > 1)
                {
                    WriteError($"You have more than one GDI/CDI file in the folder {subFolder}. Please make sure you only have one GDI/CDI per folder.");
                    CopyProgressBar.Value++;
                    CopyProgressBar.Refresh();
                    continue;
                }

                var imageFile = Directory.EnumerateFiles(
                    subFolder,
                    "*",
                    new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false,
                        ReturnSpecialDirectories = false
                    })
                    .FirstOrDefault(f => Path.GetExtension(f) == ".gdi" || Path.GetExtension(f) == ".cdi");

                if (imageFile != null)
                {
                    GameOnPc game;
                    try
                    {
                        game = GameManager.ExtractPcGameData(subFolder);
                    }
                    catch (Exception error)
                    {
                        WriteError(error.Message);
                        CopyProgressBar.Value++;
                        CopyProgressBar.Refresh();
                        continue;
                    }

                    if (game != null && !games.Any(g => g.GameName == game.GameName && g.Disc == game.Disc && g.IsGdi == game.IsGdi))
                    {
                        WriteInfo($"Found game {game.GameName} in folder {subFolder}");
                        games.Add(game);
                    }
                }

                CopyProgressBar.Value++;
                CopyProgressBar.Refresh();
            }

            foreach (var compressedFile in compressedFiles)
            {
                WriteInfo($"Checking archive {compressedFile}...");
                //Stopwatch stopwatch = new Stopwatch();
                //stopwatch.Start();
                //WriteInfo($"Reading archive {compressedFile}");
                IArchive archive;
                try
                {
                    archive = ArchiveFactory.Open(new FileInfo(compressedFile));
                }
                catch (Exception ex)
                {
                    WriteError($"Could not open archive {compressedFile}");
                    //stopwatch.Start();
                    //WriteInfo($"Finished reading archive {compressedFile}. Time elapsed: {stopwatch.Elapsed}");
                    CopyProgressBar.Value++;
                    CopyProgressBar.Refresh();
                    continue;
                }

                if (ArchiveManager.RetreiveUniqueFileFromArchiveEndingWith(archive, ".gdi") != null)
                {
                    GameOnPc game;

                    try
                    {
                        if (archive.Type == SharpCompress.Common.ArchiveType.SevenZip && !viewModel.MustScanSevenZip)
                        {
                            WriteWarning($"Archive {compressedFile} ignored as it's a 7z file and the option isn't ticked.");
                            CopyProgressBar.Value++;
                            CopyProgressBar.Refresh();
                            continue;
                        }

                        game = GameManager.ExtractPcGameDataFromArchive(compressedFile, archive);
                    }
                    catch (Exception error)
                    {
                        WriteError(error.Message);
                        //stopwatch.Start();
                        //WriteInfo($"Finished reading archive {compressedFile}. Time elapsed: {stopwatch.Elapsed}");
                        CopyProgressBar.Value++;
                        CopyProgressBar.Refresh();
                        continue;
                    }

                    if (game != null && !games.Any(g => g.GameName == game.GameName && g.Disc == game.Disc && g.IsGdi == game.IsGdi))
                    {
                        WriteInfo($"Found game {game.GameName} in archive {compressedFile}");
                        games.Add(game);
                    }
                }
                else
                {
                    WriteWarning($"Could not find GDI in archive {compressedFile} (CDI are ignored in archives)");
                    //stopwatch.Start();
                    //WriteInfo($"Finished reading archive {compressedFile}. Time elapsed: {stopwatch.Elapsed}");
                    CopyProgressBar.Value++;
                    CopyProgressBar.Refresh();
                    continue;
                }

                CopyProgressBar.Value++;
                CopyProgressBar.Refresh();
                //stopwatch.Start();
                //WriteInfo($"Finished reading archive {compressedFile}. Time elapsed: {stopwatch.Elapsed}");
            }

            viewModel.GamesOnPc = games.OrderBy(f => f.GameName);
            WriteSuccess(string.Empty);
            WriteSuccess($"{games.Count} games on PC found. You can close this window.");
            WriteSuccess(string.Empty);

            CloseButton.IsEnabled = true;
        }

        private void WriteError(string message)
        {
            WriteMessageInRichTextBox(message, Brushes.Red);
        }

        private void WriteInfo(string message)
        {
            WriteMessageInRichTextBox(message, Brushes.Black);
        }

        private void WriteWarning(string message)
        {
            WriteMessageInRichTextBox(message, Brushes.DarkOrange);
        }

        private void WriteSuccess(string message)
        {
            WriteMessageInRichTextBox(message, Brushes.Blue);
        }

        private void WriteMessageInRichTextBox(string message, SolidColorBrush color)
        {
            var error = new Paragraph(new Run(DateTime.Now.ToString("HH:mm:ss") + ": " + message))
            {
                Foreground = color
            };
            InfoRichTextBox.Document.Blocks.Add(error);
            InfoRichTextBox.Focus();
            InfoRichTextBox.CaretPosition = InfoRichTextBox.CaretPosition.DocumentEnd;
            InfoRichTextBox.ScrollToEnd();
            InfoRichTextBox.Refresh();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public static class ExtensionMethods
    {

        private static Action EmptyDelegate = delegate () { };


        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }
    }
}
