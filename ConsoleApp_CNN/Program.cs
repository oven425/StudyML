// See https://aka.ms/new-console-template for more information
using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

//MINIST dataset
//https://github.com/cvdfoundation/mnist
using BinaryReader br_idx = new BinaryReader(File.Open("train-labels.idx1-ubyte", FileMode.Open));
using BinaryReader br_img = new BinaryReader(File.Open("train-images.idx3-ubyte", FileMode.Open));
BinaryPrimitives.ReadInt32BigEndian(br_img.ReadBytes(4));
BinaryPrimitives.ReadInt32BigEndian(br_idx.ReadBytes(4));
var count1 = BinaryPrimitives.ReadInt32BigEndian(br_idx.ReadBytes(4));
int count = BinaryPrimitives.ReadInt32BigEndian(br_img.ReadBytes(4));
int rows = BinaryPrimitives.ReadInt32BigEndian(br_img.ReadBytes(4));
int cols = BinaryPrimitives.ReadInt32BigEndian(br_img.ReadBytes(4));
Dictionary<int, int> labelCount = new Dictionary<int, int>();
for (int i = 0; i < count; i++)
{
    var idx = br_idx.ReadByte();
    if(!labelCount.ContainsKey(idx))
    {
        labelCount[idx] = 0;
    }
    else
    {
        labelCount[idx] = ++labelCount[idx];
    }
    //if(!Directory.Exists(idx.ToString()))
    //{
    //    Directory.CreateDirectory(idx.ToString());
    //}
    //FastSaveBmp(pixels, cols, rows, Path.Combine(idx.ToString(), $"{i}.bmp"));
    byte[] pixels = br_img.ReadBytes(rows * cols);
    var filter = new double[,] 
    {
        { 1, 2, 1 }, 
        { 2, 0, 2 }, 
        { 1, 2, 1 } 
    };
    FastSaveBmp(pixels, cols, rows, "org.bmp");
    Filter(filter, pixels, out var dst);
    FastSaveBmp(dst, cols, rows, "filter.bmp");

    //DrawConsoleColor(img);

}

void Filter(double[,] filter, byte[] src, out byte[] dst, int width = 28, int height = 28)
{
    dst = new byte[src.Length];
    int index = 0;
    for (int y = 0; y < height-2; y++)
    {
        for (int x = 0; x < width-2; x++)
        {
            //var a00 = src[y*width+x]* filter[0,0];
            //var a01 = src[y * width + x+1] * filter[1, 0];
            //var a02 = src[y * width + x + 2] * filter[2, 0];
            //var a10 = src[(y+1) * width + x] * filter[0, 1];
            //var a11 = src[(y + 1) * width + x + 1] * filter[1, 1];
            //var a12 = src[(y + 1) * width + x + 2] * filter[2, 1];
            //var a20 = src[(y + 2) * width + x] * filter[0, 2];
            //var a21 = src[(y + 2) * width + x+1] * filter[1, 2];
            //var a22 = src[(y + 2) * width + x+2] * filter[2, 2];

            //var sum = a00 + a01 + a02 + a10 + a11 + a12 + a20 + a21 + a22;
            //sum/= 9;
            //dst[index] = (byte)Math.Clamp(sum, 0,255);
            //index++;

            var sum = 0.0;
            for(int fy = 0; fy < 3; fy++)
            {
                for(int fx = 0; fx < 3; fx++)
                {
                    sum = sum+ src[(y + fy) * width + x + fx] * filter[fx, fy];
                    
                }
            }
            sum = sum / filter.Length;
            dst[index] += (byte)Math.Clamp(sum, 0, 255);
            index++;
        }
    }
}

void DrawConsoleColor(double[,] image)
{
    for (int y = 0; y < 28; y++)
    {
        for (int x = 0; x < 28; x++)
        {
            int gray = (int)(image[x, y] * 255);
            // 使用 ANSI 色彩代碼設定背景色 \x1b[48;2;R;G;Bm
            Console.Write($"\x1b[48;2;{gray};{gray};{gray}m  \x1b[0m");
        }
        Console.WriteLine();
    }
}

double[,] NormalizeImage(byte[] pixels, int width=28, int height=28)
{
    double[,] img = new double[width, height];
    for (int i = 0; i < pixels.Length; i++)
    {
        img[i / width, i % width] = pixels[i] / 255.0;
    }
    return img;
}





void FastSaveBmp(byte[] pixels, int width, int height, string fileName)
{
    using Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
    
    byte[] rgbValues = new byte[data.Stride * height];
    for (int i = 0; i < pixels.Length; i++)
    {
        // 每個像素填三次 (B, G, R)
        rgbValues[i * 3] = pixels[i];     // Blue
        rgbValues[i * 3 + 1] = pixels[i]; // Green
        rgbValues[i * 3 + 2] = pixels[i]; // Red
    }

    Marshal.Copy(rgbValues, 0, data.Scan0, rgbValues.Length);
    bmp.UnlockBits(data);
    bmp.Save(fileName, ImageFormat.Bmp);
}

//void FastSaveBmp(byte[,] pixels, int width, int height, string fileName)
//{
//    Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
//    BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

//    byte[] rgbValues = new byte[data.Stride * height];
//    for (int i = 0; i < pixels.Length; i++)
//    {
//        // 每個像素填三次 (B, G, R)
//        //rgbValues[i * 3] = pixels[i];     // Blue
//        //rgbValues[i * 3 + 1] = pixels[i]; // Green
//        //rgbValues[i * 3 + 2] = pixels[i]; // Red
//    }

//    Marshal.Copy(rgbValues, 0, data.Scan0, rgbValues.Length);
//    bmp.UnlockBits(data);
//    bmp.Save(fileName, ImageFormat.Bmp);
//}