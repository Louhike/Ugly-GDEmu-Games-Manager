using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.DTO;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace GDEmuSdCardManager
{
    /// <summary>
    /// Interaction logic for ScanWindow.xaml
    /// </summary>
    public partial class ScanWindow : Window
    {
        private ScanViewModel viewModel;

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

            List<string> subFolders = new List<string>();
            List<string> compressedFiles = new List<string>();
            foreach (string path in paths)
            {
                subFolders.AddRange(FileManager.EnumerateFolders(path));
                compressedFiles.AddRange(FileManager.EnumerateArchives(path));
            }

            // Archives take more time to be scanned so we give them a bigger value
            // for the progress bar.
            CopyProgressBar.Maximum = subFolders.Count + (compressedFiles.Count * 10);
            CopyProgressBar.Value = 0;

            var games = new List<GameOnPc>();

            foreach (var subFolder in subFolders)
            {
                var game = RetrieveGameInFolder(subFolder);

                if (game != null && !games.Any(g => g.GameName == game.GameName && g.Disc == game.Disc && g.IsGdi == game.IsGdi))
                {
                    WriteInfo($"Found game {game.GameName} in folder {subFolder}");
                    games.Add(game);
                }
            }

            foreach (var compressedFile in compressedFiles)
            {
                var game = RetrieveGameInArchive(compressedFile);

                if (game != null && !games.Any(g => g.GameName == game.GameName && g.Disc == game.Disc && g.IsGdi == game.IsGdi))
                {
                    WriteInfo($"Found game {game.GameName} in archive {compressedFile}");
                    games.Add(game);
                }

                
            }

            viewModel.GamesOnPc = games.OrderBy(f => f.GameName);
            WriteSuccess(string.Empty);
            WriteSuccess($"{games.Count} games on PC found. You can close this window.");
            WriteSuccess(string.Empty);

            CloseButton.IsEnabled = true;
        }

        public async Task CopySelectedGames()
        {
            var sdCardManager = new SdCardManager(viewModel.SdDrive);

            var gamesToCopy = viewModel.GamesOnPc
                .Where(i => i.MustBeOnSd && (!i.IsInSdCard || i.MustShrink));

            WriteInfo($"Copying {gamesToCopy.Count()} game(s) to SD card...");

            CopyProgressLabel.Visibility = Visibility.Visible;
            CopyProgressBar.Maximum = gamesToCopy.Count();
            CopyProgressBar.Value = 0;
            CopyProgressBar.Visibility = Visibility.Visible;
            CopyProgressBar.Refresh();

            short index = 2;

            foreach (GameOnPc selectedItem in gamesToCopy)
            {
                WriteInfo($"Copying {selectedItem.GameName} {selectedItem.Disc}...");

                if (!string.IsNullOrEmpty(selectedItem.SdFolder))
                {
                    index = short.Parse(Path.GetFileName(selectedItem.SdFolder));
                }
                else
                {
                    try
                    {
                        index = sdCardManager.FindAvailableFolderForGame(index);
                    }
                    catch (Exception e)
                    {
                        WriteError("Error while trying to find an available folder to copy games: " + e.Message);
                        CopyProgressBar.Value++;
                        CopyProgressBar.Refresh();
                        continue;
                    }

                    if (index == -1)
                    {
                        WriteError($"You cannot have more than 9999 games on your SD card.");
                        CopyProgressBar.Value = CopyProgressBar.Maximum;
                        break;
                    }
                }

                try
                {
                    await sdCardManager.AddGame(selectedItem, index);
                    CopyProgressBar.Value++;
                    CopyProgressBar.Refresh();
                    WriteInfo($"{CopyProgressBar.Value}/{gamesToCopy.Count()} games copied");
                }
                catch (Exception error)
                {
                    string messageBoxText = error.Message;
                    string caption = "Error";
                    MessageBoxButton button = MessageBoxButton.OK;
                    MessageBoxImage icon = MessageBoxImage.Warning;
                    MessageBox.Show(messageBoxText, caption, button, icon);
                    WriteError(error.Message);
                    CopyProgressBar.Value++;
                    CopyProgressBar.Refresh();
                }
            }

            if (CopyProgressBar.Value < gamesToCopy.Count())
            {
                WriteInfo($"There was an error. {CopyProgressBar.Value} games were copied.");
            }
            else
            {
                WriteSuccess($"Games copied");
            }
        }

        private GameOnPc RetrieveGameInFolder(string folderPath)
        {
            GameOnPc game = null;
            var imageFiles = FileManager.GetImageFilesPathInFolder(folderPath);
            if (imageFiles.Count() > 1)
            {
                WriteError($"You have more than one GDI/CDI file in the folder {folderPath}. Please make sure you only have one GDI/CDI per folder.");
                CopyProgressBar.Value++;
                CopyProgressBar.Refresh();
                return null;
            }

            if (imageFiles.Any())
            {
                try
                {
                    game = GameManager.ExtractPcGameData(folderPath);
                }
                catch (Exception error)
                {
                    WriteError(error.Message);
                    CopyProgressBar.Value++;
                    CopyProgressBar.Refresh();
                    return null;
                }
            }

            CopyProgressBar.Value++;
            CopyProgressBar.Refresh();

            return game;
        }

        private GameOnPc RetrieveGameInArchive(string compressedFilePath)
        {
            GameOnPc game = null;
            WriteInfo($"Checking archive {compressedFilePath}...");
            IArchive archive;
            try
            {
                archive = ArchiveFactory.Open(new FileInfo(compressedFilePath));
            }
            catch (Exception ex)
            {
                WriteError($"Could not open archive {compressedFilePath}. Error: {ex.Message}");
                CopyProgressBar.Value += 10;
                CopyProgressBar.Refresh();
                return null;
            }

            if (ArchiveManager.RetreiveUniqueFileFromArchiveEndingWith(archive, ".gdi") == null)
            {
                WriteWarning($"Could not find GDI in archive {compressedFilePath} (CDI are ignored in archives)");
                CopyProgressBar.Value += 10;
                CopyProgressBar.Refresh();
                return null;
            }
            else
            {

                try
                {
                    if (archive.Type == SharpCompress.Common.ArchiveType.SevenZip && !viewModel.MustScanSevenZip)
                    {
                        WriteWarning($"Archive {compressedFilePath} ignored as it's a 7z file and the option isn't ticked.");
                        CopyProgressBar.Value += 10;
                        CopyProgressBar.Refresh();
                        return null;
                    }

                    game = GameManager.ExtractPcGameDataFromArchive(compressedFilePath, archive);
                }
                catch (Exception ex)
                {
                    WriteError(ex.Message);
                    CopyProgressBar.Value += 10;
                    CopyProgressBar.Refresh();
                    return null;
                }
            }
            CopyProgressBar.Value += 10;
            CopyProgressBar.Refresh();

            return game;
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