using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Arguments;

namespace VideoToASCII
{
    internal class Program
    {
        static List<Bitmap> bitmaps = new List<Bitmap>();
        static List<string> asciiForConsoleList = new List<string>();

        static string videoFile = @"C:\Users\denis\Desktop\VideoToASCII\Video\basevideo.mp4";
        static string audioOutput = @"C:\Users\denis\Desktop\VideoToASCII\Audio\audio.mp3";
        static string videoOutputLongVideo = @"C:\Users\denis\Desktop\VideoToASCII\Video\video_l_v.mp4";
        static string videoOutputSameDuration = @"C:\Users\denis\Desktop\VideoToASCII\Video\video_s_d.mp4";
        static string videoOutput = @"C:\Users\denis\Desktop\VideoToASCII\Video\video.mp4";
        static double framerate;

        static void Main(string[] args)
        {
            //bitmaps = GetFilesBitmap();
            //asciiForConsoleList = GetStringsAscii();
            //asciiBitmapList = GetASCIIBitmap();

            VideoCapture capture = new VideoCapture(videoFile);
            framerate = capture.Fps;

            GetFramesFromVideo(capture);
            ConvertToAscii();
            CreateLongVideo();
            CreateSameDurationVideo();
            CreateVideoWithSound();

            capture.Dispose();
        }

        private static void CreateLongVideo()
        {
            Console.WriteLine("Start creating Long Video... Please Wait");
            FFMpegArguments
                .FromFileInput(Path.Combine(@"C:\Users\denis\Desktop\VideoToASCII\AsciiFrames\", "frame%09d.png"), verifyExists: false)
                .OutputToFile(videoOutputLongVideo, overwrite: true, delegate (FFMpegArgumentOptions options)
                {
                    options
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithFramerate(framerate);
                }).ProcessSynchronously();
            Console.WriteLine("Done");
        }

        private static void CreateSameDurationVideo()
        {
            IMediaAnalysis mainVideo = FFProbe.Analyse(videoFile);
            IMediaAnalysis longVideo = FFProbe.Analyse(videoOutputLongVideo);

            string percentageShorter = (1 / longVideo.Duration.TotalSeconds * mainVideo.Duration.TotalSeconds).ToString("0.000").Replace(",", ".");

            Console.WriteLine("Start creating Same Duration Video... Please Wait");
            FFMpegArguments
                .FromFileInput(videoOutputLongVideo, verifyExists: false)
                .OutputToFile(videoOutputSameDuration, overwrite: true, delegate (FFMpegArgumentOptions options)
                {
                    options
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithFramerate(framerate)
                        .WithCustomArgument($"-filter_complex \"setpts={percentageShorter}*PTS\"");
                }).ProcessSynchronously();
            Console.WriteLine("Done");
        }

        private static void CreateVideoWithSound()
        {
            Console.WriteLine("Start Extract and add Audio");
            FFMpeg.ExtractAudio(videoFile, audioOutput);
            FFMpeg.ReplaceAudio(videoOutputSameDuration, audioOutput, videoOutput);
            Console.WriteLine("Done");
        }

        private static void ConvertToAscii()
        {
            Console.WriteLine("Start Convert to Ascii");
            int frames = 1;

            Font font = new Font(new FontFamily("Inconsolata"), 10f, FontStyle.Regular);
            SolidBrush backgroundColor = new SolidBrush(Color.FromArgb(255, 0, 0, 0));

            foreach (var image in bitmaps)
            {
                byte[] frameInByte = new byte[image.Height * image.Width * 2 + image.Height];

                Console.WriteLine("Create ASCII Frame " + frames + "/" + bitmaps.Count);
                int counter2 = 0;
                for (int y = 0; y < image.Height; y++)
                {
                    int pixelWidth = 0;
                    for (int x = 0; x < image.Width * 2; x += 2)
                    {
                        int middleValue = (image.GetPixel(pixelWidth, y).R + image.GetPixel(pixelWidth, y).G + image.GetPixel(pixelWidth, y).B) / 3;
                        pixelWidth++;

                        if (middleValue < 33)
                            middleValue = 32;
                        if (middleValue >= 33 && middleValue < 50)
                            middleValue = 35;
                        if (middleValue >= 50 && middleValue < 100)
                            middleValue = 64;
                        if (middleValue >= 100 && middleValue < 150)
                            middleValue = 38;
                        if (middleValue >= 150 && middleValue < 200)
                            middleValue = 42;
                        if (middleValue >= 200 && middleValue <= 255)
                            middleValue = 43;

                        frameInByte[counter2] += Convert.ToByte(middleValue);
                        frameInByte[counter2 + 1] += Convert.ToByte(middleValue);
                        counter2 += 2;
                    }
                    frameInByte[counter2] += 10;
                    counter2++;
                }

                File.WriteAllBytes(@"C:\Users\denis\Desktop\VideoToASCII\Frames\texts\frame" + frames + ".txt", frameInByte);
                
                char[] charArray = new char[frameInByte.Length];
                frameInByte.CopyTo(charArray, 0);
                asciiForConsoleList.Add(new string(charArray));
                frames++;

            }
            Console.WriteLine("Finished Convert to Ascii");

            Console.WriteLine("Start Save ASCII Images");
            frames = 1;
            foreach (var charArray in asciiForConsoleList)
            {
                Console.WriteLine("Save ASCII Image: " + frames + "/" + asciiForConsoleList.Count);
                using (Bitmap asciiBitmap = new Bitmap(1920, 1080))
                {
                    using (Graphics graphics = Graphics.FromImage(asciiBitmap))
                    {
                        graphics.FillRectangle(backgroundColor, new Rectangle(0, 0, asciiBitmap.Width, asciiBitmap.Height));
                        graphics.DrawString(new string(charArray), font, Brushes.White, new RectangleF(0, 0, asciiBitmap.Width, asciiBitmap.Height));
                    }
                    string path = @"C:\Users\denis\Desktop\VideoToASCII\AsciiFrames\frame" + frames.ToString().PadLeft(9, '0') + ".png";
                    asciiBitmap.Save(path, ImageFormat.Png);
                }
                frames++;
            }
            Console.WriteLine("Done");
        }

        private static void GetFramesFromVideo(VideoCapture capture)
        {
            Mat mat = new Mat();
            int frame = 1;
            while (capture.IsOpened())
            {
                capture.Read(mat);
                if (mat.Empty())
                    break;
                bitmaps.Add(new Bitmap(Image.FromStream(mat.ToMemoryStream()), new System.Drawing.Size(1920 / 15, 1080 / 15)));

                Console.WriteLine("Getting Video Frames...");
                Console.WriteLine("Frame: " + frame + "/" + capture.FrameCount);
                frame++;
            }

            Console.WriteLine("Frames Saved.");

            Console.WriteLine("Save Frames to Disk");
            int counter = 1;
            foreach (var item in bitmaps)
            {
                Console.WriteLine("Save Picture Frame: " + counter + "/" + bitmaps.Count);
                item.Save(@"C:\Users\denis\Desktop\VideoToASCII\Frames\images\frame" + counter + ".png");
                counter++;
            }
            Console.WriteLine("Frames saved to Disk");
        }

        private static List<Bitmap> GetFilesBitmap()
        {
            string directory = @"C:\Users\denis\Desktop\VideoToASCII\Frames\images";
            string[] files = Directory.GetFiles(directory);
            List<Bitmap> list = new List<Bitmap>();
            foreach (var item in files)
            {
                list.Add(new Bitmap(item));
            }
            return list;
        }

        private static List<string> GetASCIIBitmap()
        {
            Console.WriteLine("Start Read Frames from png");

            string directory = @"C:\Users\denis\Desktop\VideoToASCII\AsciiFrames";
            Console.WriteLine("GetFiles");
            string[] files = Directory.GetFiles(directory);
            Console.WriteLine("SortFiles");
            files = SortASCIIImageFiles(files);
            Console.WriteLine("Add to List");
            List<string> list = new List<string>();
            int counter = 0;
            foreach (var item in files)
            {
                Console.WriteLine("Add to List Frame: " + ++counter + "/" + files.Length);
                list.Add(item);
            }
            Console.WriteLine("Done");
            return list;
        }
        private static List<string> GetStringsAscii()
        {
            string directory = @"C:\Users\denis\Desktop\VideoToASCII\Frames\texts";
            string[] files = Directory.GetFiles(directory);
            files = SortFiles(files);
            List<string> list = new List<string>();
            foreach (var item in files)
            {
                list.Add(File.ReadAllText(item));
            }
            return list;
        }

        private static string[] SortFiles(string[] files)
        {
            string[] sorted = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                
                for (int j = 0; j < files.Length; j++)
                {
                    string[] tmp = files[j].Split("\\");
                    if (tmp[tmp.Length - 1] == "frame" + (i + 1) + ".txt")
                        sorted[i] = files[j];
                }
                
            }
            return sorted;
        }

        private static string[] SortASCIIImageFiles(string[] files)
        {
            string[] sorted = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                for (int j = 0; j < files.Length; j++)
                {
                    string[] tmp = files[j].Split("\\");
                    if (tmp[tmp.Length - 1] == "frame" + (i + 1).ToString().PadLeft(9, '0') + ".png")
                        sorted[i] = files[j];
                }

            }
            return sorted;
        }

    }
}
