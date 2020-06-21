using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ChdConverter
{
    public class Program
    {
        private static void Main(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {
                Console.WriteLine("No directory specified. Exiting...");
                return;
            }

            var gdiMode = args[0] == "-g";

            if (gdiMode && (args.Length == 1 || string.IsNullOrEmpty(args[1])))
            {
                Console.WriteLine("No directory specified. Exiting...");
                return;
            }

            var directoryPath = gdiMode ? args[1] : args[0];

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine("Specified directory doesn't exist. Exiting...");
                return;
            }

            string[] files = Directory.GetFiles(directoryPath, "*.zip");

            if (files == null || files.Length == 0)
            {
                Console.WriteLine("No zip files found in specified directory. Exiting...");
                return;
            }

            Console.WriteLine($"Starting batch conversion for {files.Length} files");

            var stopwatch = Stopwatch.StartNew();
            var gameCounter = Directory.GetFiles(directoryPath, "*.chd").Length;
            var numberOfGames = gameCounter + files.Length;

            foreach (var file in files)
            {
                var gameName = Path.GetFileNameWithoutExtension(file);
                var extractPath = Path.Combine(directoryPath, gameName);
                
                Directory.CreateDirectory(extractPath);

                Console.WriteLine($"Extracting {gameName}...");

                try
                {
                    ZipFile.ExtractToDirectory(file, extractPath);
                }
                catch
                {
                    Console.WriteLine($"Failed to extract {gameName}. Skipping...");
                    continue;
                }

                var exitCode = 0;
                var outFile = Path.Combine(directoryPath, gameName + ".chd");

                var extractedCueFile = Directory.GetFiles(extractPath, "*.cue");
                
                if (extractedCueFile == null || extractedCueFile.Length == 0 || string.IsNullOrEmpty(extractedCueFile[0]) || !File.Exists(extractedCueFile[0]))
                {
                    Console.WriteLine($"No cue file found for {gameName}. Skipping...");
                }
                else if (extractedCueFile.Length > 1)
                {
                    Console.WriteLine($"More than one cue file found for {gameName}. Skipping...");
                }
                else 
                {
                    var cueFile = extractedCueFile[0];
                    var gdiFile = cueFile;

                    if (gdiMode)
                    {
                        Console.WriteLine($"Converting {gameName} to GDI...");
                        DoGdiConversion(extractPath, cueFile);
                        gdiFile = Path.Combine(extractPath, "disc.gdi");
                    }

                    Console.WriteLine($"Converting {gameName} to CHD...");
                    var process = Process.Start("chdman.exe", $"createcd -i \"{(gdiMode ? gdiFile : cueFile)}\" -o \"{outFile}\"");
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                Console.WriteLine($"Conversion finished for {gameName}. Cleaning up...");
                DeleteDirectory(extractPath);

                if (exitCode == 0 && File.Exists(outFile))
                {
                    File.Delete(file);
                }
                else
                {
                    Console.WriteLine($"Conversion failed for {gameName}!");
                }

                gameCounter++;
                Console.WriteLine($"Finished {gameCounter} of {numberOfGames} files: {(((float)gameCounter / (float)numberOfGames) * 100.0f):F2}%");
            }

            stopwatch.Stop();

            Console.WriteLine($"Batch conversion for {files.Length} files finished in {stopwatch.Elapsed:c}. Exiting...");

            Console.ReadKey(true);
        }

        private static void DeleteDirectory(string targetDirectory)
        {
            string[] files = Directory.GetFiles(targetDirectory);
            string[] dirs = Directory.GetDirectories(targetDirectory);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(targetDirectory, false);
        }

        private static void DoGdiConversion(string workingDir, string cueFilename)
        {
            DirectoryInfo workingDirectory = new DirectoryInfo(workingDir);
            CueSheet cueSheet = new CueSheet(cueFilename);
            int currentSector = 0;
            StringWriter gdiOutput = new StringWriter();

            gdiOutput.WriteLine(cueSheet.Tracks.Length.ToString());
            for (int i = 0; i < cueSheet.Tracks.Length; i++)
            {
                Track currentTrack = cueSheet.Tracks[i];
                string inputTrackFilePath = Path.Combine(workingDirectory.FullName, currentTrack.DataFile.Filename);
                bool canPerformFullCopy = currentTrack.Indices.Length == 1;
                string outputTrackFileName = string.Format(
                    "track{0}.{1}",
                    currentTrack.TrackNumber,
                    currentTrack.TrackDataType == DataType.AUDIO ? "raw" : "bin");
                string outputTrackFilePath = Path.Combine(workingDirectory.FullName, outputTrackFileName);
                int sectorAmount;
                if (canPerformFullCopy)
                {
                    File.Copy(inputTrackFilePath, outputTrackFilePath);
                    sectorAmount = (int)(new FileInfo(inputTrackFilePath).Length / 2352);
                }
                else
                {
                    int gapOffset = CountIndexFrames(currentTrack.Indices[1]);
                    sectorAmount = CopyFileWithGapOffset(inputTrackFilePath, outputTrackFilePath, gapOffset);
                    currentSector += gapOffset;
                }

                int gap = 0;

                gdiOutput.WriteLine("{0} {1} {2} 2352 {3} {4}",
                    currentTrack.TrackNumber,
                    currentSector,
                    currentTrack.TrackDataType == DataType.AUDIO ? "0" : "4",
                    outputTrackFileName,
                    gap);

                currentSector += sectorAmount;

                if (currentTrack.Comments.Contains("HIGH-DENSITY AREA"))
                    if (currentSector < 45000)
                        currentSector = 45000;
            }

            string gdiOutputPath = Path.Combine(workingDirectory.FullName, "disc.gdi");
            File.WriteAllText(gdiOutputPath, gdiOutput.ToString());
        }

        private static int CountIndexFrames(Index index)
        {
            int result = index.Frames;
            result += (index.Seconds * 75);
            result += ((index.Minutes * 60) * 75);
            return result;
        }

        private static int CopyFileWithGapOffset(string inputFile, string outputFile, int frames)
        {
            Stream infile = File.OpenRead(inputFile);
            Stream outfile = File.OpenWrite(outputFile);
            int blockSize = 2352;
            infile.Position = frames * blockSize;
            int result = (int)((infile.Length - infile.Position) / blockSize);
            byte[] buffer = new byte[blockSize];
            while (blockSize > 0)
            {
                blockSize = infile.Read(buffer, 0, blockSize);
                outfile.Write(buffer, 0, blockSize);
            }
            outfile.Flush();
            outfile.Close();
            infile.Close();
            return result;
        }
    }
}
