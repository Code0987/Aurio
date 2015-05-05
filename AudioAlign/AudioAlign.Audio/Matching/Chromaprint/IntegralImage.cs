﻿using AudioAlign.Audio.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AudioAlign.Audio.Matching.Chromaprint {
    /// <summary>
    /// Maintains a sliding window image of the specified size and its associated integral image.
    /// - Viola, Paul, and Michael Jones. "Rapid object detection using a boosted cascade of 
    ///   simple features." Computer Vision and Pattern Recognition, 2001. CVPR 2001. Proceedings 
    ///   of the 2001 IEEE Computer Society Conference on. Vol. 1. IEEE, 2001.
    /// - http://en.wikipedia.org/wiki/Summed_area_table
    /// </summary>
    class IntegralImage {

        private int width;
        private int height;

        private RingBuffer<double[]> image;
        private RingBuffer<double[]> integralImage;

        public IntegralImage(int width, int height) {
            this.width = width;
            this.height = height;

            image = new RingBuffer<double[]>(width);
            integralImage = new RingBuffer<double[]>(width);
        }

        public void AddColumn(double[] newColumn) {
            // Add new column to image
            double[] column = image.Count < image.Length ? new double[height] : image[0];
            image.Add(column);

            // Set values of new column
            Array.Copy(newColumn, column, column.Length);

            // Add new column to integral image
            integralImage.Add(integralImage.Count < integralImage.Length ? new double[height] : integralImage[0]);

            // Calculate integral image
            var col = image[0];
            var icol = integralImage[0];
            icol[0] = col[0]; // set (0, 0)
            for (int y = 1; y < height; y++) { // iterate (0, 1...h)
                icol[y] = col[y] + icol[y - 1]; // set (0, y)
            }
            for (int x = 1; x < width; x++) { // iterate (1...w, y)
                col = image[x];
                var prevIcol = icol;
                icol = integralImage[x];
                icol[0] = col[0] + prevIcol[0]; // set (x, 0)
                for (int y = 0; y < height; y++) { // iterate (x, 1...h)
                    icol[y] = col[y] + prevIcol[y] - prevIcol[y - 1] + icol[y - 1]; // set (x, y)
                }
            }
        }

        public double this[int x, int y] {
            get { return integralImage[x][y]; }
        }

        public int Columns {
            get { return image.Count; }
        }

        public double CalculateArea(int x1, int y1, int x2, int y2) {
            double area = this[x2, y2];

            if (x1 > 0) {
                area -= this[x1 - 1, y2];
                if (y1 > 0) {
                    // re-add this area because it will be subtracted a second time below
                    area += this[x1 - 1, y1 - 1];
                }
            }
            if (y1 > 0) {
                area -= this[x2, y1 - 1];
            }

            return area;
        }
    }
}
