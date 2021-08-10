﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Ionic.Zip;

namespace WYDLauncher
{
    public partial class Window : Form
    {
        public List<string> lista = new List<string>();
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        public int zipCount = 0;
        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();
        public string Server = "";
        private int CompletserverVersion = 0;
        private int downloadlastupdate = 0;
        private XDocument serverXml;
        public Window()
        {
            InitializeComponent();
            this.Config = new Config();
            this.AddOwnedForm(this.Config);
            this.Config.Hide();
            readSettings();
            backgroundWorker1.RunWorkerAsync();
            strtGameBtn.Enabled = false;
            progressBar1.ProgressColor = Color.Gray;
            progressBar1.Value = 0;

            this.Config.ClientSize = new System.Drawing.Size(450, 242);
            closeBtn.BackgroundImage = Properties.Resources.close1;
            minimizeBtn.BackgroundImage = Properties.Resources.minimize1;
            button2.BackgroundImage = Properties.Resources.config1;
            strtGameBtn.Enabled = false;

            Config.ReadConfigFile();
        }

        public void readSettings()
        {
            try
            {
                string[] lines = File.ReadAllLines("./settings.txt");
                foreach (string line in lines)
                {
                    if (!line.Contains("http"))
                        continue;

                    Server = line;
                }

                try
                {
                    serverXml = XDocument.Load(@Server + "Updates.xml");
                    patchNotes.Url = new Uri(serverXml.Root.Element("config").Element("notic").Value);
                    CompletserverVersion = int.Parse(serverXml.Root.Element("config").Element("currentversion").Value);
                    downloadlastupdate = int.Parse(serverXml.Root.Element("config").Element("downloadlastupdate").Value);
                }
                catch (Exception Ex)
                {
                    MessageBox.Show("Servidor de atualização falhou" + Ex.Message);
                    this.Close();
                }
            }
            catch (Exception Ex)
            {
                MessageBox.Show("Não foi possível localizar o arquivo settings.txt" + Ex.Message);
                this.Close();
            }

        }
        private void Form1_MouseDown(object sender,
        System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void closeBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void closeBtn_MouseEnter(object sender, EventArgs e)
        {
            closeBtn.BackgroundImage = Properties.Resources.close2;
        }

        private void closeBtn_MouseLeave(object sender, EventArgs e)
        {
            closeBtn.BackgroundImage = Properties.Resources.close1;
        }

        private void minimizeBtn_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void minimizeBtn_MouseEnter(object sender, EventArgs e)
        {
            minimizeBtn.BackgroundImage = Properties.Resources.minimize2;
        }

        private void minimizeBtn_MouseLeave(object sender, EventArgs e)
        {
            minimizeBtn.BackgroundImage = Properties.Resources.minimize1;
        }

        static bool deleteFile(string f)
        {
            try
            {
                File.Delete(f);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public void AddUpdate()
        {
            lista.Clear();


            string Root = AppDomain.CurrentDomain.BaseDirectory;
            decimal localVersion = Config.m_Version;

            var updateList = (from p in serverXml.Descendants("update")
                              select new
                              {
                                  version = Convert.ToInt32(p.Element("version").Value),
                                  file = p.Element("file").Value
                              }).ToList();

            if (Config.m_Version != CompletserverVersion)
            {
                DialogResult confirm = MessageBox.Show("Nova versão encontrada, deseja atualizar?", "WYDLauncher", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2);

                if (confirm.ToString().ToUpper() == "YES")
                {
                    strtGameBtn.Enabled = false;
                    foreach (var update in updateList)
                    {
                        string version = update.version.ToString();
                        string file = update.file;

                        decimal serverVersion = decimal.Parse(version);


                        string sUrlToReadFileFrom = Server + file;

                        string sFilePathToWriteFileTo = Root + file;

                        bool normalCondition = serverVersion > localVersion;
                        if (downloadlastupdate == 1)
                            normalCondition = serverVersion >= localVersion;

                        if (normalCondition)
                        {
                            Uri url = new Uri(sUrlToReadFileFrom);
                            HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                            HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                            response.Close();
                            Int64 iSize = response.ContentLength;
                            Int64 iRunningByteTotal = 0;

                            using (WebClient client = new WebClient())
                            {
                                using (Stream streamRemote = client.OpenRead(new Uri(sUrlToReadFileFrom)))
                                {
                                    using (Stream streamLocal = new FileStream(sFilePathToWriteFileTo, FileMode.Create, FileAccess.Write, FileShare.None))
                                    {
                                        int iByteSize = 0;
                                        byte[] byteBuffer = new byte[iSize];
                                        while ((iByteSize = streamRemote.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                                        {
                                            streamLocal.Write(byteBuffer, 0, iByteSize);
                                            iRunningByteTotal += iByteSize;

                                            double dIndex = (double)(iRunningByteTotal);
                                            double dTotal = (double)byteBuffer.Length;
                                            double dProgressPercentage = (dIndex / dTotal);
                                            int iProgressPercentage = (int)(dProgressPercentage * 100);

                                            backgroundWorker1.ReportProgress(iProgressPercentage);
                                        }
                                        streamLocal.Close();
                                    }
                                    streamRemote.Close();
                                }
                            }
                            lista.Add(file);
                            using (ZipFile zip = ZipFile.Read(file))
                            {
                                progressBar1.Maximum = zip.Count;
                                foreach (ZipEntry zipFiles in zip)
                                {
                                    progressBar1.CustomText = "Extraindo arquivos... " + zipFiles.FileName;
                                    progressBar1.VisualMode = ProgressBarDisplayMode.TextAndCurrProgress;
                                    zipFiles.Extract(Root + "", true);
                                    progressBar1.Value++;
                                }
                            }
                        }
                    }
                }
            }
        }
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            AddUpdate();
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Maximum = e.ProgressPercentage;
            progressBar1.Value = e.ProgressPercentage;

            // downloadLbl.ForeColor = System.Drawing.Color.Silver;
            strtGameBtn.Enabled = false;
            progressBar1.VisualMode = ProgressBarDisplayMode.TextAndPercentage;
            progressBar1.CustomText = "Baixando Atualizações";
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            strtGameBtn.Enabled = true;
            strtGameBtn.Enabled = true;
            //progressBar1.Value = 100;
            // this.downloadLbl.ForeColor = System.Drawing.Color.FromArgb(0, 121, 203);
            progressBar1.VisualMode = ProgressBarDisplayMode.CustomText;

            lista.ForEach(i => deleteFile(i));

            //download new version file
            WebClient webClient = new WebClient();

            Config.m_Version = (short)CompletserverVersion;
            Config.SaveConfig();
            progressBar1.CustomText = "Jogo Atualizado !";
            strtGameBtn.Enabled = true;

            button3.Text = "" + Config.m_Version;
        }


        //Starts the game
        private void strtGameBtn_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("WYD.exe", "\\starting by launcher");
            }
            catch (Exception Ex)
            {
                MessageBox.Show("WYD.exe não foi encontrado" + Ex.Message);
            }
            this.Close();
        }

        private void patchNotes_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {


        }

        private void Config_Load(object sender, EventArgs e)
        {

        }

        private void Config_Load_1(object sender, EventArgs e)
        {

        }

        private void button2_MouseEnter(object sender, EventArgs e)
        {
            button2.BackgroundImage = Properties.Resources.config1;
        }

        private void button2_MouseLeave(object sender, EventArgs e)
        {
            button2.BackgroundImage = Properties.Resources.config2;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                this.Config = new Config();

                this.Config.Show();
            }
            catch (Exception Ex)
            {
                MessageBox.Show("Erro" + Ex.Message);
            }
        }
    }
}