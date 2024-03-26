using System.Diagnostics;
using CommandLine;
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
    }

    private static string VideoUrl { get; set; } = "";
    private static string StartTime { get; set; } = "";
    private static string EndTime { get; set; } = "";
    
    private const string VideoDirectoryPath = "../../temp/";
    private const string ClipDirectoryPath = "../../Clips/";
    
    static async Task Main(string[] args)
    {   
        Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
        {
            VideoUrl = o.Url;
            StartTime = o.StartTime;
            EndTime = o.EndTime;
        });

        if (VideoUrl == "") return;
        
        if (StartTime == "") return;
        
        if (EndTime == "") return;

        var filePath = await DownloadVideoFromYoutube();
        
        ClipVideo(filePath);
        
        DeleteDirectory(VideoDirectoryPath);
    }

    private static async ValueTask<string> DownloadVideoFromYoutube()
    {
        var youtube = new YoutubeClient();
        
        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(VideoUrl);

        var streamInfo = streamManifest
            .GetMuxedStreams()
            .Where(s => s.Container == Container.Mp4)
            .GetWithHighestVideoQuality();
        
        await youtube.Videos.Streams.GetAsync(streamInfo);

        const string videoDirectoryPath = "../../temp/";
        const string clipDirectoryPath = "../../Clips/";

        CreateFolder(videoDirectoryPath, clipDirectoryPath);

        var filePath = $"{videoDirectoryPath}video.{streamInfo.Container}";
        
        await youtube.Videos.Streams.DownloadAsync(streamInfo, filePath);

        return filePath;
    }
    
    private static void ClipVideo(string filePath)
    {
        const string ffmpegPath = "/opt/homebrew/Cellar/ffmpeg/6.1.1_6/bin/ffmpeg";
        var outputFilePath = $"{ClipDirectoryPath}/clip_{GenerateSlug()}.mp4"; 
        
        ProcessStartInfo startInfo = new()
        {
            FileName = ffmpegPath,
            Arguments = $"-i \"{filePath}\" -ss {StartTime} -to {EndTime} -c:v copy -c:a copy \"{outputFilePath}\"",
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
        // Format the DateTime object into a string
        var formattedDateTime = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        // Replace any special characters with hyphens
        var slug = formattedDateTime.Replace(" ", "-").Replace(":", "-");

        return slug;
    }

    private static void CreateFolder(string videoDirectoryPath, string clipDirectoryPath)
    {
        if (!Directory.Exists(videoDirectoryPath)) Directory.CreateDirectory(videoDirectoryPath);
        if (!Directory.Exists(clipDirectoryPath)) Directory.CreateDirectory(clipDirectoryPath);
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

