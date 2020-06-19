using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.DTO;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
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

        private IEnumerable<GameOnSd> gamesOnSdCard;

        public MainWindow()
        {
            InitializeComponent();
            LoadDefaultPaths();
            gamesOnSdCard = new List<GameOnSd>();
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

        private void LoadAllButton_Click(object sender, RoutedEventArgs e)
        {
            IsScanSuccessful = true;
            LoadGamesOnPc();
            LoadGamesOnSd();
            CopyGamesToSdButton.IsEnabled = IsScanSuccessful;
            RemoveSelectedGamesButton.IsEnabled = IsScanSuccessful;
        }

        private void LoadGamesOnPc()
        {
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
                    subFoldersWithGdiList.Add(new GameOnPc
                    {
                        FullPath = subFolder,
                        GdiName = System.IO.Path.GetFileName(gdiFile),
                        Name = System.IO.Path.GetFileName(subFolder),
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
            }

            UpdatePcFoldersIsInSdCard();

            long freeSpace = sdCardManager.GetFreeSpace();
            SdSpaceLabel.Content = FileManager.FormatSize(freeSpace);

            WriteSuccess("Games on SD scanned");
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            WriteInfo($"Deleting {PcFoldersWithGdiListView.SelectedItems.Count} game(s) from SD card...");
            foreach (GameOnPc itemToRemove in PcFoldersWithGdiListView.SelectedItems)
            {
                var gameOnSdToRemove = gamesOnSdCard.FirstOrDefault(g => g.GdiName == itemToRemove.GdiName);

                FileManager.RemoveAllFilesInDirectory(gameOnSdToRemove.FullPath);
            }

            WriteSuccess($"Games deleted");
            LoadAllButton_Click(null, null);
        }

        private async void CopySelectedGames(object sender, RoutedEventArgs e)
        {
            WriteInfo($"Copying {PcFoldersWithGdiListView.SelectedItems.Count} game(s) to SD card...");
            CopyGamesToSdButton.IsEnabled = false;
            CopyGamesToSdButton.Content = CopyGamesButtonTextWhileCopying;
            var sdSubFoldersListWithGames = Directory.EnumerateDirectories(SdFolderTextBox.Text).Where(f => Directory.EnumerateFiles(f).Any(f => System.IO.Path.GetExtension(f) == ".gdi"));

            var pcGames = PcFoldersWithGdiListView.SelectedItems.Cast<GameOnPc>().ToList();

            var gamesToCopy = pcGames.Where(si => !gamesOnSdCard.Any(f => f.GdiName == si.GdiName));

            CopyProgressLabel.Visibility = Visibility.Visible;
            CopyProgressBar.Maximum = gamesToCopy.Count();
            CopyProgressBar.Value = 0;
            CopyProgressBar.Visibility = Visibility.Visible;

            GamesCopiedTextLabel.Visibility = Visibility.Visible;
            GamesCopiedLabel.Content = $"0/{gamesToCopy.Count()}";
            GamesCopiedLabel.Visibility = Visibility.Visible;

            short index = 2;
            foreach (GameOnPc selectedItem in gamesToCopy)
            {
                string availableFolder = string.Empty;
                do
                {
                    string formattedIndex = index.ToString("D2");
                    if (!sdSubFoldersListWithGames.Any(f => System.IO.Path.GetFileName(f) == formattedIndex))
                    {
                        availableFolder = formattedIndex;
                    }

                    index++;
                } while (string.IsNullOrEmpty(availableFolder));

                string newPath = System.IO.Path.GetFullPath(SdFolderTextBox.Text + @"\" + availableFolder);
                await FileManager.CopyDirectoryContentToAnother(selectedItem.FullPath, newPath);

                CopyProgressBar.Value++;
                GamesCopiedLabel.Content = $"{CopyProgressBar.Value}/{gamesToCopy.Count()}";
            }

            CopyGamesToSdButton.IsEnabled = true;
            CopyGamesToSdButton.Content = CopyGamesButtonTextWhileActive;

            WriteSuccess($"Games copied");
            LoadAllButton_Click(null, null);
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
                if (gamesOnSdCard.Any(f => f.GdiName == pcViewItem.GdiName))
                {
                    pcViewItem.IsInSdCard = "✓";
                    pcViewItem.SdFolder = gamesOnSdCard.First(f => f.GdiName == pcViewItem.GdiName).Name;
                }
                else
                {
                    pcViewItem.IsInSdCard = "Nope";
                }
            }

            ICollectionView view = CollectionViewSource.GetDefaultView(pcItemsSource);
            view.Refresh();
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