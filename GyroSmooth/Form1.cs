using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace GyroSmooth
{
    public partial class Form1 : Form
    {
        private const float CZoomScale = 2f;
        private int FZoomLevel = 0;

        private int counter = 0;

        private string inputfile = @"H:\GX010016.MP4";
        private string outputfile = @"H:\GX010016_smooth.MP4";

        private struct Block
        {
            public Block(long start, long end)
            {
                this.start = start;
                this.end = end;
            }

            public long start;
            public long end;
        }

        public Form1()
        {
            InitializeComponent();

            //@"C:\Workspace\GoproGyroSmooth\unstab.MP4"

            List<Block> blocks = new List<Block>();
            string allblocks = "";

            using (FileStream fs = File.Open(inputfile, FileMode.Open, FileAccess.Read))
            using (BufferedStream bs = new BufferedStream(fs))
            using (BinaryReader sr = new BinaryReader(bs))
            {
                string last = "";
                long lastlastindex = 0;
                long lastindex = 0;

                while (bs.Position < bs.Length)
                {
                    string current = new string(sr.ReadBytes(10000).Select(x => (char)x).ToArray());
                    string str = last + current;

                    if (str.Contains("GYROs"))
                    {
                        while (str.LastIndexOf("STRM") < str.LastIndexOf("GYRO"))
                        {
                            str += new string(sr.ReadBytes(1000).Select(x => (char)x).ToArray());
                        }
                        blocks.Add(new Block(lastlastindex, bs.Position));
                        allblocks += str;
                        last = "";
                        Console.WriteLine("reading block - start: " + lastlastindex + " end: " + bs.Position);
                    }
                    else
                    {
                        last = current;
                    }
                    lastlastindex = lastindex;
                    lastindex = bs.Position;
                }
            }

            List<short> x_all = new List<short>();
            List<short> y_all = new List<short>();
            List<short> z_all = new List<short>();

            //byte[] databin = File.ReadAllBytes(@"C:\Workspace\GoproGyroSmooth\unstab_gyro.MP4");
            //string data = new string(databin.Select(x => (char)x).ToArray());
            int count = Regex.Matches(allblocks, "GYROs").Count;
            int lastIndex = 0;

            for (int i = 0; i < count; i++)
            {
                int index = allblocks.IndexOf("GYROs", lastIndex + 1) + 7;
                int end = allblocks.IndexOf("STRM", index + 1);

                for (int j = 0; j < (end - index) / 6; j++)
                {
                    byte[] x_val = new byte[2];
                    x_val[0] = Convert.ToByte(allblocks[index + j * 6]);
                    x_val[1] = Convert.ToByte(allblocks[index + 1 + j * 6]);
                    short x = BitConverter.ToInt16(x_val, 0);

                    byte[] y_val = new byte[2];
                    y_val[0] = Convert.ToByte(allblocks[index + 2 + j * 6]);
                    y_val[1] = Convert.ToByte(allblocks[index + 3 + j * 6]);
                    short y = BitConverter.ToInt16(y_val, 0);

                    byte[] z_val = new byte[2];
                    z_val[0] = Convert.ToByte(allblocks[index + 4 + j * 6]);
                    z_val[1] = Convert.ToByte(allblocks[index + 5 + j * 6]);
                    short z = BitConverter.ToInt16(z_val, 0);

                    x_all.Add(x);
                    y_all.Add(y);
                    z_all.Add(z);

                    //Console.WriteLine(x.ToString("X4") + " " + y.ToString("X4") + " " + z.ToString("X4"));
                }

                lastIndex = index;
            }

            chart1.MouseWheel += Chart1_MouseWheel;

            Series series_x = new Series("X");
            series_x.Points.DataBindY(x_all);
            series_x.ChartType = SeriesChartType.FastLine;
            /*
            Series series_y = new Series("Y");
            series_y.Points.DataBindY(y_all);
            series_y.ChartType = SeriesChartType.FastLine;

            Series series_z = new Series("Z");
            series_z.Points.DataBindY(z_all);
            series_z.ChartType = SeriesChartType.FastLine;
            */

            List<short> x_smooth = LowPass(15, x_all);
            List<short> y_smooth = LowPass(15, y_all);
            List<short> z_smooth = LowPass(15, z_all);


            Series series_xs = new Series("X Smooth");
            series_xs.Points.DataBindY(x_smooth);
            series_xs.ChartType = SeriesChartType.FastLine;

            chart1.Series.Clear();
            chart1.Series.Add(series_x);
            chart1.Series.Add(series_xs);
            //chart1.Series.Add(series_y);
            //chart1.Series.Add(series_z);


            using (FileStream ifs = File.Open(inputfile, FileMode.Open, FileAccess.Read))
            using (FileStream ofs = File.Open(outputfile, FileMode.Create, FileAccess.Write))
            using (BufferedStream ibs = new BufferedStream(ifs))
            using (BufferedStream obs = new BufferedStream(ofs))
            using (BinaryReader isr = new BinaryReader(ibs))
            using (BinaryWriter osr = new BinaryWriter(obs))
            {
                while (blocks.Count > 0)
                {
                    long remaining = 0;
                    do
                    {
                        remaining = blocks[0].start - ibs.Position;
                        int step = (int)Math.Min(10000, remaining);
                        osr.Write(isr.ReadBytes(step));
                    } while (remaining > 0);
                    Console.WriteLine("writing block - start: " + blocks[0].start + " end: " + blocks[0].end);
                    osr.Write(Replace(isr.ReadBytes((int)(blocks[0].end - blocks[0].start)), x_smooth, y_smooth, z_smooth));
                    blocks.RemoveAt(0);
                }

                while (ibs.Position < ibs.Length)
                {
                    osr.Write(isr.ReadBytes(10000));
                }
            }

            /*

             lastIndex = 0;
            int counter = 0;

            for (int i = 0; i < count; i++)
            {
                int index = data.IndexOf("GYROs", lastIndex + 1) + 7;
                int end = data.IndexOf("STRM", index + 1);

                for (int j = 0; j < (end - index) / 6; j++)
                {
                    databin[index + j * 6] = (byte)x_smooth[counter];
                    databin[index + 1 + j * 6] = (byte)(x_smooth[counter] >> 8);
                    databin[index + 2 + j * 6] = (byte)y_smooth[counter];
                    databin[index + 3 + j * 6] = (byte)(y_smooth[counter] >> 8);
                    databin[index + 4 + j * 6] = (byte)z_smooth[counter];
                    databin[index + 5 + j * 6] = (byte)(z_smooth[counter] >> 8);
                    counter++;
                }
                lastIndex = index;
            }

            File.WriteAllBytes(@"C:\Workspace\GoproGyroSmooth\stab_gyro.MP4", databin);*/
        }

        private byte[] Replace(byte[] databin, List<short> x, List<short> y, List<short> z)
        {
            string str = new string(databin.Select(q => (char)q).ToArray());
            int count = Regex.Matches(str, "GYROs").Count;
            int lastIndex = 0;

            for (int i = 0; i < count; i++)
            {
                int index = str.IndexOf("GYROs", lastIndex + 1) + 7;
                int end = str.IndexOf("STRM", index + 1);

                for (int j = 0; j < (end - index) / 6; j++)
                {
                    char x_c = (char)x[counter];
                    char y_c = (char)y[counter];
                    char z_c = (char)z[counter];
                    databin[index + 0 + j * 6] = (byte)(x_c);
                    databin[index + 1 + j * 6] = (byte)(x_c >> 8);
                    databin[index + 2 + j * 6] = (byte)(y_c);
                    databin[index + 3 + j * 6] = (byte)(y_c >> 8);
                    databin[index + 4 + j * 6] = (byte)(z_c);
                    databin[index + 5 + j * 6] = (byte)(z_c >> 8);

                    counter++;
                }
                lastIndex = index;
            }
            return databin;
        }

        private List<short> LowPass(int n, List<short> data)
        {
            int back = (int)((n - 0.5) / 2);
            int forward = n / 2;

            List<short> working = new List<short>(data);
            List<short> res = new List<short>();
            working.InsertRange(0, new short[back]);
            working.AddRange(new short[forward]);

            for (int i = back; i < working.Count() - forward; i++)
            {
                double val = 0;
                for (int j = -1 * back; j <= forward; j++)
                {
                    val += working[i + j] * (1f / n);
                }
                res.Add((short)val);
            }
            return res;
        }

        private void Chart1_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                Axis xAxis = chart1.ChartAreas[0].AxisX;
                double xMin = xAxis.ScaleView.ViewMinimum;
                double xMax = xAxis.ScaleView.ViewMaximum;
                double xPixelPos = xAxis.PixelPositionToValue(e.Location.X);

                if (e.Delta < 0 && FZoomLevel > 0)
                {
                    // Scrolled down, meaning zoom out
                    if (--FZoomLevel <= 0)
                    {
                        FZoomLevel = 0;
                        xAxis.ScaleView.ZoomReset();
                    }
                    else
                    {
                        double xStartPos = Math.Max(xPixelPos - (xPixelPos - xMin) * CZoomScale, 0);
                        double xEndPos = Math.Min(xStartPos + (xMax - xMin) * CZoomScale, xAxis.Maximum);
                        xAxis.ScaleView.Zoom(xStartPos, xEndPos);
                    }
                }
                else if (e.Delta > 0)
                {
                    // Scrolled up, meaning zoom in
                    double xStartPos = Math.Max(xPixelPos - (xPixelPos - xMin) / CZoomScale, 0);
                    double xEndPos = Math.Min(xStartPos + (xMax - xMin) / CZoomScale, xAxis.Maximum);
                    xAxis.ScaleView.Zoom(xStartPos, xEndPos);
                    FZoomLevel++;
                }
            }
            catch { }
        }
    }
}
