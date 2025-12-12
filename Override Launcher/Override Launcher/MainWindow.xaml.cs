using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;



namespace Override_Launcher;
enum LauncherStatus
{
    ready,
    failed,
    awaitDownload,
    awaitUpdate,
    downloadingGame,
    updatingGame
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private LauncherStatus _status;
    internal LauncherStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            switch (_status)
            {
                case LauncherStatus.ready:
                    PlayBtn.Content = "PLAY";
                    PlayBtn.Visibility = Visibility.Visible;
                    PlayBtn.IsEnabled = true;
                    ProgressBar.Visibility = Visibility.Hidden;
                    LblStatus.Visibility = Visibility.Hidden;
                    break;
                case LauncherStatus.failed:
                    PlayBtn.Content = "RETRY";
                    break;
                case LauncherStatus.awaitUpdate:
                    PlayBtn.Content = "UPDATE";
                    break;
                case LauncherStatus.updatingGame:
                    PlayBtn.Content = "UPDATING";
                    PlayBtn.Visibility = Visibility.Hidden;
                    PlayBtn.IsEnabled = false;
                    ProgressBar.Visibility = Visibility.Visible;
                    LblStatus.Visibility = Visibility.Visible;
                    break;
                default:
                    break;
            }
        }
    }

    private string rootPath;
    private string localVersionFile;
    private string zipPath;
    private string gameExe;

    private bool darkModeOn;

    public MainWindow()
    {
        InitializeComponent();

        Debug.WriteLine("Init");

        rootPath = Directory.GetCurrentDirectory();
        localVersionFile = Path.Combine(rootPath, "LocalVersion.txt");
        zipPath = Path.Combine(rootPath, "Build Zip");
        gameExe = Path.Combine(rootPath, "Build Zip_unzipped", "OverrideClient.exe");

        Debug.WriteLine($"Root path set to: {rootPath}");

        GetLocalVersion();

        CheckForUpdates();
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        
    }

    private Version GetLocalVersion()
    {
        if (!File.Exists(localVersionFile))
        {
            Debug.WriteLine($"Error: No local version file found ({GetLocalVersion()})");
            File.WriteAllText(localVersionFile, "0.0.0.0");
        }

        var text = File.ReadAllText(localVersionFile);
        Version localVersion = new Version(text);
        Debug.WriteLine($"Local version found: {localVersion.ToString()} at path {localVersionFile}");
        LocalVersionInfo.Text = $"Version: {localVersion.ToString()}";
        return localVersion;
    }

    private void SetLocalVersion(Version newVersion)
    {
        if (!File.Exists(localVersionFile))
        {
            Debug.WriteLine($"Error: No local version file found (SetLocalVersion())");
            return;
        }

        Debug.WriteLine($"Setting local version to: {newVersion.ToString()}");
        File.WriteAllText(localVersionFile, newVersion.ToString());
        LocalVersionInfo.Text = $"Version: {GetLocalVersion()}";
        Debug.WriteLine($"Local version set to: {GetLocalVersion()}");
    }

    private async void CheckForUpdates()
    {
        Debug.WriteLine("Fetching builds in cloud...");

        var listObjectsV2Response = await GetS3Client().ListObjectsV2Async(new ListObjectsV2Request { BucketName = "override-client-builds" });

        S3Object? obj = listObjectsV2Response.S3Objects.FirstOrDefault();

        if (obj == null)
        {
            Debug.WriteLine("Build not found in cloud");
            return;
        }

        Debug.WriteLine($"Remote build found: {obj.Key}");

        Version localVersion = GetLocalVersion();
        Version remoteVersion = new Version(obj.Key);

        Debug.WriteLine($"Current local version: {localVersion.ToString()}");
        Debug.WriteLine($"Current remote version: {remoteVersion.ToString()}");

        if(localVersion.IsDifferentThan(remoteVersion))
        {
            Debug.WriteLine("Local and remote version differ, starting updating process");

            await UpdateVersionAsync(remoteVersion, obj.Key);
        }
    }

    private async Task UpdateVersionAsync(Version remoteVersion, string objKey)
    {
        Status = LauncherStatus.updatingGame;

        string bucketName = "override-client-builds";

        GetObjectResponse objResponse = await GetS3Client().GetObjectAsync(bucketName, objKey);

        Debug.WriteLine($"Downloading latest build: {objResponse.Key}");

        await DownloadObj(objResponse);

        SetLocalVersion(remoteVersion);

        Debug.WriteLine($"Current local version is now: {GetLocalVersion()}");

        Status = LauncherStatus.ready;
    }

    private async Task DownloadObj(GetObjectResponse objResponse)
    {
        long totalBytes = objResponse.ContentLength;
        long readBytes = 0;

        string zipFilePath = $"{zipPath}.zip";
        string extractFolder = $"{zipPath}_unzipped";

        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

        Debug.WriteLine($"Downloading...");

        using (var output = File.Create(zipFilePath))
        using (var input = objResponse.ResponseStream)
        {
            byte[] buffer = new byte[81920]; // = 80 Kb, .NET recommanded size 
            int bytesRead;

            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, bytesRead);

                readBytes += bytesRead;
                double progress = (double)readBytes / totalBytes * 100.0;

                ProgressBar.Value = progress;
                LblStatus.Text = $"{progress:0.0}%";
                Debug.WriteLine($"Download progress: {progress:0.0}%");
            }
        }

        Debug.WriteLine($"ZIP downloaded at: {zipPath}");
        Debug.WriteLine($"Unzipping...");

        if (Directory.Exists(extractFolder)) Directory.Delete(extractFolder, true);

        Directory.CreateDirectory(extractFolder);

        using (var zipArchive = ZipFile.OpenRead(zipFilePath))
        {
            foreach (var entry in zipArchive.Entries)
            {
                string destinationPath = Path.Combine(extractFolder, entry.FullName);

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        }

        Debug.WriteLine($"Unzipped to: {extractFolder}");
    }

    private AmazonS3Client GetS3Client()
    {
        return new AmazonS3Client(
            new BasicAWSCredentials
            (
                "682ad29d84b67e11338c6496ef4e7fda", 
                "1a6977e88b5260b1cb6120a740d90fc4758218cd3a9566db111cc800229ad336"
            ),
            new AmazonS3Config
            {
                ServiceURL = "https://9eab8eea7ec7092e14a64d4c7ce8163c.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                AllowAutoRedirect = true
            });
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"Play Btn Clicked");

        if (File.Exists(gameExe) && Status == LauncherStatus.ready)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
            startInfo.WorkingDirectory = Path.Combine(rootPath, "Build Zip_unzipped");
            startInfo.Arguments = "192.168.140.201:7777";
            Process.Start(startInfo);

            Close();
        }
        else if (Status == LauncherStatus.failed)
        {
            CheckForUpdates();
        }
    }

    private void DarkMode_Click(object sender, RoutedEventArgs e)
    {
        darkModeOn = !darkModeOn;

        if (darkModeOn) 
        {
            DarkModeIcon.Source = new BitmapImage(new Uri("pack://application:,,,/res/img/Sun.png"));
            Background.Source = new BitmapImage(new Uri("pack://application:,,,/res/img/DarkMode_Background.png"));
        }
        else
        {
            DarkModeIcon.Source = new BitmapImage(new Uri("pack://application:,,,/res/img/Moon.png"));
            Background.Source = new BitmapImage(new Uri("pack://application:,,,/res/img/LightMode_Background.png"));
        }
    }

    struct Version
    {
        internal static Version zero = new Version(0, 0, 0, 0);

        private short major;
        private short minor;
        private short revision;
        private short patch;

        internal Version(short _major, short _minor, short _revision, short _patch)
        {
            major = _major;
            minor = _minor;
            revision = _revision;
            patch = _patch;
        }

        internal Version(string input)
        {
            major = minor = revision = patch = 0;

            if (string.IsNullOrWhiteSpace(input))
                return;

            string core = Path.GetFileName(input);

            if (core.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                core = core.Substring(0, core.LastIndexOf(".zip", StringComparison.OrdinalIgnoreCase));


            string[] parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                Debug.WriteLine(core);
                return;
            }

            short.TryParse(parts[0], out major);
            short.TryParse(parts[1], out minor);
            short.TryParse(parts[2], out revision);
            short.TryParse(parts[3], out patch);
        }

        internal bool IsDifferentThan(Version _otherVersion)
        {
            if (major != _otherVersion.major) return true;

            if (minor != _otherVersion.minor) return true;        

            if (revision != _otherVersion.revision) return true;
                
            if (patch != _otherVersion.patch) return true;

            return false;
        }

        public override string ToString()
        {
            return $"{major}.{minor}.{revision}.{patch}";
        }
    }
}