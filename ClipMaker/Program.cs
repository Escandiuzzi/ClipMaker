using System.Diagnostics;
using CommandLine;
using Microsoft.Data.Sqlite;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace ClipMaker;

static class Program
{
    private class Options
    {
        [Option('u', "url", Required = true, HelpText = "Set the url of the video to be trimmed")]
        public string Url { get; set; }

        [Option('s', "starttime", Required = true, HelpText = "Set the start time from the video to generate the clip - Expected format: hh-mm-ss")]
        public string StartTime { get; set; }

        [Option('e', "endtime", Required = true, HelpText = "Set the end time from the video to generate the clip - Expected format: hh-mm-ss")]
        public string EndTime { get; set; }
        
        [Option('o', "outputdir", Required = false, HelpText = "Set the output directory for the clip")]
        public string OutputDirectoryPath { get; set; }

        [Option('t', "tempdir", Required = false, HelpText = "Set the temp directory for the video file")]
        public string TempDirectoryPath { get; set; }

        [Option('d', "delete", Required = false, HelpText = "Set if the temp directory should be deleted after clip created")]
        public bool DeleteOnEnd { get; set; }
    }

    private const string _videoDirectoryPathKey = "videoDirectoryPath";
    private const string _clipDirectoryPathKey = "clipDirectoryPath";
    
    private static bool _deleteOnEnd;

    private static string _videoUrl = "";
    private static string _startTime = "";
    private static string _endTime  = "";

    private static string _videoDirectoryPath = "../../Temp/";
    private static string _clipDirectoryPath = "../../Clips/";
    
    static async Task Main(string[] args)
    {
        var data = InitDb();

        if (data.ContainsKey(_videoDirectoryPathKey)) _videoDirectoryPath = data[_videoDirectoryPathKey];
        if (data.ContainsKey(_clipDirectoryPathKey)) _clipDirectoryPath = data[_clipDirectoryPathKey];
        
        Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
        {
            _videoUrl = o.Url;
            _startTime = o.StartTime;
            _endTime = o.EndTime;
            
            if (!string.IsNullOrWhiteSpace(o.TempDirectoryPath))
            {
                _videoDirectoryPath = o.TempDirectoryPath + "/Temp";
                SaveOptionsOnDB(_videoDirectoryPathKey , _videoDirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(o.OutputDirectoryPath))
            {
                _clipDirectoryPath = o.OutputDirectoryPath + "/Clips";
                SaveOptionsOnDB(_clipDirectoryPathKey, _clipDirectoryPath);
            }
            if (o.DeleteOnEnd) _deleteOnEnd = o.DeleteOnEnd;
        });

        if (_videoUrl == "") return;
        
        if (_startTime == "") return;
        
        if (_endTime == "") return;

        CreateFolders();
        
        var filePath = await DownloadVideoFromYoutube();
        
        ClipVideo(filePath);
        
        if (_deleteOnEnd) DeleteDirectory(_videoDirectoryPath);
    }

    private static IDictionary<string, string> InitDb()
    {
        using var connection = new SqliteConnection("Data Source=clipmaker.db");
        
        connection.Open();

        var command = connection.CreateCommand();
        
        command.CommandText =
        @"
            CREATE TABLE IF NOT EXISTS configs (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                data TEXT NOT NULL
            );
        ";

        command.ExecuteNonQuery();

        command.CommandText = " SELECT * from configs ";
        
        var reader = command.ExecuteReader();
        
        Dictionary<string, string> data = new();
        
        if (!reader.HasRows) return data;
        
        while(reader.Read())
        {
            var name = reader.GetString(1);
            var value = reader.GetString(2);
            data.Add(name, value);
        }
        
        return data;
    }
    
    private static void SaveOptionsOnDB(string name, string value)
    {   
        using var connection = new SqliteConnection("Data Source=clipmaker.db");
        
        connection.Open();

        var command = connection.CreateCommand();
        
        var updateOrInsertQuery = @"
            INSERT OR REPLACE INTO configs (name, data)
            VALUES (@name, @data);";

        command.CommandText = updateOrInsertQuery;
        
        // Add parameters
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@data", value);

        command.ExecuteNonQuery();
    }

    private static async ValueTask<string> DownloadVideoFromYoutube()
    {
        var youtube = new YoutubeClient();
        
        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(_videoUrl);

        var streamInfo = streamManifest
            .GetMuxedStreams()
            .Where(s => s.Container == Container.Mp4)
            .GetWithHighestVideoQuality();
        
        await youtube.Videos.Streams.GetAsync(streamInfo);

        var filePath = $"{_videoDirectoryPath}/video.{streamInfo.Container}";
        
        await youtube.Videos.Streams.DownloadAsync(streamInfo, filePath);

        return filePath;
    }
    
    private static void ClipVideo(string filePath)
    {
        const string ffmpegPath = "/opt/homebrew/Cellar/ffmpeg/6.1.1_6/bin/ffmpeg";
        var outputFilePath = $"{_clipDirectoryPath}/clip_{GenerateSlug()}.mp4"; 
        
        ProcessStartInfo startInfo = new()
        {
            FileName = ffmpegPath,
            Arguments = $"-i \"{filePath}\" -ss {_startTime} -to {_endTime} -c:v copy -c:a copy \"{outputFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit();

        // Check for errors
        if (process?.ExitCode != 0)
        {
            var errorMessage = process?.StandardError.ReadToEnd();
            Console.WriteLine($"Error: {errorMessage}");
        }
        else Console.WriteLine($"Trimming successful. {outputFilePath}");
    }

    private static string GenerateSlug()
    {
        var formattedDateTime = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        var slug = formattedDateTime.Replace(" ", "-").Replace(":", "-");

        return slug;
    }

    private static void CreateFolders()
    {
        if (!Directory.Exists(_videoDirectoryPath)) Directory.CreateDirectory(_videoDirectoryPath);
        if (!Directory.Exists(_clipDirectoryPath)) Directory.CreateDirectory(_clipDirectoryPath);
    }
    
    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            Console.WriteLine("Folder deleted successfully.");
        }
        else Console.WriteLine("Folder does not exist.");
    }
}

