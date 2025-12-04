using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
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
                    PlayBtn.Content = "Play";
                    break;
                case LauncherStatus.failed:
                    PlayBtn.Content = "Retry";
                    break;
                case LauncherStatus.awaitDownload:
                    PlayBtn.Content = "Download";
                    break;
                case LauncherStatus.awaitUpdate:
                    PlayBtn.Content = "Update";
                    break;
                case LauncherStatus.downloadingGame:
                    PlayBtn.Content = "Downloading";
                    break;
                case LauncherStatus.updatingGame:
                    PlayBtn.Content = "Updating";
                    break;
                default:
                    break;
            }
        }
    }

    private string rootPath;
    private string versionFile;
    private string gameZip;
    private string gameExe;

    private bool darkModeOn;

    public MainWindow()
    {
        InitializeComponent();

        rootPath = Directory.GetCurrentDirectory();
        versionFile = Path.Combine(rootPath, "LocalVersion.txt");
        gameZip = Path.Combine(rootPath, "Build.zip");
        gameExe = Path.Combine(rootPath, "Build", "Pirate Game.exe");

        //CheckForUpdates();
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        
    }

    private async void CheckForUpdates()
    {
        string remoteVersionFile = await GetRemoteVersionFileAsync();
        Version remoteVersion = new Version(ExtractRemoteVersionFile(remoteVersionFile));

    }

    public async Task<string> GetRemoteVersionFileAsync()
    {
        var url = "http://192.168.140.200:8080/VersionInfo.txt";
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    public string ExtractRemoteVersionFile(string fileContent)
    {
        var major = "";
        var minor = "";
        var revision = "";
        var patch = "";

        foreach (var line in fileContent.Split('\n'))
        {
            if (line.StartsWith("Major=")) major = line.Replace("Major=", "").Trim();
            if (line.StartsWith("Minor=")) minor = line.Replace("Minor=", "").Trim();
            if (line.StartsWith("Revision=")) revision = line.Replace("Revision=", "").Trim();
            if (line.StartsWith("Patch=")) patch = line.Replace("Patch=", "").Trim();
        }

        return $"{major}.{minor}.{revision}.{patch}";
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(gameExe) && Status == LauncherStatus.ready)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
            startInfo.WorkingDirectory = Path.Combine(rootPath, "Build");
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

        internal Version(string _version)
        {
            string[] versionStrings = _version.Split('.');
            if (versionStrings.Length != 4)
            {
                major = 0;
                minor = 0;
                revision = 0;
                patch = 0;
                return;
            }

            major = short.Parse(versionStrings[0]);
            minor = short.Parse(versionStrings[1]);
            revision = short.Parse(versionStrings[2]);
            patch = short.Parse(versionStrings[3]);
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