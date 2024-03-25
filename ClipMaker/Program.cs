using System.Diagnostics;
using CommandLine;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace ClipMaker;

static class Program
{
    public class Options
    {
        [Option('u', "url", Required = true, HelpText = "Set the url of the video to be trimmed")]
        public string Url { get; set; }

        [Option('s', "starttime", Required = true, HelpText = "Set the start time from the video to generate the clip - Expected format: hh-mm-ss")]
        public string StartTime { get; set; }

        [Option('e', "endtime", Required = true, HelpText = "Set the end time from the video to generate the clip - Expected format: hh-mm-ss")]
        public string EndTime { get; set; }
    }
    
    static async Task Main(string[] args)
    {
        var videoUrl = "";
        var startTime = "";
        var endTime = "";
        
        Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
        {
            videoUrl = o.Url;
            startTime = o.StartTime;
            endTime = o.EndTime;
        });
        
        var youtube = new YoutubeClient();
        
        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);

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

        ClipVideo(filePath, clipDirectoryPath, startTime, endTime);
        
        DeleteDirectory(videoDirectoryPath);
    }

    private static void ClipVideo(string filePath, string clipDirectoryPath, string startTime, string endTime)
    {
        const string ffmpegPath = "/opt/homebrew/Cellar/ffmpeg/6.1.1_6/bin/ffmpeg";
        var outputFilePath = $"{clipDirectoryPath}/clip_{GenerateSlug()}.mp4"; 
        
        ProcessStartInfo startInfo = new()
        {
            FileName = ffmpegPath,
            Arguments = $"-i \"{filePath}\" -ss {startTime} -to {endTime} -c:v copy -c:a copy \"{outputFilePath}\"",
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

