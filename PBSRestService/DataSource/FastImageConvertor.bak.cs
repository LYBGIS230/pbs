using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using PBS.Service;

namespace PBS.DataSource
{
    public class FastImageConvertor
    {
        private byte[] srcRGBValues;
        private byte[] destRGBValues;
        private IntPtr srcPtr;
        private IntPtr destPtr;
        private Bitmap destBitmap;
        private int srcWidth;
        private int PIC_SIZE = 256;
        private void loadBitMap(Bitmap img)
        {
            srcWidth = img.Width;
            Rectangle rect = new Rectangle(0, 0, img.Width, img.Height);
            System.Drawing.Imaging.BitmapData bmpData = img.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, img.PixelFormat);
            srcPtr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * img.Height;
            srcRGBValues = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(srcPtr, srcRGBValues, 0, bytes);
            destRGBValues = new byte[PIC_SIZE * PIC_SIZE * 4];
        }
        private Bitmap packetBitMap()
        {
            Rectangle rect = new Rectangle(0, 0, 256, 256);
            System.Drawing.Imaging.BitmapData bmpData = destBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, destBitmap.PixelFormat);
            destPtr = bmpData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(destRGBValues, 0, destPtr, 256 * 256 * 4);
            destBitmap.UnlockBits(bmpData);
            return destBitmap;
        }
        private int GetPixel(int x, int y)
        {
            int result;
            //byte red, green, blue, alpha;
            unsafe
            {
                fixed (byte* srcP = &srcRGBValues[(y * srcWidth + x) * 4])
                {
                    int* j = (int*)srcP;
                    result = j[0];
                }
            }
            return result;
        }
        private void SetPixel(int x, int y, int c)
        {
            unsafe
            {
                fixed (byte* destP = &destRGBValues[(y * PIC_SIZE + x) * 4])
                {
                    int* j = (int*)destP;
                    j[0] = c;
                }
            }
        }
        public Bitmap ConvertToSquare(Bitmap img, Point leftTop, Point rightTop, Point leftBottom, Point rightBottom)
        {
            
            int WEIGHT = PIC_SIZE - 1;
            loadBitMap(img);
            destBitmap = new Bitmap(PIC_SIZE, PIC_SIZE);
            double topSlopeX = ((double)(rightTop.X - leftTop.X)) / WEIGHT;
            double bottomSlopeX = ((double)(rightBottom.X - leftBottom.X)) / WEIGHT;
            double topSlopeY = ((double)(leftTop.Y - rightTop.Y)) / WEIGHT;
            double bottomSlopeY = ((double)(leftBottom.Y - rightBottom.Y)) / WEIGHT;
            LatLngPoint top, bottom;

            for (int i = 0; i < PIC_SIZE; i++)
            {
                top = new LatLngPoint(i * topSlopeX + leftTop.X, leftTop.Y - i * topSlopeY);
                bottom = new LatLngPoint(i * bottomSlopeX + leftBottom.X, leftBottom.Y - i * bottomSlopeY);

                for (int j = 0; j < PIC_SIZE; j++)
                {
                    SetPixel(i, j, FourSquareAvg(img, new LatLngPoint((top.Lng * (WEIGHT - j) + bottom.Lng * j) / WEIGHT, (top.Lat * (WEIGHT - j) + bottom.Lat * j) / WEIGHT)));
                }
            }
            destBitmap = packetBitMap();
            return destBitmap;
        }
        public int FourSquareAvg(Bitmap img, LatLngPoint exactlyPoint)
        {
            int minX = Convert.ToInt32(Math.Floor(exactlyPoint.Lng));
            int minY = Convert.ToInt32(Math.Floor(exactlyPoint.Lat));
            int leftTopBytes = GetPixel(minX, minY);
            int rightTopBytes = GetPixel(minX + 1, minY);
            int leftBottomBytes = GetPixel(minX, minY + 1);
            int rightBottomBytes = GetPixel(minX + 1, minY + 1);
            double deltX = exactlyPoint.Lng - minX;
            double deltY = exactlyPoint.Lat - minY;
            int result;
            unsafe
            {
                byte* leftTop = (byte*)&leftTopBytes, rightTop = (byte*)&rightTopBytes, leftBottom = (byte*)&leftBottomBytes, rightBottom = (byte*)&rightBottomBytes;
                byte colorR = (byte)((leftTop[2] * (1 - deltX) + deltX * rightTop[2]) * (1 - deltY) + deltY * (leftBottom[2] * (1 - deltX) + deltX * rightBottom[2]));
                byte colorB = (byte)((leftTop[0] * (1 - deltX) + deltX * rightTop[0]) * (1 - deltY) + deltY * (leftBottom[0] * (1 - deltX) + deltX * rightBottom[0]));
                byte colorG = (byte)((leftTop[1] * (1 - deltX) + deltX * rightTop[1]) * (1 - deltY) + deltY * (leftBottom[1] * (1 - deltX) + deltX * rightBottom[1]));
                //byte colorA = (byte)((leftTop[3] * (1 - deltX) + deltX * rightTop[3]) * (1 - deltY) + deltY * (leftBottom[3] * (1 - deltX) + deltX * rightBottom[3]));
                result = ((0 << 24) | (colorR << 16) | (colorG << 8) | colorB);
            }
            return result;
        }
        /***
        byte
        3   2   1   0 
        FF  F5  F3  F0
        255 245 243 240
        A   R   G   B
         * ****/
    }
}
