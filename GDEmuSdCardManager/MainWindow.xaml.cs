using GDEmuSdCardManager.BLL;
using GDEmuSdCardManager.BLL.Comparers;
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
using System.Net.Http;
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

        public ScanViewModel ScanViewModel { get; set; }

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
            Task.Run(async () => { await CheckUpdate(); }).Wait();
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
                AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
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

        private void ListView_OnColumnClick(object sender, RoutedEventArgs e)
        {
            var columnClicked = e.OriginalSource as GridViewColumnHeader;
            if (columnClicked == null) return;
            var columnName = (columnClicked.Column.Header as string)[0..^2];
            switch (columnName)
            {
                case "Game":
                    SortByColumn<EmptyStringsAreLast, EmptyStringsAreLastDescending, string>
                        (columnClicked, columnName, g => g.GameName);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;

                case "Folder/Archive":
                    SortByColumn<EmptyStringsAreLast, EmptyStringsAreLastDescending, string>
                        (columnClicked, columnName, g => g.Path);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;

                case "SD folder":
                    SortByColumn<EmptyStringsAreLast, EmptyStringsAreLastDescending, string>
                        (columnClicked, columnName, g => g.SdFolder);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    break;

                case "Size on PC":
                    SortByColumn<NullValuesAreLastComparer, NullValuesAreLastDescendingComparer, long?>
                        (columnClicked, columnName, g => g.Size);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(SdSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;

                case "Size on SD":
                    SortByColumn<NullValuesAreLastComparer, NullValuesAreLastDescendingComparer, long?>(columnClicked, columnName, g => g.SdSize);
                    RenameSortableColumnToDefault(PathColumn);
                    RenameSortableColumnToDefault(GameNameColumn);
                    RenameSortableColumnToDefault(FormattedSizeColumn);
                    RenameSortableColumnToDefault(SdFolderColumn);
                    break;
            }
        }

        /// <summary>
        /// Sort game list view on column
        /// </summary>
        /// <typeparam name="TAscendingComparer">Comparer for ascending ordering</typeparam>
        /// <typeparam name="TDescendingComparer">Comparer for descending ordering</typeparam>
        /// <typeparam name="TValueType">Type of the property used for ordering</typeparam>
        /// <param name="columnClicked"></param>
        /// <param name="columnName"></param>
        /// <param name="propertyAccessor"></param>
        private void SortByColumn<TAscendingComparer, TDescendingComparer, TValueType>(
            GridViewColumnHeader columnClicked,
            string columnName,
            Func<GameOnPc, TValueType> propertyAccessor)
            where TAscendingComparer : IComparer<TValueType>, new()
            where TDescendingComparer : IComparer<TValueType>, new()
        {
            if (orderedBy == columnName && isAscending)
            {
                PcFoldersWithGdiListView.ItemsSource = PcFoldersWithGdiListView.Items
                    .Cast<GameOnPc>().OrderByDescending(propertyAccessor, new TDescendingComparer());
                isAscending = false;
                columnClicked.Column.Header = columnName + " ▼";

                return;
            }

            PcFoldersWithGdiListView.ItemsSource = PcFoldersWithGdiListView.Items
                .Cast<GameOnPc>().OrderBy(propertyAccessor, new TAscendingComparer());
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

        private async Task CheckUpdate()
        {
            Version lastVersion;
            using (var wc = new HttpClient())
            {
                string lastVersionString = await wc.GetStringAsync(config.VersionUrl);
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
                    OpenUrlInBrowser(config.ReleasesUrl);
                }
            }
        }

        private static void OpenUrlInBrowser(string url)
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
                OpenUrlInBrowser(config.IssuesUrl);
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

            ScanViewModel = new ScanViewModel
            {
                MustScanSevenZip = ScanSevenZipCheckbox.IsChecked == true,
                PcFolder = PcFolderTextBox.Text
            };

            var scanWindows = new ScanWindow(ScanViewModel);
            scanWindows.Show();
            scanWindows.LoadGamesOnPc();
            OnScanFinished();
            WriteSuccess("Games on PC scanned");
        }

        private void OnScanFinished()
        {
            if (ScanViewModel.GamesOnPc != null)
            {
                PcFoldersWithGdiListView.ItemsSource = ScanViewModel.GamesOnPc.OrderBy(f => f.GameName);
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

            var games = pcItemsSource.Cast<GameOnPc>();
            foreach (GameOnPc game in games)
            {
                var gameOnSd = gamesOnSdCard.FirstOrDefault(f =>
                    f.GameName == game.GameName
                    && f.Disc == game.Disc
                    && f.IsGdi == game.IsGdi);
                if (gameOnSd != null)
                {
                    GameManager.LinkGameOnPcToGameOnSd(game, gameOnSd);
                }
                else
                {
                    GameManager.UnLinkGameOnPcToGameOnSd(game);
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
            LoadGamesOnSd();
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
            ScanViewModel = new ScanViewModel
            {
                GamesOnPc = PcFoldersWithGdiListView.Items.Cast<GameOnPc>().OrderBy(g => g.GameName),
                MustScanSevenZip = ScanSevenZipCheckbox.IsChecked == true,
                PcFolder = PcFolderTextBox.Text,
                SdDrive = SdFolderComboBox.SelectedItem as string
            };

            var scanWindows = new ScanWindow(ScanViewModel);
            scanWindows.Show();
            await scanWindows.CopySelectedGames();
        }

        private void RemoveSelectedGames()
        {
            try
            {
                var gamesToRemove = PcFoldersWithGdiListView
                    .Items
                    .Cast<GameOnPc>()
                    .Where(g => !g.MustBeOnSd && gamesOnSdCard.Any(sg =>
                        sg.GameName == g.GameName
                        && sg.Disc == g.Disc));
                WriteInfo($"Deleting {gamesToRemove.Count()} game(s) from SD card...");
                foreach (GameOnPc itemToRemove in gamesToRemove)
                {
                    WriteInfo($"Deleting {itemToRemove.GameName} {itemToRemove.Disc}...");
                    var gameOnSdToRemove = gamesOnSdCard
                        .FirstOrDefault(g =>
                            g.GameName == itemToRemove.GameName
                            && g.Disc == itemToRemove.Disc);

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
}