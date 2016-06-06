﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
//Emgu
using Emgu.Util;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;

namespace DataMiner_FeatureExtractor_kv
{
    public partial class Form1 : Form
    {
        //Global
        string fPath = "";
        string featurePath = "";
        string facesPath = "";
        string haarPath = "";
        int nThread;

        public Form1()
        {
            InitializeComponent();
            initializeComboBox();
        }

        private void initializeComboBox()
        {
            //Combo box
            cb_nThreads.Items.AddRange(new object[]
            {
                "1",
                "2",
                "4"
            });
            cb_nThreads.SelectedIndex = 0;
        }

        private void btn_Exit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void btn_Input_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            if(folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                fPath = folderDialog.SelectedPath;
                LOG("File succesfully opened: \n\t" + fPath, false);
                //Get features from pictures       
            }
            else
            {
                LOG("Error opening file", true);
            }
        }//End of btn_Input

        private void LOG(string msg, Boolean error)
        {
            if (error)
                rtb_Log.SelectionColor = Color.Red;
            else
                rtb_Log.SelectionColor = Color.Black;

            rtb_Log.AppendText(msg);
            rtb_Log.AppendText("\n");
            rtb_Log.Refresh();
        }//End of LOG

        private void cb_nThreads_SelectedIndexChanged(object sender, EventArgs e)
        {

        }     

        private void btn_Start_Click(object sender, EventArgs e)
        {
            if(fPath == "")
            {
                LOG("Error! File not selected!", true);
            }
            else if(featurePath == "")
            {
                LOG("Error! Location for features not selected!", true);
            }
            else if(facesPath == "")
            {
                LOG("Error! Location for faces not selected!", true);
            }
            else if(haarPath == "")
            {
                LOG("Error! Location for haar traing set not selected!", true);
            }
            else
            {
                if(!int.TryParse(cb_nThreads.SelectedItem.ToString(), out nThread))
                {
                    LOG("Error! thread count conversion failed!", true);
                }
                else
                {
                    getFeatures(fPath, nThread);
                }           
                    
            }
           
        }//End of btn_Start



        private void getFeatures(string path, int nThr)
        {
            LOG("Feature extraction started! \n Thread count: " + nThr, false);
            Bitmap bitmap;
            String bpPath;
            int folderCounter = 1, fileCounter = 1;
            bool fEndOfDirectory = false, fEndOfAllData = false, firstStartInnerDo = true;

            do
            {

                do
                {
                    //Create path to picture
                    bpPath = path + "\\" + folderCounter + "\\" + fileCounter + ".jpg";
                    try {
                        bitmap = new Bitmap(bpPath);
                        if (bitmap == null)
                        {
                            LOG("Error! Failed to load a picture: " + bpPath, true);
                            fEndOfDirectory = true;                                               
                        }//End of if

                        else
                        {
                            LOG("Picture loaded: " + bpPath, false);
                            //Get features
                            getFeatureArray(bitmap, fileCounter.ToString(), folderCounter.ToString());
                            fileCounter++;
                            firstStartInnerDo = false;
                        }//End of else

                        
                    }
                    catch (Exception)
                    {
                        LOG("Error! Failed to load a picture: " + bpPath, true);
                        fEndOfDirectory = true;
                        if (firstStartInnerDo)
                            fEndOfAllData = true;
                    }
                }//End of do
                while (!fEndOfDirectory) ;

                fileCounter = 1;
                fEndOfDirectory = false;
                firstStartInnerDo = true;
                folderCounter++;
                LOG("New folder!\n\t" + folderCounter, false);
            }
            while (!fEndOfAllData);          

        }//End of getFeatures

        private bool getFeatureArray(Bitmap bmp, string fileCounter, string folderCounter)
        {
            Image<Bgr, byte> ImageFrame = new Image<Bgr, byte>(bmp);
            //Convert to gray scale
            Image<Gray, byte> grayFrame = ImageFrame.Convert<Gray, byte>();

            //Classifier
            CascadeClassifier classifier = new CascadeClassifier(haarPath + "\\haarcascade_frontalface_alt_tree.xml");
            //Detect faces. gray scale, windowing scale factor (closer to 1 for better detection),minimum number of nearest neighbours
            //min and max size in pixels. Start to search with a window of 800 and go down to 100 
            Rectangle[] rectangles = classifier.DetectMultiScale(grayFrame, 1.4, 0, new Size(15,15), new Size(800,800));

            if (rectangles == null)
            {
                LOG("No face!", true);
                return false;
            }
            LOG("Face number: " + rectangles.Length.ToString(), false);

            int[] feature;

            for(int counter = 0; counter < rectangles.Length; counter++)
            {
                //Get LBP BUT FIRST CUT FACE OUT AND RESIZE IMG
                feature = calculateLBP( CutFaceOut(bmp, rectangles[counter], fileCounter, folderCounter, counter.ToString()) );
                writeToFile(feature, folderCounter);
            }

            return true;
        }

        private Bitmap CutFaceOut(Bitmap srcBitmap, Rectangle section, string FileCounter, string FolderCounter, string RectangleCounter)
        {
            Bitmap bmp = new Bitmap(section.Width, section.Height);
            Graphics g = Graphics.FromImage(bmp);

            Rectangle destRect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            g.DrawImage(srcBitmap, destRect, section, GraphicsUnit.Pixel);

            g.Dispose();
            //Create directory and save image
            if(!System.IO.Directory.Exists(facesPath + "\\" + FolderCounter))
            {
                System.IO.Directory.CreateDirectory(facesPath + "\\" + FolderCounter);
            }
            bmp = resizeImage(bmp);
            bmp.Save(facesPath + "\\"+ FolderCounter + "\\"+ FileCounter + "-" + RectangleCounter+".bmp");

            LOG("Picture saved! \t" + facesPath + "\\" + FolderCounter + "\\" + FileCounter + "-" + RectangleCounter + ".bmp", false);

            return bmp;
        }

        private Bitmap resizeImage(Bitmap bmp)
        {
            int newWidth = 48, newHeight = 48;
            Bitmap newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);

            // Draws the image in the specified size with quality mode set to HighQuality
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(bmp, 0, 0, newWidth, newHeight);
            }
            LOG("Resize image", false);
            return newImage;
        }


        //OVO DORADITI NEGDJE SAM NEŠTO LOGIČKI ZEZNIO!!!!
        private int[] calculateLBP(Bitmap bmp)
        {
            List<int> LBPfeatures = new List<int>();
            int currentFeature;


            //Calculate LBP
            for (int i = 1; i < bmp.Height; i = i + 3)
            {

                for (int j = 1; j < bmp.Width; j = j + 3)
                {

                    currentFeature = calculateCurrentLBP(j,i,bmp);
                    //Write to array
                    //ERROR WAS HERE
                    LBPfeatures.Add(currentFeature);
                }//End of inner for

            }//End of outer for
            LOG("LBP Calculated", false);

            List<int> uniformLBPfeatures = new List<int>();

            //For each 8-bit binary, if it's uniform get its count from LBPfeatures
            for (int i = 0; i < 256; i++)
            {
                if (isUniform(i))
                {
                    int uc = LBPfeatures.FindAll(x => x == i).Count; //x so that x == i
                    //Add this number to the uniformLBPFeatures
                    uniformLBPfeatures.Add(uc);
                }
            }

            //Count all non-uniform
            int nuc = LBPfeatures.FindAll(x => !isUniform(x)).Count; //x so that x is not uniform
            uniformLBPfeatures.Add(nuc);

            LOG("Uniform LBP Calculated", false);

            //Convert List<int> to int[] and return it
            return uniformLBPfeatures.ToArray();
        }

        private int calculateCurrentLBP(int i, int j, Bitmap bmp)
        {
            double[] arrayNeighbours = new double[8];
            int counter = 0;
            double temp;
            double centerValue = (bmp.GetPixel(i, j).R + bmp.GetPixel(i, j).G + bmp.GetPixel(i, j).B) / 3;
            for (int a = -1; a < 2; a++)
            {

                for(int b = -1; b < 2; b++)
                {
                    if (a == 0 && b == 0)
                        continue;

                    temp = bmp.GetPixel(i + a, j + b).R + bmp.GetPixel(i + a, j + b).G + bmp.GetPixel(i + a, j + b).B;
                    temp /= 3;

                    if (temp > centerValue)
                        arrayNeighbours[counter] = 1;
                    else
                        arrayNeighbours[counter] = 0;
                    counter++;
                }//End of for
            }//End of for

            String binaryCombination;
            //Order them clockwise
            binaryCombination = arrayNeighbours[0].ToString() + arrayNeighbours[1].ToString() + arrayNeighbours[2].ToString() +
                arrayNeighbours[4].ToString() + arrayNeighbours[7].ToString() + arrayNeighbours[6].ToString() +
                arrayNeighbours[5].ToString() + arrayNeighbours[3].ToString();

            try
            {
                int returnNumb = Convert.ToInt32(binaryCombination, 2);

                return returnNumb;
            }
            catch (Exception)
            {
                LOG("Error! Error occured while converting binary to integer", true);
                return -1;
            }
            
        }

        //Check if 8-bit value is uniform
        private bool isUniform(int value)
        {
            int counter = 0;
            //From weight 0 to weight 6
            for (int i = 1; i <= 64; i = i<<1)
            {
                //If current and next bit are different increase the counter
                int currentBit = value & i;
                int nextBit = value & (i << 1);
                if (currentBit != (nextBit >> 1))
                    counter++;
            }
            //If counter is greater than 2 return false
            return counter > 2 ? false : true;
        }

        private void writeToFile(int[] features, String className)
        {
            String textToWrite = "";
            
            for(int i = 0; i < features.Length; i++)
            {
                textToWrite += features[i].ToString() + "\t";
            }
            textToWrite += className;
            try
            {
                System.IO.File.AppendAllText(featurePath + "\\features.txt", textToWrite + Environment.NewLine);
                LOG("Feature written", false);
            }
            catch (Exception)
            {
                LOG("Error! Feature writing failed!", true);
            }
            
        }

        private void originalPicture_Click(object sender, EventArgs e)
        {
            
        }

        private void FacePicture_Click(object sender, EventArgs e)
        {
            
        }

        private void rtb_Log_TextChanged(object sender, EventArgs e)
        {
            rtb_Log.SelectionStart = rtb_Log.Text.Length;
            // scroll it automatically
            rtb_Log.ScrollToCaret();
        }

        private void btn_fOutput_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                featurePath = folderDialog.SelectedPath;
                LOG("Location succesfully selected: \n\t" + featurePath, false);
                //Get features from pictures       
            }
            else
            {
                LOG("Error selecting location", true);
            }
        }

        private void btn_facesOutput_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                facesPath = folderDialog.SelectedPath;
                LOG("Location succesfully selected: \n\t" + facesPath, false);
                //Get features from pictures       
            }
            else
            {
                LOG("Error selecting location", true);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                haarPath = folderDialog.SelectedPath;
                LOG("Location succesfully selected: \n\t" + haarPath, false);
                //Get features from pictures       
            }
            else
            {
                LOG("Error selecting location", true);
            }
        }
    }//End of Class

}//End of namespace
