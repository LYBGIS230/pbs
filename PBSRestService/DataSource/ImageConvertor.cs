using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using PBS.Service;

namespace PBS.DataSource
{
    public class ImageConvertor
    {
        public static Bitmap ConvertToSquare(Bitmap img, Point leftTop, Point rightTop, Point leftBottom, Point rightBottom)
        {
            int PIC_SIZE = 256;
            int WEIGHT = PIC_SIZE - 1;
            Bitmap destBitmap = new Bitmap(PIC_SIZE, PIC_SIZE);
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
                    destBitmap.SetPixel(i, j, FourSquareAvg(img, new LatLngPoint((top.Lng * (WEIGHT - j) + bottom.Lng * j) / WEIGHT, (top.Lat * (WEIGHT - j) + bottom.Lat * j) / WEIGHT)));
                }
            }
            return destBitmap;
        }
        public static Color FourSquareAvg(Bitmap img, LatLngPoint exactlyPoint)
        {     
            int minX = Convert.ToInt32(Math.Floor(exactlyPoint.Lng));
            int minY = Convert.ToInt32(Math.Floor(exactlyPoint.Lat));
            Color leftTop = img.GetPixel(minX, minY);
            Color rightTop = img.GetPixel(minX + 1, minY);
            Color leftBottom = img.GetPixel(minX, minY + 1);
            Color rightBottom = img.GetPixel(minX + 1, minY + 1);
            double deltX = exactlyPoint.Lng - minX;
            double deltY = exactlyPoint.Lat - minY;
            int colorR = Convert.ToInt32((leftTop.R * (1 - deltX) + deltX * rightTop.R) * (1 - deltY) + deltY * (leftBottom.R * (1 - deltX) + deltX * rightBottom.R));
            int colorB = Convert.ToInt32((leftTop.B * (1 - deltX) + deltX * rightTop.B) * (1 - deltY) + deltY * (leftBottom.B * (1 - deltX) + deltX * rightBottom.B));
            int colorG = Convert.ToInt32((leftTop.G * (1 - deltX) + deltX * rightTop.G) * (1 - deltY) + deltY * (leftBottom.G * (1 - deltX) + deltX * rightBottom.G));
            int colorA = Convert.ToInt32((leftTop.A * (1 - deltX) + deltX * rightTop.A) * (1 - deltY) + deltY * (leftBottom.A * (1 - deltX) + deltX * rightBottom.A));
            return Color.FromArgb(colorA, colorR, colorG, colorB);
        }
    }
}
