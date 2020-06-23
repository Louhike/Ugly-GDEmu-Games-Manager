using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.DTO;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private static readonly string ApplySelectedActionsButtonTextWhileActive = "Apply selected actions";
        private static readonly string ApplySelectedActionsButtonTextWhileCopying = "Copying files...";
        private static readonly string ConfigurationPath = @".\config.json";
        private bool IsSdCardMounted = false;
        private bool IsScanSuccessful = false;
        private bool HavePathsChangedSinceLastScanSuccessful = true;

        private IEnumerable<GameOnSd> gamesOnSdCard;

        public MainWindow()
        {
            InitializeComponent();
            LoadDefaultPaths();
            gamesOnSdCard = new List<GameOnSd>();
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            SdFolderComboBox.ItemsSource = allDrives.Select(d => d.Name);
            PcFolderTextBox.TextChanged += OnFolderOrDriveChanged;
            SdFolderComboBox.SelectionChanged += OnFolderOrDriveChanged;
            SdFolderComboBox.SelectionChanged += OnDriveChanged;
        }

        private void OnFolderOrDriveChanged(object sender, RoutedEventArgs e)
        {
            if (!HavePathsChangedSinceLastScanSuccessful)
            {
                ApplySelectedActionsButton.IsEnabled = false;
                HavePathsChangedSinceLastScanSuccessful = true;
                WriteInfo("You have changed a path. You must rescan the folders");
            }
        }

        private void OnDriveChanged(object sender, RoutedEventArgs e)
        {
            CheckSdCardIsMountedAndInFat32();
        }

        private bool CheckSdCardIsMountedAndInFat32()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            var selectedDrive = allDrives.SingleOrDefault(d => d.Name == SdFolderComboBox.SelectedItem as string);
            if (selectedDrive == null || !selectedDrive.IsReady)
            {
                WriteError("The drive you selected for the SD card does not seem to be mounted");
                return false;
            }

            if (selectedDrive.DriveFormat != "FAT32")
            {
                WriteError("The SD card must be formatted on FAT32");
                return false;
            }

            return true;
        }

        private void LoadDefaultPaths()
        {
            var config = UgdegmConfiguration.LoadConfiguration(ConfigurationPath);
            PcFolderTextBox.Text = config.PcDefaultPath;
            SdFolderComboBox.SelectedItem = config.SdDefaultDrive;
        }

        private void PcBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var browserDialog = new VistaFolderBrowserDialog();
            browserDialog.ShowDialog();
            PcFolderTextBox.Text = browserDialog.SelectedPath;
        }

        private void LoadAllButton_Click(object sender, RoutedEventArgs e)
        {
            WriteInfo("Starting scan...");
            IsScanSuccessful = true;
            LoadGamesOnPc();
            LoadGamesOnSd();
            ApplySelectedActionsButton.IsEnabled = IsScanSuccessful;
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
                if (Directory
                    .EnumerateFiles(subFolder)
                    .Count(f => System.IO.Path.GetExtension(f) == ".gdi") > 1)
                {
                    WriteError($"You have more than one GDI file in the folder {subFolder}. Please make sure you only have one GDI per folder.");
                    continue;
                }

                var gdiFile = Directory.EnumerateFiles(subFolder).FirstOrDefault(f => System.IO.Path.GetExtension(f) == ".gdi");

                if (gdiFile != null)
                {
                    GameOnPc game;
                    try
                    {
                        game = GameManager.ExtractPcGameData(subFolder);
                    }
                    catch(Exception error)
                    {
                        WriteError(error.Message);
                        continue;
                    }

                    subFoldersWithGdiList.Add(game);
                }
            }

            PcFoldersWithGdiListView.ItemsSource = subFoldersWithGdiList.OrderBy(f => f.GameName);
            WriteSuccess("Games on PC scanned");
        }

        private void LoadGamesOnSd()
        {
            IsSdCardMounted = CheckSdCardIsMountedAndInFat32();
            if (!IsSdCardMounted)
            {
                return;
            }

            var sdCardManager = new SdCardManager(SdFolderComboBox.SelectedItem as string);
            try
            {
                List<string> errors;
                gamesOnSdCard = sdCardManager.GetGames(out errors);
                if(errors.Any())
                {
                    foreach(var error in errors)
                    {
                        WriteError(error);
                    }
                }
                SdFolderComboBox.BorderBrush = Brushes.LightGray;
            }
            catch (FileNotFoundException e)
            {
                WriteError(e.Message);
                IsScanSuccessful = false;
                SdFolderComboBox.BorderBrush = Brushes.Red;
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
                if (gamesOnSdCard.Any(f => f.GameName == pcViewItem.GameName && f.Disc == pcViewItem.Disc))
                {
                    var gameOnSd = gamesOnSdCard.First(f => f.GameName == pcViewItem.GameName && f.Disc == pcViewItem.Disc);
                    pcViewItem.IsInSdCard = true;
                    pcViewItem.IsInSdCardString = "✓";
                    pcViewItem.SdFolder = gameOnSd.Path;
                    pcViewItem.SdFormattedSize = FileManager.GetDirectoryFormattedSize(gameOnSd.FullPath);
                }
                else
                {
                    pcViewItem.IsInSdCard = false;
                    pcViewItem.IsInSdCardString = "🚫";
                }
            }

            ICollectionView view = CollectionViewSource.GetDefaultView(pcItemsSource);
            view.Refresh();
        }

        private async void ApplySelectedActions(object sender, RoutedEventArgs e)
        {
            RemoveSelectedGames();
            await CopySelectedGames();
            LoadAllButton_Click(null, null);
        }

        private async Task CopySelectedGames()
        {
            ApplySelectedActionsButton.IsEnabled = false;
            ApplySelectedActionsButton.Content = ApplySelectedActionsButtonTextWhileCopying;
            var sdCardManager = new SdCardManager(SdFolderComboBox.SelectedItem as string);

            var gamesToCopy = PcFoldersWithGdiListView.Items.Cast<GameOnPc>().Where(i => i.MustCopy);
            WriteInfo($"Copying {gamesToCopy.Count()} game(s) to SD card...");

            CopyProgressLabel.Visibility = Visibility.Visible;
            CopyProgressBar.Maximum = gamesToCopy.Count();
            CopyProgressBar.Value = 0;
            CopyProgressBar.Visibility = Visibility.Visible;

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
                    index = sdCardManager.FindAvailableFolderForGame(index);
                    if (index == -1)
                    {
                        WriteError($"You cannot have more than 9999 games on your SD card.");
                        break;
                    }
                }

                try
                {
                    await sdCardManager.AddGame(selectedItem, index);
                    CopyProgressBar.Value++;
                    WriteInfo($"{CopyProgressBar.Value}/{gamesToCopy.Count()} games copied");
                }
                catch (Exception error)
                {
                    WriteError(error.Message);
                }
            }

            ApplySelectedActionsButton.IsEnabled = true;
            ApplySelectedActionsButton.Content = ApplySelectedActionsButtonTextWhileActive;

            if (CopyProgressBar.Value < gamesToCopy.Count())
            {
                WriteInfo($"There was an error. {CopyProgressBar.Value} games were copied.");
            }
            else
            {
                WriteSuccess($"Games copied");
            }
        }

        private void RemoveSelectedGames()
        {
            var gamesToRemove = PcFoldersWithGdiListView
                .Items
                .Cast<GameOnPc>()
                .Where(g => g.MustRemove && gamesOnSdCard.Any(sg => sg.GameName == g.GameName && sg.Disc == g.Disc));
            WriteInfo($"Deleting {gamesToRemove.Count()} game(s) from SD card...");
            foreach (GameOnPc itemToRemove in gamesToRemove)
            {
                WriteInfo($"Deleting {itemToRemove.GameName} {itemToRemove.Disc}...");
                var gameOnSdToRemove = gamesOnSdCard
                    .FirstOrDefault(g => g.GameName == itemToRemove.GameName && g.Disc == itemToRemove.Disc);

                FileManager.RemoveAllFilesInDirectory(gameOnSdToRemove.FullPath);
            }

            WriteSuccess($"Games deleted");
        }

        private void SaveAsDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            var config = new UgdegmConfiguration()
            {
                PcDefaultPath = PcFolderTextBox.Text,
                SdDefaultDrive = SdFolderComboBox.SelectedItem as string
            };

            config.Save(ConfigurationPath);
        }

        private void LoadDefaultsPathsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDefaultPaths();
        }

        private void WriteError(string message)
        {
            WriteMessageInRichTextBox(message, Brushes.Red);
        }

        private void WriteInfo(string message)
        {
            WriteMessageInRichTextBox(message, Brushes.Black);
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
            InfoRichTextBox.ScrollToEnd();
        }
    }

    public class InvertedBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}