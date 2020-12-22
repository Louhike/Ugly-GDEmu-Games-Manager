using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.DTO;
using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Ookii.Dialogs.Wpf;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
        private static readonly string ApplySelectedActionsButtonTextWhileActive = "Apply";
        private static readonly string ApplySelectedActionsButtonTextWhileCopying = "Applying ";
        private static readonly string ConfigurationPath = @".\config.json";
        private static readonly string pathSplitter = @"|";
        private bool IsSdCardMounted = false;
        private bool IsScanSuccessful = false;
        private bool HavePathsChangedSinceLastScanSuccessful = true;
        private GridLength[] starHeight;
        private Version currentVersion;
        private UgdegmConfiguration config;
        private string orderedBy = "Game";
        private bool isAscending = true;

        private IEnumerable<GameOnSd> gamesOnSdCard;

        public ScanViewModel scanViewModel { get; set; }

        public MainWindow()
        {
            SetupExceptionHandling();
            InitializeComponent();

            gamesOnSdCard = new List<GameOnSd>();

            currentVersion = new Version(File.ReadAllText(@".\VERSION"));

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            SdFolderComboBox.ItemsSource = allDrives.Select(d => d.Name);

            config = UgdegmConfiguration.LoadConfiguration(ConfigurationPath);
            LoadDefaultPaths();

            PcFolderTextBox.TextChanged += OnFolderOrDriveChanged;
            SdFolderComboBox.SelectionChanged += OnFolderOrDriveChanged;
            SdFolderComboBox.SelectionChanged += OnDriveChanged;

            // Initialization for the expanders
            starHeight = new GridLength[GamesExpanderGrid.RowDefinitions.Count];
            starHeight[0] = GamesExpanderGrid.RowDefinitions[0].Height;
            starHeight[2] = GamesExpanderGrid.RowDefinitions[2].Height;
            ExpandedOrCollapsedRow(FoldersExpander);
            ExpandedOrCollapsedRow(GamesExpander);
            FoldersExpander.Expanded += ExpandedOrCollapsedRow;
            FoldersExpander.Collapsed += ExpandedOrCollapsedRow;
            GamesExpander.Expanded += ExpandedOrCollapsedRow;
            GamesExpander.Collapsed += ExpandedOrCollapsedRow;

            AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ListView_OnColumnClick));

            Title += " - " + currentVersion;
            CheckUpdate();

            //ScanFolders();
            //ApplySelectedActionsButton.IsEnabled = IsScanSuccessful;
        }

        private void ListView_OnColumnClick(object sender, RoutedEventArgs e)
        {
            var columnClicked = e.OriginalSource as GridViewColumnHeader;
            if (columnClicked == null) return;
            var columnName = (columnClicked.Column.Header as string)[0..^2];
            switch (columnName)
            {
                case "Game":
                    SortByStringColumn(columnClicked, columnName, g => g.GameName);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;

                case "Folder":
                    SortByStringColumn(columnClicked, columnName, g => g.Path);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;

                case "SD folder":
                    SortByStringColumn(columnClicked, columnName, g => g.SdFolder);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    break;

                case "Size on PC":
                    SortByNullableLongColumn(columnClicked, columnName, g => g.Size);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;

                case "Size on SD":
                    SortByNullableLongColumn(columnClicked, columnName, g => g.SdSize);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;
            }
        }

        private void SortByStringColumn(GridViewColumnHeader columnClicked, string columnName, Func<GameOnPc, string> propertyAccessor)
        {
            if (orderedBy == columnName && isAscending)
            {
                PcFoldersWithGdiListView.ItemsSource = PcFoldersWithGdiListView.Items
                    .Cast<GameOnPc>().OrderByDescending(propertyAccessor, new EmptyStringsAreLastDescending());
                isAscending = false;
                columnClicked.Column.Header = columnName + " ▼";

                return;
            }

            PcFoldersWithGdiListView.ItemsSource = PcFoldersWithGdiListView.Items
                .Cast<GameOnPc>().OrderBy(propertyAccessor, new EmptyStringsAreLast());
            isAscending = true;
            orderedBy = columnName;
            columnClicked.Column.Header = columnName + " ▲";
        }

        private void SortByNullableLongColumn(GridViewColumnHeader columnClicked, string columnName, Func<GameOnPc, long?> propertyAccessor)
        {
            if (orderedBy == columnName && isAscending)
            {
                PcFoldersWithGdiListView.ItemsSource = PcFoldersWithGdiListView.Items
                    .Cast<GameOnPc>().OrderByDescending(propertyAccessor, new NullValuesAreLastDescending());
                isAscending = false;
                columnClicked.Column.Header = columnName + " ▼";

                return;
            }

            PcFoldersWithGdiListView.ItemsSource = PcFoldersWithGdiListView.Items
                .Cast<GameOnPc>().OrderBy(propertyAccessor, new NullValuesAreLast());
            isAscending = true;
            orderedBy = columnName;
            columnClicked.Column.Header = columnName + " ▲";
        }

        private void RenameSortableColumnToDefault(GridViewColumn column)
        {
            column.Header = (column.Header as string)[0..^2] + " ▬";
        }

        private void ExpandedOrCollapsedRow(object sender, RoutedEventArgs e)
        {
            ExpandedOrCollapsedRow(sender as Expander);
        }

        private void ExpandedOrCollapsedRow(Expander expander)
        {
            var rowIndex = Grid.GetRow(expander);
            var row = GamesExpanderGrid.RowDefinitions[rowIndex];
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
            GamesExpanderGridSplitter.Visibility = bothExpanded ?
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
        }

        private void OnFolderOrDriveChanged(object sender, RoutedEventArgs e)
        {
            if (!HavePathsChangedSinceLastScanSuccessful)
            {
                ApplySelectedActionsButton.IsEnabled = false;
                HavePathsChangedSinceLastScanSuccessful = true;
            }

            SavePathsAsDefaults();
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
            if (!string.IsNullOrEmpty(PcFolderTextBox.Text))
            {
                PcFolderTextBox.Text += pathSplitter;
            }

            PcFolderTextBox.Text += browserDialog.SelectedPath;
        }

        private void LoadAllButton_Click(object sender, RoutedEventArgs e)
        {
            ScanFolders();
            ApplySelectedActionsButton.IsEnabled = IsScanSuccessful;
            HavePathsChangedSinceLastScanSuccessful = false;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string messageBoxText = "If you have an error or do not understand something, you can leave an issue at the Github page. Do you want to open the page in your browser?";
            string caption = "Need help?";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Question;
            MessageBoxResult messageBoxResult = MessageBox.Show(messageBoxText, caption, button, icon);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                OpenBrowser(config.IssuesUrl);
            }
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

            scanViewModel = new ScanViewModel
            {
                MustScanSevenZip = ScanSevenZipCheckbox.IsChecked == true,
                PcFolder = PcFolderTextBox.Text
            };

            var scanWindows = new ScanWindow(scanViewModel);
            scanWindows.Show();
            scanWindows.LoadGamesOnPc();
            OnScanFinished();
        }

        private void OnScanFinished()
        {
            if(scanViewModel.GamesOnPc != null)
            {
                PcFoldersWithGdiListView.ItemsSource = scanViewModel.GamesOnPc.OrderBy(f => f.GameName);
            }
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
            SdSpaceLabel.Content = FileManager.FormatSize(freeSpace) + " free";

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
                if (gamesOnSdCard.Any(f => f.GameName == pcViewItem.GameName && f.Disc == pcViewItem.Disc && f.IsGdi == pcViewItem.IsGdi))
                {
                    var gameOnSd = gamesOnSdCard.First(f => f.GameName == pcViewItem.GameName && f.Disc == pcViewItem.Disc);
                    pcViewItem.IsInSdCard = true;
                    pcViewItem.MustBeOnSd = true;
                    pcViewItem.IsInSdCardString = "✓";
                    pcViewItem.SdFolder = gameOnSd.Path;
                    pcViewItem.SdSize = FileManager.GetDirectorySize(gameOnSd.FullPath);
                    pcViewItem.SdFormattedSize = FileManager.GetDirectoryFormattedSize(gameOnSd.FullPath);
                }
                else
                {
                    pcViewItem.IsInSdCard = false;
                    pcViewItem.MustBeOnSd = false;
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

            var gamesToCopy = PcFoldersWithGdiListView.Items
                .Cast<GameOnPc>()
                .Where(i => i.MustBeOnSd && (!i.IsInSdCard || i.MustShrink));

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
                    .Where(g => !g.MustBeOnSd && gamesOnSdCard.Any(sg => sg.GameName == g.GameName && sg.Disc == g.Disc));
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

        private void SavePathsAsDefaults()
        {
            config.PcDefaultPath = PcFolderTextBox.Text;
            config.SdDefaultDrive = SdFolderComboBox.SelectedItem as string;
            config.Save(ConfigurationPath);
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
            InfoRichTextBox.Focus();
            InfoRichTextBox.CaretPosition = InfoRichTextBox.CaretPosition.DocumentEnd;
            InfoRichTextBox.ScrollToEnd();
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var logsWindow = new LogsWindow(GetRtfStringFromRichTextBox(InfoRichTextBox));
            logsWindow.Show();
        }

        public string GetRtfStringFromRichTextBox(RichTextBox richTextBox)
        {
            TextRange textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            MemoryStream ms = new MemoryStream();
            textRange.Save(ms, DataFormats.Rtf);

            return Encoding.Default.GetString(ms.ToArray());
        }

        private void ScanSevenZipCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            string messageBoxText = $"Some 7zip files can be quite slow to be scanned. So don't use this option on a folder with a lot of them if you want the scan to be quick.{Environment.NewLine}Other types of archives (zip, rar, tar, bz, etc.) will be scanned even if this option isn't selected.{Environment.NewLine}Do you want to activate the option?";
            string caption = "Add 7z files scanning";
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Warning;
            MessageBoxResult messageBoxResult = MessageBox.Show(messageBoxText, caption, button, icon);
            if (messageBoxResult == MessageBoxResult.No)
            {
                ScanSevenZipCheckbox.IsChecked = false;
            }
        }
    }

    public class EmptyStringsAreLast : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (String.IsNullOrEmpty(y) && !String.IsNullOrEmpty(x))
            {
                return -1;
            }
            else if (!String.IsNullOrEmpty(y) && String.IsNullOrEmpty(x))
            {
                return 1;
            }
            else
            {
                return String.Compare(x, y);
            }
        }
    }

    public class EmptyStringsAreLastDescending : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (String.IsNullOrEmpty(y) && !String.IsNullOrEmpty(x))
            {
                return 1;
            }
            else if (!String.IsNullOrEmpty(y) && String.IsNullOrEmpty(x))
            {
                return -1;
            }
            else
            {
                return String.Compare(x, y);
            }
        }
    }

    public class NullValuesAreLast : IComparer<long?>
    {
        public int Compare(long? x, long? y)
        {
            if (y == null && x != null)
            {
                return -1;
            }
            else if (y != null && x == null)
            {
                return 1;
            }
            else if (y == null) //  && x == null => implicit
            {
                return 0;
            }
            else
            {
                return ((long)x).CompareTo((long)y);
            }
        }
    }

    public class NullValuesAreLastDescending : IComparer<long?>
    {
        public int Compare(long? x, long? y)
        {
            if (y == null && x != null)
            {
                return 1;
            }
            else if (y != null && x == null)
            {
                return -1;
            }
            else if (y == null) //  && x == null => implicit
            {
                return 0;
            }
            else
            {
                return ((long)x).CompareTo((long)y);
            }
        }
    }
}