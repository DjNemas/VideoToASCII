using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;

namespace VideoToASCII
{
    internal class Program
    {
        static List<Bitmap> bitmaps = new List<Bitmap>();
        static List<string> asciiForConsoleList = new List<string>();
        static void Main(string[] args)
        {
            string videoFile = @"C:\Users\denis\Desktop\VideoToASCII\Video\video.mp4";
            string frameOutput = @"C:\Users\denis\Desktop\VideoToASCII\Frames\frame1.png";
            double framerate;




            //bitmaps = GetFilesBitmap();
            asciiForConsoleList = GetStringsAscii();

            VideoCapture capture = new VideoCapture(videoFile);
            framerate = capture.Fps;

            //GetFramesFromVideo(capture);
            //ConvertToAscii();

            foreach (var item in asciiForConsoleList)
            {
                Console.Write(item);                
                Thread.Sleep(1000 / (int)framerate);
            }

            Console.ReadKey();

        }

        private static void ConvertToAscii()
        {
            Console.WriteLine("Start Convert to Ascii");
            int frames = 1;
            foreach (var image in bitmaps)
            {
                byte[] frameInByte = new byte[image.Height * image.Width * 2 + image.Height];

                Console.WriteLine("Frame " + frames + "/" + bitmaps.Count);
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
                        if (middleValue == 127)
                            middleValue = 126;
                        if (middleValue == 129)
                            middleValue = 130;
                        if (middleValue == 141)
                            middleValue = 142;
                        if (middleValue == 143 || middleValue == 144)
                            middleValue = 145;
                        if (middleValue == 141)
                            middleValue = 142;
                        if (middleValue == 157)
                            middleValue = 158;

                        frameInByte[counter2] += Convert.ToByte(middleValue);
                        frameInByte[counter2 + 1] += Convert.ToByte(middleValue);
                        counter2 += 2;
                    }
                    frameInByte[counter2] += 10;
                    counter2++;
                }

                File.WriteAllBytes(@"C:\Users\denis\Desktop\VideoToASCII\Frames\texts\frame" + frames + ".txt", frameInByte);
                frames++;
                char[] charArray = new char[frameInByte.Length];
                frameInByte.CopyTo(charArray, 0);
                asciiForConsoleList.Add(new string(charArray));

            }
            Console.WriteLine("Finished Convert to Ascii");
        }


        private static void GetFramesFromVideo(VideoCapture capture)
        {
            Mat mat = new Mat();
            int frame = 1;
            while (capture.IsOpened())
            {


                frame++;

                capture.Read(mat);
                if (mat.Empty())
                    break;
                bitmaps.Add(new Bitmap(Image.FromStream(mat.ToMemoryStream()), new System.Drawing.Size(1920 / 15, 1080 / 15)));

                Console.WriteLine("Getting Video Frames...");
                Console.WriteLine("Frame: " + frame + "/" + capture.FrameCount);

            }
            capture.Dispose();
            Console.WriteLine("Frames Saved.");

            Console.WriteLine("Save Frames to Disk");
            int counter = 1;
            foreach (var item in bitmaps)
            {
                Console.WriteLine("Frame: " + counter + "/" + bitmaps.Count);
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

    }
}
