using System;
using System.Drawing;
using System.IO;

namespace ImageTo3d
{
    class Generator
    {
        private float[,] heights;
        private TextWriter tw;
        private BinaryWriter bw;
        private uint nTriangles;
        private readonly Options options;
        private float width;
        private float height;

        public Generator(Options options)
        {
            this.options = options;
        }

        public void ProcessFile(string filename, string outputFilename)
        {
            try
            {
                Image image = Image.FromFile(filename);

                Console.WriteLine("Size {0} {1} {2}", image.Width, image.Height, image.GetType().ToString());

                float desiredWidthMM = options.DesiredWidth;
                float stepSize = options.StepSize;
                int desiredPixelWidth = (int)(desiredWidthMM / stepSize);
                int desiredPixelHeight = image.Height * desiredPixelWidth / image.Width;
                float minThick = options.MinThickness;
                float maxThick = options.MaxThickness;

                Bitmap bm = new Bitmap(desiredPixelWidth, desiredPixelHeight);
                using (Graphics g = Graphics.FromImage(bm))
                {
                    g.DrawImage(image, 0, 0, desiredPixelWidth, desiredPixelHeight);
                }

                var gb = new GaussianBlur(bm);
                gb.Process(10);

                var grayScale = new float[bm.Width, bm.Height];
                float maxGray = 0.0f;
                float minGray = 10.0f;
                float avgGray = 0.0f;

                for (int i = 0; i < bm.Width; i++)
                {
                    for (int j = 0; j < bm.Height; j++)
                    {
                        Color oc = bm.GetPixel(i, j);
                        float grayScaleVal = (float)((oc.R * 0.3) + (oc.G * 0.59) + (oc.B * 0.11)) / 256.0f;
                        grayScale[options.MirrorX ? bm.Width - 1 - i : i, j] = grayScaleVal;
                        if (grayScaleVal < minGray)
                            minGray = grayScaleVal;
                        if (grayScaleVal > maxGray)
                            maxGray = grayScaleVal;
                        avgGray += grayScaleVal;
                    }
                }

                avgGray /= bm.Width * bm.Height;

                Console.WriteLine("Min gray {0}, Max gray {1}, Average {2}", minGray, maxGray, avgGray);

                heights = new float[bm.Width, bm.Height];
                for (int i = 0; i < bm.Width; i++)
                {
                    for (int j = 0; j < bm.Height; j++)
                    {
                        float g = (grayScale[i, j] - minGray) / (maxGray - minGray);

                        if (options.Negative)
                            g = 1.0f - g;

                        float height = g * (maxThick - minThick);
                        //heights[bm.Width - 1 - i, x] = height < minThick ? minThick : height;
                        heights[bm.Width - 1 - i, j] = maxThick - height;
                    }
                }

                if (options.Binary)
                {
                    bw = new BinaryWriter(new FileStream(outputFilename ?? Path.GetFileNameWithoutExtension(filename) + ".stl", FileMode.Create, FileAccess.ReadWrite));
                }
                else
                {
                    tw = new StreamWriter(outputFilename ?? Path.GetFileNameWithoutExtension(filename) + ".stl");
                }

                nTriangles = 0;

                width = (bm.Width - 1) * stepSize;
                height = (bm.Height - 1) * stepSize;

                OutputHeader();

                OutputFront(stepSize, bm);
                OutputFanBack(stepSize, bm);

                if (options.AddBorder)
                {
                    OutputTopBorder(stepSize, bm);
                    OutputBottomBorder(stepSize, bm);
                    OutputLeftBorder(stepSize, bm);
                    OutputRightBorder(stepSize, bm);
                }
                else
                {
                    OutputTop(stepSize, bm);
                    OutputBottom(stepSize, bm);
                    OutputLeft(stepSize, bm);
                    OutputRight(stepSize, bm);
                }

                OutputTrailer();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        // The right edge, x == w
        private void OutputRight(float stepSize, Bitmap bm)
        {
            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(width, j * stepSize, 0);
                Vector3 p2 = new Vector3(width, j * stepSize, heights[bm.Width - 1, j]);
                Vector3 p3 = new Vector3(width, (j + 1) * stepSize, heights[bm.Width - 1, j + 1]);
                Vector3 p4 = new Vector3(width, (j + 1) * stepSize, 0);
                OutputQuad(p1, p2, p3, p4);
            }
        }

        // The left edge, x == 0
        private void OutputLeft(float stepSize, Bitmap bm)
        {
            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(0, j * stepSize, 0);
                Vector3 p2 = new Vector3(0, j * stepSize, heights[0, j]);
                Vector3 p3 = new Vector3(0, (j + 1) * stepSize, heights[0, j + 1]);
                Vector3 p4 = new Vector3(0, (j + 1) * stepSize, 0);
                OutputQuad(p1, p2, p3, p4);
            }
        }

        // The bottom edge, y == h
        private void OutputBottom(float stepSize, Bitmap bm)
        {
            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, height, 0);
                Vector3 p2 = new Vector3(i * stepSize, height, heights[i, bm.Height - 1]);
                Vector3 p3 = new Vector3((i + 1) * stepSize, height, heights[i + 1, bm.Height - 1]);
                Vector3 p4 = new Vector3((i + 1) * stepSize, height, 0);

                OutputQuad(p1, p2, p3, p4);
            }
        }

        // The top edge, y == 0
        private void OutputTop(float stepSize, Bitmap bm)
        {
            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, 0, 0);
                Vector3 p2 = new Vector3((i + 1) * stepSize, 0, 0);
                Vector3 p3 = new Vector3((i + 1) * stepSize, 0, heights[i + 1, 0]);
                Vector3 p4 = new Vector3(i * stepSize, 0, heights[i, 0]);

                OutputQuad(p1, p2, p3, p4);
            }
        }

        private void OutputLeftBorder(float stepSize, Bitmap bm)
        {
            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(0, j * stepSize, heights[0, j]);
                Vector3 p2 = new Vector3(0, (j + 1) * stepSize, heights[0, j + 1]);
                Vector3 p3 = new Vector3(0, (j + 1) * stepSize, options.BorderThickness);
                Vector3 p4 = new Vector3(0, j * stepSize, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(-options.BorderWidth, j * stepSize, options.BorderThickness);
                Vector3 p2 = new Vector3(0, j * stepSize, options.BorderThickness);
                Vector3 p3 = new Vector3(0, (j + 1) * stepSize, options.BorderThickness);
                Vector3 p4 = new Vector3(-options.BorderWidth, (j + 1) * stepSize, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(-options.BorderWidth, j * stepSize, options.BorderThickness);
                Vector3 p2 = new Vector3(-options.BorderWidth, (j + 1) * stepSize, options.BorderThickness);
                Vector3 p3 = new Vector3(-options.BorderWidth, (j + 1) * stepSize, 0);
                Vector3 p4 = new Vector3(-options.BorderWidth, j * stepSize, 0);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(-options.BorderWidth, j * stepSize, 0);
                Vector3 p2 = new Vector3(-options.BorderWidth, (j + 1) * stepSize, 0);
                Vector3 p3 = new Vector3(0, (j + 1) * stepSize, 0);
                Vector3 p4 = new Vector3(0, j * stepSize, 0);

                OutputQuad(p1, p2, p3, p4);
            }

            {
                Vector3 p1 = new Vector3(0, 0, 0);
                Vector3 p2 = new Vector3(-options.BorderWidth, 0, 0);
                Vector3 p3 = new Vector3(-options.BorderWidth, -options.BorderWidth, 0);
                Vector3 p4 = new Vector3(0, 0, options.BorderThickness);
                Vector3 p5 = new Vector3(-options.BorderWidth, 0, options.BorderThickness);
                Vector3 p6 = new Vector3(-options.BorderWidth, -options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p3, p2);
                OutputTriangle(p6, p4, p5);
                OutputTriangle(p2, p3, p6);
                OutputTriangle(p2, p6, p5);
            }

            {
                Vector3 p1 = new Vector3(0, height, 0);
                Vector3 p2 = new Vector3(-options.BorderWidth, height, 0);
                Vector3 p3 = new Vector3(-options.BorderWidth, height + options.BorderWidth, 0);
                Vector3 p4 = new Vector3(0, height, options.BorderThickness);
                Vector3 p5 = new Vector3(-options.BorderWidth, height, options.BorderThickness);
                Vector3 p6 = new Vector3(-options.BorderWidth, height + options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p2, p3);
                OutputTriangle(p6, p5, p4);
                OutputTriangle(p2, p6, p3);
                OutputTriangle(p2, p5, p6);
            }
        }

        private void OutputRightBorder(float stepSize, Bitmap bm)
        {
            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(width, j * stepSize, heights[bm.Width - 1, j]);
                Vector3 p2 = new Vector3(width, j * stepSize, options.BorderThickness);
                Vector3 p3 = new Vector3(width, (j + 1) * stepSize, options.BorderThickness);
                Vector3 p4 = new Vector3(width, (j + 1) * stepSize, heights[bm.Width - 1, j + 1]);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(width + options.BorderWidth, j * stepSize, options.BorderThickness);
                Vector3 p2 = new Vector3(width + options.BorderWidth, (j + 1) * stepSize, options.BorderThickness);
                Vector3 p3 = new Vector3(width, (j + 1) * stepSize, options.BorderThickness);
                Vector3 p4 = new Vector3(width, j * stepSize, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(width + options.BorderWidth, j * stepSize, options.BorderThickness);
                Vector3 p2 = new Vector3(width + options.BorderWidth, j * stepSize, 0);
                Vector3 p3 = new Vector3(width + options.BorderWidth, (j + 1) * stepSize, 0);
                Vector3 p4 = new Vector3(width + options.BorderWidth, (j + 1) * stepSize, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(width + options.BorderWidth, j * stepSize, 0);
                Vector3 p2 = new Vector3(width, j * stepSize, 0);
                Vector3 p3 = new Vector3(width, (j + 1) * stepSize, 0);
                Vector3 p4 = new Vector3(width + options.BorderWidth, (j + 1) * stepSize, 0);

                OutputQuad(p1, p2, p3, p4);
            }


            {
                Vector3 p1 = new Vector3(width, 0, 0);
                Vector3 p2 = new Vector3(width + options.BorderWidth, 0, 0);
                Vector3 p3 = new Vector3(width + options.BorderWidth, -options.BorderWidth, 0);
                Vector3 p4 = new Vector3(width, 0, options.BorderThickness);
                Vector3 p5 = new Vector3(width + options.BorderWidth, 0, options.BorderThickness);
                Vector3 p6 = new Vector3(width + options.BorderWidth, -options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p2, p3);
                OutputTriangle(p6, p5, p4);
                OutputTriangle(p2, p6, p3);
                OutputTriangle(p2, p5, p6);
            }

            {
                Vector3 p1 = new Vector3(width, height, 0);
                Vector3 p2 = new Vector3(width + options.BorderWidth, height, 0);
                Vector3 p3 = new Vector3(width + options.BorderWidth, height + options.BorderWidth, 0);
                Vector3 p4 = new Vector3(width, height, options.BorderThickness);
                Vector3 p5 = new Vector3(width + options.BorderWidth, height, options.BorderThickness);
                Vector3 p6 = new Vector3(width + options.BorderWidth, height + options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p3, p2);
                OutputTriangle(p6, p4, p5);
                OutputTriangle(p2, p3, p6);
                OutputTriangle(p2, p6, p5);
            }
        }

        private void OutputTopBorder(float stepSize, Bitmap bm)
        {
            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, 0, heights[i, 0]);
                Vector3 p2 = new Vector3(i * stepSize, 0, options.BorderThickness);
                Vector3 p3 = new Vector3((i + 1) * stepSize, 0, options.BorderThickness);
                Vector3 p4 = new Vector3((i + 1) * stepSize, 0, heights[i + 1, 0]);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, -options.BorderWidth, options.BorderThickness);
                Vector3 p2 = new Vector3((i + 1) * stepSize, -options.BorderWidth, options.BorderThickness);
                Vector3 p3 = new Vector3((i + 1) * stepSize, 0, options.BorderThickness);
                Vector3 p4 = new Vector3(i * stepSize, 0, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, -options.BorderWidth, options.BorderThickness);
                Vector3 p2 = new Vector3(i * stepSize, -options.BorderWidth, 0);
                Vector3 p3 = new Vector3((i + 1) * stepSize, -options.BorderWidth, 0);
                Vector3 p4 = new Vector3((i + 1) * stepSize, -options.BorderWidth, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, -options.BorderWidth, 0);
                Vector3 p2 = new Vector3(i * stepSize, 0, 0);
                Vector3 p3 = new Vector3((i + 1) * stepSize, 0, 0);
                Vector3 p4 = new Vector3((i + 1) * stepSize, -options.BorderWidth, 0);

                OutputQuad(p1, p2, p3, p4);
            }

            {
                Vector3 p1 = new Vector3(0, 0, 0);
                Vector3 p2 = new Vector3(0, -options.BorderWidth, 0);
                Vector3 p3 = new Vector3(-options.BorderWidth, -options.BorderWidth, 0);
                Vector3 p4 = new Vector3(0, 0, options.BorderThickness);
                Vector3 p5 = new Vector3(0, -options.BorderWidth, options.BorderThickness);
                Vector3 p6 = new Vector3(-options.BorderWidth, -options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p2, p3);
                OutputTriangle(p6, p5, p4);
                OutputTriangle(p2, p6, p3);
                OutputTriangle(p2, p5, p6);
            }

            {
                Vector3 p1 = new Vector3(width, 0, 0);
                Vector3 p2 = new Vector3(width, -options.BorderWidth, 0);
                Vector3 p3 = new Vector3(width + options.BorderWidth, -options.BorderWidth, 0);
                Vector3 p4 = new Vector3(width, 0, options.BorderThickness);
                Vector3 p5 = new Vector3(width, -options.BorderWidth, options.BorderThickness);
                Vector3 p6 = new Vector3(width + options.BorderWidth, -options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p3, p2);
                OutputTriangle(p6, p4, p5);
                OutputTriangle(p2, p3, p6);
                OutputTriangle(p2, p6, p5);
            }
        }

        private void OutputBottomBorder(float stepSize, Bitmap bm)
        {
            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, height, heights[i, bm.Height - 1]);
                Vector3 p2 = new Vector3((i + 1) * stepSize, height, heights[i + 1, bm.Height - 1]);
                Vector3 p3 = new Vector3((i + 1) * stepSize, height, options.BorderThickness);
                Vector3 p4 = new Vector3(i * stepSize, height, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, height + options.BorderWidth, options.BorderThickness);
                Vector3 p2 = new Vector3(i * stepSize, height, options.BorderThickness);
                Vector3 p3 = new Vector3((i + 1) * stepSize, height, options.BorderThickness);
                Vector3 p4 = new Vector3((i + 1) * stepSize, height + options.BorderWidth, options.BorderThickness);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, height + options.BorderWidth, options.BorderThickness);
                Vector3 p2 = new Vector3((i + 1) * stepSize, height + options.BorderWidth, options.BorderThickness);
                Vector3 p3 = new Vector3((i + 1) * stepSize, height + options.BorderWidth, 0);
                Vector3 p4 = new Vector3(i * stepSize, height + options.BorderWidth, 0);

                OutputQuad(p1, p2, p3, p4);
            }

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, height + options.BorderWidth, 0);
                Vector3 p2 = new Vector3((i + 1) * stepSize, height + options.BorderWidth, 0);
                Vector3 p3 = new Vector3((i + 1) * stepSize, height, 0);
                Vector3 p4 = new Vector3(i * stepSize, height, 0);

                OutputQuad(p1, p2, p3, p4);
            }

            {
                Vector3 p1 = new Vector3(0, height, 0);
                Vector3 p2 = new Vector3(0, height + options.BorderWidth, 0);
                Vector3 p3 = new Vector3(-options.BorderWidth, height + options.BorderWidth, 0);
                Vector3 p4 = new Vector3(0, height, options.BorderThickness);
                Vector3 p5 = new Vector3(0, height + options.BorderWidth, options.BorderThickness);
                Vector3 p6 = new Vector3(-options.BorderWidth, height + options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p3, p2);
                OutputTriangle(p6, p4, p5);
                OutputTriangle(p2, p3, p6);
                OutputTriangle(p2, p6, p5);
            }

            {
                Vector3 p1 = new Vector3(width, height, 0);
                Vector3 p2 = new Vector3(width, height + options.BorderWidth, 0);
                Vector3 p3 = new Vector3(width + options.BorderWidth, height + options.BorderWidth, 0);
                Vector3 p4 = new Vector3(width, height, options.BorderThickness);
                Vector3 p5 = new Vector3(width, height + options.BorderWidth, options.BorderThickness);
                Vector3 p6 = new Vector3(width + options.BorderWidth, height + options.BorderWidth, options.BorderThickness);
                OutputTriangle(p1, p2, p3);
                OutputTriangle(p6, p5, p4);
                OutputTriangle(p2, p6, p3);
                OutputTriangle(p2, p5, p6);
            }
        }

        // The flat size, z == 0
        private void OutputBack(float stepSize, Bitmap bm)
        {
            for (int i = 0; i < bm.Width - 1; ++i)
                for (int j = 0; j < bm.Height - 1; ++j)
                {
                    Vector3 p1 = new Vector3(i * stepSize, j * stepSize, 0);
                    Vector3 p2 = new Vector3(i * stepSize, (j + 1) * stepSize, 0);
                    Vector3 p3 = new Vector3((i + 1) * stepSize, (j + 1) * stepSize, 0);
                    Vector3 p4 = new Vector3((i + 1) * stepSize, j * stepSize, 0);

                    OutputQuad(p1, p2, p3, p4);
                }
        }

        private void OutputFanBack(float stepSize, Bitmap bm)
        {
            Vector3 center = new Vector3(width / 2.0f, height / 2.0f, 0);

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, 0, 0);
                Vector3 p2 = new Vector3((i + 1) * stepSize, 0, 0);
                OutputTriangle(center, p2, p1);
            }

            for (int i = 0; i < bm.Width - 1; ++i)
            {
                Vector3 p1 = new Vector3(i * stepSize, height, 0);
                Vector3 p2 = new Vector3((i + 1) * stepSize, height, 0);
                OutputTriangle(center, p1, p2);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(0, j * stepSize, 0);
                Vector3 p2 = new Vector3(0, (j + 1) * stepSize, 0);
                OutputTriangle(center, p1, p2);
            }

            for (int j = 0; j < bm.Height - 1; ++j)
            {
                Vector3 p1 = new Vector3(width, j * stepSize, 0);
                Vector3 p2 = new Vector3(width, (j + 1) * stepSize, 0);
                OutputTriangle(center, p2, p1);
            }
        }

        // The picture side, z > 0
        private void OutputFront(float stepSize, Bitmap bm)
        {
            for (int i = 0; i < bm.Width - 1; ++i)
                for (int j = 0; j < bm.Height - 1; ++j)
                {
                    Vector3 p1 = new Vector3(i * stepSize, j * stepSize, heights[i, j]);
                    Vector3 p2 = new Vector3((i + 1) * stepSize, j * stepSize, heights[i + 1, j]);
                    Vector3 p3 = new Vector3((i + 1) * stepSize, (j + 1) * stepSize, heights[i + 1, j + 1]);
                    Vector3 p4 = new Vector3(i * stepSize, (j + 1) * stepSize, heights[i, j + 1]);

                    OutputQuad(p1, p2, p3, p4);
                }
        }

        private void OutputHeader()
        {
            if (bw != null)
            {
                byte[] header = new byte[80];
                for (int i = 0; i < 80; ++i)
                    header[i] = (int)' ';
                bw.Write(header);
                bw.Write((uint)0);
            }
            else
            {
                tw.WriteLine("solid lithograph");
            }
        }

        private void OutputTrailer()
        {
            if (bw != null)
            {
                bw.BaseStream.Position = 80;
                bw.Write(nTriangles);
                bw.Close();
                bw = null;
            }
            else
            {
                tw.WriteLine("endsolid lithograph");
                tw.Close();
                tw = null;
            }
        }

        private void OutputQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            OutputTriangle(p1, p2, p4);
            OutputTriangle(p2, p3, p4);
        }

        private void OutputTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            ++nTriangles;
            if (bw != null)
            {
                bw.Write(0.0f); bw.Write(0.0f); bw.Write(0.0f);
                bw.Write(p1.X); bw.Write(p1.Y); bw.Write(p1.Z);
                bw.Write(p2.X); bw.Write(p2.Y); bw.Write(p2.Z);
                bw.Write(p3.X); bw.Write(p3.Y); bw.Write(p3.Z);
                bw.Write((ushort)0);
            }
            else
            {
                tw.WriteLine("facet normal 0.0 0.0 0.0");
                tw.WriteLine("  outer loop");
                tw.WriteLine("  vertex {0}", p1.ToSTLFormat());
                tw.WriteLine("  vertex {0}", p2.ToSTLFormat());
                tw.WriteLine("  vertex {0}", p3.ToSTLFormat());
                tw.WriteLine("  endloop");
                tw.WriteLine("endfacet");
            }
        }
    }
}
