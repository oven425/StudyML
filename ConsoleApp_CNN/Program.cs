// See https://aka.ms/new-console-template for more information
using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

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
    if(!Directory.Exists(idx.ToString()))
    {
        Directory.CreateDirectory(idx.ToString());
    }
    byte[] pixels = br_img.ReadBytes(rows * cols);
    FastSaveBmp(pixels, rows, cols, $"{idx}/{labelCount[idx]}.bmp");

}


void FastSaveBmp(byte[] pixels, int width, int height, string fileName)
{
    Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
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