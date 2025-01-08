using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using FFMpegCore;
using FFMpegCore.Enums;
using System.Threading.Tasks;
using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Threading;

namespace VideoToASCII
{
    internal class Program
    {
        static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        static object locker = new object();
        static List<Bitmap> bitmaps = new List<Bitmap>();

        static string videoFile = @"C:\Users\denis\Desktop\VideoToASCII\Video\basevideo.mp4";
        static string audioOutput = @"C:\Users\denis\Desktop\VideoToASCII\Audio\audio.mp3";
        static string videoOutputLongVideo = @"C:\Users\denis\Desktop\VideoToASCII\Video\video_l_v.mp4";
        static string videoOutputSameDuration = @"C:\Users\denis\Desktop\VideoToASCII\Video\video_s_d.mp4";
        static string videoOutput = @"C:\Users\denis\Desktop\VideoToASCII\Video\video.mp4";
        static double framerate;

        static AsciiType asciiType = AsciiType.OnlyBigChar;

        static bool withColor = true;

        static EndResoultion endResoltion = EndResoultion.UHDOne;
        static Size endResolutionSize;
        static float fontSize;

        static void Main(string[] args)
        {
            switch (endResoltion)
            {
                case EndResoultion.FullHD:
                    endResolutionSize = new System.Drawing.Size(1920, 1080); // FullHD
                    fontSize = 10.7f;
                    break;
                case EndResoultion.UHDOne:
                    endResolutionSize = new System.Drawing.Size(4096, 2304); // UHD-1 | 4K
                    fontSize = 10.7f * 2;
                    break;
                case EndResoultion.UHDTwo:
                    endResolutionSize = new System.Drawing.Size(8192, 4608); // UHD-2 | 8K
                    fontSize = 10.7f * 4;
                    break;
            }
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = "./" });

            CleanUpDirectorys();

            //bitmaps = GetFilesBitmap();
            //asciiForConsoleList = GetStringsAscii();
            //asciiBitmapList = GetASCIIBitmap();
            Stopwatch totalWatch = Stopwatch.StartNew();
            Stopwatch watch = Stopwatch.StartNew();


            VideoCapture capture = new VideoCapture(videoFile);
            framerate = capture.GetCaptureProperty(CapProp.Fps);

            watch = Stopwatch.StartNew();
            GetFramesFromVideo(capture);
            watch.Stop();
            TimeSpan span = watch.Elapsed;
            Console.WriteLine("GetFramesFromVideo completed in " + span.Minutes + ":" + span.Seconds + " Minutes.");

            watch.Restart();
            ConvertToAscii();
            watch.Stop();
            span = watch.Elapsed;
            Console.WriteLine("ConvertToAscii completed in " + span.Minutes + ":" + span.Seconds + " Minutes.");

            watch.Restart();
            CreateLongVideo();
            watch.Stop();
            span = watch.Elapsed;
            Console.WriteLine("CreateLongVideo completed in " + span.Minutes + ":" + span.Seconds + " Minutes.");

            watch.Restart();
            CreateSameDurationVideo();
            watch.Stop();
            span = watch.Elapsed;
            Console.WriteLine("CreateSameDurationVideo completed in " + span.Minutes + ":" + span.Seconds + " Minutes.");

            watch.Restart();
            CreateVideoWithSound();
            watch.Stop();
            span = watch.Elapsed;
            Console.WriteLine("CreateVideoWithSound completed in " + span.Minutes + ":" + span.Seconds + " Minutes.");

            capture.Dispose();

            totalWatch.Stop();
            span = totalWatch.Elapsed;
            Console.WriteLine("Total completed in " + span.Minutes + ":" + span.Seconds + " Minutes.");

            Console.ReadKey();
        }

        private static void CleanUpDirectorys()
        {
            Console.WriteLine("Start Clean Dirs");
            string[] dirs = new string[3]
            {
                    @"C:\Users\denis\Desktop\VideoToASCII\AsciiFrames",
                    @"C:\Users\denis\Desktop\VideoToASCII\Frames\images",
                    @"C:\Users\denis\Desktop\VideoToASCII\Frames\texts"
            };

            Parallel.ForEach(dirs, dir =>
            {
                string[] files = Directory.GetFiles(dir);
                foreach (string file in files)
                {
                    File.Delete(file);
                }
            });
            Console.WriteLine("Done");
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

            string percentageShorter = (1 / longVideo.Duration.TotalSeconds * mainVideo.Duration.TotalSeconds).ToString("0.00000").Replace(",", ".");

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
        }

        private static void ConvertToAscii()
        {
            Console.WriteLine("Start Convert to Ascii");
            Dictionary<int, string> asciiForConsoleList = new Dictionary<int, string>();
            Dictionary<int, Color[]> colorList = new Dictionary<int, Color[]>();
            Font font = new Font(new FontFamily("Inconsolata"), fontSize, FontStyle.Regular);

            object locker = new object();
            Parallel.For(0, bitmaps.Count, round =>
            {
                Bitmap copyBitmap;
                lock (locker)
                {
                    copyBitmap = bitmaps[round];
                }

                int sizeArray = copyBitmap.Height * copyBitmap.Width * 2 + copyBitmap.Height;
                byte[] frameInByte = new byte[sizeArray];
                Color[] colorArray = new Color[sizeArray];

                PositionCursorZeroWrite("Create ASCII Frame " + (round + 1) + "/" + bitmaps.Count);
                int counter2 = 0;
                for (int y = 0; y < copyBitmap.Height; y++)
                {
                    int pixelWidth = 0;
                    for (int x = 0; x < copyBitmap.Width * 2; x += 2)
                    {
                        int middleValue = (copyBitmap.GetPixel(pixelWidth, y).R + copyBitmap.GetPixel(pixelWidth, y).G + copyBitmap.GetPixel(pixelWidth, y).B) / 3;

                        switch (asciiType)
                        {
                            case AsciiType.Less:
                                switch (middleValue)
                                {
                                    case < 33:
                                        middleValue = 32;
                                        break;
                                    case >= 33 and < 50:
                                        middleValue = 35;
                                        break;
                                    case >= 50 and < 100:
                                        middleValue = 64;
                                        break;
                                    case >= 100 and < 150:
                                        middleValue = 38;
                                        break;
                                    case >= 150 and < 200:
                                        middleValue = 42;
                                        break;
                                    case >= 200 and <= 255:
                                        middleValue = 43;
                                        break;
                                }
                                break;
                            case AsciiType.Full:
                                switch (middleValue)
                                {
                                    case < 33:
                                        middleValue = 32;
                                        break;
                                    case 127:
                                        middleValue = 126;
                                        break;
                                    case 160:
                                        middleValue = 161;
                                        break;
                                    case 173:
                                        middleValue = 174;
                                        break;
                                    case > 127 and < 160:
                                        middleValue = 161;
                                        break;
                                }
                                break;

                            case AsciiType.OnlyBigChar:
                                switch (middleValue)
                                {
                                    case < 33:
                                        middleValue = 32;
                                        break;
                                    case 34:
                                        middleValue = 35;
                                        break;
                                    case 39:
                                        middleValue = 38;
                                        break;
                                    case > 41 and < 48:
                                        middleValue = 48;
                                        break;
                                    case > 57 and < 63:
                                        middleValue = 63;
                                        break;
                                    case > 90 and < 130:
                                        middleValue = 82;
                                        break;
                                    case > 129 and < 160:
                                        middleValue = 77;
                                        break;
                                    case > 159 and < 192:
                                        middleValue = 82;
                                        break;
                                    case > 191 and < 200:
                                        middleValue = 200;
                                        break;
                                    case > 203 and < 209:
                                        middleValue = 209;
                                        break;
                                    case > 209 and < 217:
                                        middleValue = 217;
                                        break;
                                    case 221:
                                    case 222:
                                        middleValue = 223;
                                        break;
                                    case > 223 and < 235:
                                        middleValue = 223;
                                        break;
                                    case > 234 and < 245:
                                        middleValue = 53;
                                        break;
                                    case > 244:
                                        middleValue = 56;
                                        break;
                                }
                                break;
                        }

                        Color currentColor = copyBitmap.GetPixel(pixelWidth, y);
                        colorArray[counter2] = currentColor;
                        colorArray[counter2 + 1] = currentColor;
                        frameInByte[counter2] += Convert.ToByte(middleValue);
                        frameInByte[counter2 + 1] += Convert.ToByte(middleValue);

                        counter2 += 2;
                        pixelWidth++;
                    }
                    colorArray[counter2] = Color.FromArgb(255, 0, 0, 0);
                    frameInByte[counter2] += 10; // \n
                    counter2++;
                }

                char[] charArray = new char[frameInByte.Length];
                frameInByte.CopyTo(charArray, 0);

                lock (locker)
                {
                    File.WriteAllBytes(@"C:\Users\denis\Desktop\VideoToASCII\Frames\texts\frame" + (round + 1) + ".txt", frameInByte);
                    asciiForConsoleList.Add(round, new string(charArray));
                    colorList.Add(round, colorArray);
                }
            });
            Console.WriteLine("Finished Convert to Ascii");

            Console.WriteLine("Start Save ASCII Images");

            Parallel.For(0, asciiForConsoleList.Count, round =>
            {
                PositionCursorZeroWrite("Create and Save ASCII Image: " + (round + 1) + "/" + asciiForConsoleList.Count);

                using (Bitmap asciiBitmap = new Bitmap(endResolutionSize.Width, endResolutionSize.Height))
                {
                    using (Graphics graphics = Graphics.FromImage(asciiBitmap))
                    {
                        Dictionary<int, string> asciiForConsoleList2;
                        Dictionary<int, Color[]> colorList2;
                        SolidBrush backgroundColor;
                        Bitmap bitmapCopy;
                        lock (locker)
                        {
                            asciiForConsoleList2 = new Dictionary<int, string>(asciiForConsoleList);
                            colorList2 = new Dictionary<int, Color[]>(colorList);
                            backgroundColor = new SolidBrush(Color.FromArgb(255, 0, 0, 0));
                            bitmapCopy = new Bitmap(bitmaps[0]);
                        }

                        graphics.FillRectangle(backgroundColor, new Rectangle(0, 0, asciiBitmap.Width, asciiBitmap.Height));
                        if (!withColor)
                            graphics.DrawString(asciiForConsoleList2[round], font, Brushes.White, new RectangleF(20, 0, asciiBitmap.Width, asciiBitmap.Height));
                        else
                        {
                            bool isDecimal = false;
                            if (asciiBitmap.Width / (bitmapCopy.Width * 2f) % 1 > 0)
                                isDecimal = true;

                            int stepDistance = asciiBitmap.Width / (bitmapCopy.Width * 2);
                            int strCounter = 0;
                            for (int y = 0; y < bitmapCopy.Height; y++)
                            {
                                int charWidthDistance = 0;
                                bool changer = false;

                                for (int x = 0; x < bitmapCopy.Width * 2 + 1; x++)
                                {
                                    if (!(asciiForConsoleList2[round][strCounter] == '\n'))
                                        graphics.DrawString(
                                        asciiForConsoleList2[round][strCounter].ToString(),
                                        font,
                                        new SolidBrush(colorList2[round][strCounter]),
                                        new PointF(charWidthDistance, asciiBitmap.Height / bitmapCopy.Height * y)
                                        );
                                    if (isDecimal)
                                    {
                                        if (changer)
                                        {
                                            charWidthDistance += stepDistance + 1;
                                            changer = false;
                                        }
                                        else
                                        {
                                            charWidthDistance += stepDistance;
                                            changer = true;
                                        }
                                    }
                                    else
                                    {
                                        charWidthDistance += stepDistance;
                                    }
                                    strCounter++;
                                }
                            }
                        }
                    }
                    string path = @"C:\Users\denis\Desktop\VideoToASCII\AsciiFrames\frame" + (round + 1).ToString().PadLeft(9, '0') + ".png";
                    asciiBitmap.Save(path, ImageFormat.Png);
                }
            });
            bitmaps.Clear();
            Console.WriteLine("Done");
        }

        private static void GetFramesFromVideo(VideoCapture capture)
        {
            using (var mat = new Mat())
            {
                int frame = 1;
                Console.WriteLine("Getting Video Frames...");

                while (capture.Read(mat) && !mat.IsEmpty)
                {
                    using (var gpuMat = new CudaImage<Bgr, byte>(mat))
                    using (var gpuResizedMat = new CudaImage<Bgr, byte>())
                    {
                        CudaInvoke.Resize(gpuMat, gpuResizedMat, new Size(1920 / 15, 1080 / 15));
                        var resizedMat = gpuResizedMat.ToMat();
                        bitmaps.Add(MatToBitmap(resizedMat));
                    }
                    PositionCursorZeroWrite("Frame: " + frame + "/" + capture.GetCaptureProperty(CapProp.FrameCount));
                    frame++;
                }
            }
            Console.WriteLine("Frames Saved.");

            Console.WriteLine("Save Frames to Disk");
            Parallel.ForEach(bitmaps, (item, state, index) =>
            {
                PositionCursorZeroWrite("Save Picture Frame: " + (index + 1) + "/" + bitmaps.Count);
                item.Save($@"C:\Users\denis\Desktop\VideoToASCII\Frames\images\frame{index + 1}.png");
            });
            Console.WriteLine("Frames saved to Disk");
        }

        private static Bitmap MatToBitmap(Mat mat)
        {
            Bitmap bitmap = new Bitmap(mat.Width, mat.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            mat.CopyTo(new Mat(mat.Rows, mat.Cols, DepthType.Cv8U, 3, data.Scan0, data.Stride));
            bitmap.UnlockBits(data);
            return bitmap;
        }

        private static List<Bitmap> GetFilesBitmap()
        {
            string directory = @"C:\Users\denis\Desktop\VideoToASCII\Frames\images";
            string[] files = Directory.GetFiles(directory);
            files = SortFiles(files, FileType.PNG);
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
            files = SortFiles(files, FileType.TXT);
            List<string> list = new List<string>();
            foreach (var item in files)
            {
                list.Add(File.ReadAllText(item));
            }
            return list;
        }

        private static string[] SortFiles(string[] files, FileType type)
        {
            string fileType = string.Empty;
            switch (type)
            {
                case FileType.PNG:
                    fileType = ".png";
                    break;
                case FileType.TXT:
                    fileType = ".txt";
                    break;
            }

            string[] sorted = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {

                for (int j = 0; j < files.Length; j++)
                {
                    string[] tmp = files[j].Split("\\");
                    if (tmp[tmp.Length - 1] == "frame" + (i + 1) + fileType)
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

        private static void PositionCursorZeroWrite(string msg)
        {
            semaphore.Wait();
            Console.CursorLeft = 0;
            Console.Write(msg);
            semaphore.Release();
        }


        private enum AsciiType
        {
            Less,
            OnlyBigChar,
            Full
        }

        private enum EndResoultion
        {
            FullHD,
            UHDOne,
            UHDTwo
        }

        private enum FileType
        {
            PNG,
            TXT
        }

    }
}
