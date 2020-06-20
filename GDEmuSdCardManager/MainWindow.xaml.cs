using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.DTO;
using Medallion.Shell;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace GDEmuSdCardManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string CopyGamesButtonTextWhileActive = "Copy selected games to SD";
        private static string CopyGamesButtonTextWhileCopying = "Copying files...";
        private static string ConfigurationPath = @".\config.json";
        private bool IsScanSuccessful = false;
        private bool HavePathsChangedSinceLastScanSuccessful = true;

        private IEnumerable<GameOnSd> gamesOnSdCard;

        public MainWindow()
        {
            InitializeComponent();
            LoadDefaultPaths();
            gamesOnSdCard = new List<GameOnSd>();
            PcFolderTextBox.TextChanged += OnFolderChanged;
            SdFolderTextBox.TextChanged += OnFolderChanged;
        }

        private void OnFolderChanged(object sender, TextChangedEventArgs e)
        {
            if(!HavePathsChangedSinceLastScanSuccessful)
            {
                CopyGamesToSdButton.IsEnabled = false;
                RemoveSelectedGamesButton.IsEnabled = false;
                HavePathsChangedSinceLastScanSuccessful = true;
                WriteInfo("You have changed a path. You must rescan the folders");
            }
        }

        private void LoadDefaultPaths()
        {
            var config = UGDEBConfiguration.LoadConfiguration(ConfigurationPath);
            PcFolderTextBox.Text = config.PcDefaultPath;
            SdFolderTextBox.Text = config.SdDefaultDrive;
        }

        private void PcBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var browserDialog = new VistaFolderBrowserDialog();
            browserDialog.ShowDialog();
            PcFolderTextBox.Text = browserDialog.SelectedPath;
        }

        private void SdBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var browserDialog = new VistaFolderBrowserDialog();
            browserDialog.ShowDialog();
            SdFolderTextBox.Text = browserDialog.SelectedPath;
        }

        private async void LoadAllButton_Click(object sender, RoutedEventArgs e)
        {
            IsScanSuccessful = true;
            LoadGamesOnPc();
            LoadGamesOnSd();
            CopyGamesToSdButton.IsEnabled = IsScanSuccessful;
            RemoveSelectedGamesButton.IsEnabled = IsScanSuccessful;
            HavePathsChangedSinceLastScanSuccessful = false;
        }

        private void LoadGamesOnPc()
        {
            PcFoldersWithGdiListView.ItemsSource = new List<GameOnPc>();
            if (!Directory.Exists(PcFolderTextBox.Text))
            {
                WriteError("PC path is invalid");
                IsScanSuccessful = false;
                return;
            }

            var subFoldersList = Directory.EnumerateDirectories(PcFolderTextBox.Text);
            var subFoldersWithGdiList = new List<GameOnPc>();

            foreach (var subFolder in subFoldersList)
            {
                var gdiFile = Directory.EnumerateFiles(subFolder).SingleOrDefault(f => System.IO.Path.GetExtension(f) == ".gdi");

                if (gdiFile != null)
                {
                    var bin1File = Directory.EnumerateFiles(subFolder).SingleOrDefault(f => Path.GetFileName(f) == "track01.bin");
                    string gameName = "Unknown name";
                    // Reading the game name
                    byte[] buffer = File.ReadAllBytes(bin1File).Skip(144).Take(140).ToArray();
                    gameName = System.Text.Encoding.UTF8.GetString(buffer).Replace('\0',' ').Trim();

                    subFoldersWithGdiList.Add(new GameOnPc
                    {
                        FullPath = subFolder,
                        GameName = gameName,
                        //GdiName = System.IO.Path.GetFileName(gdiFile),
                        Path = System.IO.Path.GetFileName(subFolder),
                        FormattedSize = FileManager.GetDirectoryFormattedSize(subFolder)
                    });
                }
            }

            PcFoldersWithGdiListView.ItemsSource = subFoldersWithGdiList;
            WriteSuccess("Games on PC scanned");
        }

        private void LoadGamesOnSd()
        {
            var sdCardManager = new SdCardManager(SdFolderTextBox.Text);
            try
            {
                gamesOnSdCard = sdCardManager.GetGames();
                SdFolderTextBox.BorderBrush = Brushes.LightGray;
            }
            catch(FileNotFoundException e)
            {
                WriteError(e.Message);
                IsScanSuccessful = false;
                SdFolderTextBox.BorderBrush = Brushes.Red;
                gamesOnSdCard = new List<GameOnSd>();
                return;
            }
            finally
            {
                UpdatePcFoldersIsInSdCard();
            }

            long freeSpace = sdCardManager.GetFreeSpace();
            SdSpaceLabel.Content = FileManager.FormatSize(freeSpace);

            WriteSuccess("Games on SD scanned");
        }

        private void UpdatePcFoldersIsInSdCard()
        {
            var pcItemsSource = PcFoldersWithGdiListView.ItemsSource;
            if (pcItemsSource == null)
            {
                return;
            }

            foreach (GameOnPc pcViewItem in pcItemsSource)
            {
                if (gamesOnSdCard.Any(f => f.GameName == pcViewItem.GameName))
                {
                    var gameOnSd = gamesOnSdCard.First(f => f.GameName == pcViewItem.GameName);
                    pcViewItem.IsInSdCard = "✓";
                    pcViewItem.SdFolder = gameOnSd.Path;
                    pcViewItem.SdFormattedSize = FileManager.GetDirectoryFormattedSize(gameOnSd.FullPath);
                }
                else
                {
                    pcViewItem.IsInSdCard = "Nope";
                }
            }

            ICollectionView view = CollectionViewSource.GetDefaultView(pcItemsSource);
            view.Refresh();
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var gamesToRemove = PcFoldersWithGdiListView.SelectedItems.Cast<GameOnPc>().Where(g => gamesOnSdCard.Any(sg => sg.GameName == g.GameName));
            WriteInfo($"Deleting {gamesToRemove.Count()} game(s) from SD card...");
            foreach (GameOnPc itemToRemove in gamesToRemove)
            {
                WriteInfo($"Deleting {itemToRemove.GameName}...");
                var gameOnSdToRemove = gamesOnSdCard.FirstOrDefault(g => g.GameName == itemToRemove.GameName);

                FileManager.RemoveAllFilesInDirectory(gameOnSdToRemove.FullPath);
            }

            WriteSuccess($"Games deleted");
            LoadAllButton_Click(null, null);
        }

        private async void CopySelectedGames(object sender, RoutedEventArgs e)
        {
            CopyGamesToSdButton.IsEnabled = false;
            CopyGamesToSdButton.Content = CopyGamesButtonTextWhileCopying;
            var sdSubFoldersListWithGames = Directory.EnumerateDirectories(SdFolderTextBox.Text).Where(f => Directory.EnumerateFiles(f).Any(f => System.IO.Path.GetExtension(f) == ".gdi"));

            var pcGames = PcFoldersWithGdiListView.SelectedItems.Cast<GameOnPc>().ToList();
            var gamesToCopy = pcGames
                .Where(si => !gamesOnSdCard.Any(f => f.GameName == si.GameName)
                || (si.MustShrink && si.FormattedSize == si.SdFormattedSize));

            WriteInfo($"Copying {gamesToCopy.Count()} game(s) to SD card...");

            CopyProgressLabel.Visibility = Visibility.Visible;
            CopyProgressBar.Maximum = gamesToCopy.Count();
            CopyProgressBar.Value = 0;
            CopyProgressBar.Visibility = Visibility.Visible;

            short index = 2;

            bool noAvailableFolder = false;
            foreach (GameOnPc selectedItem in gamesToCopy)
            {
                WriteInfo($"Copying {selectedItem.GameName}...");
                string availableFolder = string.Empty;

                if(!string.IsNullOrEmpty(selectedItem.SdFolder))
                {
                    availableFolder = selectedItem.SdFolder;
                }
                else
                {
                    do
                    {
                        string format = index < 100 ? "D2" : index < 1000 ? "D3" : "D4";
                        string formattedIndex = index.ToString(format);
                        if (!sdSubFoldersListWithGames.Any(f => System.IO.Path.GetFileName(f) == formattedIndex))
                        {
                            availableFolder = formattedIndex;
                        }

                        index++;
                        if (index == 10000)
                        {
                            WriteError($"You cannot have more than 9999 games on your SD card.");
                            noAvailableFolder = true;
                            break;
                        }
                    } while (string.IsNullOrEmpty(availableFolder));
                }

                if(noAvailableFolder == false)
                {
                    string newPath = System.IO.Path.GetFullPath(SdFolderTextBox.Text + @"\" + availableFolder);

                    if(selectedItem.MustShrink)
                    {
                        string tempPath = @".\Extract Re-Build GDI's\temp_game_copy";
                        Directory.CreateDirectory(tempPath);
                        FileManager.RemoveAllFilesInDirectory(tempPath);
                        await FileManager.CopyDirectoryContentToAnother(
                            selectedItem.FullPath,
                            tempPath);
                        var commandResult = await Command
                            .Run(@".\Extract Re-Build GDI's\Extract GDI Image.bat", tempPath)
                            .Task;
                        var commandResult2 = await Command
                            .Run(@".\Extract Re-Build GDI's\Build Truncated GDI Image.bat", tempPath + " Extracted")
                            .Task;

                        await FileManager.CopyDirectoryContentToAnother(
                            tempPath,
                            newPath);

                        Directory.Delete(tempPath, true);

                        Directory.Delete(tempPath + " Extracted", true);
                    }
                    else
                    {
                        await FileManager.CopyDirectoryContentToAnother(selectedItem.FullPath, newPath);
                    }

                    CopyProgressBar.Value++;
                    WriteInfo($"{CopyProgressBar.Value}/{gamesToCopy.Count()} games copied");
                }
            }

            CopyGamesToSdButton.IsEnabled = true;
            CopyGamesToSdButton.Content = CopyGamesButtonTextWhileActive;

            if(noAvailableFolder)
            {
                WriteInfo($"There was an error. {CopyProgressBar.Value} games were copied.");
            }
            else
            {
                WriteSuccess($"Games copied");
            }
            LoadAllButton_Click(null, null);
        }

        private void SaveAsDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            var config = new UGDEBConfiguration()
            {
                PcDefaultPath = PcFolderTextBox.Text,
                SdDefaultDrive = SdFolderTextBox.Text
            };

            config.Save(ConfigurationPath);
        }

        private void LoadDefaultsPathsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDefaultPaths();
        }

        private void WriteError(string message)
        {
            var error = new Paragraph(new Run(DateTime.Now.ToString("HH:mm:ss") + ": " + message));
            error.Foreground = Brushes.Red;
            InfoRichTextBox.Document.Blocks.Add(error);
            InfoRichTextBox.ScrollToEnd();
        }

        private void WriteInfo(string message)
        {
            var error = new Paragraph(new Run(DateTime.Now.ToString("HH:mm:ss") + ": " + message));
            error.Foreground = Brushes.Black;
            InfoRichTextBox.Document.Blocks.Add(error);
            InfoRichTextBox.ScrollToEnd();
        }

        private void WriteSuccess(string message)
        {
            var error = new Paragraph(new Run(DateTime.Now.ToString("HH:mm:ss") + ": " + message));
            error.Foreground = Brushes.Blue;
            InfoRichTextBox.Document.Blocks.Add(error);
            InfoRichTextBox.ScrollToEnd();
        }
    }
}