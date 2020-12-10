using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.DTO;
using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        private static readonly ILog logger = LogManager.GetLogger(typeof(MainWindow));
        private static readonly string ApplySelectedActionsButtonTextWhileActive = "Apply selected actions";
        private static readonly string ApplySelectedActionsButtonTextWhileCopying = "Applying selected actions...";
        private static readonly string ConfigurationPath = @".\config.json";
        private static readonly string pathSplitter = @"|";
        private bool IsSdCardMounted = false;
        private bool IsScanSuccessful = false;
        private bool HavePathsChangedSinceLastScanSuccessful = true;
        private GridLength[] starHeight;
        private Version currentVersion;
        private UgdegmConfiguration config;

        private IEnumerable<GameOnSd> gamesOnSdCard;

        public MainWindow()
        {
            SetupExceptionHandling();
            InitializeComponent();

            gamesOnSdCard = new List<GameOnSd>();

            currentVersion = new Version(File.ReadAllText(@".\VERSION"));
            config = UgdegmConfiguration.LoadConfiguration(ConfigurationPath);

            LoadDefaultPaths();

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            SdFolderComboBox.ItemsSource = allDrives.Select(d => d.Name);

            PcFolderTextBox.TextChanged += OnFolderOrDriveChanged;
            SdFolderComboBox.SelectionChanged += OnFolderOrDriveChanged;
            SdFolderComboBox.SelectionChanged += OnDriveChanged;

            // Initialization for the expanders
            starHeight = new GridLength[ExpanderGrid.RowDefinitions.Count];
            starHeight[0] = ExpanderGrid.RowDefinitions[0].Height;
            starHeight[2] = ExpanderGrid.RowDefinitions[2].Height;
            ExpandedOrCollapsed(FoldersExpander);
            ExpandedOrCollapsed(GamesExpander);
            FoldersExpander.Expanded += ExpandedOrCollapsed;
            FoldersExpander.Collapsed += ExpandedOrCollapsed;
            GamesExpander.Expanded += ExpandedOrCollapsed;
            GamesExpander.Collapsed += ExpandedOrCollapsed;

            Title += " - " + currentVersion;
            CheckUpdate();
        }

        private void ExpandedOrCollapsed(object sender, RoutedEventArgs e)
        {
            ExpandedOrCollapsed(sender as Expander);
        }

        private void ExpandedOrCollapsed(Expander expander)
        {
            var rowIndex = Grid.GetRow(expander);
            var row = ExpanderGrid.RowDefinitions[rowIndex];
            if (expander.IsExpanded)
            {
                row.Height = starHeight[rowIndex];
                row.MinHeight = 50;
            }
            else
            {
                starHeight[rowIndex] = row.Height;
                row.Height = GridLength.Auto;
                row.MinHeight = 0;
            }

            var bothExpanded = FoldersExpander.IsExpanded && GamesExpander.IsExpanded;
            ExpanderGridSplitter.Visibility = bothExpanded ?
                Visibility.Visible : Visibility.Collapsed;
        }

        private void SetupExceptionHandling()
        {
            log4net.Repository.ILoggerRepository repository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            Hierarchy hierarchy = (Hierarchy)repository;
            PatternLayout patternLayout = new PatternLayout
            {
                ConversionPattern = "%date %level - %message%newline%exception"
            };
            patternLayout.ActivateOptions();

            RollingFileAppender roller = new RollingFileAppender
            {
                AppendToFile = true,
                File = "Log_",
                DatePattern = "dd_MM_yyyy'.log'",
                Layout = patternLayout,
                MaxFileSize = 1024 * 1024 * 10,
                MaxSizeRollBackups = 10,
                StaticLogFileName = false,
                RollingStyle = RollingFileAppender.RollingMode.Composite
            };
            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);
            hierarchy.Root.Level = log4net.Core.Level.Info;
            hierarchy.Configured = true;

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                message = string.Format("Unhandled exception in {0}", assemblyName.Name);
            }
            catch (Exception ex)
            {
                logger.Error("Exception in LogUnhandledException", ex);
                WriteError("Exception in LogUnhandledException - " + exception.Message);
            }
            finally
            {
                logger.Error(message, exception);
                WriteError(message + " - " + exception.Message);
            }
        }

        private void CheckUpdate()
        {
            Version lastVersion;
            using (System.Net.WebClient wc = new System.Net.WebClient())
            {
                string lastVersionString = wc.DownloadString(config.VersionUrl);
                lastVersion = new Version(Regex.Replace(lastVersionString, @"\t|\n|\r", ""));
            }

            if (currentVersion.CompareTo(lastVersion) < 0)
            {
                string messageBoxText = "A new version is available. Do you want to download it?";
                string caption = "New version available!";
                MessageBoxButton button = MessageBoxButton.YesNo;
                MessageBoxImage icon = MessageBoxImage.Warning;
                MessageBoxResult messageBoxResult = MessageBox.Show(messageBoxText, caption, button, icon);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    OpenBrowser(config.ReleasesUrl);
                }
            }
        }

        private static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            //else
            //{
            //    ...
            //}
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
            PcFolderTextBox.Text = config.PcDefaultPath;
            SdFolderComboBox.SelectedItem = config.SdDefaultDrive;
        }

        private void PcAddButton_Click(object sender, RoutedEventArgs e)
        {
            var browserDialog = new VistaFolderBrowserDialog();
            browserDialog.ShowDialog();
            PcFolderTextBox.Text += pathSplitter + browserDialog.SelectedPath;
        }

        private void PcClearButton_Click(object sender, RoutedEventArgs e)
        {
            PcFolderTextBox.Text = string.Empty;
        }

        private void LoadAllButton_Click(object sender, RoutedEventArgs e)
        {
            ScanFolders();
            ApplySelectedActionsButton.IsEnabled = IsScanSuccessful;
            HavePathsChangedSinceLastScanSuccessful = false;
        }

        private void ScanFolders()
        {
            WriteInfo("Starting scan...");
            IsScanSuccessful = true;
            LoadGamesOnPc();
            LoadGamesOnSd();
        }

        private void LoadGamesOnPc()
        {
            PcFoldersWithGdiListView.ItemsSource = new List<GameOnPc>();

            if (string.IsNullOrEmpty(PcFolderTextBox.Text))
            {
                WriteError("PC path must not be empty");
                IsScanSuccessful = false;
                return;
            }

            IEnumerable<string> paths = PcFolderTextBox.Text.Split(pathSplitter);

            foreach(string path in paths)
            {
                if (!Directory.Exists(path))
                {
                    WriteError($"PC path {path} is invalid");
                    IsScanSuccessful = false;
                }

            }

            if(!IsScanSuccessful)
            {
                return;
            }

            List<string> subFoldersList = new List<string>();
            foreach (string path in paths)
            {
                subFoldersList.AddRange(Directory.EnumerateDirectories(path));

            }

            var subFoldersWithGdiList = new List<GameOnPc>();

            foreach (var subFolder in subFoldersList)
            {
                if (Directory
                    .EnumerateFiles(subFolder)
                    .Count(f => Path.GetExtension(f) == ".gdi") > 1)
                {
                    WriteError($"You have more than one GDI file in the folder {subFolder}. Please make sure you only have one GDI per folder.");
                    continue;
                }

                var gdiFile = Directory.EnumerateFiles(subFolder).FirstOrDefault(f => Path.GetExtension(f) == ".gdi");

                if (gdiFile != null)
                {
                    GameOnPc game;
                    try
                    {
                        game = GameManager.ExtractPcGameData(subFolder);
                    }
                    catch (Exception error)
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
                gamesOnSdCard = sdCardManager.GetGames(out List<string> errors);
                if (errors.Any())
                {
                    foreach (var error in errors)
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
            catch (Exception ex)
            {
                WriteError(ex.Message);
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
            ApplySelectedActionsButton.IsEnabled = false;
            ApplySelectedActionsButton.Content = ApplySelectedActionsButtonTextWhileCopying;
            RemoveSelectedGames();
            await CopySelectedGames();
            ScanFolders();
            WriteInfo("Creating Menu...");

            try
            {
                if (CreateMenuIndexCheckbox.IsChecked == true)
                {
                    await MenuManager.CreateIndex(SdFolderComboBox.SelectedItem as string, gamesOnSdCard);
                }
                else
                {
                    await MenuManager.CreateMenuWithoutIndex(SdFolderComboBox.SelectedItem as string);
                }

                WriteSuccess("Menu created");
            }
            catch (Exception ex)
            {
                WriteError("Error while trying to create the menu: " + ex.Message);
            }

            ScanFolders();

            ApplySelectedActionsButton.Content = ApplySelectedActionsButtonTextWhileActive;
            ApplySelectedActionsButton.IsEnabled = IsScanSuccessful;
        }

        private async Task CopySelectedGames()
        {
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
                    try
                    {
                        index = sdCardManager.FindAvailableFolderForGame(index);
                    }
                    catch (Exception e)
                    {
                        WriteError("Error while trying to find an available folder to copy games: " + e.Message);
                    }

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
                    string messageBoxText = error.Message;
                    string caption = "Error";
                    MessageBoxButton button = MessageBoxButton.OK;
                    MessageBoxImage icon = MessageBoxImage.Warning;
                    MessageBox.Show(messageBoxText, caption, button, icon);
                    WriteError(error.Message);
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

        private void RemoveSelectedGames()
        {
            try
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
            catch (Exception e)
            {
                WriteError("Error while removing games: " + e.Message);
            }
        }

        private void SaveAsDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            config.PcDefaultPath = PcFolderTextBox.Text;
            config.SdDefaultDrive = SdFolderComboBox.SelectedItem as string;
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
}