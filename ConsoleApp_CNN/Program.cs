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
    //FastSaveBmp(pixels, cols, rows, "org.bmp");
    //Filter(filter, pixels, out var dst);
    //FastSaveBmp(dst, cols, rows, "filter.bmp");
    Filter_(filter, pixels.Select(x=>(double)x/255.0).ToArray(), out var dst_filter);
    MaxPool(dst_filter, out var dst_pool);

}

void MaxPool(double[] src, out double[]dst, int width=26, int height=26)
{
    dst = new double[width/2 * height/2];
    for(int y=0;y<height; y=y+2 )
    {
        for(int x = 0; x<width; x=x+2)
        {
            var s1 = src[y * width + x];
            var s2 = src[y * width + x + 1];
            var s3 = src[(y + 1) * width + x];
            var s4 = src[(y + 1) * width + x + 1];
            dst[(y/2) * (width/2) + x/2] = Math.Max(Math.Max(s1, s2), Math.Max(s3, s4));
        }
    }
}

void Filter_(double[,] filter, double[] src, out double[] dst, int width = 28, int height = 28)
{
    dst = new double[(width-2) * (height-2)];
    int index = 0;
    for (int y = 0; y < height - 2; y++)
    {
        for (int x = 0; x < width - 2; x++)
        {
            var sum = 0.0;
            for (int fy = 0; fy < 3; fy++)
            {
                for (int fx = 0; fx < 3; fx++)
                {
                    sum = sum + src[(y + fy) * width + x + fx] * filter[fx, fy];
                }
            }
            dst[index] = Math.Max(0.0, sum);
            index++;
        }
    }
}

void Filter(double[,] filter, byte[] src, out byte[] dst, int width = 28, int height = 28)
{
    dst = new byte[src.Length];
    int index = 0;
    for (int y = 0; y < height-2; y++)
    {
        for (int x = 0; x < width-2; x++)
        {
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
